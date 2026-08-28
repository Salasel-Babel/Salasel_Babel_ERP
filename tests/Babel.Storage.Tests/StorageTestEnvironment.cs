using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using Npgsql;

namespace Babel.Storage.Tests;

/// <summary>
/// نطاق التشغيل: لاحقة خاصّة بهذه العملية تُلحَق باسم قاعدة البيانات وبجذر المخزن،
/// فتملك كل عملية قواعدها ومجلداتها وحدها (فخ-51).
/// <para>
/// <b>ولا يُقصّ الاسم أبداً:</b> معرّفات PostgreSQL تُقصّ عند 63 بايتاً <b>بصمت</b>،
/// واسمان مختلفان يُقصّان إلى النصّ نفسه هما قاعدة واحدة.
/// </para>
/// </summary>
internal static class StorageTestScope
{
    /// <summary>حدّ المعرّف في PostgreSQL.</summary>
    public const int MaxIdentifierBytes = 63;

    /// <summary>لاحقة هذه العملية.</summary>
    public static string Suffix { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"p{Environment.ProcessId}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant()}");

    /// <summary>يبني اسماً خاصّاً بهذه العملية من جذع ثابت.</summary>
    /// <param name="stem">الجذع.</param>
    public static string Name(string stem)
    {
        string name = stem + "_" + Suffix;
        int bytes = System.Text.Encoding.UTF8.GetByteCount(name);

        return bytes <= MaxIdentifierBytes
            ? name
            : throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "اسم قاعدة الاختبار «{0}» طوله {1} بايتاً ويتجاوز {2} — القصّ الصامت يجعل اسمين قاعدةً واحدة.",
                    name,
                    bytes,
                    MaxIdentifierBytes),
                nameof(stem));
    }
}

/// <summary>
/// بيئة الاختبار: <b>PostgreSQL حقيقية ونظام ملفّات حقيقي</b>.
/// <para>
/// <b>ولماذا لا محاكاة:</b> الادّعاء المُختبَر هنا هو أن الرفض يأتي من PostgreSQL
/// نفسها — الرمز <c>42501</c> من الصلاحيات، والرمز <c>2F004</c> من المشغّل. ومخزنٌ
/// في الذاكرة يُثبت أن المخزن في الذاكرة يعمل، ولا يقول شيئاً عن الطبقة التي
/// وُجدت هذه الوحدة لأجلها.
/// </para>
/// </summary>
internal static class StorageTestEnvironment
{
    /// <summary>
    /// دور التطبيق. <b>اسمٌ ثابت لأن الأدوار عامّة على العنقود</b>، وإنشاؤه محروس
    /// بقفل استشاري كي لا يتسابق عليه وكيلان يشغّلان المجموعة معاً.
    /// </summary>
    public const string AppRole = "babel_storage_test_app";

    private static readonly string AdminConnectionString =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    /// <summary>هل توجد قاعدة بيانات صالحة للاختبار على هذا الجهاز؟</summary>
    public static bool Available { get; private set; }

    /// <summary>سبب التخطّي إن لم تكن متاحة — <b>يُطبع، فلا يُقرأ الصمت نجاحاً</b>.</summary>
    public static string? Unavailable { get; private set; }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _prepared;
    private static string _database = string.Empty;
    private static string _root = string.Empty;

    /// <summary>يُهيّئ القاعدة والمجلد مرّة واحدة لكل عملية، ويعيد إعدادات جاهزة.</summary>
    public static async Task<StorageOptions?> OptionsAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_prepared)
            {
                _prepared = true;
                await PrepareAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Gate.Release();
        }

        if (!Available)
        {
            return null;
        }

        return new StorageOptions
        {
            OwnerConnectionString = $"Host=127.0.0.1;Port=5432;Database={_database};Username=postgres;Include Error Detail=true",
            AppConnectionString = $"Host=127.0.0.1;Port=5432;Database={_database};Username={AppRole};Include Error Detail=true",
            AppRole = AppRole,
            RootPath = _root,
            TicketSigningKey = RandomNumberGenerator.GetBytes(32),
        };
    }

    /// <summary>الاتصال بدور <b>المالك</b> — لإثبات أن المشغّل يرفضه هو أيضاً.</summary>
    public static string OwnerConnectionString =>
        $"Host=127.0.0.1;Port=5432;Database={_database};Username=postgres;Include Error Detail=true";

    /// <summary>الاتصال بدور <b>التطبيق</b> — لإثبات أن الصلاحيات ترفضه.</summary>
    public static string AppConnectionString =>
        $"Host=127.0.0.1;Port=5432;Database={_database};Username={AppRole};Include Error Detail=true";

    private static async Task PrepareAsync(CancellationToken cancellationToken)
    {
        _database = StorageTestScope.Name("babel_storage_test");
        _root = Path.Combine(Path.GetTempPath(), "babel-storage-" + StorageTestScope.Suffix);

        try
        {
            await using NpgsqlConnection admin = new(AdminConnectionString);
            await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

            // القفل الاستشاري: الدور عامّ على العنقود، وعمليتان تنشئانه معاً تتسابقان.
            await ExecAsync(admin, $"""
                do $role$
                begin
                    perform pg_advisory_xact_lock(hashtextextended('{AppRole}', 0));
                    if not exists (select 1 from pg_roles where rolname = '{AppRole}') then
                        create role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit;
                    end if;
                end
                $role$;
                """, cancellationToken).ConfigureAwait(false);

            await ExecAsync(admin, $"drop database if exists \"{_database}\" with (force)", cancellationToken).ConfigureAwait(false);
            await ExecAsync(admin, $"create database \"{_database}\"", cancellationToken).ConfigureAwait(false);
            await ExecAsync(admin, $"grant connect on database \"{_database}\" to {AppRole}", cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(_root);

            await StorageSchema.DeployAsync(
                new StorageOptions
                {
                    OwnerConnectionString = OwnerConnectionString,
                    AppConnectionString = AppConnectionString,
                    AppRole = AppRole,
                    RootPath = _root,
                },
                cancellationToken).ConfigureAwait(false);

            Available = true;

            // **حذفٌ عند الانتهاء لا عند البدء** (فخ-51): الإسقاط عند البدء على اسم
            // ثابت يقطع جلسات عملية أخرى، والاسم هنا خاصّ بهذه العملية أصلاً.
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => Cleanup();
        }
        catch (NpgsqlException exception)
        {
            Available = false;
            Unavailable = exception.Message;
            Console.WriteLine(
                "── تخطّي اختبارات المخزن: لا PostgreSQL صالحة. وهذا نقصُ تغطية لا نجاح.\n   "
                + exception.Message);
        }
        catch (SocketException exception)
        {
            Available = false;
            Unavailable = exception.Message;
            Console.WriteLine("── تخطّي اختبارات المخزن: تعذّر الاتصال. وهذا نقصُ تغطية لا نجاح.\n   " + exception.Message);
        }
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>ينفّذ عبارة على اتصالٍ بعينه ويعيد رمز الخطأ إن رُفضت.</summary>
    /// <param name="connectionString">الاتصال — مالكاً أو تطبيقاً.</param>
    /// <param name="sql">العبارة.</param>
    /// <returns>‏<c>null</c> إن نجحت، أو رمز <c>SQLSTATE</c> إن رُفضت.</returns>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<string?> RefusalCodeAsync(string connectionString, string sql, CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using NpgsqlCommand command = new(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (PostgresException exception)
        {
            Debug.Assert(exception.SqlState is not null, "PostgresException بلا SQLSTATE");
            return exception.SqlState;
        }
    }

    /// <summary>يُسقط قاعدة هذه العملية ومجلدها. أخطاء التنظيف لا تُفشل تشغيلاً انتهى.</summary>
    private static void Cleanup()
    {
        try
        {
            // تُقطع اتصالات هذه العملية قبل الإسقاط: تجمّع اتصالاتٍ حيّ يجعل
            // ‏drop database يعلن «القاعدة مستعملة» بعد انتظارٍ طويل.
            NpgsqlConnection.ClearAllPools();

            using NpgsqlConnection admin = new(AdminConnectionString);
            admin.Open();
            using NpgsqlCommand drop = new($"drop database if exists \"{_database}\" with (force)", admin);
            drop.ExecuteNonQuery();
        }
        catch (NpgsqlException exception)
        {
            // الخروج لا يُفشَل بسببه، لكنه لا يمرّ صامتاً: قاعدة متروكة خبرٌ يُقال.
            Console.WriteLine("        تعذّر حذف قاعدة هذا التشغيل: " + exception.Message);
        }

        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // كما أعلاه.
        }
        catch (UnauthorizedAccessException)
        {
            // كما أعلاه.
        }
    }
}
