using System.Globalization;
using System.Security.Cryptography;
using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Storage.Tests;

/// <summary>
/// المسار كاملاً على <b>PostgreSQL حقيقية ونظام ملفّات حقيقي</b>.
/// <para>
/// <b>ولا محاكاة عمداً:</b> ما يُثبت هنا هو أن الرفض يأتي من الطبقة التي وُضعت له —
/// الصلاحيات ثم المشغّل — ومخزنٌ في الذاكرة يُثبت أن المخزن في الذاكرة يعمل.
/// </para>
/// </summary>
public sealed class AttachmentStoreTests
{
    private static readonly UserId Actor = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));

    /// <summary>بايتات JPEG صادقة الترويسة، بحشو يميّز كل عيّنة.</summary>
    private static byte[] Jpeg(string marker)
    {
        byte[] head = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        byte[] tail = System.Text.Encoding.UTF8.GetBytes(marker);
        return [.. head, .. tail];
    }

    private static string Digest(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static async Task<(FileSystemAttachmentStore Store, StorageOptions Options)?> StoreAsync()
    {
        StorageOptions? options = await StorageTestEnvironment.OptionsAsync(TestContext.Current.CancellationToken);

        return options is null ? null : (new FileSystemAttachmentStore(options, TimeProvider.System), options);
    }

    /// <summary>
    /// مخزنٌ <b>بجذرٍ خاصّ به وحده</b> — لمن يُثبت دعوى «ولم يُكتب شيء» بعدّ الملفّات.
    /// <para>
    /// <b>ولماذا لا يكفي الجذر المشترك:</b> <c>StorageTestEnvironment</c> يُهيّئ مجلّداً
    /// واحداً <b>للعملية كلّها</b> (‏<c>babel-storage-p&lt;pid&gt;_&lt;عشوائي&gt;</c>)، وxunit
    /// يُشغّل أصناف الاختبار <b>بالتوازي</b> داخل العملية الواحدة. فعدُّ الشجرة كلّها يقيس
    /// ما يكتبه <b>غيرُك</b> أيضاً: مقيس في بوّابةٍ كاملة أن هذا الاختبار سقط
    /// <c>Expected: 0 · Actual: 1</c> بينما هو أخضر منفرداً وأخضر بمجموعته وحدها —
    /// و<c>AppendOnlyIsEnforcedByPostgresTests</c> في الصنف المجاور يُودِع مرفقاً حقيقياً.
    /// </para>
    /// <para>
    /// <b>والعلاج تضييق المقياس لا تعطيل التوازي:</b> جذرٌ فرعيّ لكل إثبات ⇒ الكاتب الوحيد
    /// المحتمل فيه هو الإثبات نفسه، فتصير الدعوى «لم يُكتب شيء <b>مني</b>» مقيسةً بالضبط.
    /// وتعطيلُ التوازي كان سيُخفي التسابق ويُبطئ المجموعة، ولا يجعل المقياس صادقاً.
    /// </para>
    /// </summary>
    private static async Task<(FileSystemAttachmentStore Store, StorageOptions Options)?> StoreInAPrivateRootAsync(
        [System.Runtime.CompilerServices.CallerMemberName] string proof = "")
    {
        StorageOptions? shared = await StorageTestEnvironment.OptionsAsync(TestContext.Current.CancellationToken);

        if (shared is null)
        {
            return null;
        }

        StorageOptions own = new()
        {
            OwnerConnectionString = shared.OwnerConnectionString,
            AppConnectionString = shared.AppConnectionString,
            AppRole = shared.AppRole,
            RootPath = Path.Combine(shared.RootPath, "proof-" + proof),
            TicketSigningKey = shared.TicketSigningKey,
        };

        return (new FileSystemAttachmentStore(own, TimeProvider.System), own);
    }

    // ── الإيداع والقراءة ─────────────────────────────────────────────────────

    /// <summary>
    /// إيداعٌ يكتب البايتات مرّة، ويسجّل بصمتها ونوعها المشموم ومسارها الغامض؛
    /// والقراءة تعيدها كما هي.
    /// </summary>
    [Fact]
    public async Task Bytes_go_to_the_store_and_the_digest_goes_beside_the_path()
    {
        if (await StoreAsync() is not var (store, options))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        byte[] content = Jpeg("فاتورة-١");

        Result<StoredAttachment> put = await store.PutAsync(
            new AttachmentSubmission
            {
                Tenant = tenant,
                Actor = Actor,
                Content = content,
                DeclaredFileName = "فاتورة المورد.jpeg",
                DeclaredMediaType = "image/jpeg",
            },
            TestContext.Current.CancellationToken);

        Assert.True(put.IsSuccess, put.IsFailure ? put.Errors[0].ToString() : string.Empty);

        StoredAttachment stored = put.Value;
        Assert.Equal(AttachmentMediaType.Jpeg, stored.MediaType);
        Assert.Equal(Digest(content), stored.ContentHash);
        Assert.Equal(content.Length, stored.ByteLength);
        Assert.Equal(1, stored.Version);
        Assert.True(stored.IsCurrent);

        // المسار غامض ولا يحمل الاسم ولا المعرّف — وهذا ما يجعل تسريب أحدهما بلا أثر.
        Assert.DoesNotContain("فاتورة", stored.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain(stored.Id.Value.ToString("N", CultureInfo.InvariantCulture), stored.ObjectKey, StringComparison.Ordinal);
        Assert.StartsWith(tenant.Value.ToString("N", CultureInfo.InvariantCulture), stored.ObjectKey, StringComparison.Ordinal);

        // والبايتات على القرص فعلاً، تحت الجذر لا خارجه.
        string absolute = Path.Combine(options.RootPath, stored.ObjectKey);
        Assert.True(File.Exists(absolute), absolute);

        Result<AttachmentContent> opened = await store.OpenAsync(tenant, stored.Id, TestContext.Current.CancellationToken);

        Assert.True(opened.IsSuccess);
        Assert.Equal(content, opened.Value.Content.ToArray());
    }

    /// <summary>الحمولة الفارغة والحمولة الضخمة كلتاهما رفضٌ باسمه.</summary>
    [Fact]
    public async Task An_empty_or_oversized_payload_is_refused_by_its_own_code()
    {
        if (await StoreAsync() is not var (_, options))
        {
            return;
        }

        StorageOptions tight = new()
        {
            OwnerConnectionString = options.OwnerConnectionString,
            AppConnectionString = options.AppConnectionString,
            AppRole = options.AppRole,
            RootPath = options.RootPath,
            MaximumBytes = 32,
        };

        FileSystemAttachmentStore store = new(tight, TimeProvider.System);
        TenantId tenant = new(Guid.CreateVersion7());

        Result<StoredAttachment> empty = await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = default },
            TestContext.Current.CancellationToken);

        Assert.Equal("storage.content_empty", empty.Errors[0].Code);

        Result<StoredAttachment> huge = await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg(new string('x', 200)) },
            TestContext.Current.CancellationToken);

        Assert.Equal("storage.content_too_large", huge.Errors[0].Code);
    }

    /// <summary>
    /// <b>الحجم يُفحص قبل الشمّ.</b> حمولةٌ ضخمة لا تُصنَّف ثم تُرفض — تُرفض أولاً.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_payload_is_refused_and_nothing_is_written()
    {
        if (await StoreInAPrivateRootAsync() is not var (store, options))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        int filesBefore = CountFiles(options.RootPath);

        Result<StoredAttachment> put = await store.PutAsync(
            new AttachmentSubmission
            {
                Tenant = tenant,
                Actor = Actor,
                Content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 },
                DeclaredFileName = "invoice.jpg",
                DeclaredMediaType = "image/jpeg",
            },
            TestContext.Current.CancellationToken);

        Assert.True(put.IsFailure);
        Assert.Equal("storage.content_not_recognised", put.Errors[0].Code);
        Assert.Equal(filesBefore, CountFiles(options.RootPath));
    }

    /// <summary>وإعلانٌ يخالف البايتات رفضٌ، ولا يُكتب شيء.</summary>
    [Fact]
    public async Task A_lying_declaration_is_refused_and_nothing_is_written()
    {
        if (await StoreInAPrivateRootAsync() is not var (store, options))
        {
            return;
        }

        int filesBefore = CountFiles(options.RootPath);

        Result<StoredAttachment> put = await store.PutAsync(
            new AttachmentSubmission
            {
                Tenant = new TenantId(Guid.CreateVersion7()),
                Actor = Actor,
                Content = Jpeg("كاذب"),
                DeclaredMediaType = "application/pdf",
            },
            TestContext.Current.CancellationToken);

        Assert.True(put.IsFailure);
        Assert.Equal("storage.declared_type_mismatch", put.Errors[0].Code);
        Assert.Equal(filesBefore, CountFiles(options.RootPath));
    }

    // ── حدّ المستأجر ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>المعرّف المسرَّب لا يعبر حدّ المستأجر.</b> والرفض <c>not_found</c> لا
    /// «ليس لك»: التمييز بينهما يُخبر السائل بوجود ما لا يخصّه.
    /// </summary>
    [Fact]
    public async Task A_leaked_identifier_still_reads_nothing_in_another_tenant()
    {
        if (await StoreAsync() is not var (store, options))
        {
            return;
        }

        TenantId mine = new(Guid.CreateVersion7());
        TenantId theirs = new(Guid.CreateVersion7());

        StoredAttachment stored = (await store.PutAsync(
            new AttachmentSubmission { Tenant = mine, Actor = Actor, Content = Jpeg("سرّي") },
            TestContext.Current.CancellationToken)).Value;

        foreach (Result<StoredAttachment> attempt in new[]
        {
            await store.DescribeAsync(theirs, stored.Id, TestContext.Current.CancellationToken),
        })
        {
            Assert.True(attempt.IsFailure);
            Assert.Equal("storage.attachment_not_found", attempt.Errors[0].Code);
        }

        Result<AttachmentContent> opened = await store.OpenAsync(theirs, stored.Id, TestContext.Current.CancellationToken);
        Assert.Equal("storage.attachment_not_found", opened.Errors[0].Code);

        Result<AttachmentIntegrity> verified = await store.VerifyAsync(theirs, stored.Id, TestContext.Current.CancellationToken);
        Assert.Equal("storage.attachment_not_found", verified.Errors[0].Code);

        Result<StoredAttachment> withdrawn = await store.WithdrawAsync(
            theirs, stored.Id, Actor, "mistake", TestContext.Current.CancellationToken);
        Assert.Equal("storage.attachment_not_found", withdrawn.Errors[0].Code);

        // والمالك الحقيقي ما زال يقرؤه — فالرفض أعلاه ليس عطلاً عامّاً.
        Assert.True((await store.DescribeAsync(mine, stored.Id, TestContext.Current.CancellationToken)
            ).IsSuccess);

        // ومسار المرفق داخل شجرة مستأجره وحده.
        Assert.StartsWith(mine.Value.ToString("N", CultureInfo.InvariantCulture), stored.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain(theirs.Value.ToString("N", CultureInfo.InvariantCulture), stored.ObjectKey, StringComparison.Ordinal);
        Assert.True(Directory.Exists(options.RootPath));
    }

    // ── العبث ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>هذا هو الاختبار الذي وُجدت البصمة لأجله.</b> ملفٌّ بُدِّل تحت المسار نفسه:
    /// ‏<c>Verify</c> يقولها، و<c>Open</c> يرفض التسليم — لا يسلّم ثم يخبر.
    /// </summary>
    [Fact]
    public async Task A_file_swapped_under_the_same_path_is_detected_and_not_served()
    {
        if (await StoreAsync() is not var (store, options))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        byte[] original = Jpeg("الأصل");

        StoredAttachment stored = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = original },
            TestContext.Current.CancellationToken)).Value;

        Result<AttachmentIntegrity> before = await store.VerifyAsync(tenant, stored.Id, TestContext.Current.CancellationToken);
        Assert.True(before.Value.Matches);
        Assert.Equal(original.Length, before.Value.BytesRead);

        // العبث: نفس المسار، بايتات أخرى — وهو ما لا يكتشفه مسارٌ مسجَّل بلا بصمة.
        byte[] swapped = Jpeg("مُبدَّل");
        await File.WriteAllBytesAsync(
            Path.Combine(options.RootPath, stored.ObjectKey), swapped, TestContext.Current.CancellationToken);

        Result<AttachmentIntegrity> after = await store.VerifyAsync(tenant, stored.Id, TestContext.Current.CancellationToken);

        Assert.False(after.Value.Matches);
        Assert.Equal(Digest(original), after.Value.RecordedHash);
        Assert.Equal(Digest(swapped), after.Value.ObservedHash);

        Result<AttachmentContent> opened = await store.OpenAsync(tenant, stored.Id, TestContext.Current.CancellationToken);

        Assert.True(opened.IsFailure);
        Assert.Equal("storage.content_hash_mismatch", opened.Errors[0].Code);
    }

    /// <summary>وصفٌّ قائم بلا بايتات يُقال باسمه، لا يُقرأ فراغاً.</summary>
    [Fact]
    public async Task A_row_whose_bytes_vanished_says_so_by_its_own_code()
    {
        if (await StoreAsync() is not var (store, options))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        StoredAttachment stored = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("مفقود") },
            TestContext.Current.CancellationToken)).Value;

        File.Delete(Path.Combine(options.RootPath, stored.ObjectKey));

        Result<AttachmentContent> opened = await store.OpenAsync(tenant, stored.Id, TestContext.Current.CancellationToken);

        Assert.True(opened.IsFailure);
        Assert.Equal("storage.content_missing", opened.Errors[0].Code);
    }

    // ── التصحيح والسحب ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>التصحيح إصدار جديد يشير إلى سلفه</b> — والسلف يبقى مقروءاً ببايتاته الأصلية.
    /// </summary>
    [Fact]
    public async Task A_correction_is_a_new_version_that_references_its_predecessor()
    {
        if (await StoreAsync() is not var (store, _))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        byte[] first = Jpeg("الصورة الأولى");

        StoredAttachment original = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = first },
            TestContext.Current.CancellationToken)).Value;

        StoredAttachment corrected = (await store.PutAsync(
            new AttachmentSubmission
            {
                Tenant = tenant,
                Actor = Actor,
                Content = Jpeg("الصورة المصحَّحة"),
                Supersedes = original.Id,
            },
            TestContext.Current.CancellationToken)).Value;

        Assert.Equal(2, corrected.Version);
        Assert.Equal(original.Id, corrected.Supersedes);
        Assert.True(corrected.IsCurrent);

        // والسلف يُقرأ الآن وقد صار له خلف — **بلا أن يُعدَّل صفّه**.
        StoredAttachment reread = (await store.DescribeAsync(tenant, original.Id, TestContext.Current.CancellationToken)
            ).Value;

        Assert.Equal(corrected.Id, reread.SupersededBy);
        Assert.False(reread.IsCurrent);
        Assert.Equal(original.ContentHash, reread.ContentHash);

        // وبايتاته الأصلية ما زالت تُقرأ: التصحيح لم يمسّ الدليل السابق.
        Result<AttachmentContent> old = await store.OpenAsync(tenant, original.Id, TestContext.Current.CancellationToken);
        Assert.Equal(first, old.Value.Content.ToArray());
    }

    /// <summary>و<b>السلسلة خطّية</b>: فرعان يصحّحان السلف نفسه رفضٌ باسمه.</summary>
    [Fact]
    public async Task The_version_chain_is_linear_and_does_not_fork()
    {
        if (await StoreAsync() is not var (store, _))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        StoredAttachment original = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("أصل") },
            TestContext.Current.CancellationToken)).Value;

        await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("فرع ١"), Supersedes = original.Id },
            TestContext.Current.CancellationToken);

        Result<StoredAttachment> second = await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("فرع ٢"), Supersedes = original.Id },
            TestContext.Current.CancellationToken);

        Assert.True(second.IsFailure);
        Assert.Equal("storage.attachment_already_superseded", second.Errors[0].Code);
    }

    /// <summary>
    /// <b>السحب علامة لا محو.</b> البايتات باقية، وتُقرأ، ولا يُسحب المرفق مرّتين.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_is_a_marker_and_the_bytes_stay_where_they_are()
    {
        if (await StoreAsync() is not var (store, options))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        byte[] content = Jpeg("سند");

        StoredAttachment stored = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = content },
            TestContext.Current.CancellationToken)).Value;

        StoredAttachment withdrawn = (await store.WithdrawAsync(
            tenant, stored.Id, Actor, "duplicate", TestContext.Current.CancellationToken)).Value;

        Assert.NotNull(withdrawn.Withdrawal);
        Assert.Equal("duplicate", withdrawn.Withdrawal.ReasonKey);
        Assert.Equal(Actor, withdrawn.Withdrawal.WithdrawnBy);
        Assert.False(withdrawn.IsCurrent);

        // البايتات على القرص كما هي — الاحتفاظ بسند القيد واجب نظامي.
        Assert.True(File.Exists(Path.Combine(options.RootPath, stored.ObjectKey)));

        Result<AttachmentContent> opened = await store.OpenAsync(tenant, stored.Id, TestContext.Current.CancellationToken);
        Assert.True(opened.IsSuccess);
        Assert.Equal(content, opened.Value.Content.ToArray());

        // ولا يُسحب مرّتين، ولا يُصحَّح مسحوب.
        Result<StoredAttachment> again = await store.WithdrawAsync(
            tenant, stored.Id, Actor, "duplicate", TestContext.Current.CancellationToken);
        Assert.Equal("storage.attachment_withdrawn", again.Errors[0].Code);

        Result<StoredAttachment> correct = await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("بعد السحب"), Supersedes = stored.Id },
            TestContext.Current.CancellationToken);
        Assert.Equal("storage.attachment_withdrawn", correct.Errors[0].Code);
    }

    /// <summary>
    /// <b>والسباق تحسمه القاعدة، ويصل رفضاً لا استثناءً.</b>
    /// <para>
    /// الفحص «هل صُحِّح السلف من قبل؟» يقرأ ثم يكتب، وبينهما نافذة. فطلبان متزامنان
    /// يمرّان كلاهما من الفحص، ويرفض الفهرس الفريد الجزئي الثاني — <b>والمطلوب أن يصل
    /// ذلك الرفض بالرمز نفسه</b>، لا بـ<c>DbUpdateException</c> يصعد إلى نقطة النهاية
    /// فيصير 500 على واقعةٍ مفهومة تماماً (فخ-41).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_corrections_leave_exactly_one_successor_and_the_loser_gets_a_code()
    {
        if (await StoreAsync() is not var (store, _))
        {
            return;
        }

        TenantId tenant = new(Guid.CreateVersion7());
        StoredAttachment original = (await store.PutAsync(
            new AttachmentSubmission { Tenant = tenant, Actor = Actor, Content = Jpeg("أصل متنازع عليه") },
            TestContext.Current.CancellationToken)).Value;

        AttachmentSubmission Correction(string marker) => new()
        {
            Tenant = tenant,
            Actor = Actor,
            Content = Jpeg(marker),
            Supersedes = original.Id,
        };

        Task<Result<StoredAttachment>> first = Task.Run(
            async () => await store.PutAsync(Correction("متسابق ١"), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Task<Result<StoredAttachment>> second = Task.Run(
            async () => await store.PutAsync(Correction("متسابق ٢"), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Result<StoredAttachment>[] outcomes = await Task.WhenAll(first, second);

        // واحدٌ يفوز وواحدٌ يُرفض — ولا استثناء يتسرّب من أيّهما.
        Assert.Equal(1, outcomes.Count(outcome => outcome.IsSuccess));
        Assert.Equal(1, outcomes.Count(outcome => outcome.IsFailure));

        Result<StoredAttachment> loser = outcomes.Single(outcome => outcome.IsFailure);
        Assert.Equal("storage.attachment_already_superseded", loser.Errors[0].Code);

        // والسلف له خلفٌ **واحد**، وهو الفائز بعينه.
        StoredAttachment reread = (await store.DescribeAsync(tenant, original.Id, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(outcomes.Single(outcome => outcome.IsSuccess).Value.Id, reread.SupersededBy);
    }

    private static int CountFiles(string root) =>
        Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0;
}
