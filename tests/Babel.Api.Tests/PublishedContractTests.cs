using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>العقد المنشور: مُودَع، ومطابق، وحتمي.</b>
/// <para>
/// حارس الانحراف بلا حتمية <b>أسوأ من غيابه</b>: يُفشل البناء على تغييرات لم تقع، فيتعلّم
/// الفريق تجاهله، ثم يمرّ التغيير الحقيقي وسط ضجيجه. ولذلك الحتمية تُثبَت هنا أولاً —
/// بتكرار التوليد تحت ثقافات مختلفة ومع تعطيل بيانات المناطق وبدونه — قبل أن يُوثق
/// بالمقارنة أصلاً.
/// </para>
/// </summary>
public sealed class PublishedContractTests
{
    private static string CommittedPath => Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json");

    [Fact]
    public void العقد_مُودَع_في_المستودع_ولا_يُبنى_من_ذاكرة_أحد()
    {
        Assert.True(File.Exists(CommittedPath), $"العقد المنشور غير موجود عند {CommittedPath}.");
        Assert.True(new FileInfo(CommittedPath).Length > 10_000, "العقد المُودَع أصغر من أن يصف سطحاً.");
    }

    [Fact]
    public async Task العقد_المُودَع_يطابق_المُولَّد_بايتاً_بايت()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "babel-openapi-" + Guid.CreateVersion7().ToString("N") + ".json");

        try
        {
            await GenerateAsync(temporary, culture: "en_US.UTF-8", invariantGlobalization: false);

            byte[] generated = await Http.ReadBytesAsync(temporary);
            byte[] committed = await Http.ReadBytesAsync(CommittedPath);

            Console.WriteLine($"مُولَّد : {generated.Length} بايت · {Hash(generated)}");
            Console.WriteLine($"مُودَع  : {committed.Length} بايت · {Hash(committed)}");

            Assert.True(
                generated.AsSpan().SequenceEqual(committed),
                "العقد المُودَع في contracts/openapi/v1.json لا يطابق ما يولّده السطح الآن.\n"
                + "أعِد توليده بالأمر التالي وأودِع الناتج في نفس طلب الدمج الذي غيّر السطح:\n"
                + "  dotnet src/Babel.Api/bin/Release/net10.0/Babel.Api.dll --emit-openapi contracts/openapi/v1.json\n"
                + "وإن كان التغيير يحذف حقلاً أو يعيد تسميته أو يضيّق نوعاً، فهو v2 لا v1 — راجع سياسة الإصدار في ADR-0018.\n"
                + FormattableString.Invariant($"بصمة المُولَّد: {Hash(generated)}\nبصمة المُودَع : {Hash(committed)}"));
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public async Task التوليد_حتمي_عبر_الثقافات_ومع_تعطيل_بيانات_المناطق_وبدونه()
    {
        (string Culture, bool Invariant)[] runs =
        [
            ("en_US.UTF-8", false),
            ("en_US.UTF-8", false),
            ("ar_SA.UTF-8", false),
            ("tr_TR.UTF-8", false),
            ("hi_IN.UTF-8", false),
            ("de_DE.UTF-8", false),
            ("ar_SA.UTF-8", true),
            ("tr_TR.UTF-8", true),
        ];

        List<string> hashes = [];

        foreach ((string culture, bool invariant) in runs)
        {
            string temporary = Path.Combine(
                Path.GetTempPath(), "babel-openapi-" + Guid.CreateVersion7().ToString("N") + ".json");

            try
            {
                await GenerateAsync(temporary, culture, invariant);
                string hash = Hash(await Http.ReadBytesAsync(temporary));
                hashes.Add(hash);
                Console.WriteLine($"{culture,-14} invariant={invariant,-5} → {hash}");
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        Assert.Equal(8, hashes.Count);
        Assert.Single(hashes.Distinct(StringComparer.Ordinal));
        Assert.Equal(Hash(await Http.ReadBytesAsync(CommittedPath)), hashes[0]);
    }

    [Fact]
    public async Task العقد_يصف_كل_ما_يحتاجه_فريق_الواجهة_ولا_يصف_باباً_غير_موجود()
    {
        using JsonDocument document = JsonDocument.Parse(await Http.ReadTextAsync(CommittedPath));
        JsonElement root = document.RootElement;

        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        // سياسة الإصدار مكتوبة **داخل العقد نفسه**: من يقرأ الوثيقة يعرف ما يكسره وما لا يكسره.
        string description = root.GetProperty("info").GetProperty("description").GetString()!;
        Assert.Contains("v2", description, StringComparison.Ordinal);
        Assert.Contains("Forces v2", description, StringComparison.Ordinal);

        JsonElement paths = root.GetProperty("paths");

        // ‏**76 عمليةً — والعدد مكتوب بيد عمداً.** عدٌّ مشتقٌّ من الوثيقة نفسها يقارنها
        // بذاتها فيمرّ على أي إضافة وأي حذف؛ والرقم هنا يُجبر من يوسّع السطح على أن
        // **يمرّ بهذا الملف** فيقرأ سياسة الإصدار قبل أن يوسّعه. ورفعاته: من 16 إلى 17
        // بإضافة GET /companies/{companyId}/chart-of-accounts، ثم من 17 إلى 19 ببابَي
        // التوثيق — GET /openapi/v1.json و GET /docs — ثم من 19 إلى 33 بأربعة عشر
        // باباً لسطح المبيعات والمشتريات: العملاء والموردون، وفواتير المبيعات وفواتير
        // المصروف بمسوّداتها وقراءتها وترحيلها، والإشعارات الدائنة، وأعمار الذمم من
        // الطرفين. ثم من 33 إلى 38 بخمسة أبواب للمصادقة والعضوية: فتح الجلسة
        // وتجديدها وإبطالها، ودعوة عضو وقراءة الأعضاء. ثم من 38 إلى **49** بأحد عشر
        // باباً للنقد وأوامر الشراء: سندات القبض وسندات الصرف — إنشاءً وقراءةً وترحيلاً
        // لكلٍّ — وأوامر الشراء **بابين لا ثلاثة** (لا ترحيل لأمر شراء)، واستلام البضاعة
        // بأبوابه الثلاثة.
        //
        // ثم من 49 إلى **62** بثلاثة عشر باباً: ثمانيةٌ لوحدة المخزون — الأصناف
        // تسجيلاً وقراءةً وقائمةً، وحركات المخزون مسوّدةً وقائمةً وترحيلاً، والأرصدة،
        // والتقييم — وأربعةٌ لتتمّة سلسلة المشتريات المخزنية: الفاتورة المخزنية،
        // ومرتجع المشتريات بمسوّدته وقراءته وترحيله — **وواحدٌ** لسطور الاستلام
        // بمعرّفاتها، وهو مورد فرعي جديد لا شكلٌ مُعاد كتابته: أمر الشراء والاستلام
        // منشوران منذ ADR-0047 ولم يتغيّر لهما جوابٌ ولا عمليةٌ.
        // ثم من 62 إلى **69** بسبعة أبواب للتسجيل والاشتراك ودورة حياة العضوية:
        // إنشاء مستأجر، وقراءة الاشتراك، وتغيير الخطّة، والانقطاع، والاستئناف،
        // وسحب عضوية، وتغيير دورها.
        // ثم من 69 إلى **76** بسبعة أبواب للمرفقات: الإيداع والقراءة والقائمة،
        // والإصدار الجديد بدل التعديل، والسحب بدل الحذف، وسكُّ تذكرة التنزيل
        // والتنزيل بها.
        //
        // ثم من 76 إلى **107** بواحدٍ وثلاثين باباً لوحدة الموارد البشرية على ثمانية
        // وعشرين مساراً: الموظف تسجيلاً وقراءةً وإنهاءَ خدمةٍ **مورداً فرعياً**؛
        // ومكوّنات الأجر تعريفاً وقراءةً — وهي الموضع الذي يصير فيه الأثر التنظيمي
        // بياناتٍ لا شيفرة؛ وعناصر الأجر إسناداً بتاريخ سريان وقراءةً؛ و**نِسَب
        // التأمينات إيداعاً وقراءةً — وهو الموضع الوحيد الذي تدخل منه نسبة إلى هذا
        // النظام، وجدولُه يُسلَّم فارغاً فيُرفض كل مسيّر حتى يُعتمد أول إصدار**؛
        // والمسيّر مسوّدةً وقراءةً وقسائمَ وترحيلاً؛ والقسيمة قراءةً مفردة — **وهي
        // مستند الترحيل**؛ وسند صرف الرواتب وسداد التأمينات بثلاثة أبواب لكلٍّ؛
        // وسجلّ الجزاءات ببابين؛ والسلفة ببابين **لا ثلاثة**؛ ومخصص نهاية الخدمة
        // بثلاثة، والمخالصة بثلاثة؛ ومطابقة دفتر الموظف بابٌ واحد.
        //
        // ‏**وثلاثة أبواب لم تُنشر عمداً، وغيابُها مقروءٌ في العقد نفسه لا في تعليق:**
        //   • ‏POST …/employee-advances/{advanceId}/posting — حدثه غير موجود في مصفوفة
        //     الترحيل، والمحرك يرفض رمزاً لا يعرفه ولا يخترع قالباً.
        //   • ‏POST …/employee-deductions/{deductionId}/posting — الاستقطاع يُرحَّل داخل
        //     المسيّر لا بذاته.
        //   • ‏POST …/payroll-runs/{runId}/wage-protection-files — مواصفة الملفّ نفسها
        //     غير متحقَّق منها، ومخزنُ المرفقات المنشور يقبل مجموعة أنواع محتوى مغلقة
        //     ليس فيها نوعٌ نصّي — وتوسيعُها **تغييرٌ في مجموعة مغلقة منشورة**.
        //
        // وكلّها إضافات محضة تُبقي v1: **لا مسار حُذف، ولا مخطّط حُذف، ولا حقل ضُيّق،
        // ولا اختياري صار إلزامياً** — وذلك مُثبَت بفرق بين العقد المُودَع على
        // ‏origin/develop والعقد هنا، لا بادّعاء.
        Assert.Equal(107, paths.EnumerateObject().SelectMany(static p => p.Value.EnumerateObject())
            .Count(static o => o.Name is "get" or "post" or "put" or "patch" or "delete"));

        // ولا فعل حذف على السطح كلّه — لا على قيد، ولا على مركز تكلفة، ولا على منشأة.
        Assert.DoesNotContain(
            paths.EnumerateObject().SelectMany(static p => p.Value.EnumerateObject()),
            static o => o.Name is "delete");

        // النطاق في كل مسار أعمال مُصدَّر، ولا مسار **بيانات** خارج شركة.
        //
        // والاستثناءان مُسمَّيان هنا حرفياً لا بنمط: نمطٌ فضفاض («ما لا يحمل companyId
        // مسموح») كان سيقبل أي مسار جديد بلا نطاق دون أن ينتبه أحد، وهو بالضبط ما يجعل
        // حارساً كهذا يمرّ على العطل الذي وُجد لأجله.
        //   • ‏/health           — خارج المصادقة، ولا يقرأ بيانات مستأجر ولا يكتبها.
        //   • ‏/api/v1/session   — داخل المصادقة، وخارج النطاق بحكم وظيفته: من لا يعرف
        //     معرّف شركته لا يستطيع أن يضعه في المسار ليسأل عن شركاته. وما يخرج منه هو
        //     مجموعة الاعتماد نفسها، لا استعلام على جدول شركات.
        //   • ‏/openapi/v1.json  — بايتات ملفٍّ مُودَع في المستودع. لا مستأجر له أصلاً،
        //     فالنطاق لا معنى له عليه، والمصادقة عليه تمنع المتصفّح من فتحه (انظر أدناه).
        //   • ‏/docs             — صفحة ساكنة تقرأ ذلك الملفّ. لا تحمل رمزاً ولا تمنح
        //     امتيازاً: زرّ «جرّب» فيها عميلٌ يمرّ بالمصادقة والنطاق والاستحقاق كأي عميل.
        //   • ‏/api/v1/access/sessions و.../renewal و.../revocation — دورةُ حياة الاعتماد
        //     نفسه. وغيابُ النطاق عنها بنيوي كغيابه عن /session: من لا جلسة له لا يعرف
        //     معرّف منشأته ليضعه في المسار، ومن يُبطل جلسته يُبطلها كلّها لا في منشأة.
        //     ولا يخرج من الثلاثة بيانُ منشأةٍ واحد: أسماءُ المنشآت ليست فيها، ودعوةُ
        //     العضو وقراءةُ الأعضاء **داخل** النطاق حيث ينبغي أن تكونا.
        string[] scopeless =
        [
            "/health",
            "/api/v1/session",
            "/openapi/v1.json",
            "/docs",
            "/api/v1/access/sessions",
            "/api/v1/access/sessions/renewal",
            "/api/v1/access/sessions/revocation",
        ];

        // ‏**ونطاقٌ ثانٍ: المستأجر.** وهو ليس ثقباً في «لا مسار خارج نطاق» بل نطاقٌ
        // آخر — المستأجر **فوق** المنشأة لا داخلها، ومن يشترك لأول مرّة لا يملك منشأةً
        // يضع معرّفها في المسار. والمطابقة مفروضة عليه كما تُفرض على نطاق المنشأة:
        // اعتمادٌ لا يبلغ المستأجر يُرفض بـtenancy.tenant_out_of_scope، ولا يُفرَّق في
        // الرفض بين «لا وجود له» و«ليس مستأجرك».
        //
        // **ولا يخرج منه بيانُ مستأجرٍ آخر**: ما تحته اشتراك صاحبه وحده.
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            Assert.True(
                scopeless.Contains(path.Name, StringComparer.Ordinal)
                || path.Name.StartsWith("/api/v1/companies/{companyId}", StringComparison.Ordinal)
                || path.Name == "/api/v1/tenants"
                || path.Name.StartsWith("/api/v1/tenants/{tenantId}", StringComparison.Ordinal),
                $"مسار خارج نطاق الشركة ونطاق المستأجر: {path.Name}");
        }

        // وكل مسار بلا نطاق **مصادَق عليه** إلا ثلاثةً مسمّاةً هنا حرفياً: مسارٌ بلا نطاق
        // وبلا مصادقة هو باب مفتوح، والفرق بينه وبين الباب المقصود سطرٌ واحد في هذا الملف.
        //
        // والثلاثة تكتب بايتات ثابتة أو حالةَ عملية، ولا واحد منها يلمس مستأجراً. وأمّا
        // بابا التوثيق فمجهولان **لسبب تقنيّ قبل أن يكون سياسة**: المتصفّح لا يستطيع أن
        // يضع ترويسة Authorization على تنقّلٍ عُلوي، فصفحةُ توثيق محميّة بـBearer غير
        // قابلة للفتح من شريط العنوان أصلاً — وعلاجها الوحيد ملفّ ارتباط أو جلسة، أي
        // آلية تصريح ثانية بجانب القائمة، وهي أخطر من غيابها (ADR-0036 · فخ-81).
        //
        // ‏**وبابا الجلسة يُضافان إلى هذه القائمة، ولا يُضاف ثالثهما:** فتحُ الجلسة
        // وتجديدها بلا مصادقة **بحكم البنية** — من يطلب اعتماداً لا يملك اعتماداً، وبابٌ
        // يُصدر جلسةً ويشترط جلسةً بابٌ لا يُفتح أبداً. والاعتماد ليس غائباً عنهما بل
        // **منقولاً من الترويسة إلى الجسم**: يُبصَم ويُطابَق بالبصمة كأي اعتماد، والرفض
        // 401 لا 403. أمّا الإبطال فمصادَقٌ عليه: الاعتماد المُقدَّم هو الذي يسمّي ما
        // يُبطَل، وبابُ إبطالٍ بلا مصادقة بابٌ يُسقط به أي أحد جلسة أي أحد.
        //
        // **وبابٌ سادس: التسجيل الأول.** ومن ليس عنده حساب هو بالضبط من يستعمله، فاشتراط
        // اعتماد عليه يجعل المنتَج غير قابل للشراء. وما لا يُفتح بفتحه: لا يقرأ بيانات
        // مستأجرٍ قائم ولا يكشف وجوده، والخطّة لا تُختار من جسمه بل هي خطّة الدخول وحدها،
        // وعليه حدّ معدّل لكل عنوان ولكل مفتاح طلب.
        string[] anonymous =
        [
            "/health",
            "/openapi/v1.json",
            "/docs",
            "/api/v1/access/sessions",
            "/api/v1/access/sessions/renewal",
            "/api/v1/tenants",
        ];

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (anonymous.Contains(path.Name, StringComparer.Ordinal)
                || !(scopeless.Contains(path.Name, StringComparer.Ordinal)
                    || path.Name.StartsWith("/api/v1/tenants", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                // ‏`parameters` على المسار مصفوفةٌ لا عملية — ووسائط المسار تُكتب عليه
                // حيث يشترك فيها كل أفعاله. وقراءتُها عمليةً ترمي «المطلوب كائن ووُجدت
                // مصفوفة»، وهو عطلٌ في الفحص يُقرأ عطلاً في العقد.
                if (operation.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                Assert.False(
                    operation.Value.TryGetProperty("security", out JsonElement security)
                    && security.GetArrayLength() == 0,
                    $"مسار بلا نطاق وبلا مصادقة: {operation.Name} {path.Name}");
            }
        }

        // نمط المال معلن في العقد: من يقرأ الوثيقة يعرف النحو بلا سؤال.
        string moneyPattern = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("Money").GetProperty("pattern").GetString()!;
        Assert.Equal(@"^-?(0|[1-9][0-9]*)(\.[0-9]{1,4})?$", moneyPattern);

        // ولا حقل حساب في أي مخطّط طلب: القاعدة 2 مُعلنة على السلك أيضاً.
        string requestSchemas = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("PostingLine").GetRawText();
        Assert.DoesNotContain("accountCode", requestSchemas, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task GenerateAsync(string target, string culture, bool invariantGlobalization)
    {
        string executable = RepositoryPaths.ApiExecutable;
        Assert.True(File.Exists(executable), $"ثنائي الخادم غير موجود عند {executable} — ابنِ الحل أولاً.");

        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("--emit-openapi");
        start.ArgumentList.Add(target);
        start.Environment["LANG"] = culture;
        start.Environment["LC_ALL"] = culture;

        if (invariantGlobalization)
        {
            start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        }

        using Process process = Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            FormattableString.Invariant($"فشل توليد العقد ({culture}, invariant={invariantGlobalization}) برمز {process.ExitCode}:\n{output}\n{error}"));
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
