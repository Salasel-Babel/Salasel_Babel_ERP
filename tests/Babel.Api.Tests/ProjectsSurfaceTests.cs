using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح المقاولات عبر السلك</b> — المشروع، والعقد، وجدول الكميات، والمستخلص، والرفض.
/// <para>
/// وما تُثبته هذه المجموعة ليس أن الوحدة تحسب صحيحاً — لذلك مجموعتها الخاصّة في
/// <c>Babel.Projects.Tests</c> على دفتر أستاذ حقيقي — بل أن <b>ما تفعله يمكن طلبه من
/// الشبكة</b>: باعتماد وعنوان وعقد منشور، وأن **الرفض يخرج برمز الحالة الذي يَعِد به
/// العقد** لا برمزٍ آخر.
/// </para>
/// <para>
/// <b>ولماذا يستحقّ ذلك مجموعةً:</b> خمسةٌ وثلاثون باباً في عقدٍ منشور لا يطرقها اختبارٌ
/// واحد هي خمسةٌ وثلاثون باباً <b>لم تُنفَّذ قطّ</b> — ورمزُ حالةٍ خاطئ عليها لا يُكتشف
/// إلا عند أول عميل. وقد كان ذلك واقعاً فعلاً في هذا الفرع: كل رمز <c>projects.*</c> كان
/// يسقط إلى <b>500</b> في مُصنِّف المشكلات، والعقد يَعِد بـ404 و409 و422 — ولم يقل ذلك
/// حارسٌ واحد.
/// </para>
/// <para>
/// <b>وعلى الشركة «ب»</b> كسائر المجموعات التي تكتب: ميزان الشركة «أ» مقسومٌ بين
/// اختبارات تؤكّد عدد صفوفه بالضبط. وهذه المجموعة <b>لا تُرحّل شيئاً</b> على أي حال:
/// الترحيل الوحيد الممكن في الوحدة يُثبَت حيث يوجد دفترٌ حقيقي.
/// </para>
/// </summary>
public sealed class ProjectsSurfaceTests
{
    [Fact]
    public async Task المشروع_والعقد_يُسجَّلان_من_الشبكة_وبنود_جدول_الكميات_تُقرأ_بمعرّفاتها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;
        string projectCode = Documents.Number("PRJ");

        // ── ١ · تسجيل مشروع: 201 وعنوانٌ يوجّه إلى مورد القراءة ──────────────
        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "projects"),
            ApiFixture.TokenB,
            $$"""
              {
                "code": "{{projectCode}}",
                "nameAr": "مشروع اختبار السطح",
                "nameTranslations": [{ "name": "en", "value": "Surface test project" }],
                "startedOn": "2026-01-01"
              }
              """));

        (string createdText, JsonElement project) = await Http.BodyAsync(created);
        Console.WriteLine("المشروع: " + createdText);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(projectCode, project.GetProperty("code").GetString());

        // ‏**والاسم العربي سجلٌّ وترجماته صفوف.**
        // وأمّا غيابُ النصف الإنجليزي الثابت من الجواب الحيّ فيُثبَت في
        // ‏`EnglishIsOneOfNOnTheWireTests` لا هنا: الشاهد الموجب يكتب الشكل الممنوع
        // بالضرورة، وكتابتُه هنا ترفع الدين الذي يقيسه حارس القاعدة 14 — وذلك الملفّ
        // وحده مُقصى من العدّ بسببٍ مكتوب، فيُجمع فيه كل شاهدٍ من هذا النوع.
        Assert.Equal("مشروع اختبار السطح", project.GetProperty("nameAr").GetString());
        JsonElement translation = Assert.Single(project.GetProperty("nameTranslations").EnumerateArray().ToList());
        Assert.Equal("en", translation.GetProperty("name").GetString());

        string projectId = project.GetProperty("id").GetString()!;

        // ── ٢ · العقد ببنده، ومعه بنوده المعلَّقة ────────────────────────────
        string contractNumber = Documents.Number("PC");

        using HttpResponseMessage contractCreated = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "project-contracts"),
            ApiFixture.TokenB,
            $$"""
              {
                "number": "{{contractNumber}}",
                "projectId": "{{projectId}}",
                "customerPartyId": "CUST-SURFACE",
                "signedOn": "2026-01-10",
                "retentionRate": "0",
                "guaranteeMonths": 12,
                "items": [
                  {
                    "code": "B-1",
                    "descriptionAr": "حفر وردم",
                    "contractQuantity": { "magnitude": "100.000000", "unit": "m3" },
                    "unitRate": "1.0000"
                  }
                ]
              }
              """));

        (string contractText, JsonElement contract) = await Http.BodyAsync(contractCreated);
        Assert.True(contractCreated.StatusCode == HttpStatusCode.Created, contractText);

        string contractId = contract.GetProperty("id").GetString()!;

        // ‏**البنود المعلَّقة تخرج على العقد نفسه**: من يقرأ العقد يعرف سلفاً ما الذي
        // سيرفضه الترحيل ولماذا، بدل أن يكتشفه عند أول محاولة مالية.
        List<JsonElement> pending = [.. contract.GetProperty("pendingPolicy").EnumerateArray()];
        Console.WriteLine("البنود المعلَّقة: " + string.Join(
            " · ", pending.Select(item => item.GetProperty("code").GetString())));

        Assert.Equal(4, pending.Count);
        foreach (JsonElement item in pending)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("titleAr").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("titleEn").GetString()));
        }

        // ── ٣ · بنود جدول الكميات بمعرّفاتها ووحداتها ────────────────────────
        using HttpResponseMessage items = await api.Call(Http.Request(
            HttpMethod.Get, Path(company, "project-contracts/" + contractId + "/boq-items"), ApiFixture.TokenB));

        (string itemsText, JsonElement itemList) = await Http.BodyAsync(items);
        Assert.True(items.StatusCode == HttpStatusCode.OK, itemsText);
        Assert.Equal(1, itemList.GetProperty("itemCount").GetInt32());

        JsonElement boq = Assert.Single(itemList.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("m3", boq.GetProperty("contractQuantity").GetProperty("unit").GetString());
        Assert.False(string.IsNullOrWhiteSpace(boq.GetProperty("id").GetString()));
    }

    /// <summary>
    /// <b>المسوّدة تُحفظ والترحيل يُرفض — بالرمز الذي يَعِد به العقد.</b>
    /// <para>
    /// وهذا هو البند الذي كان سيمرّ صامتاً: الرفض يقع، والرسالة صحيحة، و<b>رمز الحالة
    /// 500</b> — فيقرؤه العميل عطلاً في الخادم ويُعيد المحاولة إلى الأبد، بدل أن يقرأه
    /// حالةً على العقد فيتوقّف ويسأل محاسبه.
    /// </para>
    /// </summary>
    [Fact]
    public async Task مستخلص_عقدٍ_بلا_بندٍ_محسوم_يُرفض_بـ409_ورسالةٍ_بلغتين_تسمّي_البند()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        await SaveCertificateProfileAsync(api, company);

        string projectId = await CreateProjectAsync(api, company);
        (string contractId, string boqItemId) = await CreateContractAsync(api, company, projectId);

        // ── المسوّدة تُحفظ: الكمّيات المُقاسة واقعةٌ يسجّلها المهندس ──────────
        string number = Documents.Number("IPC");

        using HttpResponseMessage drafted = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "client-certificates"),
            ApiFixture.TokenB,
            $$"""
              {
                "number": "{{number}}",
                "ownerId": "{{contractId}}",
                "sequenceNo": 1,
                "periodFrom": "2026-03-01",
                "periodTo": "2026-03-31",
                "lines": [
                  {
                    "itemId": "{{boqItemId}}",
                    "lineKind": "WORK",
                    "descriptionAr": "أعمال الفترة",
                    "cumulativeQuantity": { "magnitude": "4.000000", "unit": "m3" },
                    "amount": "0.0000"
                  }
                ]
              }
              """));

        (string draftText, JsonElement draft) = await Http.BodyAsync(drafted);
        Assert.True(drafted.StatusCode == HttpStatusCode.Created, draftText);
        Assert.Equal("DRAFT", draft.GetProperty("state").GetString());

        // ‏**ولا مبالغ محسوبة في الجواب**: أساسُ كلٍّ منها بندٌ معلَّق، وعرضُ رقمٍ قبل أن
        // يُحسم أساسه أسوأ من غيابه. والكمّية السابقة صفر لأن الأساس من المُرحَّل وحده.
        Assert.False(draft.TryGetProperty("workValue", out _));
        Assert.False(draft.TryGetProperty("tax", out _));

        JsonElement line = Assert.Single(draft.GetProperty("lines").EnumerateArray().ToList());
        Assert.Equal("0.000000", line.GetProperty("previousQuantity").GetProperty("magnitude").GetString());

        string certificateId = draft.GetProperty("id").GetString()!;

        // ── والترحيل يُرفض: 409 ورمزٌ مستقرّ ورسالتان تسمّيان البنود ─────────
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "client-certificates/" + certificateId + "/posting"),
            ApiFixture.TokenB));

        (string postedText, JsonElement problem) = await Http.BodyAsync(posted);
        Console.WriteLine("رفض الترحيل: " + postedText);

        Assert.Equal(HttpStatusCode.Conflict, posted.StatusCode);
        Assert.Equal("application/problem+json", posted.Content.Headers.ContentType?.MediaType);
        Assert.Equal("projects.contract_policy.pending", Http.CodeOf(problem));

        JsonElement error = problem.GetProperty("errors").EnumerateArray().First();
        Assert.Contains("وعاء", error.GetProperty("messageAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("retention", error.GetProperty("messageEn").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وما لا يُرحَّل لا يُنشر له باب ترحيل — والغياب يُقرأ من السطح لا من تعليق.</b>
    /// <para>
    /// الأمر التغييري التزامٌ تعاقدي وخطاب الضمان سجلّ، ولا حدث لأيٍّ منهما في مصفوفة
    /// الترحيل. فمخطّطا جوابيهما بلا <c>entryId</c> وبلا <c>alreadyPosted</c>، ومورد
    /// <c>…/posting</c> عليهما <b>يردّ 404</b> لأنه غير مُسجَّل أصلاً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الأمر_التغييري_بابان_لا_ثلاثة_ولا_حقل_قيدٍ_في_جوابه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        string projectId = await CreateProjectAsync(api, company);
        (string contractId, _) = await CreateContractAsync(api, company, projectId);

        string number = Documents.Number("CO");

        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "change-orders"),
            ApiFixture.TokenB,
            $$"""
              {
                "number": "{{number}}",
                "contractId": "{{contractId}}",
                "issuedOn": "2026-02-01",
                "reasonAr": "زيادة كميات الحفر",
                "approvedBy": "مدير المشروع",
                "addedItems": [
                  {
                    "code": "B-2",
                    "descriptionAr": "حفر إضافي",
                    "contractQuantity": { "magnitude": "20.000000", "unit": "m3" },
                    "unitRate": "1.0000"
                  }
                ]
              }
              """));

        (string createdText, JsonElement order) = await Http.BodyAsync(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdText);

        // ‏**الغياب بنيوي**: حقلٌ فارغ لهما يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً».
        Assert.False(order.TryGetProperty("entryId", out _));
        Assert.False(order.TryGetProperty("alreadyPosted", out _));

        string orderId = order.GetProperty("id").GetString()!;

        using HttpResponseMessage posting = await api.Call(Http.Request(
            HttpMethod.Post, Path(company, "change-orders/" + orderId + "/posting"), ApiFixture.TokenB));

        Assert.Equal(HttpStatusCode.NotFound, posting.StatusCode);
    }

    /// <summary>ومستخلصٌ لا وجود له يُرفض بـ404 لا بـ500 — والفرق هو الفرق كلّه عند العميل.</summary>
    [Fact]
    public async Task مستندٌ_لا_وجود_له_يُرفض_بـ404_برمزه_لا_بعطلٍ_في_الخادم()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;
        string absent = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Path(company, "client-certificates/" + absent), ApiFixture.TokenB));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("مستند غائب: " + text);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("projects.not_found", Http.CodeOf(problem));
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static string Path(Guid company, string resource) => string.Create(
        CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/{resource}");

    private static async Task<string> CreateProjectAsync(ApiProcess api, Guid company)
    {
        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "projects"),
            ApiFixture.TokenB,
            $$"""
              {
                "code": "{{Documents.Number("PRJ")}}",
                "nameAr": "مشروع اختبار",
                "nameTranslations": [],
                "startedOn": "2026-01-01"
              }
              """));

        (string text, JsonElement body) = await Http.BodyAsync(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, text);
        return body.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// عقدٌ ببندٍ واحد في جدول كمياته.
    /// <para>
    /// <b>ونسبة المحتجز صفر لأن هذا العقد لا ينصّ على محتجز</b> — لا لأن الاختبار
    /// يختار نسبة. والنسبة حقلٌ من نصّ العقد يكتبه الطالب، لا سياسةً تُشتقّ هنا؛
    /// وكتابةُ رقمٍ غير الصفر كانت ستُثبّت نسبةً لم يقلها محاسب ثم تصير «مُختبَرة».
    /// </para>
    /// </summary>
    private static async Task<(string ContractId, string BoqItemId)> CreateContractAsync(
        ApiProcess api,
        Guid company,
        string projectId)
    {
        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post,
            Path(company, "project-contracts"),
            ApiFixture.TokenB,
            $$"""
              {
                "number": "{{Documents.Number("PC")}}",
                "projectId": "{{projectId}}",
                "customerPartyId": "CUST-SURFACE",
                "signedOn": "2026-01-10",
                "retentionRate": "0",
                "guaranteeMonths": 12,
                "items": [
                  {
                    "code": "B-1",
                    "descriptionAr": "حفر وردم",
                    "contractQuantity": { "magnitude": "100.000000", "unit": "m3" },
                    "unitRate": "1.0000"
                  }
                ]
              }
              """));

        (string text, JsonElement contract) = await Http.BodyAsync(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, text);

        string contractId = contract.GetProperty("id").GetString()!;

        using HttpResponseMessage items = await api.Call(Http.Request(
            HttpMethod.Get, Path(company, "project-contracts/" + contractId + "/boq-items"), ApiFixture.TokenB));

        (string itemsText, JsonElement list) = await Http.BodyAsync(items);
        Assert.True(items.StatusCode == HttpStatusCode.OK, itemsText);

        return (contractId, list.GetProperty("items").EnumerateArray().First().GetProperty("id").GetString()!);
    }

    /// <summary>
    /// يحفظ ملفّ قدرات يعرّف <c>projects.client_certificate</c> — شرطُ قبول المستخلص.
    /// <para>
    /// <b>وغياب الملفّ رفضٌ لا فتح</b> (ADR-0023): بلا هذا الحفظ يُرفض المستخلص بـ422
    /// <c>projects.capability_profile_missing</c> قبل أن يبلغ بوابة الإعدادات أصلاً —
    /// وهو الجواب الصحيح، ولذلك يُبذر الملفّ صراحةً بدل أن يُفتح الغياب.
    /// </para>
    /// <para>
    /// <b>ومعه سبب سحبٍ دائماً و200 وحدها مقبولة</b>، بالشكل نفسه المُودَع في
    /// <c>CashAndOrdersSurfaceTests.SaveProfileAsync</c>: الملفّ يُستبدل بالكامل، وقد
    /// يكون ملفّ المنشأة أوسع مما يريده هذا الاختبار فيُقرأ الاستبدال سحباً لقدرة
    /// ويُرفض بـ409. وقبولُ الـ409 هنا كان سيترك الاختبار يمضي بملفٍّ لا يعرف نوع
    /// المستند أصلاً فيسقط لسببٍ لا علاقة له بما يفحص — وهو «أخضر بترتيب التشغيل لا
    /// ببنائه» بعينه (<c>traps.md#fakh-green-by-ordering-not-by-construction</c>).
    /// </para>
    /// <para>
    /// <b>والقدرتان مُطفأتان</b>: المستخلص هنا يعرض <c>contract</c> و<c>workValue</c>
    /// وحدهما، وهما حقلان أساسيان في الكتالوج. وفتحُ <c>retention</c> أو <c>advance</c>
    /// كان سيرخّص حقلين لا يحملهما هذا المستند — ترخيصٌ بلا مُرخَّصٍ له.
    /// </para>
    /// </summary>
    private static async Task SaveCertificateProfileAsync(ApiProcess api, Guid company)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put,
            Documents.CapabilityProfile(company),
            ApiFixture.TokenB,
            """
            {"documents":[{"documentType":"projects.client_certificate",
              "capabilities":[{"capability":"advance","enabled":false},
                              {"capability":"retention","enabled":false}]}],
             "withdrawalReason":"بذر حالة اختبار سطح المقاولات — الملفّ يُستبدل بالكامل"}
            """));

        (string text, _) = await Http.BodyAsync(response);

        Assert.True(response.StatusCode == HttpStatusCode.OK, "حفظ ملفّ القدرات: " + text);
    }
}
