using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Babel.Ai.Capture;
using Babel.Ai.Promotion;
using Babel.Ai.Tests.Support;
using NetArchTest.Rules;
using Xunit;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace Babel.Ai.Tests.Architecture;

/// <summary>
/// شاهدٌ موجب للحارس أدناه: نوعٌ <b>يعتمد فعلاً</b> على عقد الترحيل، ويعيش في تجميعة
/// الاختبار لا في الوحدة. وجوده هو ما يُثبت أن الفحص <b>يعضّ</b>: لو توقّف المُحلِّل عن
/// رؤية الاعتمادات لمرّ الحارس فارغاً وهو يعد بما لا يفعل — وهو عطلٌ شُحن في هذا
/// المستودع من قبل.
/// </summary>
internal static class PostingAwareControl
{
    /// <summary>اعتماد صريح على عقد الترحيل — لا يُحذف، فهو موضوع الشاهد.</summary>
    public static Type Referenced => typeof(Babel.Contracts.Posting.PostingRequest);
}

/// <summary>
/// <b>الحارس — المسوّدة عاجزة بنيوياً عن بلوغ الدفتر.</b>
/// <para>
/// على غرار القاعدة 12: لا يذكّر بالقاعدة، بل يجعل خرقها <b>يُفشل البناء</b>. والفرق بين
/// «لا نكتب في الدفتر من هنا» و«لا نستطيع» هو الفرق بين عُرفٍ يُنسى وبنيةٍ تصمد.
/// </para>
/// <para>
/// <b>ما الذي يجعله ضرورياً:</b> المستند الملتقَط يبدو فاتورةً — له بائع ورقم وتاريخ
/// وإجمالي وضريبة وسطور. وأقصر طريق من هذا الشكل إلى قيدٍ في الدفتر يبدو في كل مراجعة
/// «تحسيناً»: تُحقن <c>IPostingService</c>، ويُبنى <c>PostingRequest</c> من الحقول
/// الجاهزة، ويُختصر على المحاسب ضغطتان. والنتيجة <b>مسار ثانٍ يجيب عن سؤال أُصلح المسار
/// الأول ليجيب عنه</b> — وهو صنف العطل الذي أنفق هذا المستودع شهراً في إزالته.
/// </para>
/// <para>
/// والإنفاذ بثلاث طبقات، كلٌّ منها وحدها قابلة للالتفاف:
/// <list type="number">
///   <item><b>لا مرجع مشروع</b> من <c>Babel.Ai</c> إلى الدفتر ولا إلى وحدة أفقية.</item>
///   <item><b>لا اعتماد IL ولا سطح</b> على <c>Babel.Contracts.Posting</c> — فلا يوجد في
///         التجميعة ما يُبنى منه طلب ترحيل أصلاً، ولو أُتيح المرجع.</item>
///   <item><b>لا جدول ولا SQL</b> في الوحدة: المسوّدة لا تسكن جداول وحدة مالية، والترقية
///         تمرّ بخدمات الوحدة المالكة عبر منفذ معلن.</item>
/// </list>
/// </para>
/// </summary>
public sealed class TheCapturedDraftCannotReachTheLedger(ITestOutputHelper output)
{
    /// <summary>فضاء أسماء عقد الترحيل — الطريق الوحيد إلى الدفتر في هذا النظام كله.</summary>
    private const string PostingContract = "Babel.Contracts.Posting";

    private static Assembly CaptureModule => typeof(AiModuleInfo).Assembly;

    private static IReadOnlyList<Type> CaptureTypes { get; } =
        [.. CaptureModule.GetTypes().Where(static type => (type.FullName?.Contains('<', StringComparison.Ordinal) ?? false) == false)];

    // ── الطبقة الأولى: المرجع ───────────────────────────────────────────────

    /// <summary>
    /// مراجع <c>Babel.Ai</c> ثلاثة بالضبط: النواة المشتركة والعقود ونواة النظام.
    /// لا دفتر، ولا وحدة أفقية، ولا مزوّد التزام. والمرجع يُمنع <b>حتى وهو غير مستعمل</b>:
    /// مرجعٌ موجود ولم يُستعمل بعد لا يظهر في IL، فيمرّ حتى يوم يكتب أحدهم أول سطر يستعمله.
    /// </summary>
    [Fact]
    public void TheCaptureModuleReferencesNeitherTheLedgerNorAnyHorizontalModule()
    {
        string[] references = [.. XDocument
            .Load(RepositoryRoot.At("src/Babel.Ai/Babel.Ai.csproj"))
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .Select(static include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)];

        foreach (string reference in references)
        {
            output.WriteLine("مرجع: " + reference);
        }

        Assert.Equal(["Babel.Contracts", "Babel.Core", "Babel.SharedKernel"], references);
    }

    // ── الطبقة الثانية: لا أثر لعقد الترحيل داخل التجميعة ───────────────────

    /// <summary>
    /// لا نوع في التجميعة يعتمد على <c>Babel.Contracts.Posting</c> — لا محرك ترحيل،
    /// ولا طلب، ولا سطر، ولا دور، ولا رمز حدث كنوع.
    /// </summary>
    [Fact]
    public void NoTypeInTheCaptureModuleDependsOnThePostingContract()
    {
        ArchTestResult result = Types.InAssembly(CaptureModule)
            .Should()
            .NotHaveDependencyOn(PostingContract)
            .GetResult();

        output.WriteLine("أنواع الوحدة المفحوصة: " + CaptureTypes.Count.ToString(CultureInfo.InvariantCulture));

        Assert.True(
            result.IsSuccessful,
            "أنواع في وحدة الالتقاط تعتمد على عقد الترحيل — وهذه هي اللحظة التي تصير فيها المسوّدة قادرة على بلوغ الدفتر:\n"
            + string.Join('\n', result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// والسطح كذلك: لا حقل ولا خاصية ولا وسيط ولا ناتج في الوحدة كلها نوعُه من عقد الترحيل.
    /// فحص الانعكاس هنا ليس تكراراً لفحص IL: هو يُمسك بما يمرّ عبر <b>التوقيعات</b>
    /// ولو لم يُستعمل جسده بعد.
    /// </summary>
    [Fact]
    public void NoMemberOfTheCaptureModuleExposesATypeFromThePostingContract()
    {
        const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        List<string> violations = [];
        int inspected = 0;

        foreach (Type type in CaptureTypes)
        {
            foreach (MemberInfo member in type.GetMembers(All))
            {
                foreach ((string description, Type valueType) in ValueTypesOf(member))
                {
                    inspected++;
                    foreach (Type candidate in Unwrap(valueType))
                    {
                        if (candidate.Namespace?.StartsWith(PostingContract, StringComparison.Ordinal) == true)
                        {
                            violations.Add(type.FullName + "." + description + " : " + candidate.FullName);
                        }
                    }
                }
            }
        }

        output.WriteLine("أعضاء مفحوصة: " + inspected.ToString(CultureInfo.InvariantCulture));

        Assert.True(inspected > 200, "عدد الأعضاء المفحوصة " + inspected.ToString(CultureInfo.InvariantCulture) + " أقلّ من أن يثبت شيئاً");
        Assert.True(violations.Count == 0, "أعضاء تكشف نوعاً من عقد الترحيل:\n" + string.Join('\n', violations));
    }

    // ── الطبقة الثالثة: لا جدول ولا SQL ولا ترقية ذاتية ─────────────────────

    /// <summary>
    /// الوحدة لا تملك جدولاً ولا تكتب SQL. المسوّدة ليست مستنداً محاسبياً فلا تسكن جداول
    /// وحدة مالية، ولا تفتح لنفسها طريقاً ثانياً إلى قاعدة البيانات.
    /// </summary>
    [Fact]
    public void TheCaptureModuleOwnsNoTableAndShipsNoSql()
    {
        List<string> contexts = [.. CaptureTypes.Where(IsDbContext).Select(static type => type.FullName!)];
        Assert.True(contexts.Count == 0, "سياق EF داخل وحدة الالتقاط:\n" + string.Join('\n', contexts));

        string[] sql = [.. Directory
            .EnumerateFiles(RepositoryRoot.At("src/Babel.Ai"), "*.sql", SearchOption.AllDirectories)
            .Where(static path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(static path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))];

        Assert.True(sql.Length == 0, "نصوص SQL داخل وحدة الالتقاط:\n" + string.Join('\n', sql));

        string[] packages = [.. XDocument
            .Load(RepositoryRoot.At("src/Babel.Ai/Babel.Ai.csproj"))
            .Descendants("PackageReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .Order(StringComparer.Ordinal)];

        foreach (string package in packages)
        {
            output.WriteLine("حزمة: " + package);
        }

        Assert.DoesNotContain(packages, package => package.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(packages, package => package.Contains("Npgsql", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>الترقية منفذ لا تنفيذ.</b> لا نوع داخل الوحدة ينفّذ
    /// <see cref="ICapturedInvoiceReceiver"/> — فالوحدة لا تستطيع أن تُرقّي مسوّدتها بنفسها،
    /// والوحدة المالكة للمستند هي التي تنفّذه بخدماتها المعتادة.
    /// </summary>
    [Fact]
    public void TheModuleShipsNoImplementationOfThePromotionPortAndThereforeCannotPromoteItself()
    {
        List<string> implementations = [.. CaptureTypes
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => typeof(ICapturedInvoiceReceiver).IsAssignableFrom(type))
            .Select(static type => type.FullName!)];

        Assert.True(
            implementations.Count == 0,
            "تنفيذ لمنفذ الترقية داخل وحدة الالتقاط — أي مسوّدة تُرقّي نفسها:\n" + string.Join('\n', implementations));

        // والمنفذ موجود فعلاً وواجهة: لو حُذف لمرّ الفحص أعلاه فارغاً.
        Assert.True(typeof(ICapturedInvoiceReceiver).IsInterface);
    }

    /// <summary>
    /// المسوّدة لا تحمل هوية ترحيل: لا معرّف قيد، ولا رقم قيد، ولا بصمة، ولا مفتاح إحكام.
    /// حقلٌ واحد من هذه يجعل «المسوّدة صارت قيداً» تعبيراً ممكناً في النموذج.
    /// </summary>
    [Fact]
    public void TheDraftCarriesNoPostingIdentity()
    {
        string[] forbidden =
        [
            "JournalEntryId", "EntryNumber", "EntryHash", "ChainSequence",
            "IdempotencyKey", "PostingGeneration", "Generation", "PeriodCode",
        ];

        string[] members = [.. typeof(CapturedInvoiceDraft)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static member => member.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        foreach (string name in forbidden)
        {
            Assert.DoesNotContain(name, members, StringComparer.Ordinal);
        }

        // ولافراغ: المسوّدة تحمل ما يجب أن تحمله، فالفحص أعلاه لم يمرّ على نوع فارغ.
        output.WriteLine("أعضاء المسوّدة: " + string.Join(" · ", members));
        Assert.Contains(nameof(CapturedInvoiceDraft.DraftId), members, StringComparer.Ordinal);
        Assert.Contains(nameof(CapturedInvoiceDraft.State), members, StringComparer.Ordinal);
        Assert.True(members.Length >= 15, "سطح المسوّدة أضيق من المتوقّع — الفحص يمرّ فارغاً");
    }

    // ── اللافراغ: الشاهد الموجب ─────────────────────────────────────────────

    /// <summary>
    /// <b>الحارس يعضّ.</b> الفحص نفسه يُطبَّق على تجميعة الاختبار — وفيها
    /// <see cref="PostingAwareControl"/> يعتمد على عقد الترحيل عمداً — فيجب أن <b>يسقط</b>
    /// ويُسمّي ذلك النوع بعينه.
    /// <para>
    /// ولولا هذا الشاهد لكان «لا اعتماد» ادّعاءً لا يُميَّز عن «لم يُفحص شيء». وهذا
    /// المستودع شحن قاعدةً مرّت فارغاً بينما اسمها يعد بغير ذلك، فالشاهد الموجب شرط.
    /// </para>
    /// </summary>
    [Fact]
    public void TheGuardIsNotVacuousBecauseItCatchesADeliberateViolationInThisVeryAssembly()
    {
        ArchTestResult control = Types.InAssembly(typeof(PostingAwareControl).Assembly)
            .Should()
            .NotHaveDependencyOn(PostingContract)
            .GetResult();

        output.WriteLine("النوع الشاهد يعتمد على: " + PostingAwareControl.Referenced.FullName);
        output.WriteLine("الفحص على تجميعة الاختبار نجح؟ " + control.IsSuccessful.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("الأنواع التي أسقطها: " + string.Join(" · ", control.FailingTypeNames ?? []));

        Assert.False(control.IsSuccessful, "الفحص لم يلتقط اعتماداً مقصوداً — أي أنه لا يلتقط شيئاً");
        Assert.Contains(
            control.FailingTypeNames ?? [],
            name => name.Contains(nameof(PostingAwareControl), StringComparison.Ordinal));

        // ونطاق الفحص ليس ضامراً: التجميعة محمَّلة وأنواعها كثيرة، وعقد الترحيل قابل للحلّ.
        Assert.True(CaptureTypes.Count >= 20, "عدد أنواع وحدة الالتقاط " + CaptureTypes.Count.ToString(CultureInfo.InvariantCulture) + " أقلّ من المتوقّع");
        Assert.NotNull(typeof(Babel.Contracts.Posting.IPostingService).FullName);
    }

    // ── أدوات ───────────────────────────────────────────────────────────────

    private static bool IsDbContext(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.FullName == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Description, Type Type)> ValueTypesOf(MemberInfo member)
    {
        switch (member)
        {
            case FieldInfo field:
                yield return ("حقل " + field.Name, field.FieldType);
                break;

            case PropertyInfo property:
                yield return ("خاصية " + property.Name, property.PropertyType);
                break;

            case MethodInfo method:
                if (method.ReturnType != typeof(void))
                {
                    yield return ("ناتج " + method.Name, method.ReturnType);
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return ("وسيط " + method.Name + "(" + parameter.Name + ")", parameter.ParameterType);
                }

                break;

            case ConstructorInfo constructor:
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    yield return ("وسيط منشئ (" + parameter.Name + ")", parameter.ParameterType);
                }

                break;

            default:
                break;
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        Type current = type;
        if (current.IsByRef || current.IsPointer)
        {
            current = current.GetElementType() ?? current;
        }

        if (current.IsArray)
        {
            foreach (Type inner in Unwrap(current.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        if (current.IsGenericType)
        {
            yield return current.GetGenericTypeDefinition();
            foreach (Type argument in current.GetGenericArguments())
            {
                foreach (Type inner in Unwrap(argument))
                {
                    yield return inner;
                }
            }

            yield break;
        }

        yield return current;
    }
}
