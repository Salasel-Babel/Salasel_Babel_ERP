using System.Text.RegularExpressions;
using Babel.SharedKernel;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>العملةُ ووحدتُها الصغرى تتبعان المنشأة لا الشيفرة (ADR-0089).</b>
/// <para>
/// <b>العطل الذي يجعله مستحيلاً، بنصّ ما كان مكتوباً:</b>
/// <code>
/// public string CompanyCurrency { get; set; } = "SAR";   // في سبعة أنواعِ إعدادات
/// private const int Halalas = 2;                          // في أربعة أنواعِ حساب
/// </code>
/// فكانت المنشأةُ الكويتية تُقرَّب فواتيرُها إلى الهللة وهي تدفع بالفلس، وكان تغييرُ
/// العملة إعادةَ ترجمة. والموضعُ الصحيح صفُّ التأسيس، وتصل الوحداتِ عبر
/// <c>ICompanyMoneyResolver</c> في كلّ نداء.
/// </para>
/// <para>
/// <b>ولكلّ فحصٍ شاهدُه الموجب (ADR-0056):</b> النمطُ يُثبَت أنه ينطق على عيّنةٍ
/// مُركَّبة، والمسحُ يُثبَت أنه زار ملفّات — فحارسٌ يمرّ على مجموعةٍ فارغة لا يُفرَّق عن
/// حارسٍ معطَّل.
/// </para>
/// </summary>
public sealed partial class TheCurrencyFollowsTheCompany
{
    /// <summary>المجلّدات الممسوحة — كما في حارس الارتداد الصامت: الوحدات وصورةُ الترحيل.</summary>
    private static readonly string[] ScannedFolders = ["src", Path.Combine("demo", "company")];

    /// <summary>خاصّيةُ عملةِ شركةٍ في نوعِ إعدادات — بأيّ قيمةٍ ابتدائية أو بلا قيمة.</summary>
    [GeneratedRegex(@"\bstring\s+CompanyCurrency\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyCurrencyProperty();

    /// <summary>ثابتُ «الهللة» — عددُ خاناتٍ مكتوبٌ باسم عملةٍ بعينها.</summary>
    [GeneratedRegex(@"\bconst\s+int\s+Halalas\b", RegexOptions.CultureInvariant)]
    private static partial Regex HalalasConstant();

    /// <summary>ثابتُ عملةٍ يُقرأ عملةً للمنشأة في مسارٍ إنتاجي: <c>CurrencyCode.Sar</c> خارج نوعه.</summary>
    [GeneratedRegex(@"\bCurrencyCode\.Sar\b", RegexOptions.CultureInvariant)]
    private static partial Regex SarConstantUse();

    [Fact]
    public void لا_نوعَ_إعدادات_يحمل_عملةَ_الشركة()
    {
        Assert.Matches(CompanyCurrencyProperty(), "    public string CompanyCurrency { get; set; } = \"SAR\";");
        Assert.Matches(CompanyCurrencyProperty(), "public string CompanyCurrency {get;init;}");

        List<string> offenders = [];
        int visited = 0;

        foreach (string path in SourceFiles())
        {
            visited++;
            if (CompanyCurrencyProperty().IsMatch(File.ReadAllText(path)))
            {
                offenders.Add(Path.GetRelativePath(RepositoryLayout.Root, path));
            }
        }

        Assert.True(visited > 100, "المسح لم يزر ملفّات: " + visited);
        Assert.True(offenders.Count == 0,
            "عملةُ الشركة عادت إلى نوعِ إعدادات — موضعُها صفُّ التأسيس ويصلها ICompanyMoneyResolver (ADR-0089):\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void لا_ثابتَ_هللةٍ_في_حسابِ_سطر()
    {
        Assert.Matches(HalalasConstant(), "    private const int Halalas = 2;");

        List<string> offenders = [];
        int visited = 0;

        foreach (string path in SourceFiles())
        {
            visited++;
            if (HalalasConstant().IsMatch(File.ReadAllText(path)))
            {
                offenders.Add(Path.GetRelativePath(RepositoryLayout.Root, path));
            }
        }

        Assert.True(visited > 100, "المسح لم يزر ملفّات: " + visited);
        Assert.True(offenders.Count == 0,
            "عددُ خاناتِ التقريب عاد ثابتاً باسم عملةٍ بعينها — يُقرأ من CompanyMoney.MinorUnits (ADR-0089):\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void ثابتُ_الريال_لا_يُقرأ_عملةً_للمنشأة_في_مسارٍ_إنتاجي()
    {
        Assert.Matches(SarConstantUse(), "Money.Of(row.CreditLimit, CurrencyCode.Sar)");

        List<string> offenders = [];
        int visited = 0;

        foreach (string path in SourceFiles().Where(static p => !p.EndsWith("CurrencyCode.cs", StringComparison.Ordinal)))
        {
            visited++;
            if (SarConstantUse().IsMatch(File.ReadAllText(path)))
            {
                offenders.Add(Path.GetRelativePath(RepositoryLayout.Root, path));
            }
        }

        Assert.True(visited > 100, "المسح لم يزر ملفّات: " + visited);
        Assert.True(offenders.Count == 0,
            "CurrencyCode.Sar رمزٌ مسمّى للاختبارات والبيانات التجريبية، لا عملةٌ افتراضية لمسارٍ إنتاجي (ADR-0089):\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void الجدولُ_المرجعي_يعرف_عملاتِ_الخليج_ولا_يتجاوز_مقياسَ_التخزين()
    {
        Assert.Equal(2, Iso4217.MinorUnitsOf(CurrencyCode.Sar));
        Assert.Equal(3, Iso4217.MinorUnitsOf(CurrencyCode.FromString("KWD")));
        Assert.Equal(0, Iso4217.MinorUnitsOf(CurrencyCode.FromString("JPY")));
        Assert.Null(Iso4217.MinorUnitsOf(CurrencyCode.FromString("XXX")));
        Assert.All(Iso4217.KnownCodes, code => Assert.InRange(Iso4217.MinorUnitsOf(CurrencyCode.FromString(code))!.Value, 0, Money.CanonicalScale));
    }

    private static IEnumerable<string> SourceFiles()
        => ScannedFolders
            .Select(folder => Path.Combine(RepositoryLayout.Root, folder))
            .Where(Directory.Exists)
            .SelectMany(static folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                  && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
