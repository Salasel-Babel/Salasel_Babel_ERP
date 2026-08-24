using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 11 — محرك الترحيل يحمل الثوابت المقيسة، لا يذكّر بها.</b>
/// <para>
/// القواعد التسع الأولى تحرس <b>الحدود</b>؛ هذه تحرس <b>السلوك المقيس</b> داخل
/// الدفتر. كل بند أدناه يقابل فخّاً في <c>docs/evidence/traps.md</c> وقع فعلاً
/// وقيس أثره، وارتداده لا يُرى في اختبار وحدة ولا في مراجعة: يُنتج رقماً خاطئاً
/// صامتاً يُكتشف عند التدقيق بعد شهور.
/// </para>
/// <list type="number">
///   <item><b>لا <c>SEQUENCE</c> للترقيم</b> — التسلسل غير معاملاتي ويُهدر أرقاماً
///         عند التراجع، والمدقّق يقرأ الرقم المفقود مستنداً محذوفاً (فخ-12 · ADR-0008).</item>
///   <item><b>العدّاد يُؤخذ بـ<c>FOR UPDATE</c></b> في المعاملة نفسها.</item>
///   <item><b>الأرصدة <c>INSERT ... ON CONFLICT DO UPDATE</c></b> لا <c>UPDATE</c>
///         مجرّد: المجرّد على فترة لم تُنشأ صفوفها يُصيب صفر صفوف ويُثبِّت (فخ-09).</item>
///   <item><b>صفوف الأرصدة مرتّبة صراحةً</b> — غير المرتّبة قيست عند 0.161 مقابل
///         1,841.3 معاملة/ث مع 22–35 جموداً (فخ-10).</item>
///   <item><b>عدد الصفوف مؤكَّد</b> بعد كل عبارة، والاختلاف يُجهض المعاملة.</item>
///   <item><b>لا حارس إحكام تصاعدي لكل حساب</b> — قيس وهو يُسقط بصمت 500 من
///         1500 ريال عند وصول خارج الترتيب (فخ-13).</item>
///   <item><b>لا <c>UPDATE</c> ولا <c>DELETE</c> على الدفتر في أي شيفرة</b> —
///         التصحيح بقيد عكسي (ADR-0002 · ADR-0003).</item>
///   <item><b>لا فاصلة عائمة في أي مخطّط</b> — المال <c>numeric(19,4)</c> دائماً.</item>
/// </list>
/// </summary>
public sealed class Rule11_ThePostingPathIsTheOnlyWriteToTheLedger
{
    private static string LedgerRoot => Path.Combine(RepositoryLayout.Root, "src", ModuleMap.Ledger);

    private static IReadOnlyList<(string Path, string Text)> SqlScripts { get; } = Load("*.sql");

    private static IReadOnlyList<(string Path, string Text)> CSharpSources { get; } = Load("*.cs");

    /// <summary>
    /// الشيفرة بلا تعليقات.
    /// <para>
    /// القواعد التي تمنع <b>شكلاً</b> يجب أن تفحص ما يُنفَّذ لا ما يُشرح: هذا الملف
    /// نفسه، وتعليقات الدفتر، تذكر الشكل الممنوع حرفياً كي تُبيّن سبب منعه. قاعدة
    /// تخلط الاثنين تُجبر المهندس على <b>حذف الشرح</b> ليمرّ البناء — وهو أسوأ ما
    /// يمكن أن تفعله قاعدة.
    /// </para>
    /// </summary>
    private static IReadOnlyList<(string Path, string Text)> CodeOnly { get; } =
        [.. Load("*.sql").Concat(Load("*.cs")).Select(static file => (file.Path, StripComments(file.Text)))];

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"^[^\S
]*//.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"//.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"--.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return text;
    }

    private static string PostEntry => SqlScripts
        .Single(static script => script.Path.EndsWith("PostEntryFunction.sql", StringComparison.Ordinal)).Text;

    [Fact]
    public void TheNumberingIsACounterRowTakenForUpdateAndNeverASequence()
    {
        foreach ((string path, string text) in CodeOnly.Where(static file => file.Path.EndsWith(".sql", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("create sequence", text, StringComparison.OrdinalIgnoreCase);
            Assert.False(
                text.Contains("nextval", StringComparison.OrdinalIgnoreCase),
                $"{path}: التسلسل يُهدر أرقاماً عند التراجع — الترقيم صفّ عدّاد (فخ-12 · ADR-0008).");
        }

        Assert.Contains("for update", PostEntry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ledger.posting_counter", PostEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBalanceProjectionIsOneUpsertWithOrderedRowsAndAnAssertedRowcount()
    {
        Assert.Contains("insert into ledger.account_balance", PostEntry, StringComparison.Ordinal);
        Assert.Contains("on conflict", PostEntry, StringComparison.Ordinal);
        Assert.Contains("order by b.code", PostEntry, StringComparison.Ordinal);
        Assert.Contains("BALANCE_ROWCOUNT_MISMATCH", PostEntry, StringComparison.Ordinal);

        // عبارة واحدة بالضبط تمسّ جدول الأرصدة داخل الترحيل.
        int writes = Regex.Count(PostEntry, @"(insert\s+into|update)\s+ledger\.account_balance",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        Assert.True(writes == 1,
            $"عدد العبارات التي تمسّ ledger.account_balance = {writes}. الواحدة شرط، وليست أسلوباً (ADR-0004).");
    }

    [Fact]
    public void EveryWritingStatementAssertsItsRowcount()
    {
        foreach (string marker in new[]
        {
            "ENTRY_ROWCOUNT_MISMATCH", "LINE_ROWCOUNT_MISMATCH",
            "CHAIN_ROWCOUNT_MISMATCH", "COUNTER_ROWCOUNT_MISMATCH",
        })
        {
            Assert.Contains(marker, PostEntry, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void IdempotencyIsAKeyNotAMonotonicPerAccountGuard()
    {
        // الحارس الممنوع شكله: WHERE ... applied_seq < @seq — قيس وهو يُسقط بصمت
        // قيداً وصل بعد أحدث منه، ومزامنة نقاط البيع دون اتصال تُسلّم خارج الترتيب
        // بطبيعتها (فخ-13).
        Regex forbidden = new(@"applied_se(q|quence)\s*[<>]", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        foreach ((string path, string text) in CodeOnly)
        {
            Assert.False(forbidden.IsMatch(text),
                $"{path}: حارس إحكام تصاعدي لكل حساب — ممنوع منعاً باتاً (فخ-13).");
        }

        // والشكل المطلوب: مفتاح فريد لكل قيد، مستقلّ عن الترتيب.
        string model = File.ReadAllText(Path.Combine(LedgerRoot, "Persistence", "LedgerDbContext.cs"));
        Assert.Contains("uq_posting_identity", model, StringComparison.Ordinal);
        Assert.Contains("row.SourceDocType", model, StringComparison.Ordinal);
        Assert.Contains("row.PostingGeneration", model, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingInTheLedgerEverUpdatesOrDeletesAPostedEntry()
    {
        Regex forbidden = new(
            @"(update\s+ledger\.(journal_entry|journal_line|chain_link)|delete\s+from\s+ledger\.(journal_entry|journal_line|chain_link)|truncate\s+ledger\.(journal_entry|journal_line|chain_link))",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        foreach ((string path, string text) in CodeOnly)
        {
            // نصّ الصلاحيات يذكر UPDATE/DELETE ليسحبهما — وهذا عكس المخالفة.
            if (path.EndsWith("LedgerGrants.sql", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(forbidden.IsMatch(text),
                $"{path}: تعديل أو حذف على جدول يُضاف إليه فقط. التصحيح بقيد عكسي (ADR-0002).");
        }
    }

    [Fact]
    public void TheGrantScriptRevokesUpdateDeleteAndTruncateAndRefusesASuperuserApplicationRole()
    {
        string grants = SqlScripts
            .Single(static script => script.Path.EndsWith("LedgerGrants.sql", StringComparison.Ordinal)).Text;

        Assert.Contains("revoke update, delete, truncate on ledger.journal_entry", grants, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rolsuper", grants, StringComparison.Ordinal);
        Assert.Contains("APP_ROLE_IS_SUPERUSER", grants, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeferredConstraintTriggerChecksBalanceAndLineCountAtCommit()
    {
        string triggers = SqlScripts
            .Single(static script => script.Path.EndsWith("LedgerTriggers.sql", StringComparison.Ordinal)).Text;

        Assert.Contains("deferrable initially deferred", triggers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v_lines < 2", triggers, StringComparison.Ordinal);
        Assert.Contains("sum(debit_company)", triggers, StringComparison.Ordinal);
        Assert.Contains("GR-RE-001", triggers, StringComparison.Ordinal);
    }

    [Fact]
    public void NoBinaryFloatingPointTypeAppearsInAnyLedgerSchema()
    {
        Regex forbidden = new(@"\b(real|double\s+precision|float4|float8)\b",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        foreach ((string path, string text) in CodeOnly.Where(static file => file.Path.EndsWith(".sql", StringComparison.Ordinal)))
        {
            Assert.False(forbidden.IsMatch(text), $"{path}: فاصلة عائمة ثنائية في مخطّط مالي (Rule04 · فخ-17).");
        }

        Assert.Contains("numeric(19,4)", File.ReadAllText(
            Path.Combine(LedgerRoot, "Persistence", "LedgerDbContext.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePostingIsASingleServerCallWithNoRoundTripUnderTheLock()
    {
        // القاعدة القابلة للفحص: صفر رحلات بين أخذ القفل والـCOMMIT. ما يُفحص هنا
        // هو أن مسار الترحيل في C# لا يفتح معاملة صريحة أصلاً — فلا يوجد مكان
        // يمكن أن تُحبس فيه رحلة (فخ-14: فارق 127× مقيس).
        string service = File.ReadAllText(Path.Combine(LedgerRoot, "Posting", "PostingService.cs"));

        Assert.DoesNotContain("BeginTransaction", service, StringComparison.Ordinal);
        Assert.Contains("ledger.post_entry", service, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        Assert.True(SqlScripts.Count >= 3, "نصوص المخطّط لم تُقرأ — القاعدة تمرّ فراغاً.");
        Assert.True(CSharpSources.Count >= 10, "مصادر الدفتر لم تُقرأ — القاعدة تمرّ فراغاً.");
        Assert.True(CodeOnly.Count == SqlScripts.Count + CSharpSources.Count);
        Assert.DoesNotContain("فخ-13", CodeOnly.Single(
            static file => file.Path.EndsWith("LedgerDbContext.cs", StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);
        Assert.True(PostEntry.Length > 2000);
    }

    private static IReadOnlyList<(string Path, string Text)> Load(string pattern)
        => [.. Directory.EnumerateFiles(LedgerRoot, pattern, SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(static path => (Path.GetRelativePath(RepositoryLayout.Root, path), File.ReadAllText(path)))];
}
