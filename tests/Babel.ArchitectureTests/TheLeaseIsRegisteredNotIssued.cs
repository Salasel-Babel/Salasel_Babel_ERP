using System.Reflection;
using System.Text.Json;
using Babel.ArchitectureTests.Support;
using Babel.Core.Entitlement;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>عقد الإيجار يُسجَّل ولا يُحرَّر — والسطح لا يجوز أن يدّعي غير ذلك.</b>
/// <para>
/// <b>القرار:</b> منصّة إيجار الحكومية هي الطرف المخوَّل بتحرير عقود الإيجار. وما
/// يملكه هذا النظام <b>أرشفةٌ وفوترة</b>: قيدٌ عندنا لعقدٍ حُرِّر هناك، مرجعُه رقمُ
/// عقد إيجار، ثم اعتمادُ ذلك القيد <b>للفوترة</b> — وهو إذنٌ داخلي لا نفاذٌ نظامي.
/// (‏<c>docs/decisions/ADR-جديد-the-lease-is-registered-not-issued.md</c>).
/// </para>
/// <para>
/// <b>ولماذا حارسٌ لا مراجعة:</b> ادّعاءُ التحرير لا يدخل بقرارٍ بل <b>بكلمة</b>:
/// مورد فرعي اسمه <c>activation</c>، أو مُعرَّف عملية أوّله <c>create</c>، أو عضو
/// تعدادٍ اسمه <c>ACTIVE</c>. وكلٌّ منها يمرّ في مراجعةٍ لأنه يبدو تسميةً تقنية،
/// ويصل إلى عميلٍ يقرؤه <b>حكماً قانونياً</b>. والفرق بين «قيدُنا معتمَدٌ للفوترة»
/// و«العقد سارٍ» هو الفرق بين وصفِ حالتنا وانتحالِ صلاحية جهةٍ حكومية.
/// </para>
/// <para>
/// <b>وثلاثةُ فحوصٍ من الأربعة بنيويّة لا لفظية</b> — تقرأ شكل السطح المنشور وشكل
/// الخدمة، فتُمسك عودةَ الادّعاء ولو بكلماتٍ لم تخطر لكاتب هذا الملفّ:
/// <list type="number">
///   <item>سطحُ القيد ينشر <b>أربع عمليات بأعيانها</b> ولا خامسة: لا PUT ولا PATCH
///         ولا DELETE، ولا موردَ فرعيّاً غير الجدول والاعتماد. فبابُ فسخٍ أو تجديدٍ
///         أو تعديلٍ يُحمِّر البناء <b>بوجوده</b> لا بتسميته.</item>
///   <item>الخدمة لا تملك <b>مسار كتابةٍ ثالثاً</b> إلى القيد: نقاط الكتابة المحمولة
///         بسمة الاستحقاق اثنتان بأعيانهما.</item>
///   <item>المرجع الموثوق <b>مُصرَّحٌ في العقد</b>: حقلٌ إلزامي اسمه
///         <c>ejarContractNumber</c>، ولا حقلَ اسمه <c>contractNo</c>، ولا عضوَ
///         تعدادٍ اسمه <c>ACTIVE</c> على حالة القيد.</item>
/// </list>
/// </para>
/// <para>
/// <b>والرابع لفظيّ، وحدُّه مُعلَن:</b> قائمةُ عباراتٍ <b>مغلقة ومكتوبة</b> أدناه،
/// تلتقط الصيغ التي <b>لا تصحّ في أي سياق على هذا السطح</b> («تفعيل العقد» ·
/// «العقد سارٍ» · «فسخ العقد» · "activate the lease" …). <b>وصياغةٌ رابعة تُفلت
/// منها</b> — ولذلك هي رابعُ الفحوص لا أوّلها، والحاملُ هو الثلاثة قبلها. وهي
/// <b>لا تمنع ذكر منصّة إيجار بوصفها المحرِّر</b>: «الطرف المخوَّل بتحرير عقود
/// الإيجار» جملةٌ صحيحة، والشاهد السالب أدناه يُثبت أنها لا تُلتقط.
/// </para>
/// <para>
/// <b>وشاهدٌ موجب مُودَع مع كل عبارة:</b> عبارةٌ توقّفت عن المطابقة تجعل «لم يُعثر
/// على شيء» براءةً كاذبة — وهو العطل الذي يوثّقه
/// <c>traps.md#fakh-a-guard-that-never-fires-cannot-be-told-from-a-broken-one</c>.
/// </para>
/// </summary>
public sealed class TheLeaseIsRegisteredNotIssued
{
    /// <summary>العقد المنشور — مصدرُ الفحوص البنيوية الثلاثة.</summary>
    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    /// <summary>
    /// السطح المنشور لقيد التسجيل — <b>أربعة أبواب بأعيانها</b>، بالفعل والمسار معاً.
    /// <para>
    /// وأيُّ بابٍ خامس على المورد نفسه — تعديلاً أو فسخاً أو تجديداً أو «تفعيلاً» —
    /// يُسقط هذا الفحص <b>بوجوده</b>، مهما سُمّي.
    /// </para>
    /// </summary>
    private static readonly string[] TheOnlyDoors =
    [
        "POST /api/v1/companies/{companyId}/lease-registrations",
        "GET /api/v1/companies/{companyId}/lease-registrations/{leaseId}",
        "GET /api/v1/companies/{companyId}/lease-registrations/{leaseId}/schedule",
        "POST /api/v1/companies/{companyId}/lease-registrations/{leaseId}/billing-approval",
    ];

    /// <summary>
    /// <b>نقاط الكتابة إلى القيد — اثنتان بأعيانهما.</b> مسوّدةُ التسجيل، والاعتماد
    /// للفوترة. وثالثةٌ تُكتب غداً (فسخ · تجديد · تعديل) تُحمِّر البناء هنا.
    /// </summary>
    private static readonly string[] TheOnlyWrites = ["ApproveForBillingAsync", "DraftAsync"];

    /// <summary>
    /// <b>العبارات التي لا تصحّ في أي سياق على هذا السطح — ومع كلٍّ شاهدُها الموجب.</b>
    /// <para>
    /// <b>وحدُّها مُعلَن:</b> هي <b>قائمة</b>، لا قاعدةٌ بنيوية. تلتقط الصيغة المكتوبة
    /// ولا تلتقط معناها في صياغةٍ أخرى، وهذا مقبولٌ هنا لأن الحاملَ الفحوصُ الثلاثة
    /// البنيوية قبلها؛ وهذه تمنع <b>عودة اللفظ نفسه</b> الذي كان مكتوباً فعلاً.
    /// </para>
    /// </summary>
    private static readonly (string Claim, string PositiveControl)[] ClaimsTheSurfaceMustNotMake =
    [
        ("تفعيل العقد", "تفعيل العقد يجعل جدول الدفعات قابلاً للفوترة"),
        ("تفعيل عقد", "تفعيل عقد إيجار"),
        ("يُفعّل العقد", "يُفعّل العقد فيولّد جدول دفعاته"),
        ("فعّل العقد", "الخطوة التالية: فعّل العقد أوّلاً"),
        ("العقد سارٍ", "العقد سارٍ، وجدول دفعاته مُولَّد"),
        ("عقدٍ سارٍ", "من جدول دفعات عقدٍ سارٍ"),
        ("عقدٌ سارٍ", "على الوحدة عقدٌ سارٍ"),
        ("إنشاء عقد إيجار", "إنشاء عقد إيجار مسوّدة"),
        ("يُنشئ عقد إيجار", "يُنشئ عقد إيجار في حالة DRAFT"),
        ("فسخ العقد", "فسخ العقد من هذه الشاشة"),
        ("إنهاء العقد", "إنهاء العقد قبل مدّته"),
        ("تجديد العقد", "تجديد العقد لسنة أخرى"),
        ("تعديل العقد", "تعديل العقد بعد توقيعه"),
        ("activate the lease", "Activate the lease and generate its schedule"),
        ("activate a lease", "Activate a lease contract"),
        ("activation of the lease", "The activation of the lease generates the schedule"),
        ("the lease is active", "The lease is active and its schedule is generated"),
        ("an active lease", "instalments from an active lease's payment schedule"),
        ("a live lease", "This term overlaps a live lease on the same unit"),
        ("draft a lease contract", "A request to draft a lease contract"),
        ("creates a lease contract", "Creates a lease contract in state DRAFT"),
        ("create a lease contract", "The system will create a lease contract for you"),
        ("issue a lease", "The company may issue a lease to the tenant"),
        ("terminate the lease", "Terminate the lease from this screen"),
        ("renew the lease", "Renew the lease for another year"),
        ("amend the lease", "Amend the lease after signature"),
    ];

    /// <summary>
    /// <b>شواهد سالبة</b> — جملٌ <b>صحيحة</b> يجب ألّا تلتقطها أي عبارة أعلاه.
    /// <para>
    /// وبدونها تكون القائمة قد تمنع <b>قولَ الحقيقة</b>: أن منصّة إيجار هي المحرِّر،
    /// وأن قيدنا معتمَدٌ للفوترة. وحارسٌ يمنع الصواب أسوأ من حارسٍ لا يمنع الخطأ.
    /// </para>
    /// </summary>
    private static readonly string[] SentencesThatMustPass =
    [
        "منصّة إيجار الحكومية هي الطرف المخوَّل بتحرير عقود الإيجار",
        "يُنشئ قيد تسجيل لعقد إيجار مُحرَّر في منصّة إيجار",
        "القيد معتمَد للفوترة، وجدول دفعاته جاهز للفوترة",
        "The government Ejar platform is the party authorised to issue lease contracts",
        "Creates a registration record for a lease contract, in state DRAFT",
        "The registration is approved for billing and its schedule can be invoiced",
    ];

    /// <summary>
    /// ‏<b>١ · السطح المنشور يحمل أربعة أبواب على القيد ولا خامس.</b>
    /// <para>
    /// بنيويّ بالكامل: يقرأ <c>paths</c> من العقد ولا يقرأ كلمة واحدة من وصف. فبابُ
    /// <c>DELETE</c> أو <c>…/termination</c> أو <c>…/activation</c> يسقط هنا بوجوده.
    /// </para>
    /// </summary>
    [Fact]
    public void TheLeaseSurfacePublishesRegistrationDoorsAndNothingElse()
    {
        List<string> doors = [];

        using JsonDocument contract = JsonDocument.Parse(File.ReadAllText(ContractPath));

        foreach (JsonProperty path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            // ‏`lease-contracts` مقروءٌ عمداً بجانب `lease-registrations`: عودةُ المسار
            // القديم يجب أن تسقط هنا، لا أن تمرّ لأن الفحص لا يراها أصلاً.
            if (!path.Name.Contains("/lease-registrations", StringComparison.Ordinal)
                && !path.Name.Contains("/lease-contracts", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("operationId", out _))
                {
                    doors.Add(operation.Name.ToUpperInvariant() + " " + path.Name);
                }
            }
        }

        doors.Sort(StringComparer.Ordinal);
        string[] expected = [.. TheOnlyDoors.Order(StringComparer.Ordinal)];

        Assert.True(
            doors.Count >= 4,
            "أبواب القيد المقروءة من العقد: " + doors.Count + " — النطاق انكسر ولا شيء يُفحَص. · "
            + "The scan read no lease doors from the published contract.");

        Assert.True(
            expected.SequenceEqual(doors, StringComparer.Ordinal),
            "سطحُ قيد التسجيل يخالف أبوابه الأربعة — وبابٌ خامس على هذا المورد يعني أن النظام "
            + "يدّعي تحرير عقدٍ أو تعديله أو فسخه، وذلك ليس له:\n  المنشور:\n    "
            + string.Join("\n    ", doors)
            + "\n  المسموح:\n    " + string.Join("\n    ", expected));
    }

    /// <summary>
    /// ‏<b>٢ · الوحدة لا تملك مسار كتابةٍ ثالثاً إلى القيد.</b>
    /// <para>
    /// بنيويّ: يقرأ سمة الاستحقاق على أعضاء الخدمة نفسها. فدالّةُ كتابةٍ ثالثة —
    /// مهما سُمّيت، وقبل أن يُنشر لها باب — تسقط هنا.
    /// </para>
    /// </summary>
    [Fact]
    public void TheModuleHasNoThirdWritePathIntoALeaseRegistration()
    {
        Assembly realEstate = BabelAssemblies.Named("Babel.RealEstate");

        Type service = Assert.Single(
            BabelAssemblies.TypesOf(realEstate),
            static type => type.Name == "LeaseRegistrationService");

        MethodInfo[] declared = [.. service
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)];

        Assert.True(
            declared.Length >= 3,
            "دوالُّ الخدمة المقروءة: " + declared.Length + " — الانعكاس انكسر ولا شيء يُفحَص.");

        string[] writes = [.. declared
            .Where(static method =>
                method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is
                { Access: EntitlementAccess.Write })
            .Select(static method => method.Name)
            .Order(StringComparer.Ordinal)];

        Assert.True(
            TheOnlyWrites.SequenceEqual(writes, StringComparer.Ordinal),
            "نقاط الكتابة إلى قيد تسجيل العقد ليست الاثنتين المعلنتين — وثالثةٌ تعني أن الوحدة "
            + "صارت تُعدّل عقداً أو تفسخه أو تُجدّده:\n  الموجود: " + string.Join(" · ", writes)
            + "\n  المسموح: " + string.Join(" · ", TheOnlyWrites));

        Assert.Contains(
            declared,
            static method => method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is
                { Access: EntitlementAccess.Read });
    }

    /// <summary>
    /// ‏<b>٣ · المرجع الموثوق مُصرَّحٌ في العقد، ولا حالةَ تقول «سارٍ».</b>
    /// <para>
    /// بنيويّ: أسماءُ حقولٍ وأعضاءُ تعداد، لا أوصاف. فعودةُ <c>contractNo</c> —
    /// أي «رقمُنا نحن» — أو عودةُ <c>ACTIVE</c> تسقط هنا.
    /// </para>
    /// </summary>
    [Fact]
    public void TheAuthoritativeReferenceIsTheEjarContractNumberAndNoStateSaysInForce()
    {
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllText(ContractPath));
        JsonElement schemas = contract.RootElement.GetProperty("components").GetProperty("schemas");

        JsonElement request = Schema(schemas, "LeaseRegistrationRequest");
        JsonElement registration = Schema(schemas, "LeaseRegistration");

        foreach ((string name, JsonElement schema) in new[]
                 {
                     ("LeaseRegistrationRequest", request),
                     ("LeaseRegistration", registration),
                 })
        {
            string[] properties = [.. schema.GetProperty("properties").EnumerateObject().Select(static p => p.Name)];

            Assert.True(properties.Length >= 5, name + ": حقولٌ مقروءة = " + properties.Length);

            Assert.True(
                properties.Contains("ejarContractNumber", StringComparer.Ordinal),
                name + " بلا حقل ejarContractNumber — والمرجع الموثوق للقيد رقمُ عقد إيجار، "
                + "لا رقمٌ يولّده هذا النظام.");

            Assert.False(
                properties.Contains("contractNo", StringComparer.Ordinal),
                name + " يحمل contractNo — وهو يقول «رقمُنا نحن»، وذلك عودةُ الادّعاء بأن العقد "
                + "يُحرَّر هنا.");

            Assert.Contains(
                "ejarContractNumber",
                schema.GetProperty("required").EnumerateArray().Select(static value => value.GetString()));
        }

        string[] states = [.. registration
            .GetProperty("properties").GetProperty("state").GetProperty("enum")
            .EnumerateArray().Select(static value => value.GetString()!)];

        Assert.True(states.Length >= 2, "أعضاء تعداد الحالة المقروءة: " + states.Length);

        Assert.False(
            states.Contains("ACTIVE", StringComparer.Ordinal),
            "حالةُ القيد تحمل ACTIVE — وهي تُقرأ «العقد سارٍ»، وسريانُ عقد الإيجار حكمٌ لمنصّة "
            + "إيجار لا لهذا الجدول. الحالات المنشورة: " + string.Join(" · ", states));

        Assert.True(
            states.Contains("BILLABLE", StringComparer.Ordinal),
            "حالةُ القيد بلا BILLABLE — والاعتماد للفوترة هو الحدث الوحيد الذي يملكه هذا النظام "
            + "على القيد. الحالات المنشورة: " + string.Join(" · ", states));
    }

    /// <summary>
    /// ‏<b>٤ · لا نصَّ مرئيّ على سطح العقارات يدّعي تحريراً أو تعديلاً أو فسخاً.</b>
    /// <para>
    /// لفظيّ، وحدُّه مُعلَن في رأس هذا الملفّ. ويمسح: نصوصَ العقد المنشور على أبواب
    /// القيد ومخطّطاته، وشيفرةَ الوحدة كلَّها ونصوصَ ترقيتها، وشاشاتِ العقارات،
    /// و<b>شجرة <c>realestate</c> وحدها</b> من ملفّات اللغات الأربع.
    /// </para>
    /// </summary>
    [Fact]
    public void NoVisibleTextOnTheRealEstateSurfaceClaimsToIssueOrAmendOrTerminateAContract()
    {
        List<(string Where, string Text)> scanned = [.. LeaseTextFromTheContract()];

        foreach (string path in Directory
                     .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src", "Babel.RealEstate"), "*.*",
                         SearchOption.AllDirectories)
                     .Where(static path => path.EndsWith(".cs", StringComparison.Ordinal)
                                        || path.EndsWith(".sql", StringComparison.Ordinal))
                     .Concat(Directory.EnumerateFiles(
                         Path.Combine(RepositoryLayout.Root, "web", "src", "screens", "realestate"), "*.*",
                         SearchOption.AllDirectories))
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            scanned.Add((Relative(path), File.ReadAllText(path)));
        }

        foreach (string tag in new[] { "ar", "en", "hi", "ur" })
        {
            string path = Path.Combine(
                RepositoryLayout.Root, "web", "src", "i18n", "locales", tag + ".web.ts");

            scanned.Add((Relative(path) + " › realestate", RealEstateSubtree(File.ReadAllText(path))));
        }

        Assert.True(scanned.Count >= 15, "مواضع ممسوحة: " + scanned.Count + " — النطاق انكسر.");
        Assert.True(
            scanned.Sum(static entry => entry.Text.Length) >= 100_000,
            "محارف ممسوحة: " + scanned.Sum(static entry => entry.Text.Length) + " — النطاق انكسر.");

        List<string> violations = [];

        foreach ((string where, string text) in scanned)
        {
            foreach ((string claim, _) in ClaimsTheSurfaceMustNotMake)
            {
                if (text.Contains(claim, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(where + ": «" + claim + "»");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "نصٌّ على سطح العقارات يدّعي أن النظام يُحرّر عقد إيجار أو يُعدّله أو يفسخه أو يجعله "
            + "سارياً — والمحرِّر منصّةُ إيجار وحدها:\n  " + string.Join("\n  ", violations));
    }

    /// <summary>
    /// <b>الشاهد الموجب والسالب معاً — الحارس يُثبت أنه ينطق وأنه لا ينطق على الصواب.</b>
    /// <para>
    /// عبارةٌ توقّفت عن المطابقة تجعل الفحص أعلاه يمرّ على لا شيء ويبدو أخضر؛ وعبارةٌ
    /// اتّسعت تمنع كتابةَ الجملة الصحيحة عن منصّة إيجار. والاثنان يسقطان هنا.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryDeclaredClaimFiresOnItsControlAndNoneFiresOnTheTruth()
    {
        Assert.True(ClaimsTheSurfaceMustNotMake.Length >= 20);

        foreach ((string claim, string control) in ClaimsTheSurfaceMustNotMake)
        {
            Assert.True(
                control.Contains(claim, StringComparison.OrdinalIgnoreCase),
                "العبارة «" + claim + "» لا تطابق شاهدها الموجب «" + control + "» — "
                + "وعبارةٌ لا نعرف أنها تعمل تجعل «لم يُعثر على شيء» براءةً كاذبة.");
        }

        foreach (string truth in SentencesThatMustPass)
        {
            string[] wrong = [.. ClaimsTheSurfaceMustNotMake
                .Select(static entry => entry.Claim)
                .Where(claim => truth.Contains(claim, StringComparison.OrdinalIgnoreCase))];

            Assert.True(
                wrong.Length == 0,
                "جملةٌ صحيحة تلتقطها القائمة: «" + truth + "» بـ" + string.Join(" · ", wrong)
                + " — وحارسٌ يمنع قول الحقيقة أسوأ من حارسٍ لا يمنع الخطأ.");
        }
    }

    /// <summary>نصوصُ أبواب القيد ومخطّطاته من العقد المنشور — لا من الباعث.</summary>
    private static IEnumerable<(string Where, string Text)> LeaseTextFromTheContract()
    {
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllText(ContractPath));

        foreach (JsonProperty path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.Contains("/lease-registrations", StringComparison.Ordinal)
                && !path.Name.Contains("/lease-contracts", StringComparison.Ordinal))
            {
                continue;
            }

            yield return ("contracts/openapi/v1.json › " + path.Name, path.Value.GetRawText());
        }

        JsonElement schemas = contract.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (JsonProperty schema in schemas.EnumerateObject())
        {
            if (schema.Name.StartsWith("Lease", StringComparison.Ordinal))
            {
                yield return ("contracts/openapi/v1.json › " + schema.Name, schema.Value.GetRawText());
            }
        }
    }

    /// <summary>
    /// شجرة <c>realestate</c> وحدها من ملفّ لغة — <b>لا الملفّ كلّه</b>: «تفعيل» مشروعةٌ
    /// في الاشتراك وفي المخزون، والحكمُ هنا على سطح العقارات لا على المستودع.
    /// </summary>
    private static string RealEstateSubtree(string file)
    {
        const string Marker = "\n  realestate: {";
        int start = file.IndexOf(Marker, StringComparison.Ordinal);

        Assert.True(start >= 0, "شجرة realestate غير موجودة في ملفّ اللغة — النطاق انكسر.");

        int depth = 0;
        for (int index = start + Marker.Length - 1; index < file.Length; index++)
        {
            if (file[index] == '{')
            {
                depth++;
            }
            else if (file[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return file[start..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("شجرة realestate غير مغلقة في ملفّ اللغة.");
    }

    private static JsonElement Schema(JsonElement schemas, string name)
    {
        Assert.True(
            schemas.TryGetProperty(name, out JsonElement schema),
            "المخطّط " + name + " غير منشور في العقد — والسطح لم يعد سطحَ قيدِ تسجيل.");

        return schema;
    }

    private static string Relative(string path)
        => Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
}
