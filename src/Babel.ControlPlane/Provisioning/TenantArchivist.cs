using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Provisioning;

/// <summary>
/// حصيلة أرشفة مستأجر — وهي <b>الدليل</b> على أن البيانات باقية بعد قطع الوصول:
/// الأعداد تُقرأ من قاعدة المستأجر <b>بعد</b> الأرشفة.
/// </summary>
/// <param name="TenantCode">رمز المستأجر.</param>
/// <param name="DatabaseName">اسم قاعدته — ما تزال قائمة، ولم تُحذف.</param>
/// <param name="JournalEntries">عدد قيود اليومية الباقية بعد الأرشفة.</param>
/// <param name="JournalLines">عدد سطور القيود الباقية.</param>
/// <param name="Accounts">عدد الحسابات الباقية.</param>
/// <param name="AppConnectionError">
/// خطأ محاولة اتصال دور التطبيق بعد الأرشفة — يجب أن يكون <c>42501</c>،
/// وهو الإثبات المباشر أن الوصول التطبيقي مقطوع.
/// </param>
public sealed record ArchiveOutcome(
    string TenantCode, string DatabaseName, long JournalEntries, long JournalLines,
    long Accounts, string AppConnectionError);

/// <summary>
/// «إنهاء الخدمة» في منتج محاسبي = <b>أرشفة</b>، لا حذف.
///
/// <para><b>لماذا:</b> السجلات المحاسبية تحمل التزامات احتفاظ، والقيد المُرحَّل
/// غير قابل للحذف بصلاحيات قاعدة البيانات نفسها (ADR-0003)، وحذف قاعدة مستأجر
/// يُلغي قدرته على إخراج بياناته في نزاع أو تدقيق لاحق.</para>
///
/// <para><b>⚠️ مدّة الاحتفاظ نفسها غير مُتحقَّق منها:</b> لم يُتَح أي مصدر تنظيمي
/// سعودي أثناء بناء هذا المكوّن (<c>docs/evidence/verification-debt.md</c> §1).
/// المكوّن يُنفّذ «لا حذف» ويترك <b>المدّة</b> إعداداً تشغيلياً يُملأ حين يُتحقَّق
/// منها بمصدر أوّلي. لا رقم مُخترَع هنا.</para>
///
/// <para><b>آلية القطع:</b> سحب <c>CONNECT</c> من دور التطبيق ومن
/// <c>PUBLIC</c>، وإنهاء الجلسات القائمة. مالك القاعدة يبقى قادراً على النسخ
/// والتصدير — وهذا هو الفرق بين «غير قابل للوصول من التطبيق» و«محذوف».</para>
/// </summary>
/// <param name="options">إعدادات مستوى التحكّم.</param>
public sealed class TenantArchivist(ControlPlaneOptions options)
{
    /// <summary>
    /// يُؤرشف مستأجراً: يقطع وصول التطبيق ويُبقي البيانات كاملةً. <b>لا حذف.</b>
    /// مُحكَم: أرشفة مستأجر مؤرشف تُرجِع الحصيلة نفسها بلا أثر إضافي.
    /// </summary>
    /// <param name="tenantCode">رمز المستأجر.</param>
    /// <param name="actor">من نفّذ الأرشفة — لا أرشفة بلا فاعل مُسمّى.</param>
    /// <param name="reasonAr">سبب الأرشفة بالعربية — لا أرشفة بلا سبب مُسجَّل.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>حصيلة الأرشفة وما بقي من بيانات بعدها.</returns>
    public async Task<ArchiveOutcome> ArchiveAsync(string tenantCode, string actor,
        string reasonAr, CancellationToken ct = default)
    {
        await using var control = await Db.OpenAsync(options.ControlConnectionString, ct);
        var tenant = await TenantRegistry.RequireByCodeAsync(control, tenantCode, null, ct);

        if (tenant.Status == TenantStatus.Archived)
            return await SnapshotAsync(tenant, ct);

        var db = Db.Ident(tenant.DatabaseName);
        var role = Db.Ident(options.AppRole);

        await using (var maint = await Db.OpenAsync(options.MaintenanceConnectionString, ct))
        {
            await Db.ExecAsync(maint, $"revoke connect on database {db} from {role}", null, ct);
            await Db.ExecAsync(maint, $"revoke connect on database {db} from public", null, ct);
            // إنهاء الجلسات القائمة: سحب الصلاحية لا يقطع اتصالاً مفتوحاً.
            await Db.ExecAsync(maint, """
                select pg_terminate_backend(pid)
                  from pg_stat_activity
                 where datname = @d and pid <> pg_backend_pid()
                """.Replace("@d", $"'{tenant.DatabaseName}'"), null, ct);
        }

        await TenantRegistry.MarkArchivedAsync(control, tenant.TenantId, actor, reasonAr, null, ct);
        await OperationLog.WriteAsync(control, tenant.TenantId, actor, "tenant.archive",
            OperationOutcome.Recorded,
            $"أُرشِف المستأجر «{tenantCode}»: الوصول التطبيقي مقطوع والبيانات محفوظة. السبب: {reasonAr}",
            new { database = tenant.DatabaseName }, null, ct);

        return await SnapshotAsync(tenant with { Status = TenantStatus.Archived }, ct);
    }

    /// <summary>
    /// إعادة الوصول — لأن الأرشفة عكوسة والحذف ليس كذلك. الحالة تعود
    /// <c>Suspended</c> لا <c>Active</c>: إعادة التشغيل قرار منفصل.
    /// </summary>
    public async Task RestoreAsync(string tenantCode, string actor, string reasonAr,
        CancellationToken ct = default)
    {
        await using var control = await Db.OpenAsync(options.ControlConnectionString, ct);
        var tenant = await TenantRegistry.RequireByCodeAsync(control, tenantCode, null, ct);
        var db = Db.Ident(tenant.DatabaseName);
        var role = Db.Ident(options.AppRole);

        await using (var maint = await Db.OpenAsync(options.MaintenanceConnectionString, ct))
            await Db.ExecAsync(maint, $"grant connect on database {db} to {role}", null, ct);

        await TenantRegistry.SetStatusAsync(control, tenant.TenantId, TenantStatus.Suspended, null, null, ct);
        await OperationLog.WriteAsync(control, tenant.TenantId, actor, "tenant.restore",
            OperationOutcome.Recorded, $"أُعيد وصول المستأجر «{tenantCode}»: {reasonAr}", null, null, ct);
    }

    /// <summary>
    /// يقيس <b>بقاء البيانات</b> بعد الأرشفة، ويُثبت أن دور التطبيق لم يعد
    /// يستطيع الاتصال — الادّعاءان معاً هما تعريف الأرشفة.
    /// </summary>
    public async Task<ArchiveOutcome> SnapshotAsync(TenantRecord tenant, CancellationToken ct = default)
    {
        await using var owner = await Db.OpenAsync(
            options.TenantOwnerConnectionString(tenant.DatabaseName), ct);

        var entries = await Db.ScalarAsync<long>(owner,
            "select count(*) from ledger.journal_entry", null, null, ct);
        var lines = await Db.ScalarAsync<long>(owner,
            "select count(*) from ledger.journal_line", null, null, ct);
        var accounts = await Db.ScalarAsync<long>(owner,
            "select count(*) from ledger.account", null, null, ct);

        var appError = "(الاتصال نجح — الأرشفة لم تُطبَّق)";
        try
        {
            await using var app = await Db.OpenAsync(
                options.TenantAppProbeConnectionString(tenant.DatabaseName), ct);
            await Db.ScalarAsync<long>(app, "select 1", null, null, ct);
        }
        catch (PostgresException ex)
        {
            appError = $"SQLSTATE {ex.SqlState}: {ex.MessageText}";
        }
        catch (Exception ex)
        {
            appError = ex.GetType().Name + ": " + ex.Message.Split('\n')[0];
        }

        return new ArchiveOutcome(tenant.TenantCode, tenant.DatabaseName,
            entries, lines, accounts, appError);
    }
}
