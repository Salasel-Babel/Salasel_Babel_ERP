using Babel.Ai.Lookup;
using Babel.Core.NameRegister;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>تعريفان للطيّ، ويجب أن يتّفقا حرفاً بحرف — وامتدادٌ مفقود يوقف النشر.</b>
/// <para>
/// <b>لماذا تعريفان أصلاً:</b> المطابقة كلّها تجري في القاعدة، فدالّة <c>babel.fold_arabic</c>
/// هي وحدها التي تحكم العمود المخزَّن ونصّ الاستعلام. والنسخة بلغة C#
/// (<see cref="ArabicNameFold"/>) موجودة لقاعدة السبر — مفتاحان أحدهما بادئة الآخر —
/// ولاختبارات لا تحتاج قاعدة. <b>وتعريفان لا يتّفقان أسوأ من تعريفٍ واحد ناقص</b>،
/// ولذلك هذا الحارس: هو <b>الشاهد الموجب</b> على أن المرآة ما زالت مرآة، على نمط
/// ‏<c>TheSecretGuardCarriesItsOwnPositiveControl</c> و‏ADR-0056.
/// </para>
/// </summary>
public sealed class TheTwoFoldsAgreeAndTheMissingExtensionStopsTheDeploy
{
    /// <summary>
    /// المتن: كل قاعدة طيٍّ مرّةً على الأقلّ، ومعها ما لا يُطوى — فالاتّفاق على نصٍّ
    /// لا يتغيّر ليس اتّفاقاً.
    /// </summary>
    /// <summary>المتن نفسه — مصفوفةٌ واحدة يقرؤها المُعطي والحارس معاً.</summary>
    public static readonly string[] CorpusTexts =
    [
            "أحمد", "احمد", "إبراهيم", "آدم", "ٱحمد",
            "محمــــد", "محمّد", "مُحَمَّدٌ", "فاطمة", "يحيى",
            "مؤسسة", "رئيس", "عبدالله", "عبد الله",
            "شركة المسار الأمثل", "شركة المسار الامثل", "محمد علي القحطاني",
            "مستودع ٣", "مستودع ۳", "مستودع 3",
            "Al-Masar LLC", "AL-MASAR llc", "  شركة   المسار  ",
            "شركة المسار", "شركة\u200FالمسارStore\u200B", "الرياض", "رياض",
            "شركة المسارات", "القحطان", "محمد الغامدي", "خالد بن عبد العزيز",
    ];

    /// <summary>المتن للمُعطي.</summary>
    public static TheoryData<string> Corpus()
    {
        TheoryData<string> data = [];
        foreach (string text in CorpusTexts)
        {
            data.Add(text);
        }

        return data;
    }

    /// <summary>الطيّان يُنتجان النصّ نفسه بالضبط — بالمفتاحين معاً.</summary>
    /// <param name="text">مدخلٌ من المتن.</param>
    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task TheCSharpFoldMatchesTheDatabaseFold(string text)
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        string database = await LookupTestEnvironment.ScalarAsync(
            "select babel.fold_arabic(@value)", text, TestContext.Current.CancellationToken);
        string tight = await LookupTestEnvironment.ScalarAsync(
            "select babel.fold_arabic_tight(@value)", text, TestContext.Current.CancellationToken);

        Assert.Equal(database, ArabicNameFold.Fold(text));
        Assert.Equal(tight, ArabicNameFold.FoldTight(text));
    }

    /// <summary>
    /// <b>حارس لافراغ للمتن نفسه:</b> لو صار الطيّ دالّةَ هوية لمرّ الاتّفاق أعلاه دائماً.
    /// فالمتن يجب أن يحمل نصّاً واحداً على الأقلّ يتغيّر بالطيّ فعلاً.
    /// </summary>
    [Fact]
    public void TheCorpusActuallyExercisesTheFold()
    {
        int changed = CorpusTexts.Count(
            static text => !string.Equals(text, ArabicNameFold.Fold(text), StringComparison.Ordinal));

        Assert.True(changed >= 15, FormattableString.Invariant($"المتن لا يُشغّل الطيّ إلا في {changed} نصّاً — فالاتّفاق أعلاه يمرّ فارغاً"));
    }

    /// <summary>
    /// <b>الشاهد الموجب على الرفض:</b> امتدادٌ لا وجود له يوقف النشر برسالةٍ تسمّيه وتسمّي
    /// ما يُفقَد بغيابه.
    /// <para>
    /// وهو الفحص الذي لا يمكن أن يُقاس على <c>pg_trgm</c> نفسه على هذا الجهاز — فهو متاح —
    /// فيُقاس على اسمٍ مفتعَل: المطلوب إثباته أن <b>المسار يرفض</b>، لا أن الامتداد الفلاني
    /// موجود. وحارسٌ لم يُرَ وهو ينطق لا يُصدَّق صمتُه (‏ADR-0056).
    /// </para>
    /// </summary>
    [Fact]
    public async Task AMissingExtensionStopsTheDeployAndNamesWhatIsLost()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection =
            await LookupTestEnvironment.OpenAsync(TestContext.Current.CancellationToken);

        await using NpgsqlCommand command = new(
            "select babel.require_extension('babel_no_such_extension', @lost)", connection);
        command.Parameters.AddWithValue("lost", "مطابقة الأسماء بالتشابه الثلاثي");

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
            async () => await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        // ‏0A000 = feature_not_supported: النشر يقف، ولا يمضي بفهرسٍ غير مبنيّ.
        Assert.Equal("0A000", refusal.SqlState);
        Assert.Contains("babel_no_such_extension", refusal.MessageText, StringComparison.Ordinal);
        Assert.Contains("مطابقة الأسماء بالتشابه الثلاثي", refusal.MessageText, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والامتداد الحقيقيّ مُركَّب فعلاً وفهرساه موجودان.</b> نشرٌ يمضي بلا فهرس يبقى
    /// صحيحاً ويصير مسحاً كاملاً، فيظهر العطل بطئاً تحت الحمل لا خطأً عند النشر — ولذلك
    /// يُقرأ وجودهما لا يُفترض.
    /// </summary>
    [Fact]
    public async Task TheExtensionAndBothIndexesAreActuallyThere()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection =
            await LookupTestEnvironment.OpenAsync(TestContext.Current.CancellationToken);

        await using NpgsqlCommand command = new(
            """
            select
              (select count(*) from pg_extension where extname = 'pg_trgm'),
              (select count(*) from pg_indexes
                where schemaname = 'sales'
                  and indexname in ('ix_sales_customer_search', 'ix_sales_customer_search_tight'))
            """,
            connection);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
    }

    /// <summary>
    /// عمودٌ غائب في وصف الجدول يُرفَع بصوته ولا يُتخطّى — ووصفٌ بلا عمود نطاق لا يُبنى أصلاً.
    /// </summary>
    [Fact]
    public async Task AnAbsentColumnIsRefusedAndAScopelessRegisterCannotBeDescribed()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        // ‏`sales.customer` لا يحمل `CompanyId` — مقيس على هذه الشجرة.
        NameRegisterTable withCompany = new(
            "customer_with_company", "sales", "customer", "Id", "NameAr", ["TenantId", "CompanyId"]);

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
            async () => await NameRegisterSchema.AttachAsync(
                LookupTestEnvironment.ConnectionString, withCompany, TestContext.Current.CancellationToken));

        // ‏42703 = undefined_column.
        Assert.Equal("42703", refusal.SqlState);
        Assert.Contains("CompanyId", refusal.MessageText, StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(
            static () => new NameRegisterTable("x", "sales", "customer", "Id", "NameAr", []));

        // ومعرّفٌ فيه علامة اقتباس لا يمرّ إلى نصّ استعلام بحال.
        Assert.Throws<ArgumentException>(
            static () => new NameRegisterTable("x", "sales", "customer\"; drop table sales.customer; --", "Id", "NameAr", ["TenantId"]));
    }

    /// <summary>
    /// <b>وسجلّان بمفتاحٍ واحد لا يُركَّبان.</b> سجلٌّ نصفه صالح يبحث تسعاً وتسعين مرّة في
    /// سجلّه ثم مرّةً في سجلّ وحدةٍ أخرى — نفس علّة <c>AiModuleRegistration</c>.
    /// </summary>
    [Fact]
    public void TwoRegistersClaimingOneKeyRefuseToCompose()
    {
        ArgumentException refusal = Assert.Throws<ArgumentException>(() => new NameRegisterLookup(
            [LookupTestEnvironment.Register(), LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options));

        Assert.Contains("customer", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>سجلٌّ غير مسجَّل يُرفض ولا يُبحَث في سجلٍّ غيره.</summary>
    [Fact]
    public async Task AnUnknownRegisterIsRefusedRatherThanSubstituted()
    {
        NameRegisterLookup lookup = new(
            [LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options);

        Result<NameLookupResult> answer = await lookup.ResolveAsync(
            "supplier",
            "شركة المسار الأمثل",
            new LookupSession(LookupTestEnvironment.UnknownRegisterTenant, Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(answer.IsFailure);
        Assert.Equal("ai.lookup.register_not_registered", answer.Errors[0].Code);
    }

    /// <summary>نصٌّ يطوى إلى فراغ ليس سؤالاً — ولا يُطابق السجلّ كلّه.</summary>
    [Fact]
    public async Task TextThatFoldsToNothingIsRefused()
    {
        NameRegisterLookup lookup = new(
            [LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options);

        Result<NameLookupResult> answer = await lookup.ResolveAsync(
            "customer",
            "  \u200Fـــًٌ ",
            new LookupSession(LookupTestEnvironment.EmptyTextTenant, Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(answer.IsFailure);
        Assert.Equal("ai.lookup.text_empty", answer.Errors[0].Code);
    }
}
