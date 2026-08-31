using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// الخطوة الأولى: قواعد البيانات ودور التطبيق — <b>باتصال الصيانة وحده</b>.
/// <para>
/// وهي الخطوة الوحيدة التي تحتاج دوراً خارقاً، ولذلك هي أوّلها وأقصرها ولا يُستدعى
/// اتصال الصيانة بعدها إطلاقاً. أمّا نشر المخطّط فبدور المالك، والترحيل بدور التطبيق
/// الذي لا يملك <c>UPDATE</c> ولا <c>DELETE</c> على الدفتر (ADR-0003).
/// </para>
/// <para>
/// <b>وكل ما هنا يُعاد تشغيله بلا أثر:</b> النشر يقع مرّتين حين تُعاد محاولة إطلاق
/// فاشلة، ونصٌّ لا يحتمل ذلك يترك الخادم بقاعدة نصف مبنيّة عند أول انقطاع شبكة.
/// </para>
/// </summary>
internal static class Bootstrap
{
    /// <summary>ينشئ قواعد البيانات ودور التطبيق إن لم توجد.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("تهيئة القواعد والدور / bootstrapping databases and the application role");
        Say.Detail("اتصال الصيانة: " + Settings.Redact(settings.Maintenance));

        await using NpgsqlConnection admin = new(settings.Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        // الدور أولاً: منح CONNECT على قاعدة يحتاج الدور موجوداً.
        //
        // ‏nosuperuser ليست تفصيلاً بل هي الشرط: دورٌ خارق يتجاوز كل صلاحية وكل RLS،
        // فتصير طبقة الحصانة كلها زينة (فخ-30 · ADR-0003). و LedgerGrants.sql يرفض
        // الدور الخارق صراحةً فيتوقّف النشر — وهذا هو السلوك المطلوب.
        await ExecAsync(
            admin,
            $"""
            do $$
            begin
                perform pg_advisory_xact_lock(hashtextextended('{settings.Ledger.AppRole}', 0));
                begin
                    create role {Quote(settings.Ledger.AppRole)} login nosuperuser nocreatedb nocreaterole noinherit;
                exception when duplicate_object or unique_violation then
                    alter role {Quote(settings.Ledger.AppRole)} login nosuperuser nocreatedb nocreaterole noinherit;
                end;
            end
            $$;
            """,
            cancellationToken).ConfigureAwait(false);

        Say.Detail("دور التطبيق: " + settings.Ledger.AppRole + " (غير مالك، وغير superuser)");

        // ‏**والقائمة تُقرأ من موضع واحد** (<see cref="ModuleProvisioning"/>) لا تُعاد
        // كتابتها هنا: قاعدةٌ تُنسى في هذه الحلقة لا تُفشل شيئاً — يقلع الخادم ويفشل
        // أول نداءٍ يبلغ وحدتها بـ«Connection refused»، وهي رسالةٌ تُقرأ عطلَ شبكةٍ في
        // قاعدة البيانات لا إعداداً ناقصاً، فتُرسل من يبحث إلى المكان الخطأ.
        IEnumerable<string> databases =
        [
            settings.LedgerDatabase,
            settings.CoreDatabase,
            .. ModuleProvisioning.Of(settings).Select(static module => module.Database),
        ];

        foreach (string database in databases)
        {
            await EnsureDatabaseAsync(admin, database, cancellationToken).ConfigureAwait(false);
            await ExecAsync(
                admin,
                $"grant connect on database {Quote(database)} to {Quote(settings.Ledger.AppRole)}",
                cancellationToken).ConfigureAwait(false);
            Say.Detail("قاعدة: " + database);
        }
    }

    /// <summary>
    /// كلمة مرور دور التطبيق تُسنَد من البيئة إن وُجدت.
    /// <para>
    /// <b>ولا افتراض لها ولا توليد:</b> غيابها يعني مصادقة <c>trust</c> أو <c>peer</c>
    /// محلياً، وهو ما يجعل التشغيل على جهاز المطوّر ممكناً بلا أن يُودَع سرّ. أمّا على
    /// خادم فالمتغيّر يصل من مخزن أسرار، وغيابه هناك يظهر فوراً بفشل اتصال دور
    /// التطبيق — لا بفتحة صامتة.
    /// </para>
    /// </summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="password">كلمة المرور القادمة من البيئة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task SetApplicationPasswordAsync(
        Settings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await using NpgsqlConnection admin = new(settings.Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        // خطوتان على الجلسة نفسها: القيمة تدخل **معاملاً** فلا تُبنى جملة SQL بالنصّ،
        // ثم تُقرأ داخل ‎format(%L)‎ الذي يقتبسها اقتباس PostgreSQL. و‎ALTER ROLE‎ لا
        // يقبل معاملاً مباشرةً — وهذا هو الطريق الوحيد الذي لا يمرّ بتجميع نصّي.
        await using (NpgsqlCommand carry = new("select set_config('babel.app_password', $1, false)", admin))
        {
            carry.Parameters.AddWithValue(password);
            await carry.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await ExecAsync(
            admin,
            $"""
            do $$
            begin
                execute format('alter role {Quote(settings.Ledger.AppRole)} password %L',
                               current_setting('babel.app_password'));
            end
            $$;
            """,
            cancellationToken).ConfigureAwait(false);

        Say.Detail("أُسندت كلمة مرور دور التطبيق من البيئة (لا تُطبع ولا تُسجَّل).");
    }

    private static async Task EnsureDatabaseAsync(
        NpgsqlConnection admin, string database, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand exists = new("select 1 from pg_database where datname = $1", admin);
        exists.Parameters.AddWithValue(database);
        object? found = await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (found is not null)
        {
            return;
        }

        try
        {
            await ExecAsync(admin, $"create database {Quote(database)}", cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException failure) when (failure.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // سباق بين تشغيلين: القاعدة موجودة الآن وهو المطلوب.
        }
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
