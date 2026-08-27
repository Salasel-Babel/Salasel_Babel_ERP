using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Core.Persistence;
using Npgsql;

namespace Babel.Core.Tests;

/// <summary>
/// بيئة اختبار النواة: قاعدة PostgreSQL <b>حقيقية</b>، ودور تطبيق <b>غير مالك وغير
/// superuser</b>، والمخطّط منشوراً بالهجرة نفسها التي ينشرها الخادم.
/// <para>
/// <b>ولماذا لا قاعدة في الذاكرة:</b> نصف ما يُثبَت هنا لا يقع إلا في PostgreSQL —
/// رفضُ الصلاحيات بالرمز 42501، ومشغّل ثبات مقياس العرض، وقيود التحقق، وذرّية
/// التأسيس الأول بمفتاح أوّلي لا بفحصٍ يسبق كتابة. اختبارٌ بمخزن ذاكرة يمرّ وكلٌّ
/// منها مكسور — وهو بعينه ما وقع: المخزن كان في الذاكرة، فمرّ كل شيء وسقط العرض.
/// </para>
/// <para>
/// واسم القاعدة خاصّ بهذه العملية، وتُحذف عند خروجها: قاعدةٌ باسم ثابت تُسقَط عند
/// البدء تسحب المخطّط من تحت عملية أخرى تعمل الآن.
/// </para>
/// </summary>
internal static class CoreTestEnvironment
{
    /// <summary>دور التطبيق — مشترك بين العمليات عمداً: الأدوار عامّة ولا تملك شيئاً.</summary>
    public const string AppRole = "babel_core_test_app";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;
    private static Exception? _failure;
    private static int _cleanupRegistered;

    /// <summary>قاعدة الاختبار لهذه العملية وحدها.</summary>
    public static string Database { get; } = Name("babel_core_tests");

    /// <summary>اتصال الصيانة — إنشاء القاعدة والدور وحدهما.</summary>
    public static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_CORE_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    /// <summary>إعدادات النواة لهذه العملية — ولا تجاوز من البيئة على اسم القاعدة.</summary>
    public static CoreOptions Options { get; } = new()
    {
        OwnerConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true",
        AppConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true",
        AppRole = AppRole,
    };

    /// <summary>ينشئ القاعدة والدور وينشر المخطّط. يُستدعى في مستهلّ كل حالة.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (_ready)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
            {
                return;
            }

            // فشلٌ جزئي واحد يكفي: لا تُعاد التهيئة على قاعدة نصف مبنيّة.
            if (_failure is not null)
            {
                throw new InvalidOperationException("فشلت تهيئة بيئة اختبار النواة مرّة، ولن يُعاد بناؤها.", _failure);
            }

            try
            {
                RegisterCleanup();
                await CreateDatabaseAndRoleAsync(cancellationToken).ConfigureAwait(false);
                await CoreSchema.DeployAsync(Options, cancellationToken).ConfigureAwait(false);
                _ready = true;
            }
            catch (Exception failure)
            {
                _failure = failure;
                throw;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// ينفّذ جملة بدور <b>المالك</b> — لزرع صفٍّ مخالف عمداً.
    /// <para>
    /// ورمز الإلغاء يُقرأ من <c>TestContext</c> لا يُمرَّر: مُعامِلٌ اختياري هنا يُنسى
    /// في نداءٍ فيبقى النداء غير قابل للإلغاء بصمت.
    /// </para>
    /// </summary>
    /// <param name="sql">الجملة.</param>
    public static async Task<int> OwnerAsync(string sql)
    {
        CancellationToken cancellationToken = Xunit.TestContext.Current.CancellationToken;

        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, owner);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>ينفّذ جملة بدور <b>التطبيق</b> — لإثبات ما لا يستطيعه.</summary>
    /// <param name="sql">الجملة.</param>
    public static async Task<int> ApplicationAsync(string sql)
    {
        CancellationToken cancellationToken = Xunit.TestContext.Current.CancellationToken;

        await using NpgsqlConnection app = new(Options.AppConnectionString);
        await app.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, app);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يقرأ عدداً بدور المالك.</summary>
    /// <param name="sql">الاستعلام.</param>
    public static async Task<long> CountAsync(string sql)
    {
        CancellationToken cancellationToken = Xunit.TestContext.Current.CancellationToken;

        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, owner);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    /// <summary>معرّف منشأة جديد لكل حالة — فلا تتسابق الحالات على الصفّ نفسه.</summary>
    public static Guid NewCompany() => Guid.NewGuid();

    /// <summary>يطبع سطر دليل إلى مخرَج التشغيل — الرقم يُقاس ولا يُدَّعى.</summary>
    /// <param name="text">النصّ.</param>
    public static void Note(string text) => Console.WriteLine("        · " + text);

    private static async Task CreateDatabaseAndRoleAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection admin = new(Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecAsync(admin, $"create database {Database}", cancellationToken).ConfigureAwait(false);

        // القفل الاستشاري يُسلسل إنشاء الدور عبر العمليات المتزامنة: الاسم مشترك،
        // والإنشاء وحده هو ما يتسابق (‏23505 على pg_authid_rolname_index لا 42710).
        await ExecAsync(
            admin,
            $"""
            do $$
            begin
                perform pg_advisory_xact_lock(hashtextextended('{AppRole}', 0));
                begin
                    create role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit;
                exception when duplicate_object or unique_violation then
                    alter role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit;
                end;
            end
            $$;
            """,
            cancellationToken).ConfigureAwait(false);

        string password = Environment.GetEnvironmentVariable("BABEL_CORE_TEST_APP_PASSWORD") ?? string.Empty;

        if (password.Length > 0)
        {
            await ExecAsync(
                admin,
                $"alter role {AppRole} password '{password.Replace("'", "''", StringComparison.Ordinal)}'",
                cancellationToken).ConfigureAwait(false);
        }

        await ExecAsync(admin, $"grant connect on database {Database} to {AppRole}", cancellationToken)
            .ConfigureAwait(false);
    }

    private static void RegisterCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => Drop();
    }

    private static void Drop()
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
            using NpgsqlConnection admin = new(Maintenance);
            admin.Open();
            using NpgsqlCommand terminate = new(
                $"select pg_terminate_backend(pid) from pg_stat_activity where datname = '{Database}' and pid <> pg_backend_pid()",
                admin);
            terminate.ExecuteNonQuery();
            using NpgsqlCommand drop = new($"drop database if exists {Database}", admin);
            drop.ExecuteNonQuery();
        }
        catch (NpgsqlException exception)
        {
            // قاعدة متروكة خبرٌ يُقال، ولا يُفشَل الخروج بسببه.
            Console.WriteLine("        تعذّر حذف قاعدة هذا التشغيل: " + exception.Message);
        }
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// اسمٌ خاصّ بهذه العملية. المعرّف وحده لا يكفي — نظام التشغيل يعيد استعماله —
    /// فيُضاف رمز عشوائي معمّى. ولا قصّ أبداً: حدّ المعرّف 63 بايتاً ويُقصّ صامتاً.
    /// </summary>
    private static string Name(string stem)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        string name = string.Create(
            CultureInfo.InvariantCulture,
            $"{stem}_p{Environment.ProcessId}_{token}");

        return Encoding.UTF8.GetByteCount(name) <= 63
            ? name
            : throw new UnreachableException("اسم قاعدة اختبار النواة تجاوز 63 بايتاً: " + name);
    }
}
