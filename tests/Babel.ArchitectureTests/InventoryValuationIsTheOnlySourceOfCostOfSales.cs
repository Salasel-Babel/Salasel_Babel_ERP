using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Inventory;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>تكلفة المبيعات تُحسَب في حدّ تقييم المخزون، ولا يُسلّمها مستدعٍ.</b>
/// <para>
/// مصفوفة الترحيل تقول نصّاً إن مبلغ <c>sales.invoice.cost_of_sales</c> يُشتقّ
/// «بطريقة التكلفة المعتمدة لحظة البيع لا بسعر الشراء الأخير». وقبل هذا الحارس كان
/// النوع <c>CostOfSalesDraft</c> يحمل حقلاً <c>Money Cost</c> — أي أن الواقعة
/// المحاسبية <b>مُسمّاة في المصفوفة ولا يحسبها شيء</b>، وأي رقم يمرّ إلى الدفتر.
/// </para>
/// <para>
/// وهذا صنف العطل الذي يقول عنه هذا المستودع إنه الأخطر: لا انهيار، ولا رسالة، ولا
/// ميزان مراجعة غير متوازن — قيدٌ متوازن تماماً بمبلغٍ خاطئ، يظهر بعد شهور في هامش
/// ربحٍ لا أحد يعرف من أين جاء.
/// </para>
/// <para>
/// <b>وطبقتان لأن واحدة لا تكفي:</b> الأولى تمنع أن <b>يدخل</b> المبلغ من مستدعٍ،
/// والثانية تمنع أن <b>يُبنى</b> جواب التقييم خارج الوحدة التي تملك الرصيد. من يلتفّ
/// على الأولى بتمرير النوع الناتج يقع في الثانية.
/// </para>
/// </summary>
public sealed class InventoryValuationIsTheOnlySourceOfCostOfSales
{
    /// <summary>الكلمة التي تُعرّف نقطة الدخول محلّ الحراسة.</summary>
    private const string EntryPointMarker = "CostOfSales";

    /// <summary>الوحدة الوحيدة التي يجوز أن تبني جواب التقييم.</summary>
    private const string ValuationProject = "src/Babel.Inventory/";

    /// <summary>
    /// كلمات تجعل حقلاً <c>decimal</c> مبلغاً لا كميةً.
    /// <para>
    /// و<c>Quantity</c> ليست منها عمداً: <b>الكمية هي ما يجوز أن يُسلَّم</b>، وهي
    /// بالضبط ما حلّ محلّ المبلغ. حارسٌ يمنع كل <c>decimal</c> كان سيمنع الإصلاح نفسه.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> MoneyWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cost", "amount", "price", "value", "total", "net", "gross", "sum",
        };

    /// <summary>
    /// ‏١ · <b>لا نقطة دخول لتكلفة المبيعات تقبل مبلغاً من مستدعيها.</b>
    /// <para>
    /// والفحص <b>بالشكل لا بالاسم</b>: يُعدَّد كل عضو في كل نوع معامِل مُعلَن في تجميعة
    /// بابل، ويُسأل «هل هذا مبلغ؟» — فإعادة تسمية الحقل لا تُفلت منه.
    /// </para>
    /// </summary>
    [Fact]
    public void تكلفة_المبيعات_لا_يُسلّمها_مستدعٍ()
    {
        CostOfSalesSurface surface = ScanSurface();
        AssertTheReflectionScanIsNotVacuous(surface);

        Assert.True(
            surface.Violations.Count == 0,
            "نقاط دخول تكلفة مبيعات تقبل مبلغاً من مستدعيها — والمبلغ يُحسب في حدّ "
            + "التقييم ولا يُملى:\n" + string.Join('\n', surface.Violations));
    }

    /// <summary>
    /// ‏٢ · <b>جواب التقييم لا يُبنى إلا داخل وحدة المخزون.</b>
    /// <para>
    /// والمسح على <b>ما يتعقّبه git</b> لا على ما يقع على القرص: مجموعةٌ يغيّرها من
    /// يبني لا من يُودِع تجعل الحارس يقيس البيئة لا الشيفرة
    /// (‏<c>docs/evidence/traps.md#fakh-68</c>). وملفّ القاعدة نفسه خارج المجموعة
    /// بحكم موضعها: النطاق <c>src/</c> وحده، وهذا الملف في <c>tests/</c> — فتقوية
    /// الحارس لا ترفع العدد الذي يقيسه.
    /// </para>
    /// </summary>
    [Fact]
    public void جواب_التقييم_يُبنى_في_وحدة_المخزون_وحدها()
    {
        ConstructionScan scan = ScanConstructions();
        AssertTheSourceScanIsNotVacuous(scan);

        string[] outside = [.. scan.Sites
            .Where(static site => !site.Path.StartsWith(ValuationProject, StringComparison.Ordinal))
            .Select(static site => site.Path + ":" + site.Line.ToString(CultureInfo.InvariantCulture))];

        Assert.True(
            outside.Length == 0,
            "جواب تقييم المخزون مبنيّ خارج وحدة المخزون — أي أن وحدةً أخرى تستطيع أن "
            + "تُملي على الدفتر تكلفةً اخترعتها:\n" + string.Join('\n', outside));
    }

    /// <summary>
    /// ‏٣ · <b>ما يُثبته هذا الحارس بمثالين موجب وسالب.</b>
    /// <para>
    /// الطرف الآخر من اللافراغ: مسحٌ يمرّ لأنه لم يعد يعرف ما يبحث عنه يُقرأ أخضر
    /// إلى الأبد. فالمسند نفسه يُختبر على أشكال معلومة الحكم.
    /// </para>
    /// </summary>
    [Fact]
    public void المسند_يميّز_المبلغ_من_الكمية()
    {
        Assert.True(IsMoneyShaped(typeof(Money), "Anything"));
        Assert.True(IsMoneyShaped(typeof(decimal), "Cost"));
        Assert.True(IsMoneyShaped(typeof(decimal), "UnitPrice"));
        Assert.True(IsMoneyShaped(typeof(Money?), "Cost"));
        Assert.False(IsMoneyShaped(typeof(decimal), "Quantity"));
        Assert.False(IsMoneyShaped(typeof(string), "Cost"));
        Assert.False(IsMoneyShaped(typeof(int), "LineNo"));

        // والنوع الذي أُصلح: كميةٌ لا مبلغ — والحارس يراه كذلك.
        Type draft = BabelAssemblies.TypesOf(BabelAssemblies.Named("Babel.Sales"))
            .Single(static type => type.Name == "CostOfSalesDraft");

        Assert.DoesNotContain(MoneyShapedMembers(draft), static member => true);
        Assert.Contains(TypeShapes.DeclaredMembers(draft), static member => member.Name == "Quantity");
    }

    // ────────────────────────────────────────────────────────────────────────

    private static CostOfSalesSurface ScanSurface()
    {
        List<string> violations = [];
        List<string> entryPoints = [];
        HashSet<Type> inspected = [];

        foreach (Type type in BabelAssemblies.AllTypes()
                     .Where(static t => !TypeShapes.IsCompilerGenerated(t))
                     .Where(TypeShapes.IsVisibleOutsideAssembly))
        {
            foreach (MethodInfo method in type
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(static m => !m.IsSpecialName)
                         .Where(static m => m.Name.Contains(EntryPointMarker, StringComparison.Ordinal)))
            {
                entryPoints.Add(type.FullName + "." + method.Name);

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    foreach (Type candidate in TypeShapes.Unwrap(parameter.ParameterType))
                    {
                        // المعامِل الذي هو مبلغ بنفسه: أوضح شكل للعطل.
                        if (IsMoneyShaped(candidate, parameter.Name ?? string.Empty))
                        {
                            violations.Add(
                                $"{type.FullName}.{method.Name}({parameter.Name}) — معامِل مبلغ مباشر");
                            continue;
                        }

                        if (!IsBabelDeclared(candidate) || !inspected.Add(candidate))
                        {
                            continue;
                        }

                        foreach (MemberInfo member in MoneyShapedMembers(candidate))
                        {
                            violations.Add(
                                $"{type.FullName}.{method.Name} ⇐ {candidate.FullName}.{member.Name}");
                        }
                    }
                }
            }
        }

        return new CostOfSalesSurface(entryPoints, inspected.Count, violations);
    }

    private static IEnumerable<MemberInfo> MoneyShapedMembers(Type type)
        => TypeShapes.DeclaredMembers(type)
            .Where(static member => member is PropertyInfo or FieldInfo)
            .Where(static member => !member.Name.StartsWith('<'))
            .Where(member => TypeShapes.ValueTypesOf(member)
                .Any(entry => IsMoneyShaped(entry.Type, member.Name)));

    /// <summary>
    /// هل هذا العضو <b>مبلغ</b>؟ ‏<see cref="Money"/> دائماً، و<c>decimal</c> إن حمل
    /// اسمه كلمة مبلغ. وما عدا ذلك فلا.
    /// </summary>
    private static bool IsMoneyShaped(Type type, string name)
    {
        Type bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(Money))
        {
            return true;
        }

        return bare == typeof(decimal) && Identifiers.ContainsWord(name, MoneyWords);
    }

    private static bool IsBabelDeclared(Type type)
        => type.Assembly.GetName().Name?.StartsWith("Babel.", StringComparison.Ordinal) == true;

    private static void AssertTheReflectionScanIsNotVacuous(CostOfSalesSurface surface)
    {
        Assert.True(
            surface.EntryPoints.Count >= 1,
            "لا نقطة دخول لتكلفة المبيعات إطلاقاً — الماسح توقّف عن المطابقة، فالخُضرة "
            + "لا تعني شيئاً. (‏traps.md#fakh-68)");

        Assert.True(
            surface.InspectedTypes >= 1,
            FormattableString.Invariant(
                $"نقاط الدخول {surface.EntryPoints.Count} ولم يُفحَص أي نوع معامِل مُعلَن في بابل — الماسح يمرّ فوق المعاملات ولا يقرؤها."));

        // ونقطة الدخول المقصودة موجودة بالاسم: لو أُعيدت تسميتها لسقط هذا السطر
        // وقيل السبب، بدل أن يبقى الحارس أخضر على سطحٍ لم يعد يشمله.
        Assert.Contains(
            surface.EntryPoints,
            static name => name.EndsWith("SalesInvoiceService.PostCostOfSalesAsync", StringComparison.Ordinal));
    }

    private static void AssertTheSourceScanIsNotVacuous(ConstructionScan scan)
    {
        Assert.True(
            scan.FilesRead >= 100,
            FormattableString.Invariant(
                $"المسح ضامر: {scan.FilesRead} ملفاً فقط تحت src/ — المجموعة ليست المستودع."));

        Assert.True(
            scan.Sites.Count >= 1,
            "لا موضع يبني جواب تقييم إطلاقاً — الماسح توقّف عن المطابقة، أو أُعيدت تسمية "
            + "النوع فصار الحارس يحرس اسماً لا وجود له. (‏traps.md#fakh-68)");
    }

    private static ConstructionScan ScanConstructions()
    {
        string marker = "new " + nameof(InventoryMovementCost);
        List<ConstructionSite> sites = [];
        int filesRead = 0;

        foreach (string tracked in TrackedSourceFiles())
        {
            // فواصل المسار تُطبَّع قبل أي فحص نمطي: مقطعٌ بفاصل ويندوز يعبر كل نمط
            // يبحث عن الشرطة الأمامية (‏traps.md#fakh-68).
            string path = tracked.Replace('\\', '/');
            if (!path.EndsWith(".cs", StringComparison.Ordinal))
            {
                continue;
            }

            filesRead++;
            string[] lines = File.ReadAllLines(Path.Combine(RepositoryLayout.Root, tracked));

            for (int i = 0; i < lines.Length; i++)
            {
                string code = StripComment(lines[i]);
                if (code.Contains(marker, StringComparison.Ordinal))
                {
                    sites.Add(new ConstructionSite(path, i + 1));
                }
            }
        }

        return new ConstructionScan(sites, filesRead);
    }

    /// <summary>الشيفرة بلا تعليق سطري: الحارس لا يحسب شرحاً يذكر الشكل الممنوع.</summary>
    private static string StripComment(string line)
    {
        int marker = line.IndexOf("//", StringComparison.Ordinal);
        return marker < 0 ? line : line[..marker];
    }

    /// <summary>
    /// ما يتعقّبه git تحت <c>src/</c> — لا ما يقع على القرص.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن تعذّر سؤال git — والصمت هنا أسوأ من الرمي.</exception>
    private static string[] TrackedSourceFiles()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("src");

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "‏git ls-files أخفق، فلا سبيل إلى معرفة محتوى المستودع — والحارس يرمي ولا "
                + "يخمّن على ما يقع على القرص. / git ls-files failed: " + error);
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record CostOfSalesSurface(
        IReadOnlyList<string> EntryPoints, int InspectedTypes, IReadOnlyList<string> Violations);

    private sealed record ConstructionScan(IReadOnlyList<ConstructionSite> Sites, int FilesRead);

    private sealed record ConstructionSite(string Path, int Line);
}
