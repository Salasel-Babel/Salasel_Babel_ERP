using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 10 — لا تحويل يقرأ الثقافة على قيمة تُحفظ أو تُجزَّأ أو تُفهرَس أو تُقارَن أو تُرسَل.</b>
/// <para>
/// <b>العطل الذي تجعله مستحيلاً — وهو مقيس على هذا الجهاز:</b> رمز فترة الفوترة كان يُبنى
/// بسلسلة مُستكمَلة <c>$"{at.UtcDateTime:yyyy-MM}"</c>. والسلسلة المُستكمَلة <b>تقرأ ثقافة
/// العملية</b>، وقراءة الثقافة في التاريخ تعني قراءة <b>تقويمها</b> لا فواصلها فقط:
/// </para>
/// <code>
/// invariant → 2026-08     ar-SA → 1448-03 (أم القرى)
/// fa-IR     → 1405-06     th-TH → 2569-08
/// </code>
/// <para>
/// فتُكتب أحداث القياس تحت رمز فترة هجري، ويُرجع استعلام الفوترة عن <c>2026-08</c> صفراً،
/// و<b>تُصدَر فاتورة الشهر خالية</b>: بلا استثناء، وبلا سطر سجل، وبلا اختبار فاشل. يقع على
/// خادم إنتاج عربي فقط، ولا يقع على جهاز المطوّر.
/// </para>
/// <para>
/// <b>ولماذا لا يكفي محلّل ولا إعداد:</b>
/// </para>
/// <list type="number">
///   <item><description>
///     <c>InvariantGlobalization=true</c> ليس علاجاً بل عطل أسوأ: يجعل
///     <c>String.Normalize</c> عملية لا شيء بصمت فتنكسر سلسلة تجزئة القيود لكل نصّ عربي
///     يحمل أ/إ/آ. ولهذا يفرض <c>Directory.Build.props</c> الإعداد <b>false</b> — أي أن
///     الإجراء الذي يحمي السلسلة هو نفسه ما <b>يُسلّح</b> هذا الفخّ. الإعدادان في تعارض،
///     ولا أحدهما آمن وحده، والحلّ الوحيد هو التنسيق الثابت الصريح عند كل موضع.
///   </description></item>
///   <item><description>
///     <c>CA1305</c> <b>لا يلتقط الشكل المُستكمَل</b>. هو يفحص وجود حِمل زائد يقبل
///     <c>IFormatProvider</c> على <c>ToString(string)</c>؛ و<c>$"{x:fmt}"</c> ليس استدعاء
///     <c>ToString</c> أصلاً بل <c>DefaultInterpolatedStringHandler</c>. مقيس: الحلّ كان
///     يُبنى بصفر تحذيرات مع <c>TreatWarningsAsErrors</c> والمخالفة قائمة فيه.
///   </description></item>
/// </list>
/// <para>
/// <b>الاستثناء ضيّق بحكم بنائه:</b> التنسيق المُعتمِد على الثقافة <b>مطلوب</b> في نصّ
/// يُعرض لمستخدم — تاريخ هجري في واجهة عربية ميزة لا عطل. ولذلك لا تمنع القاعدة التنسيق
/// المحلي، بل تمنع أن يكون <b>ضمنياً</b>: يُكتب وسم <c>ثقافة-عرض:</c> مع سبب مكتوب على
/// السطر نفسه أو السطر الذي يسبقه، فيُعفى <b>ذلك السطر وحده</b>. لا إعفاء لملف، ولا
/// لمشروع، ولا لمجلد، ولا وسم بلا سبب.
/// </para>
/// <para>
/// <b>ما لا تلتقطه هذه القاعدة</b> — مكتوب هنا عمداً، لأن قاعدة توحي بتغطية لا تملكها
/// أخطر من غياب القاعدة:
/// </para>
/// <list type="bullet">
///   <item><description>
///     فجوة بلا مُحدِّد: <c>$"{someDecimal}"</c> و<c>$"{someDate}"</c> تقرآن الثقافة أيضاً،
///     والماسح مُعجَمي فلا يعرف نوع التعبير. حاجزها الوحيد هو <c>CA1305</c> على
///     <c>ToString()</c> ومراجعة بشرية.
///   </description></item>
///   <item><description>
///     ‏<c>StringBuilder.Append(decimal)</c> و<c>Append(int)</c> — نفس السبب: لا نوع.
///   </description></item>
///   <item><description>
///     تنسيق يقع داخل حزمة خارجية أو داخل <c>ToString()</c> مُعاد تعريفه في نوع آخر.
///   </description></item>
///   <item><description>
///     مقارنة ثقافية على متغيّر نصّي بدل نصّ حرفي: <c>a.StartsWith(b)</c>.
///   </description></item>
///   <item><description>
///     الفرز الثقافي: <c>OrderBy(x =&gt; x)</c> على نصّ بلا <c>StringComparer.Ordinal</c>.
///   </description></item>
/// </list>
/// </summary>
public sealed class Rule10_NoCultureDependentPersistence
{
    private static readonly CultureScan.Result Scan = CultureScan.ScanRepository();

    /// <summary>
    /// المشاريع التي لا يجوز أن يظهر فيها <b>أي</b> استثناء عرض: بايتاتها تُجزَّأ أو تُفهرَس
    /// أو تُرسَل إلى جهة رسمية. «هذا للعرض فقط» في مسار مُجزَّأ جملة لا معنى لها.
    /// </summary>
    private static readonly string[] NoExemptionZones =
    [
        "src/Babel.Canonicalization/",
        "src/Babel.Compliance/Canonical/",
        "src/Babel.ControlPlane/Metering/",
        "src/Babel.SharedKernel/",
        "demo/vertical-slice/Support/",
    ];

    [Fact]
    public void NoCultureDependentConversionReachesPersistenceOrAHashOrAKey()
    {
        IReadOnlyList<CultureScan.Finding> violations = Scan.Violations;

        Assert.True(
            violations.Count == 0,
            $"تحويل يقرأ ثقافة العملية على قيمة تُحفظ أو تُجزَّأ أو تُقارَن أو تُرسَل "
            + $"({violations.Count} موضعاً). الصواب: تمرير CultureInfo.InvariantCulture صراحةً — "
            + $"أو، إن كان الناتج للعرض وحده، وسمُ «{CultureScan.ExemptionMarker} <سبب>» على السطر أو الذي قبله:\n"
            + string.Join('\n', violations.Select(static v => "  " + v)));
    }

    [Fact]
    public void EveryExemptionIsNarrowAndCarriesAWrittenReason()
    {
        // وسم بلا سبب مكتوب يظهر أصلاً في Violations بصنف UnjustifiedExemption؛
        // وهذا الاختبار يثبت أن الآلية تُميّز الاثنين ولا تعدّ الوسم الفارغ إعفاءً.
        Assert.All(Scan.Exemptions, exemption => Assert.NotEqual(CultureScan.Kind.UnjustifiedExemption, exemption.Category));

        List<string> inZone = [.. Scan.Exemptions
            .Where(static e => NoExemptionZones.Any(zone => e.File.StartsWith(zone, StringComparison.Ordinal)))
            .Select(static e => e.ToString())];

        Assert.True(
            inZone.Count == 0,
            "استثناء «عرض فقط» داخل مسار مُجزَّأ أو مفتاحي — هنا لا وجود لعرض، والثقافة الثابتة هي الجواب الوحيد:\n"
            + string.Join('\n', inZone));
    }

    [Fact]
    public void TheBillingPeriodKeyIsBuiltInvariantly()
    {
        // الموضع الأصلي بعينه، مثبَّتاً بقيمة: أي ارتداد فيه يفشل هنا باسمه لا بسطر ماسح.
        string previous = System.Globalization.CultureInfo.CurrentCulture.Name;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("2026-08", new SharedKernel.BillingPeriod(2026, 8).ToString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(previous);
        }
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // ١ — الماسح رأى شيئاً فعلاً. قاعدة تمسح مجموعة فارغة تمرّ إلى الأبد بلا قيمة.
        Assert.True(Scan.FilesScanned > 150, $"عدد الملفات الممسوحة {Scan.FilesScanned} أقل من أن يثبت شيئاً.");
        Assert.True(Scan.ConversionSites > 60, $"عدد مواضع التحويل المفحوصة {Scan.ConversionSites} أقل من أن يثبت شيئاً.");

        // ٢ — الماسح يلتقط العطل التاريخي نفسه، بنصّه، في أشكاله الأربعة.
        foreach (string bad in new[]
                 {
                     "var k = $\"{at.UtcDateTime:yyyy-MM}\";",
                     "var k = $@\"{at.UtcDateTime:yyyy-MM}\";",
                     "var k = @$\"{at.UtcDateTime:yyyy-MM}\";",
                     "var k = $\"\"\"\n{at.UtcDateTime:yyyy-MM}\n\"\"\";",
                     "var k = $\"prefix {x} {at:yyyy-MM-dd} suffix\";",
                     "var s = value.ToString(\"0.0000\");",
                     "var n = int.Parse(raw);",
                     "var u = code.ToUpper();",
                     "var t = DateTime.Now;",
                     "if (id.StartsWith(\"BABEL-\")) { }",
                 })
        {
            CultureScan.Result caught = CultureScan.ScanText(bad, "synthetic.cs");
            Assert.True(caught.Violations.Count > 0, $"الماسح لم يلتقط مخالفة مصنوعة: {bad}");
        }

        // ٣ — ولا يلتقط الصيغة الصحيحة، ولا الستّ‌عشري، ولا ما كان داخل تعليق أو نصّ خامّ.
        foreach (string good in new[]
                 {
                     "var k = at.UtcDateTime.ToString(\"yyyy-MM\", CultureInfo.InvariantCulture);",
                     "var k = string.Create(CultureInfo.InvariantCulture, $\"{Year:D4}-{Month:D2}\");",
                     "sb.Append(CultureInfo.InvariantCulture, $\"{f.Name,-24}\\n\");",
                     "var k = FormattableString.Invariant($\"{at:yyyy-MM}\");",
                     "var m = $\"U+{cp:X4} ممنوع\";",
                     "// var k = $\"{at:yyyy-MM}\"; هذا تعليق يشرح الفخّ",
                     "var sql = \"\"\"\n select 1::text where a.ToString(\\\"D\\\") = 'x'\n \"\"\";",
                     "if (id.StartsWith(\"BABEL-\", StringComparison.Ordinal)) { }",
                     "var n = int.Parse(raw, CultureInfo.InvariantCulture);",
                 })
        {
            CultureScan.Result clean = CultureScan.ScanText(good, "synthetic.cs");
            Assert.True(clean.Violations.Count == 0, $"إيجابية كاذبة على صيغة سليمة: {good}\n  " + string.Join("\n  ", clean.Violations));
        }

        // ٤ — الإعفاء يعمل، و«يعمل» تعني: بسبب مكتوب فقط، وعلى ذلك السطر وحده.
        CultureScan.Result exempted = CultureScan.ScanText(
            "var label = $\"{total:N2} ريال\"; // " + CultureScan.ExemptionMarker + " نصّ يُعرض للمستخدم في واجهة عربية ولا يُحفظ",
            "synthetic.cs");
        Assert.Empty(exempted.Violations);
        Assert.Single(exempted.Exemptions);

        CultureScan.Result unjustified = CultureScan.ScanText(
            "var label = $\"{total:N2} ريال\"; // " + CultureScan.ExemptionMarker + " عرض",
            "synthetic.cs");
        Assert.Contains(unjustified.Violations, static v => v.Category == CultureScan.Kind.UnjustifiedExemption);
        Assert.Contains(unjustified.Violations, static v => v.Category == CultureScan.Kind.InterpolatedFormat);

        // ٥ — الوسم لا يُعفي سطراً بعيداً: نطاقه سطران لا أكثر.
        CultureScan.Result outOfRange = CultureScan.ScanText(
            "// " + CultureScan.ExemptionMarker + " سبب مكتوب طويل بما يكفي ليُقبَل شكلاً\n\n\nvar label = $\"{total:N2}\";",
            "synthetic.cs");
        Assert.NotEmpty(outOfRange.Violations);
    }

    [Fact]
    public void TheFormatClassifierIsPreciseNotSubstringBased()
    {
        Assert.True(CultureScan.IsCultureSensitiveFormat("yyyy-MM"));
        Assert.True(CultureScan.IsCultureSensitiveFormat("0.0000"));
        Assert.True(CultureScan.IsCultureSensitiveFormat("N2"));
        Assert.True(CultureScan.IsCultureSensitiveFormat("D"));   // نمط التاريخ الطويل على DateTime
        Assert.True(CultureScan.IsCultureSensitiveFormat("F1"));
        Assert.False(CultureScan.IsCultureSensitiveFormat("X4")); // ستّ‌عشري: 0-9A-F فقط
        Assert.False(CultureScan.IsCultureSensitiveFormat("x"));
        Assert.False(CultureScan.IsCultureSensitiveFormat(""));   // محاذاة بلا تنسيق
    }
}
