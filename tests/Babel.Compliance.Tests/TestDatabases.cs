using Npgsql;

namespace Babel.Compliance.Tests;

/// <summary>
/// قواعد بيانات هذه المجموعة — <b>كلٌّ منها مملوكة لعمليتها وحدها</b>.
/// <para>
/// <b>ما كان قبل هذا الملف:</b> اسمان ثابتان
/// (<c>babel_compliance_test_pipeline</c> و<c>babel_compliance_test_counter</c>)
/// وتهيئةٌ تبدأ بـ<c>drop database if exists … with (force)</c>. و<c>with (force)</c>
/// هو الحدّ الحادّ: إنه يقطع <b>جلسات عمليات أخرى</b>. فعمليتان متزامنتان من هذه
/// المجموعة تُسقط كلٌّ منهما قاعدة الأخرى في منتصف تشغيلها.
/// </para>
/// <para>
/// <b>مقيس على هذا الجهاز على الشيفرة قبل هذا الإصلاح:</b> أربع عمليات متزامنة من
/// <c>RelationalStoreTests</c> ⇒ <b>خمسة إخفاقات من ثمانية تنفيذات</b>، كلها
/// <c>23505: duplicate key value violates unique constraint "pg_database_datname_index"</c>
/// — أي أن عمليةً أسقطت القاعدة بينما أخرى تُنشئها.
/// </para>
/// <para>
/// والعلاج منسوخ حرفياً من <c>tests/Babel.Api.Tests/TestRunScope.cs</c> و
/// <c>tests/Babel.Sales.Tests/SalesTestEnvironment.cs</c>: لاحقةٌ لكل عملية،
/// و<b>لا إسقاط عند البدء</b>، والإسقاط عند خروج العملية، وكنسٌ لما تركته عملية
/// <b>ثبت موتها</b>.
/// (‏<c>docs/evidence/traps.md#fakh-test-databases-share-a-fixed-name-across-processes</c>)
/// </para>
/// </summary>
internal static class TestDatabases
{
    /// <summary>جذع قاعدة اختبار خطّ الأنابيب الكامل.</summary>
    public const string PipelineStem = "babel_compliance_test_pipeline";

    /// <summary>جذع قاعدة اختبار العدّاد.</summary>
    public const string CounterStem = "babel_compliance_test_counter";

    /// <summary>عدد محاولات الحذف قبل اللجوء إلى الإنهاء القسري.</summary>
    private const int DropAttempts = 40;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HashSet<string> Owned = new(StringComparer.Ordinal);
    private static string? _maintenance;
    private static int _swept;
    private static int _cleanupRegistered;

    /// <summary>قاعدة خطّ الأنابيب <b>لهذه العملية وحدها</b>.</summary>
    public static string Pipeline { get; } = TestRunScope.Name(PipelineStem);

    /// <summary>قاعدة العدّاد <b>لهذه العملية وحدها</b>.</summary>
    public static string Counter { get; } = TestRunScope.Name(CounterStem);

    /// <summary>
    /// ينشئ قاعدةً خاصّةً بهذه العملية ويُرجع اتصالها.
    /// <para>
    /// <b>لا <c>drop</c> هنا إطلاقاً:</b> الاسم لم يوجد قبل هذه العملية، فإن وُجد فذلك
    /// خبرٌ حقيقي يُرفع بصوته (‏<c>42P04</c>)، لا يُبتلع بإسقاط قاعدة قد تكون لغيرنا.
    /// </para>
    /// </summary>
    /// <param name="maintenance">اتصال الصيانة (‏<c>BABEL_COMPLIANCE_TEST_DB</c>).</param>
    /// <param name="database">اسم القاعدة الخاصّ بهذه العملية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    /// <returns>سلسلة اتصال بالقاعدة المُنشأة.</returns>
    public static async Task<string> CreateAsync(
        string maintenance,
        string database,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(maintenance);
        ArgumentException.ThrowIfNullOrEmpty(database);

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _maintenance = maintenance;
            RegisterCleanup();

            await using NpgsqlConnection admin = new(maintenance);
            await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

            // كنسة واحدة لكل عملية تكفي: ما مات قبلها لا يُبعث أثناءها.
            if (Interlocked.Exchange(ref _swept, 1) == 0)
            {
                await SweepAbandonedAsync(admin, cancellationToken).ConfigureAwait(false);
            }

            await ExecAsync(admin, $"create database {database}", cancellationToken).ConfigureAwait(false);
            lock (Owned)
            {
                Owned.Add(database);
            }
        }
        finally
        {
            Gate.Release();
        }

        return new NpgsqlConnectionStringBuilder(maintenance) { Database = database }.ConnectionString;
    }

    /// <summary>
    /// يُسجّل حذف قواعد هذه العملية <b>عند خروجها لا عند بدئها</b>.
    /// <para>
    /// الحذف عند البدء هو العطل نفسه: افتراضٌ صامت بأن لا أحد غيرك يعمل الآن.
    /// و<c>ProcessExit</c> يعمل عند الخروج الطبيعي وعند الفشل وعند <c>SIGTERM</c>؛
    /// أمّا <c>SIGKILL</c> فلا يترك فرصة، ولذلك يُكنس المتروك في التشغيل التالي.
    /// </para>
    /// </summary>
    private static void RegisterCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DropOwnDatabases();
    }

    private static void DropOwnDatabases()
    {
        string? maintenance = _maintenance;
        if (maintenance is null)
        {
            return;
        }

        string[] databases;
        lock (Owned)
        {
            databases = [.. Owned];
        }

        try
        {
            NpgsqlConnection.ClearAllPools();

            using NpgsqlConnection admin = new(maintenance);
            admin.Open();
            foreach (string database in databases)
            {
                DropOne(admin, database);
            }
        }
        catch (NpgsqlException exception)
        {
            // الخروج لا يُفشَل بسببه، لكنه لا يمرّ صامتاً: قاعدة متروكة خبرٌ يُقال.
            Console.WriteLine("        تعذّر حذف قواعد هذا التشغيل: " + exception.Message);
        }
    }

    private static void DropOne(NpgsqlConnection admin, string database)
    {
        // تُقطع اتصالات **هذه العملية** قبل أوّل محاولة لا بعد فشلها: PostgreSQL ينتظر
        // خمس ثوانٍ كاملة قبل أن يعلن «القاعدة مستعملة». والقطع هنا لا يمسّ أحداً —
        // الاسم خاصّ بهذه العملية والجلسات عليه جلساتها. وهذا هو الفرق كلّه عن
        // `with (force)` على اسم ثابت، الذي كان يقطع جلسات عملية أخرى تعمل.
        TerminateOwnSessions(admin, database);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using NpgsqlCommand command = new($"drop database if exists {database}", admin);
                command.ExecuteNonQuery();
                return;
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.ObjectInUse && attempt < DropAttempts)
            {
                TerminateOwnSessions(admin, database);
                Thread.Sleep(25);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ObjectInUse)
            {
                using NpgsqlCommand forced = new($"drop database if exists {database} with (force)", admin);
                forced.ExecuteNonQuery();
                return;
            }
        }
    }

    private static void TerminateOwnSessions(NpgsqlConnection admin, string database)
    {
        using NpgsqlCommand command = new(
            "select pg_terminate_backend(pid) from pg_stat_activity where datname = $1 and pid <> pg_backend_pid()",
            admin);
        command.Parameters.AddWithValue(database);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// يحذف قواعد تشغيلات سابقة قُتلت قبل أن تُصفّي نفسها — ولا يمسّ قاعدة عمليةٍ حيّة
    /// أبداً. وعند الشكّ في حياة المالك تُترك القاعدة: تسريبها خبرٌ سيّئ، وحذفها تحت
    /// اختبارٍ جارٍ عطلٌ يوم كامل.
    /// </summary>
    private static async Task SweepAbandonedAsync(NpgsqlConnection admin, CancellationToken cancellationToken)
    {
        foreach (string stem in new[] { PipelineStem, CounterStem })
        {
            List<string> candidates = [];

            await using (NpgsqlCommand query = new(
                "select datname from pg_database where datname like $1", admin))
            {
                query.Parameters.AddWithValue(stem + "_p%");
                await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    candidates.Add(reader.GetString(0));
                }
            }

            foreach (string database in candidates)
            {
                int? owner = TestRunScope.OwnerProcessId(database, stem);
                if (owner is null || TestRunScope.OwnerIsAlive(owner.Value))
                {
                    continue;
                }

                try
                {
                    // بلا (force): إن كان عليها اتصال حيّ فالمالك لم يمت حقاً، فتُترك.
                    await ExecAsync(admin, $"drop database if exists {database}", cancellationToken)
                        .ConfigureAwait(false);
                    Console.WriteLine("        كُنست قاعدة متروكة من تشغيل ميت: " + database);
                }
                catch (PostgresException exception)
                {
                    Console.WriteLine(
                        "        تُركت قاعدة متروكة كما هي (" + exception.SqlState + "): " + database);
                }
            }
        }
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
