using System.Globalization;
using Babel.Canonicalization;
using Babel.Core;
using Babel.Ledger;
using Babel.Inventory;
using Babel.Purchasing;
using Babel.Sales;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// الخطوة الثانية: المخطّطات والبيانات المرجعية — <b>بدور المالك حصراً</b>.
/// <para>
/// الترتيب لا يجوز أن ينقلب، وهو الموضع الذي يفشل عند الثانية صباحاً إن انقلب:
/// </para>
/// <list type="number">
///   <item>هجرات الدفتر ومشغّلاته ودالّة الترحيل ثم <b>الصلاحيات</b> — كلّها داخل
///         <c>LedgerSchema.DeployAsync</c> وبالترتيب الذي تفرضه هي، لا بترتيب يُعاد
///         تأليفه هنا. ولو نُسخت خطواته هنا لانحرفت النسخة عن الأصل عند أول ترحيل جديد.</item>
///   <item>مخطّطا المبيعات والمشتريات، ثم منح دور التطبيق حقوق الدفاتر المساعدة —
///         وهي <b>ليست</b> حقوق الدفتر: الدفتر المساعد مستندٌ حيّ يُعدَّل ويُلغى،
///         والدفتر قيدٌ لا يُمسّ (ADR-0002).</item>
///   <item>البيانات المرجعية: دليل الحسابات وأدوار الترحيل وخريطتها والفترات والعدّاد.
///         كلّها بدور المالك، لأن دور التطبيق لا يملك <c>INSERT</c> على جدول مرجعي واحد.</item>
/// </list>
/// </summary>
internal static class Schema
{
    /// <summary>ينشر المخطّطات ويبذر البيانات المرجعية.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("نشر المخطّطات بدور المالك / deploying schemas as the owner role");

        // النواة أولاً: تأسيس المنشأة ومراكز تكلفتها هو ما يفترضه كل ما بعده — بوّابة
        // الترحيل تسأل عن مركز التكلفة **قبل** أن تبني طلباً (ADR-0026 · ADR-0029).
        await CoreSchema.DeployAsync(settings.Core, cancellationToken).ConfigureAwait(false);
        Say.Detail("النواة: هجرات + مشغّل ثبات المقياس + الصلاحيات → " + settings.CoreDatabase);

        await LedgerSchema.DeployAsync(settings.Ledger, cancellationToken).ConfigureAwait(false);
        Say.Detail("الدفتر: هجرات + مشغّلات + دالّة الترحيل + الصلاحيات → " + settings.LedgerDatabase);

        await SalesSchemaDeployer.DeployAsync(settings.SalesOwner, cancellationToken).ConfigureAwait(false);
        Say.Detail("المبيعات → " + settings.SalesDatabase);

        await PurchasingSchemaDeployer.DeployAsync(settings.PurchasingOwner, cancellationToken).ConfigureAwait(false);
        Say.Detail("المشتريات → " + settings.PurchasingDatabase);

        await InventorySchemaDeployer.DeployAsync(settings.InventoryOwner, cancellationToken).ConfigureAwait(false);
        Say.Detail("المخزون → " + settings.InventoryDatabase);

        await GrantSubledgerAsync(settings.SalesOwner.ConnectionString, "sales", settings.Ledger.AppRole, cancellationToken)
            .ConfigureAwait(false);
        await GrantSubledgerAsync(
                settings.PurchasingOwner.ConnectionString, "purchasing", settings.Ledger.AppRole, cancellationToken)
            .ConfigureAwait(false);
        await GrantSubledgerAsync(
                settings.InventoryOwner.ConnectionString, "inventory", settings.Ledger.AppRole, cancellationToken)
            .ConfigureAwait(false);

        await SeedReferenceAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// حقوق دور التطبيق على دفتر مساعد: قراءة وكتابة وتعديل — <b>ولا حذف ولا اقتطاع</b>.
    /// <para>
    /// والفرق عن الدفتر مقصود ومعلَن: مسوّدة فاتورة تُعدَّل قبل ترحيلها، والقيد الناتج
    /// عنها لا يُعدَّل أبداً. من يخلط الاثنين يمنح الدفتر ما يستحقه الدفتر المساعد.
    /// </para>
    /// </summary>
    private static async Task GrantSubledgerAsync(
        string ownerConnection, string schema, string appRole, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection owner = new(ownerConnection);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecAsync(owner, $"grant usage on schema {Quote(schema)} to {Quote(appRole)}", cancellationToken)
            .ConfigureAwait(false);
        await ExecAsync(
                owner,
                $"grant select, insert, update on all tables in schema {Quote(schema)} to {Quote(appRole)}",
                cancellationToken)
            .ConfigureAwait(false);
        await ExecAsync(
                owner,
                $"revoke delete, truncate on all tables in schema {Quote(schema)} from {Quote(appRole)}",
                cancellationToken)
            .ConfigureAwait(false);
        await ExecAsync(
                owner,
                $"grant usage, select on all sequences in schema {Quote(schema)} to {Quote(appRole)}",
                cancellationToken)
            .ConfigureAwait(false);

        Say.Detail($"صلاحيات «{schema}» لدور التطبيق: select/insert/update — بلا delete ولا truncate");
    }

    private static async Task SeedReferenceAsync(Settings settings, CancellationToken cancellationToken)
    {
        Say.Step("البيانات المرجعية بدور المالك / reference data as the owner role");

        await using NpgsqlConnection owner = new(settings.Ledger.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        int roles = 0;
        foreach (Dictionary<string, string> row in Csv.Embedded("BabelDemoCompany.account-roles.csv"))
        {
            await using (NpgsqlCommand command = new(
                """
                insert into ledger.posting_role
                    (role_code, name_ar, expected_account_type, expected_side, status, note_ar, note_en)
                values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                """, owner))
            {
                command.Parameters.AddWithValue(row["role_code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
                command.Parameters.AddWithValue(Null(row["expected_account_type"]));
                command.Parameters.AddWithValue(Null(row["expected_side"]));
                command.Parameters.AddWithValue(row["status"]);
                command.Parameters.AddWithValue(Null(row["note_ar"]));
                command.Parameters.AddWithValue(Null(row["note_en"]));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await TranslateAllAsync(owner, Guid.Empty, "posting_role", row["role_code"], row, cancellationToken)
                .ConfigureAwait(false);
            roles++;
        }

        Say.Detail("أدوار الترحيل: " + Say.Count(roles));

        IReadOnlyList<Dictionary<string, string>> accounts = Csv.Embedded("BabelDemoCompany.accounts.csv");

        foreach (Dictionary<string, string> row in accounts
                     .OrderBy(static a => a["code"].Length)
                     .ThenBy(static a => a["code"], StringComparer.Ordinal))
        {
            await using (NpgsqlCommand command = new(
                """
                insert into ledger.account
                    (company_id, account_code, name_ar, name_ar_search, parent_code, account_level,
                     account_type, natural_side, is_postable, is_contra, statement_section, subledger_type,
                     required_dimensions, currency_mode, currency_code, is_protected, is_active, status,
                     source_ref, caveat_ar, caveat_en)
                values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,true,$17,$18,$19,$20)
                on conflict do nothing
                """, owner))
            {
                command.Parameters.AddWithValue(settings.Company);
                command.Parameters.AddWithValue(row["code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
                command.Parameters.AddWithValue(ArabicSearch.Normalize(row["name_ar"]).Value);
                command.Parameters.AddWithValue(Null(row["parent_code"]));
                command.Parameters.AddWithValue(int.Parse(row["level"], CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue(row["account_type"]);
                command.Parameters.AddWithValue(row["natural_side"]);
                command.Parameters.AddWithValue(string.Equals(row["is_postable"], "true", StringComparison.Ordinal));
                command.Parameters.AddWithValue(string.Equals(row["is_contra"], "true", StringComparison.Ordinal));
                command.Parameters.AddWithValue(Null(row["statement_section"]));
                command.Parameters.AddWithValue(row["subledger_type"]);
                command.Parameters.AddWithValue(row["required_dimensions"].Length == 0
                    ? Array.Empty<string>()
                    : row["required_dimensions"].Split('|'));
                command.Parameters.AddWithValue(row["currency_mode"]);
                command.Parameters.AddWithValue(Null(row["currency_code"]));
                command.Parameters.AddWithValue(string.Equals(row["is_protected"], "true", StringComparison.Ordinal));
                command.Parameters.AddWithValue(row["status"]);
                command.Parameters.AddWithValue(Null(row["source_ref"]));
                command.Parameters.AddWithValue(Null(row["caveat_ar"]));
                command.Parameters.AddWithValue(Null(row["caveat_en"]));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await TranslateAllAsync(owner, settings.Company, "account", row["code"], row, cancellationToken)
                .ConfigureAwait(false);
        }

        Say.Detail("دليل الحسابات: " + Say.Count(accounts.Count) + " حساباً بأسمائها العربية وترجماتها");

        int maps = 0;
        foreach (Dictionary<string, string> row in Csv.Embedded("BabelDemoCompany.role-map.default.csv"))
        {
            await using NpgsqlCommand command = new(
                """
                insert into ledger.role_account_map
                    (company_id, role_code, qualifier, account_code, status, note_ar, note_en)
                values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                """, owner);
            command.Parameters.AddWithValue(settings.Company);
            command.Parameters.AddWithValue(row["role_code"]);
            command.Parameters.AddWithValue(row["qualifier"]);
            command.Parameters.AddWithValue(row["account_code"]);
            command.Parameters.AddWithValue(row["status"]);
            command.Parameters.AddWithValue(Null(row["note_ar"]));
            command.Parameters.AddWithValue(Null(row["note_en"]));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            maps++;
        }

        Say.Detail("خريطة الأدوار → الحسابات: " + Say.Count(maps) + " صفّاً");

        for (int month = 1; month <= 12; month++)
        {
            string code = FormattableString.Invariant($"{settings.FiscalYear:0000}-{month:00}");
            DateOnly start = new(settings.FiscalYear, month, 1);
            DateOnly end = start.AddMonths(1).AddDays(-1);

            await using (NpgsqlCommand command = new(
                """
                insert into ledger.fiscal_period
                    (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar)
                values ($1,$2,$3,$4,$5,$6,'open',$7) on conflict do nothing
                """, owner))
            {
                command.Parameters.AddWithValue(settings.Company);
                command.Parameters.AddWithValue(settings.FiscalYear);
                command.Parameters.AddWithValue(month);
                command.Parameters.AddWithValue(code);
                command.Parameters.AddWithValue(start);
                command.Parameters.AddWithValue(end);
                command.Parameters.AddWithValue("الفترة " + code);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await TranslateAsync(owner, settings.Company, "fiscal_period", code, "en", "Period " + code, cancellationToken)
                .ConfigureAwait(false);
        }

        Say.Detail(FormattableString.Invariant($"الفترات المالية: 12 فترة لسنة {settings.FiscalYear}، كلّها مفتوحة"));

        await using (NpgsqlCommand command = new(
            """
            insert into ledger.posting_counter (company_id, book_id, fiscal_year, next_entry_no, next_chain_seq)
            values ($1,$2,$3,1,1) on conflict do nothing
            """, owner))
        {
            command.Parameters.AddWithValue(settings.Company);
            command.Parameters.AddWithValue(Settings.Book);
            command.Parameters.AddWithValue(settings.FiscalYear);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        Say.Detail("عدّاد الترحيل: صفّ واحد لنطاق (شركة × دفتر × سنة) — وهو مصدر انعدام الفجوات");
    }

    /// <summary>بادئة أعمدة الاسم في ملفّات <c>data/</c>: <c>name_&lt;وسم اللغة&gt;</c>.</summary>
    private const string NameColumnPrefix = "name_";

    /// <summary>وسم لغة السجلّ. لا يدخل جدول الترجمات إطلاقاً (ADR-0021).</summary>
    private const string RecordLanguage = "ar";

    /// <summary>
    /// يكتب <b>كل</b> ترجمة يحملها الصفّ، لا الإنجليزية وحدها.
    /// <para>
    /// ولماذا مسحٌ للأعمدة لا اسمُ عمودٍ مكتوب: القرار يقول إن اللغة الخامسة
    /// <b>صفوف إدخال لا هجرة مخطّط</b> (ADR-0021). وباذرٌ يذكر لغةً بعينها في شيفرته
    /// يُبقي تلك اللغة **مميّزة في الشيفرة** بينما المخطّط تحرّر منها — فيظهر العطب
    /// عند أول عمود <c>name_ur</c> يُضاف إلى <c>data/</c> ولا يصل قاعدة البيانات.
    /// وهنا يصل من تلقاء نفسه.
    /// </para>
    /// </summary>
    private static async Task TranslateAllAsync(
        NpgsqlConnection owner,
        Guid company,
        string kind,
        string key,
        Dictionary<string, string> row,
        CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, string> column in row)
        {
            if (!column.Key.StartsWith(NameColumnPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string tag = column.Key[NameColumnPrefix.Length..];

            // العربية سجلٌّ لا ترجمة، وعمود البحث المُطبَّع ليس اسماً معروضاً.
            if (tag.Length == 0
                || string.Equals(tag, RecordLanguage, StringComparison.Ordinal)
                || tag.Contains('_', StringComparison.Ordinal))
            {
                continue;
            }

            await TranslateAsync(owner, company, kind, key, tag, column.Value, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>ترجمة صفّاً لا عموداً (ADR-0021). والفراغ لا يُكتب: الغياب ارتدادٌ إلى العربية.</summary>
    private static async Task TranslateAsync(
        NpgsqlConnection owner,
        Guid company,
        string kind,
        string key,
        string languageTag,
        string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await using NpgsqlCommand command = new(
            """
            insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
            values ($1,$2,$3,$4,$5) on conflict do nothing
            """, owner);
        command.Parameters.AddWithValue(company);
        command.Parameters.AddWithValue(kind);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(languageTag);
        command.Parameters.AddWithValue(name.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object Null(string value) => value.Length == 0 ? DBNull.Value : value;

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
