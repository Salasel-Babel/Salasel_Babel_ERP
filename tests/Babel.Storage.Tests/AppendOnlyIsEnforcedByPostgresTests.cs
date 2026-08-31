using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Storage.Tests;

/// <summary>
/// <b>الطبقتان اللتان تبقيان صحيحتين حين تفشل الشيفرة.</b>
/// <para>
/// «الشيفرة لا تُحدِّث صفّ مرفق» جملة صحيحة اليوم ويُلتَفّ عليها غداً. وهذه المجموعة
/// لا تسأل الشيفرة شيئاً: تفتح اتصالاً وتُصدر <c>UPDATE</c> و<c>DELETE</c> خامَّين،
/// مرّةً بدور التطبيق ومرّةً <b>بدور المالك</b>، وتقرأ رمز <c>SQLSTATE</c> الذي عاد.
/// </para>
/// <list type="number">
///   <item><b>‏42501</b> — <c>insufficient_privilege</c>: الصلاحيات. تحرس ضدّ التطبيق
///         ومَن سرق بيانات اعتماده.</item>
///   <item><b>‏23001</b> — <c>restrict_violation</c>: المشغّل. يحرس ضدّ <b>المالك نفسه</b>،
///         أي ضدّ سكربت الصيانة و<c>psql</c> المفتوح — وهو المسار الذي تفوته الطبقة الأولى.</item>
/// </list>
/// </summary>
public sealed class AppendOnlyIsEnforcedByPostgresTests
{
    /// <summary>‏<c>insufficient_privilege</c> — رفضٌ من الصلاحيات.</summary>
    public const string InsufficientPrivilege = "42501";

    /// <summary>‏<c>restrict_violation</c> — رفضٌ من مشغّل «يُضاف ولا يُعدَّل».</summary>
    public const string RestrictViolation = "23001";

    private static readonly UserId Actor = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private static async Task<(StoredAttachment Row, TenantId Tenant)?> SeedAsync()
    {
        StorageOptions? options = await StorageTestEnvironment.OptionsAsync(TestContext.Current.CancellationToken);
        if (options is null)
        {
            return null;
        }

        FileSystemAttachmentStore store = new(options, TimeProvider.System);
        TenantId tenant = new(Guid.CreateVersion7());

        Result<StoredAttachment> put = await store.PutAsync(
            new AttachmentSubmission
            {
                Tenant = tenant,
                Actor = Actor,
                Content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x41 },
            },
            TestContext.Current.CancellationToken);

        Assert.True(put.IsSuccess, put.IsFailure ? put.Errors[0].ToString() : string.Empty);
        return (put.Value, tenant);
    }

    /// <summary>
    /// دور التطبيق يستطيع أن يقرأ ويُدرج، <b>ولا يستطيع أن يُحدِّث ولا أن يحذف ولا
    /// أن يقتطع</b> — والرفض يأتي من PostgreSQL بالرمز 42501 قبل أي منطق.
    /// </summary>
    [Fact]
    public async Task The_application_role_may_add_and_read_and_nothing_else()
    {
        if (await SeedAsync() is not var (row, _))
        {
            return;
        }

        string id = row.Id.Value.ToString();
        string application = StorageTestEnvironment.AppConnectionString;

        // القراءة تعمل — فالرفض أدناه ليس عجزاً عن الاتصال.
        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            application, "select count(*) from storage.attachment", TestContext.Current.CancellationToken));

        foreach (string statement in new[]
        {
            $"update storage.attachment set \"FileName\" = 'swapped.jpg' where \"Id\" = '{id}'",
            $"update storage.attachment set \"ContentHash\" = repeat('0', 64) where \"Id\" = '{id}'",
            $"delete from storage.attachment where \"Id\" = '{id}'",
            "truncate storage.attachment cascade",
            $"update storage.attachment_withdrawal set \"ReasonKey\" = 'x' where \"AttachmentId\" = '{id}'",
            $"delete from storage.attachment_withdrawal where \"AttachmentId\" = '{id}'",
        })
        {
            string? code = await StorageTestEnvironment.RefusalCodeAsync(
                application, statement, TestContext.Current.CancellationToken);

            Assert.Equal(InsufficientPrivilege, code);
        }
    }

    /// <summary>
    /// <b>والمالك يُرفض أيضاً</b> — وهذه هي الطبقة التي تفوت كل نظام يكتفي بالصلاحيات.
    /// <para>
    /// ولا يُدَّعى أن هذا مثالي: من يملك المخطّط يستطيع أن يُسقط المشغّل. لكن الإسقاط
    /// <b>فعلٌ صريح باسمه</b> يظهر في السجلّ وفي المراجعة، و<c>UPDATE</c> بلا مشغّل
    /// لا يترك أثراً على الإطلاق. الغرض أن يصير التعديل قراراً يُتّخذ لا حادثاً يقع.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Even_the_owner_role_is_refused_by_the_trigger()
    {
        if (await SeedAsync() is not var (row, _))
        {
            return;
        }

        string id = row.Id.Value.ToString();
        string owner = StorageTestEnvironment.OwnerConnectionString;

        foreach (string statement in new[]
        {
            $"update storage.attachment set \"ContentHash\" = repeat('a', 64) where \"Id\" = '{id}'",
            $"delete from storage.attachment where \"Id\" = '{id}'",
        })
        {
            string? code = await StorageTestEnvironment.RefusalCodeAsync(
                owner, statement, TestContext.Current.CancellationToken);

            Assert.Equal(RestrictViolation, code);
        }

        // والمالك يُدرج بلا مانع: المشغّل يحرس التعديل لا الإضافة.
        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            owner, "select 1 from storage.attachment limit 1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// والرسالة تقول <b>ما البديل</b>، بالعربية والإنجليزية: تصحيحٌ إصدارٌ جديد،
    /// وإزالةٌ علامة سحب. رفضٌ لا يقول البديل يُنتج التفافاً لا امتثالاً.
    /// </summary>
    [Fact]
    public async Task The_refusal_names_the_alternative_in_both_languages()
    {
        if (await SeedAsync() is not var (row, _))
        {
            return;
        }

        await using NpgsqlConnection connection = new(StorageTestEnvironment.OwnerConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using NpgsqlCommand command = new(
            $"update storage.attachment set \"Version\" = 9 where \"Id\" = '{row.Id.Value}'",
            connection);

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
            async () => await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestrictViolation, refusal.SqlState);
        Assert.Contains("ATTACHMENT_IS_APPEND_ONLY", refusal.MessageText, StringComparison.Ordinal);
        Assert.Contains("التصحيح إصدار جديد", refusal.MessageText, StringComparison.Ordinal);
        Assert.Contains("append-only", refusal.MessageText, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهد موجب على أن الفحص ما زال يطابق.</b> جدولٌ في المخطّط نفسه بلا مشغّل
    /// ولا سحب صلاحيات <b>يُحدَّث بنجاح</b> — فلو صار كل <c>UPDATE</c> في هذه القاعدة
    /// يفشل لسبب لا علاقة له بما نحرسه، لسقط هذا الاختبار وانكشف الالتباس.
    /// </summary>
    [Fact]
    public async Task The_guard_is_not_vacuous_an_unguarded_table_updates_fine()
    {
        StorageOptions? options = await StorageTestEnvironment.OptionsAsync(TestContext.Current.CancellationToken);
        if (options is null)
        {
            return;
        }

        string owner = StorageTestEnvironment.OwnerConnectionString;

        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            owner,
            "create table if not exists storage.mutation_probe (id int primary key, note text)",
            TestContext.Current.CancellationToken));

        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            owner,
            "insert into storage.mutation_probe values (1, 'before') on conflict (id) do nothing",
            TestContext.Current.CancellationToken));

        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            owner,
            "update storage.mutation_probe set note = 'after' where id = 1",
            TestContext.Current.CancellationToken));

        Assert.Null(await StorageTestEnvironment.RefusalCodeAsync(
            owner, "delete from storage.mutation_probe where id = 1", TestContext.Current.CancellationToken));
    }
}
