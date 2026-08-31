using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح النقد وأوامر الشراء عبر السلك</b> — سندات القبض وسندات الصرف وأوامر الشراء
/// واستلام البضاعة، من خارج العملية وبعقدٍ منشور وحده.
/// <para>
/// وما تُثبته هذه المجموعة ليس «الخدمة تعمل» — لها مجموعاتها في وحدتيها — بل ثلاثة
/// أشياء لا يُثبتها إلا الطلب الحقيقي: أنّ الأثر المحاسبي يقع فعلاً وبالرقم الصحيح
/// (<b>بميزان مراجعة قبل/بعد</b>)، وأنّ الحصانة تعبر الحدّ (‏201 ثم 200 ومعرّف القيد
/// نفسه)، وأنّ ما لا يوجد على السطح <b>لا يوجد</b> — لا مورد ترحيل لأمر شراء.
/// </para>
/// <para>
/// <b>ولماذا الشركة «ب»:</b> هذه المجموعة تُرحّل، وميزان الشركة «أ» مقسومٌ بين اختبارات
/// تحجز فتراتٍ بعينها وتؤكّد عدد صفوفها بالضبط. والشركة «ب» لا يُؤكَّد على ميزانها إلا
/// أنّه خالٍ في 2026-12، وهذه المجموعة لا تقترب منها. والاستثناء الوحيد اختبارُ استحقاق
/// المخزون: يستعمل الشركة «أ» <b>لأنه يُرفض قبل أن يكتب في الدفتر شيئاً</b> — الحركة
/// المخزنية تُطلب قبل القيد، ورفضُها يترك الدفتر بلا لمسة.
/// </para>
/// <para>
/// <b>ولا رمز حساب مكتوب بيد في هذا الملفّ.</b> حساب مراقبة العملاء وحساب مراقبة
/// الموردين يُعرفان من <b>دليل الحسابات المنشور</b> بنوع طرفهما في الدفتر المساعد
/// (<c>subledgerType</c>)، ثم يُجمع أثرهما من ميزان المراجعة. واختبارٌ يكتب رمز حساب
/// بيده يفحص تخمينه لا سلوك الخادم — وهو أيضاً ما تمنعه القاعدة 2 على الوحدات.
/// </para>
/// </summary>
public sealed class CashAndOrdersSurfaceTests
{
    /// <summary>ملفّ قدرات يفتح المطابقة الثلاثية — شرطُ ترحيل الاستلام.</summary>
    private const string ThreeWayMatchProfile = """
        {"documents":[{"documentType":"purchasing.supplier_bill",
          "capabilities":[{"capability":"three_way_match","enabled":true},
                          {"capability":"landed_cost","enabled":false}]}]}
        """;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · سند القبض يُسقط من ذمّة العميل — بميزان مراجعة قبل/بعد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task المقبوض_يُسقط_من_ذمّة_العميل_بالرقم_نفسه_في_ميزان_المراجعة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        IReadOnlySet<string> customerControl = await ControlAccountsAsync(api, company, "customer");

        decimal opening = await ControlNetAsync(api, company, customerControl);

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenB);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenB, customerId);

        using (HttpResponseMessage postedInvoice = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenB)))
        {
            (string text, _) = await Http.BodyAsync(postedInvoice);
            Assert.True(postedInvoice.StatusCode == HttpStatusCode.Created, "ترحيل الفاتورة: " + text);
        }

        decimal afterInvoice = await ControlNetAsync(api, company, customerControl);

        // الفاتورة: 10 × 100 وضريبة 15٪ ⇒ 1150 على ذمّة العميل بالضبط.
        Assert.Equal(1_150.0000m, afterInvoice - opening);

        // ── سند قبض بكامل المستحق ───────────────────────────────────────────
        string receiptId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB,
                   Documents.Receipt(Documents.Number("RCP"), customerId, invoiceId, "1150.0000"))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Console.WriteLine("مسوّدة القبض: " + text);

            Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("entryId").ValueKind);
            Assert.False(body.GetProperty("alreadyPosted").GetBoolean());

            // ‏net = المقبوض، tax = خصم التعجيل، gross = مجموعهما.
            Assert.Equal("1150.0000", body.GetProperty("net").GetString());
            Assert.Equal("0.0000", body.GetProperty("tax").GetString());
            Assert.Equal("1150.0000", body.GetProperty("gross").GetString());

            receiptId = body.GetProperty("id").GetString()!;
            Assert.Equal(
                Documents.CustomerReceipt(company, receiptId), drafted.Headers.Location?.OriginalString);
        }

        // المسوّدة **لا تمسّ** الذمّة: التخصيص يُنزَل مع القيد لا قبله.
        Assert.Equal(afterInvoice, await ControlNetAsync(api, company, customerControl));

        // ── القراءة ─────────────────────────────────────────────────────────
        using (HttpResponseMessage read = await api.Call(Http.Request(
                   HttpMethod.Get, Documents.CustomerReceipt(company, receiptId), ApiFixture.TokenB)))
        {
            (_, JsonElement body) = await Http.BodyAsync(read);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());
            Assert.False(body.GetProperty("alreadyPosted").GetBoolean());
        }

        // ── الترحيل ─────────────────────────────────────────────────────────
        using (HttpResponseMessage posted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceiptPosting(company, receiptId), ApiFixture.TokenB)))
        {
            (string text, JsonElement body) = await Http.BodyAsync(posted);
            Console.WriteLine("ترحيل القبض: " + text);

            Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
            Assert.Equal("POSTED", body.GetProperty("state").GetString());
            Assert.False(body.GetProperty("alreadyPosted").GetBoolean());
            Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("entryId").GetString()!));
        }

        decimal afterReceipt = await ControlNetAsync(api, company, customerControl);

        Console.WriteLine(FormattableString.Invariant(
            $"ذمّة العملاء: افتتاحي={opening:0.0000} بعد الفاتورة={afterInvoice:0.0000} بعد القبض={afterReceipt:0.0000}"));

        // **الحكم:** المقبوض أسقط من الذمّة 1150 بالضبط، والرصيد عاد إلى ما كان.
        Assert.Equal(-1_150.0000m, afterReceipt - afterInvoice);
        Assert.Equal(opening, afterReceipt);

        // والفاتورة صارت مسدَّدة: لا شيء متبقٍّ عليها في أعمار الذمم.
        using HttpResponseMessage aging = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(company, "2026-03-31"), ApiFixture.TokenB));

        (_, JsonElement report) = await Http.BodyAsync(aging);
        Assert.Equal(HttpStatusCode.OK, aging.StatusCode);

        JsonElement? party = report.GetProperty("parties").EnumerateArray()
            .Cast<JsonElement?>()
            .FirstOrDefault(p => p!.Value.GetProperty("partyId").GetString() == customerId);

        if (party is not null)
        {
            Assert.Equal("0.0000", party.Value.GetProperty("bands").GetProperty("total").GetString());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · ترحيل سند القبض مرّتين: 201 ثم 200، والقيد نفسه، وبلا تخصيص ثانٍ
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task ترحيل_سند_القبض_مرّتين_يُعيد_القيد_ذاته_ولا_يُنزل_التخصيص_مرّتين()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        IReadOnlySet<string> customerControl = await ControlAccountsAsync(api, company, "customer");

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenB);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenB, customerId);

        using (HttpResponseMessage postedInvoice = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenB)))
        {
            Assert.Equal(HttpStatusCode.Created, postedInvoice.StatusCode);
        }

        // خمسمئة من ألفٍ ومئة وخمسين: يبقى 650 متبقّياً، ولا يصير 150 بتخصيصٍ مضاعف.
        string receiptId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB,
                   Documents.Receipt(Documents.Number("RCP"), customerId, invoiceId, "500.0000"))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Assert.True(drafted.StatusCode == HttpStatusCode.Created, text);
            receiptId = body.GetProperty("id").GetString()!;
        }

        decimal before = await ControlNetAsync(api, company, customerControl);

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.CustomerReceiptPosting(company, receiptId), ApiFixture.TokenB));
        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.CustomerReceiptPosting(company, receiptId), ApiFixture.TokenB));
        (string secondText, JsonElement secondBody) = await Http.BodyAsync(second);

        Console.WriteLine("الأول : " + firstText);
        Console.WriteLine("الثاني: " + secondText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());

        // ‏200 لا 201، و«رُحّل سلفاً» في الجسم أيضاً: رمز الحالة وحده يضيع خلف أي وسيط.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());

        // والقيد **هو هو**: لا قيد ثانٍ ولا معرّف ثانٍ.
        Assert.Equal(firstBody.GetProperty("entryId").GetString(), secondBody.GetProperty("entryId").GetString());

        // ولا أثر ثانٍ على الدفتر: الذمّة نقصت 500 مرّةً واحدة لا مرّتين.
        decimal after = await ControlNetAsync(api, company, customerControl);
        Console.WriteLine(FormattableString.Invariant($"حركة الذمّة بعد ترحيلين: {after - before:0.0000}"));
        Assert.Equal(-500.0000m, after - before);

        // **والأخطر: التخصيص لم يُنزَل مرّتين.** البوّابة تحرس القيد، وأثرُ التخصيص على
        // الفاتورة أثرٌ جانبي بعدها. والمتبقّي 1150 − 500 = 650؛ ولو أُنزل التخصيص
        // مرّتين لصار 150 — بلا قيد ثانٍ يدلّ عليه.
        using HttpResponseMessage aging = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(company, "2026-03-31"), ApiFixture.TokenB));

        (_, JsonElement report) = await Http.BodyAsync(aging);
        JsonElement party = Assert.Single(
            report.GetProperty("parties").EnumerateArray(),
            p => p.GetProperty("partyId").GetString() == customerId);

        Console.WriteLine("المتبقّي: " + party.GetProperty("bands").GetProperty("total").GetString());
        Assert.Equal("650.0000", party.GetProperty("bands").GetProperty("total").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · مقبوضٌ يتجاوز المستحقّ: رفضٌ صريح، لا دفعةٌ مقدَّمة صامتة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task مقبوضٌ_يتجاوز_الرصيد_المستحقّ_يُرفض_ولا_يصير_دفعةً_مقدَّمة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenB);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenB, customerId);

        using (HttpResponseMessage postedInvoice = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenB)))
        {
            Assert.Equal(HttpStatusCode.Created, postedInvoice.StatusCode);
        }

        // على الفاتورة 1150، ويُخصَّص عليها 2000.
        using HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB,
            Documents.Receipt(Documents.Number("RCP"), customerId, invoiceId, "2000.0000")));

        (string text, JsonElement problem) = await Http.BodyAsync(refused);
        Console.WriteLine(text);

        // **القرار: رفضٌ لا تحويل.** الدفعة المقدّمة مستندٌ آخر وحدثٌ آخر في المصفوفة
        // يُنشئ التزاماً على المنشأة بدل أن يُسقط ذمّةً لها.
        Assert.Equal(HttpStatusCode.UnprocessableContent, refused.StatusCode);
        Assert.Equal("sales.over_allocation", Http.CodeOf(problem));

        // والرسالة تسمّي الرقمين لا تقول «مبلغ غير صالح».
        Assert.Contains("2000.0000", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("1150.0000", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("2000.0000", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);

        // ولا مسوّدة كُتبت: الفحص كاملاً قبل أي كتابة.
        using HttpResponseMessage aging = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(company, "2026-03-31"), ApiFixture.TokenB));

        (_, JsonElement report) = await Http.BodyAsync(aging);
        JsonElement party = Assert.Single(
            report.GetProperty("parties").EnumerateArray(),
            p => p.GetProperty("partyId").GetString() == customerId);

        Assert.Equal("1150.0000", party.GetProperty("bands").GetProperty("total").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · سند الصرف يُسقط من ذمّة المورد — بميزان مراجعة قبل/بعد، وحصانته
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task المدفوع_يُسقط_من_ذمّة_المورد_والترحيل_الثاني_يُعيد_القيد_ذاته()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        IReadOnlySet<string> supplierControl = await ControlAccountsAsync(api, company, "supplier");

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenB);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenB);

        decimal opening = await ControlNetAsync(api, company, supplierControl);

        string billId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SupplierBills(company), ApiFixture.TokenB,
                   Documents.ExpenseBill(Documents.Number("EXP"), supplierId, costCenter))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Assert.True(drafted.StatusCode == HttpStatusCode.Created, text);
            billId = body.GetProperty("id").GetString()!;
        }

        using (HttpResponseMessage postedBill = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SupplierBillPosting(company, billId), ApiFixture.TokenB)))
        {
            (string text, _) = await Http.BodyAsync(postedBill);
            Assert.True(postedBill.StatusCode == HttpStatusCode.Created, "ترحيل فاتورة المورد: " + text);
        }

        decimal afterBill = await ControlNetAsync(api, company, supplierControl);

        // الفاتورة المصروفية: 5 × 100 وضريبة 15٪ ⇒ 575 التزاماً. وحساب المراقبة دائن
        // بطبعه، فأثرُه بمنطق «مدين ناقص دائن» سالبٌ بالمقدار نفسه.
        Console.WriteLine(FormattableString.Invariant($"ذمّة الموردين بعد الفاتورة: {afterBill - opening:0.0000}"));
        Assert.Equal(-575.0000m, afterBill - opening);

        string paymentId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SupplierPayments(company), ApiFixture.TokenB,
                   Documents.Payment(Documents.Number("PAY"), supplierId, billId, "575.0000"))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Console.WriteLine("مسوّدة الصرف: " + text);

            Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());
            Assert.Equal("575.0000", body.GetProperty("net").GetString());
            Assert.Equal("0.0000", body.GetProperty("tax").GetString());
            paymentId = body.GetProperty("id").GetString()!;
        }

        using (HttpResponseMessage read = await api.Call(Http.Request(
                   HttpMethod.Get, Documents.SupplierPayment(company, paymentId), ApiFixture.TokenB)))
        {
            (_, JsonElement body) = await Http.BodyAsync(read);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());
        }

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierPaymentPosting(company, paymentId), ApiFixture.TokenB));
        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);
        Console.WriteLine("ترحيل الصرف: " + firstText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("POSTED", firstBody.GetProperty("state").GetString());
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierPaymentPosting(company, paymentId), ApiFixture.TokenB));
        (_, JsonElement secondBody) = await Http.BodyAsync(second);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(firstBody.GetProperty("entryId").GetString(), secondBody.GetProperty("entryId").GetString());

        decimal afterPayment = await ControlNetAsync(api, company, supplierControl);

        Console.WriteLine(FormattableString.Invariant(
            $"ذمّة الموردين: افتتاحي={opening:0.0000} بعد الفاتورة={afterBill:0.0000} بعد الصرف={afterPayment:0.0000}"));

        // **الحكم:** المدفوع أسقط من ذمّة المورد 575 بالضبط — مرّةً واحدة رغم ترحيلين.
        Assert.Equal(575.0000m, afterPayment - afterBill);
        Assert.Equal(opening, afterPayment);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · أمر الشراء ليس حدثاً محاسبياً: لا قيد، ولا مورد ترحيل يوجد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task أمر_الشراء_لا_يُرحَّل_ولا_مورد_ترحيل_له_ولا_يمسّ_الدفتر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        IReadOnlySet<string> supplierControl = await ControlAccountsAsync(api, company, "supplier");
        decimal before = await ControlNetAsync(api, company, supplierControl);

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenB);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenB);

        string orderId;
        string orderLineId;

        using (HttpResponseMessage created = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.PurchaseOrders(company), ApiFixture.TokenB,
                   Documents.PurchaseOrder(Documents.Number("PO"), supplierId, costCenter))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(created);
            Console.WriteLine("أمر الشراء: " + text);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("1000.0000", body.GetProperty("net").GetString());
            Assert.Equal("150.0000", body.GetProperty("tax").GetString());
            Assert.Equal("1150.0000", body.GetProperty("gross").GetString());

            // **ولا حقل ترحيل عليه أصلاً**: لا entryId ولا alreadyPosted في المخطّط.
            Assert.False(body.TryGetProperty("entryId", out _), "أمر شراء يحمل معرّف قيد");
            Assert.False(body.TryGetProperty("alreadyPosted", out _), "أمر شراء يحمل «رُحّل سلفاً»");
            Assert.NotEqual("POSTED", body.GetProperty("state").GetString());

            orderId = body.GetProperty("id").GetString()!;

            JsonElement line = Assert.Single(body.GetProperty("lines").EnumerateArray());
            orderLineId = line.GetProperty("id").GetString()!;
            Assert.Equal("ITEM-A", line.GetProperty("itemId").GetString());
            Assert.Equal("10.0000", line.GetProperty("quantity").GetString());
            Assert.Equal(1, line.GetProperty("lineNo").GetInt32());

            Assert.Equal(Documents.PurchaseOrder(company, orderId), created.Headers.Location?.OriginalString);
        }

        using (HttpResponseMessage read = await api.Call(Http.Request(
                   HttpMethod.Get, Documents.PurchaseOrder(company, orderId), ApiFixture.TokenB)))
        {
            (_, JsonElement body) = await Http.BodyAsync(read);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(orderLineId, Assert.Single(body.GetProperty("lines").EnumerateArray())
                .GetProperty("id").GetString());
        }

        // **الحكم الأول: الدفتر لم يُمسّ.** أمر شراء بألف ومئة وخمسين، وصفرُ حركة.
        decimal after = await ControlNetAsync(api, company, supplierControl);
        Console.WriteLine(FormattableString.Invariant($"حركة ذمّة الموردين عن أمر الشراء: {after - before:0.0000}"));
        Assert.Equal(0m, after - before);

        // **الحكم الثاني: المورد الفرعي لا وجود له** — ولا يُخترع. و404 لا 405: المسار
        // نفسه غير مسجَّل، لا فعلٌ ممنوع على مورد قائم.
        using HttpResponseMessage posting = await api.Call(Http.Request(
            HttpMethod.Post,
            Documents.PurchaseOrder(company, orderId) + "/posting",
            ApiFixture.TokenB,
            "{}"));

        Console.WriteLine(FormattableString.Invariant($"POST …/purchase-orders/{{id}}/posting ⇒ {(int)posting.StatusCode}"));
        Assert.Equal(HttpStatusCode.NotFound, posting.StatusCode);

        // **والحكم الثالث: العقد المنشور يقول ذلك أيضاً** — لا مسار ترحيل لأمر شراء فيه.
        string contract = await Http.ReadTextAsync(
            Path.Combine(RepositoryPaths.Root, "contracts/openapi/v1.json"));

        using JsonDocument document = JsonDocument.Parse(contract);
        List<string> orderPaths =
        [
            .. document.RootElement.GetProperty("paths").EnumerateObject()
                .Select(static p => p.Name)
                .Where(static name => name.Contains("/purchase-orders", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            [
                "/api/v1/companies/{companyId}/purchase-orders",
                "/api/v1/companies/{companyId}/purchase-orders/{orderId}",
            ],
            orderPaths);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · استلام البضاعة: يمسّ المخزون، ويُنشئ التزاماً، وحصانته على السلك
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task استلام_البضاعة_يُنشئ_التزام_بضاعة_لم_تُفوتر_وترحيله_الثاني_بلا_أثر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        await SaveProfileAsync(api, company, ApiFixture.TokenB);

        IReadOnlySet<string> supplierControl = await ControlAccountsAsync(api, company, "supplier");
        IReadOnlySet<string> itemControl = await ControlAccountsAsync(api, company, "item");

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenB);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenB);

        (string orderId, string orderLineId) = await OrderAsync(api, company, ApiFixture.TokenB, supplierId, costCenter);

        decimal supplierBefore = await ControlNetAsync(api, company, supplierControl);
        decimal itemBefore = await ControlNetAsync(api, company, itemControl);

        string receiptId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.GoodsReceipts(company), ApiFixture.TokenB,
                   Documents.GoodsReceipt(Documents.Number("GRN"), orderId, orderLineId, "4"))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Console.WriteLine("مسوّدة الاستلام: " + text);

            Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());

            // أربع وحدات بسعر الأمر مئة ⇒ 400، ولا ضريبة على الاستلام.
            Assert.Equal("400.0000", body.GetProperty("net").GetString());
            Assert.Equal("0.0000", body.GetProperty("tax").GetString());
            Assert.Equal("400.0000", body.GetProperty("gross").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("entryId").ValueKind);

            receiptId = body.GetProperty("id").GetString()!;
        }

        using (HttpResponseMessage read = await api.Call(Http.Request(
                   HttpMethod.Get, Documents.GoodsReceipt(company, receiptId), ApiFixture.TokenB)))
        {
            (_, JsonElement body) = await Http.BodyAsync(read);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("DRAFT", body.GetProperty("state").GetString());
        }

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.GoodsReceiptPosting(company, receiptId), ApiFixture.TokenB));
        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);
        Console.WriteLine("ترحيل الاستلام: " + firstText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("POSTED", firstBody.GetProperty("state").GetString());
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());
        Assert.NotEqual(Guid.Empty, Guid.Parse(firstBody.GetProperty("entryId").GetString()!));

        decimal supplierAfter = await ControlNetAsync(api, company, supplierControl);
        decimal itemAfter = await ControlNetAsync(api, company, itemControl);

        Console.WriteLine(FormattableString.Invariant(
            $"الاستلام: التزام المورد={supplierAfter - supplierBefore:0.0000} ومراقبة المخزون={itemAfter - itemBefore:0.0000}"));

        // **الحكم:** الاستلام أنشأ التزاماً على المورد بأربعمئة (دائن ⇒ سالب بمنطق
        // «مدين ناقص دائن») ودان حساب مراقبة المخزون بالمبلغ نفسه — وهو الطرف الذي
        // يقابله رصيدُ صنفٍ في الدفتر المساعد.
        Assert.Equal(-400.0000m, supplierAfter - supplierBefore);
        Assert.Equal(400.0000m, itemAfter - itemBefore);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.GoodsReceiptPosting(company, receiptId), ApiFixture.TokenB));
        (_, JsonElement secondBody) = await Http.BodyAsync(second);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(firstBody.GetProperty("entryId").GetString(), secondBody.GetProperty("entryId").GetString());

        // ولا كمية ثانية صُرفت ولا قيد ثانٍ كُتب.
        Assert.Equal(supplierAfter, await ControlNetAsync(api, company, supplierControl));
        Assert.Equal(itemAfter, await ControlNetAsync(api, company, itemControl));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 · الاستلام يمسّ المخزون — فمنشأةٌ لم تشترِ المخزون تُرفض بصراحة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task ترحيل_الاستلام_عند_منشأة_بلا_استحقاق_مخزون_يُرفض_ولا_يمسّ_الدفتر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // الشركة «أ»: المخزون وحدة اختيارية لم تُشترَ. والرفض يقع **قبل** أي كتابة في
        // الدفتر — الحركة المخزنية تُطلب أولاً — فلا يُمسّ ميزان هذه الشركة.
        Guid company = ApiTestDatabase.CompanyA;

        await SaveProfileAsync(api, company, ApiFixture.TokenA);

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenA);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenA);

        (string orderId, string orderLineId) = await OrderAsync(api, company, ApiFixture.TokenA, supplierId, costCenter);

        string receiptId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.GoodsReceipts(company), ApiFixture.TokenA,
                   Documents.GoodsReceipt(Documents.Number("GRN"), orderId, orderLineId, "2"))))
        {
            (string text, JsonElement drafts) = await Http.BodyAsync(drafted);
            Assert.True(drafted.StatusCode == HttpStatusCode.Created, text);
            receiptId = drafts.GetProperty("id").GetString()!;
        }

        using HttpResponseMessage posting = await api.Call(Http.Request(
            HttpMethod.Post, Documents.GoodsReceiptPosting(company, receiptId), ApiFixture.TokenA));

        (string postingText, JsonElement problem) = await Http.BodyAsync(posting);
        Console.WriteLine(postingText);

        // **الحكم:** الرفض صريح ومصنَّف — لا 500، ولا نجاحٌ يترك الحساب الضابط متحرّكاً
        // بلا رصيد صنف يقابله. والاستلام يبقى مسوّدة.
        Assert.Equal(HttpStatusCode.Forbidden, posting.StatusCode);
        Assert.Equal("entitlement.not_entitled", Http.CodeOf(problem));

        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Documents.GoodsReceipt(company, receiptId), ApiFixture.TokenA));

        (_, JsonElement body) = await Http.BodyAsync(read);
        Assert.Equal("DRAFT", body.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("entryId").ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8 · المال والكمّية نصوصٌ على الطرفين، ولا فعل تعديل ولا حذف
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task المبلغ_والكمّية_رمزاً_رقمياً_يُرفضان_على_أبواب_النقد_وأوامر_الشراء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenB);
        string stranger = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        // ‏(أ) المقبوض رمزاً رقمياً: القناة نفسها تُرفض قبل أن يصل الرقم إلى الوحدة.
        string receipt = Documents.Receipt(Documents.Number("RCP"), customerId, stranger, "100.0000")
            .Replace("\"received\":\"100.0000\"", "\"received\":100.0000", StringComparison.Ordinal);

        using (HttpResponseMessage response = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB, receipt)))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine(text);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("wire.money.number_token", Http.CodeOf(problem));
        }

        // ‏(ب) ومبلغ التخصيص كذلك — الحقل المتداخل لا يُفلت.
        string allocation = Documents.Receipt(Documents.Number("RCP"), customerId, stranger, "100.0000")
            .Replace("\"amount\":\"100.0000\"", "\"amount\":100.0000", StringComparison.Ordinal);

        using (HttpResponseMessage response = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB, allocation)))
        {
            (_, JsonElement problem) = await Http.BodyAsync(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("wire.money.number_token", Http.CodeOf(problem));
        }

        // ‏(ج) وكمّية الاستلام: ليست مبلغاً لكنها تُضرب في مبلغ.
        string goods = Documents.GoodsReceipt(Documents.Number("GRN"), stranger, stranger, "4")
            .Replace("\"quantity\":\"4\"", "\"quantity\":4", StringComparison.Ordinal);

        using (HttpResponseMessage response = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.GoodsReceipts(company), ApiFixture.TokenB, goods)))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine(text);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("wire.money.number_token", Http.CodeOf(problem));
        }

        // ‏(د) وخانة عشرية خامسة تُرفض ولا تُقرَّب.
        string scale = Documents.Payment(Documents.Number("PAY"), stranger, stranger, "100.00001");

        using (HttpResponseMessage response = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SupplierPayments(company), ApiFixture.TokenB, scale)))
        {
            (_, JsonElement problem) = await Http.BodyAsync(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("wire.number.scale_exceeded", Http.CodeOf(problem));
        }
    }

    [Fact]
    public async Task لا_تعديل_ولا_حذف_على_سند_ولا_على_أمر_شراء_ولا_على_استلام()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenB);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenB);
        (string orderId, _) = await OrderAsync(api, company, ApiFixture.TokenB, supplierId, costCenter);

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenB);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenB, customerId);

        using (HttpResponseMessage postedInvoice = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenB)))
        {
            Assert.Equal(HttpStatusCode.Created, postedInvoice.StatusCode);
        }

        string receiptId;
        using (HttpResponseMessage drafted = await api.Call(Http.Request(
                   HttpMethod.Post, Documents.CustomerReceipts(company), ApiFixture.TokenB,
                   Documents.Receipt(Documents.Number("RCP"), customerId, invoiceId, "100.0000"))))
        {
            (string text, JsonElement body) = await Http.BodyAsync(drafted);
            Assert.True(drafted.StatusCode == HttpStatusCode.Created, text);
            receiptId = body.GetProperty("id").GetString()!;
        }

        // والمستندات **مسوّدات** هنا لا وقائع مُرحَّلة: أي أن الغياب ليس «لأنها مُرحَّلة»
        // بل لأن السطح لا يحمل هذه الأفعال أصلاً.
        foreach (string path in new[]
                 {
                     Documents.CustomerReceipt(company, receiptId),
                     Documents.PurchaseOrder(company, orderId),
                 })
        {
            foreach (HttpMethod method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
            {
                using HttpResponseMessage response = await api.Call(
                    Http.Request(method, path, ApiFixture.TokenB, "{}"));

                Console.WriteLine(FormattableString.Invariant($"{method} {path} ⇒ {(int)response.StatusCode}"));
                Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            }
        }
    }

    // ── أدوات ────────────────────────────────────────────────────────────────

    /// <summary>
    /// رموز حسابات المراقبة التي تطلب نوع طرفٍ بعينه في دفترها المساعد — <b>مقروءةً
    /// من دليل الحسابات المنشور لا مكتوبةً بيد</b>.
    /// </summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="subledgerType">نوع الطرف: <c>customer</c> · <c>supplier</c> · <c>item</c>.</param>
    private static async Task<IReadOnlySet<string>> ControlAccountsAsync(
        ApiProcess api, Guid company, string subledgerType)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(company), Credential(company)));

        (string text, JsonElement chart) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "دليل الحسابات: " + text);

        HashSet<string> codes =
        [
            .. chart.GetProperty("accounts").EnumerateArray()
                .Where(account => string.Equals(
                    account.GetProperty("subledgerType").GetString(), subledgerType, StringComparison.Ordinal))
                .Select(static account => account.GetProperty("accountCode").GetString()!),
        ];

        // اللافراغ: دليلٌ بلا حساب مراقبة واحد يجعل «الفرق صفر» جملةً عن لا شيء.
        Assert.True(
            codes.Count > 0,
            "لا حساب مراقبة بنوع طرف «" + subledgerType + "» في دليل هذه الشركة — القياس ضامر.");

        return codes;
    }

    /// <summary>
    /// صافي أثر مجموعة حسابات في ميزان المراجعة بمنطق «مدين ناقص دائن» — <b>والجمع
    /// هنا بـ<c>decimal</c></b>، وهو ما لا يجوز أن يقع في السطح ولا في المتصفّح.
    /// </summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="accountCodes">رموز الحسابات.</param>
    private static async Task<decimal> ControlNetAsync(
        ApiProcess api, Guid company, IReadOnlySet<string> accountCodes)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(company, ApiTestDatabase.Book), Credential(company)));

        (string text, JsonElement trial) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "ميزان المراجعة: " + text);

        decimal net = 0m;

        foreach (JsonElement row in trial.GetProperty("rows").EnumerateArray())
        {
            if (!accountCodes.Contains(row.GetProperty("accountCode").GetString()!))
            {
                continue;
            }

            net += decimal.Parse(row.GetProperty("debit").GetString()!, CultureInfo.InvariantCulture)
                 - decimal.Parse(row.GetProperty("credit").GetString()!, CultureInfo.InvariantCulture);
        }

        return net;
    }

    /// <summary>
    /// يحفظ ملفّ قدرات يفتح المطابقة الثلاثية — شرطُ ترحيل الاستلام.
    /// <para>
    /// <b>ومعه سبب سحب دائماً</b>، وهو الشرط الذي يجعل البذر غير معتمدٍ على ترتيب
    /// التشغيل: الخادم مشترك بين اختبارات المجموعة، وقد يكون ملفّ المنشأة أوسع مما
    /// يريده هذا الاختبار — فيُقرأ الاستبدال <b>سحباً لقدرة</b> ويُرفض بـ409. وقبولُ
    /// الـ409 هنا كان سيترك الاختبار يمضي بملفٍّ لا يحمل <c>purchasing.supplier_bill</c>
    /// أصلاً، فيسقط برمزٍ آخر لسبب لا علاقة له بما يفحص — وهو «أخضر بترتيب التشغيل
    /// لا ببنائه» بعينه (<c>traps.md#fakh-green-by-ordering-not-by-construction</c>).
    /// ولذلك: سببٌ مكتوب دائماً، و<b>200 وحدها مقبولة</b>.
    /// </para>
    /// </summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    private static async Task SaveProfileAsync(ApiProcess api, Guid company, TestCredential credential)
    {
        string payload = ThreeWayMatchProfile[..ThreeWayMatchProfile.LastIndexOf('}')]
            + ",\"withdrawalReason\":\"بذر حالة اختبار المطابقة الثلاثية — الملفّ يُستبدل بالكامل\"}";

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, Documents.CapabilityProfile(company), credential, payload));

        (string text, _) = await Http.BodyAsync(response);

        Assert.True(response.StatusCode == HttpStatusCode.OK, "حفظ ملفّ القدرات: " + text);
    }

    /// <summary>يُنشئ أمر شراء ويُعيد معرّفه ومعرّف سطره الأول.</summary>
    private static async Task<(string OrderId, string OrderLineId)> OrderAsync(
        ApiProcess api, Guid company, TestCredential credential, string supplierId, string costCenter)
    {
        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post, Documents.PurchaseOrders(company), credential,
            Documents.PurchaseOrder(Documents.Number("PO"), supplierId, costCenter)));

        (string text, JsonElement body) = await Http.BodyAsync(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, "أمر الشراء: " + text);

        return (
            body.GetProperty("id").GetString()!,
            Assert.Single(body.GetProperty("lines").EnumerateArray()).GetProperty("id").GetString()!);
    }

    private static TestCredential Credential(Guid company) =>
        company == ApiTestDatabase.CompanyA ? ApiFixture.TokenA : ApiFixture.TokenB;
}
