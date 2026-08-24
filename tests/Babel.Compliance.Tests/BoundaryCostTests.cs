using System.Reflection;
using Babel.Compliance.Abstractions;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// <b>قياس تكلفة التأجيل، لا وصفها.</b> هذان ملفان يخرجان من الكود نفسه:
/// <list type="number">
///   <item><b>طابور التحقق قبل البناء</b> — كل تفصيلة تنظيمية أو بروتوكولية غير مُتحقَّق منها.</item>
///   <item><b>فاتورة تعميم شكلَي الحيازة</b> — كل عنصر يوجد فقط لأن القرار مؤجَّل.</item>
/// </list>
/// وجودهما كاختبارين يعني أن الرقمين <b>لا يمكن أن يتقادما</b>: إضافة بند مؤقَّت
/// أو تعميم جديد تظهر في الطابور فوراً.
/// </summary>
public class BoundaryCostTests(Xunit.ITestOutputHelper output)
{
    private static Assembly[] Surface =>
    [
        typeof(ProvisionalAttribute).Assembly,
        typeof(Babel.Compliance.Canonical.ComplianceCanonical).Assembly,
        typeof(Babel.Compliance.FakeProvider.FakeAuthority).Assembly
    ];

    [Fact]
    public void The_pre_build_verification_queue_is_complete_and_every_item_says_how_to_close_it()
    {
        var items = ProvisionalRegistry.Collect(Surface);
        output.WriteLine(ProvisionalRegistry.Render(items));

        Assert.NotEmpty(items);

        // كل بند مؤقَّت يجب أن يقول **كيف يُغلق**، وإلا صار طابوراً بلا مخرج.
        var withoutVerification = items.Where(i => string.IsNullOrWhiteSpace(i.VerifyBy)).ToList();
        Assert.True(withoutVerification.Count == 0,
            "بنود مؤقَّتة بلا طريقة تحقق:\n" + string.Join("\n", withoutVerification.Select(i => i.Location)));

        // والنص الملزم لا يتغيّر.
        Assert.All(items, i =>
            Assert.Equal("غير مُتحقَّق منه — يُثبَّت من الوثيقة الرسمية قبل البناء", i.Notice));

        // البنود البنيوية هي التي تحجز البناء فعلاً — يجب أن تكون معروفة العدد.
        var structural = items.Count(i => i.Risk == ProvisionalRisk.Structural);
        output.WriteLine($"\nإجمالي البنود: {items.Count} — منها بنيوية: {structural}");
        Assert.True(structural >= 5, "البنود البنيوية أقل من المتوقَّع — هل فُقدت سمات؟");
    }

    [Fact]
    public void The_cost_of_keeping_both_custody_shapes_open_is_enumerated_not_asserted()
    {
        var cost = ProvisionalRegistry.CollectCustodyCost(Surface);

        output.WriteLine("فاتورة تعميم شكلَي حيازة المفتاح");
        output.WriteLine(new string('-', 90));
        foreach (var group in cost.GroupBy(c => c.Kind))
        {
            output.WriteLine($"\n### {group.Key} ({group.Count()})");
            foreach (var c in group)
            {
                output.WriteLine($"  • {c.Location}");
                output.WriteLine($"      {c.Reason}");
                if (c.DeadUnder != DeadUnderShape.None)
                    output.WriteLine($"      كود ميّت تحت: {c.DeadUnder}");
            }
        }

        Assert.NotEmpty(cost);

        // فرعان ميّتان بالضبط: واجهة حيازة لكل شكل، ولا تنفيذ لها تحت الشكل الآخر.
        var dead = cost.Where(c => c.Kind == CustodyCostKind.DeadBranch).ToList();
        Assert.Equal(2, dead.Count);
        Assert.Contains(dead, c => c.DeadUnder == DeadUnderShape.ProviderHeld);
        Assert.Contains(dead, c => c.DeadUnder == DeadUnderShape.SelfHeld);

        output.WriteLine($"\nإجمالي بنود التعميم: {cost.Count}");
    }

    /// <summary>
    /// نسبة السطح المدفوعة ثمناً للتأجيل: عدد الأنواع العامة في العقد التي لا وجود لها
    /// لو حُسم القرار، منسوبةً إلى سطح العقد كله.
    /// </summary>
    [Fact]
    public void The_share_of_the_contract_that_exists_only_because_the_decision_is_open_is_measured()
    {
        var contract = typeof(ProvisionalAttribute).Assembly;
        var publicTypes = contract.GetExportedTypes()
            .Where(t => !t.IsNested && t.Namespace == "Babel.Compliance.Abstractions")
            .ToList();

        var custodyOnly = publicTypes
            .Where(t => t.GetCustomAttribute<DualCustodyCostAttribute>() is not null)
            .ToList();

        // أنواع تختفي أو تنكمش عند الحسم، عُدَّت يدوياً في DESIGN.md وتُثبَّت هنا آلياً.
        var custodySpecific = publicTypes.Count(t =>
            t.Name is nameof(KeyCustody) or nameof(SealState) or nameof(SealedPayload)
                   or nameof(IDocumentSealer) or nameof(ILocalKeyCustodian) or nameof(IProviderKeyCustodian)
                   or nameof(SealingContext) or nameof(SigningInput) or nameof(SigningInputForm)
                   or nameof(SignatureMaterial));

        output.WriteLine($"أنواع العقد العامة            : {publicTypes.Count}");
        output.WriteLine($"منها معلَّمة بتكلفة التعميم   : {custodyOnly.Count}");
        output.WriteLine($"منها متعلقة بحيازة المفتاح    : {custodySpecific}");
        output.WriteLine($"نسبة السطح المتعلقة بالحيازة  : {100.0 * custodySpecific / publicTypes.Count:0.0}%");

        Assert.True(publicTypes.Count > 20);
        Assert.True(custodySpecific >= 10);
    }

    /// <summary>
    /// <b>لا اعتمادية مورّد في العقد.</b> الاختبار العملي المذكور في وثيقة المعمارية:
    /// الواجهة تُكتب قبل قراءة توثيق المورّد. إن ظهرت حزمة مورّد هنا يوماً فقد تسرّب.
    /// </summary>
    [Fact]
    public void The_contract_assembly_references_nothing_but_the_base_class_library()
    {
        var referenced = typeof(ProvisionalAttribute).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System.", StringComparison.Ordinal) &&
                        n is not "System" and not "netstandard" and not "mscorlib")
            .ToList();

        Assert.True(referenced.Count == 0,
            "العقد يعتمد على حزم خارج مكتبة الأساس: " + string.Join(", ", referenced));
    }

    /// <summary>لا مفاتيح ولا أسرار تعبر الحدّ — مقابض فقط. يُفحص بالنوع، لا بالمراجعة.</summary>
    [Fact]
    public void No_key_material_type_appears_anywhere_on_the_contract_surface()
    {
        var forbidden = new[] { "ECDsa", "RSA", "X509Certificate2", "AsymmetricAlgorithm", "SafeHandle" };
        var offenders = new List<string>();

        foreach (var type in typeof(ProvisionalAttribute).Assembly.GetExportedTypes())
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (forbidden.Contains(m.ReturnType.Name)) offenders.Add($"{type.Name}.{m.Name} -> {m.ReturnType.Name}");
                foreach (var p in m.GetParameters())
                    if (forbidden.Contains(p.ParameterType.Name))
                        offenders.Add($"{type.Name}.{m.Name}({p.Name}: {p.ParameterType.Name})");
            }
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (forbidden.Contains(p.PropertyType.Name)) offenders.Add($"{type.Name}.{p.Name}: {p.PropertyType.Name}");
        }

        Assert.True(offenders.Count == 0, "مادة مفتاح على سطح العقد:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// المقاصة والإبلاغ نوعان منفصلان تماماً على مستوى العقد: لا واجهة مشتركة،
    /// ولا نوع طلب مشترك، ولا نوع نتيجة مشترك.
    /// </summary>
    [Fact]
    public void Clearance_and_reporting_share_no_type_on_the_contract_surface()
    {
        Assert.Empty(typeof(IClearanceChannel).GetInterfaces());
        Assert.Empty(typeof(IReportingChannel).GetInterfaces());
        Assert.NotEqual(typeof(ClearanceRequest), typeof(ReportingSubmission));
        Assert.NotEqual(typeof(ClearanceOutcome), typeof(ReportingAcknowledgement));

        // ولا يوجد نوع أساس مشترك بينهما غير object.
        Assert.Equal(typeof(object), typeof(ClearanceRequest).BaseType);
        Assert.Equal(typeof(object), typeof(ReportingSubmission).BaseType);

        // وحده الفرق الجوهري ظاهر في العقد: المقاصة تعيد نسخة مختومة، والإبلاغ لا.
        Assert.NotNull(typeof(ClearanceOutcome).GetProperty("StampedDocument"));
        Assert.Null(typeof(ReportingAcknowledgement).GetProperty("StampedDocument"));
    }
}
