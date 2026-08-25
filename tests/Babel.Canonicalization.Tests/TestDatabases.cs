using Npgsql;

namespace Babel.Canonicalization.Tests;

/// <summary>
/// قاعدة الدورة الحقيقية — <b>مملوكة لعمليتها وحدها</b>.
/// <para>
/// <b>ما كان قبل هذا الملف:</b> اسم ثابت <c>babel_canon_tests</c>، و<c>drop table if
/// exists canon_chain; create table …</c> عند تهيئة <b>كل</b> حالة اختبار. فعمليتان
/// متزامنتان تُفرِّغ كلٌّ منهما جداول الأخرى في منتصف تشغيلها، فيسقط اختبارٌ سليم
/// تماماً بسبب لا علاقة له بما يفحصه.
/// </para>
/// <para>
/// <b>مقيس على هذا الجهاز على الشيفرة قبل هذا الإصلاح:</b> أربع عمليات متزامنة من
/// <c>PostgresRoundTripTests</c> ⇒ <b>خمسة عشر إخفاقاً من عشرين تنفيذاً</b>، موزّعةً
/// على أربع حالات من الخمس.
/// </para>
/// <para>
/// والعلاج منسوخ من <c>tests/Babel.Api.Tests/TestRunScope.cs</c> و
/// <c>tests/Babel.Sales.Tests/SalesTestEnvironment.cs</c>: لاحقةٌ لكل عملية، و<b>لا
/// إسقاط عند البدء</b>، والإسقاط عند خروج العملية، وكنسٌ لما تركته عملية <b>ثبت
/// موتها</b>.
/// (‏<c>docs/evidence/traps.md#fakh-test-databases-share-a-fixed-name-across-processes</c>)
/// </para>
/// </summary>
internal static class TestDatabases
{
    /// <summary>الجذع الثابت لاسم قاعدة الدورة — تُلحق به لاحقة هذه العملية.</summary>
    public const string CanonStem = "babel_canon_tests";

    /// <summary>عدد محاولات الحذف قبل اللجوء إلى الإنهاء القسري.</summary>
    private const int DropAttempts = 40;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _maintenance;
    private static bool _created;
    private static int _cleanupRegistered;

    /// <summary>قاعدة الدورة <b>لهذه العملية وحدها</b>.</summary>
    public static string Canon { get; } = TestRunScope.Name(CanonStem);

    /// <summary>
    /// يضمن وجود قاعدة هذه العملية — يُنشئها مرّةً واحدة، ويكنس ما تركته تشغيلات ماتت.
    /// <para>
    /// <b>ولا <c>drop database</c> عند البدء إطلاقاً:</b> الاسم لم يوجد قبل هذه العملية،
    /// فإن وُجد فذلك خبرٌ حقيقي يُرفع بصوته لا يُبتلع بإسقاط قاعدة قد تكون لغيرنا.
    /// </para>
    /// </summary>
    /// <param name="maintenance">اتصال الصيانة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task EnsureAsync(string maintenance, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(maintenance);

        if (_created)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_created)
            {
                return;
            }

            _maintenance = maintenance;
            RegisterCleanup();

            await using NpgsqlConnection admin = new(maintenance);
            await admin.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SweepAbandonedAsync(admin, cancellationToken).ConfigureAwait(false);
            await ExecAsync(admin, $"create database {Canon}", cancellationToken).ConfigureAwait(false);

            _created = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// يُسجّل حذف قاعدة هذه العملية <b>عند خروجها لا عند بدئها</b>.
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

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DropOwnDatabase();
    }

    private static void DropOwnDatabase()
    {
        string? maintenance = _maintenance;
        if (maintenance is null || !_created)
        {
            return;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();

            using NpgsqlConnection admin = new(maintenance);
            admin.Open();
            DropOne(admin, Canon);
        }
        catch (NpgsqlException exception)
        {
            // الخروج لا يُفشَل بسببه، لكنه لا يمرّ صامتاً: قاعدة متروكة خبرٌ يُقال.
            Console.WriteLine("        تعذّر حذف قاعدة هذا التشغيل: " + exception.Message);
        }
    }

    private static void DropOne(NpgsqlConnection admin, string database)
    {
        // تُقطع اتصالات **هذه العملية** قبل أوّل محاولة لا بعد فشلها: PostgreSQL ينتظر
        // خمس ثوانٍ كاملة قبل أن يعلن «القاعدة مستعملة». والقطع هنا لا يمسّ أحداً —
        // الاسم خاصّ بهذه العملية والجلسات عليه جلساتها. وهذا هو الفرق كلّه عن
        // `with (force)` على اسم ثابت، الذي يقطع جلسات عملية أخرى تعمل.
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
        List<string> candidates = [];

        await using (NpgsqlCommand query = new(
            "select datname from pg_database where datname like $1", admin))
        {
            query.Parameters.AddWithValue(CanonStem + "_p%");
            await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                candidates.Add(reader.GetString(0));
            }
        }

        foreach (string database in candidates)
        {
            int? owner = TestRunScope.OwnerProcessId(database, CanonStem);
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

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
