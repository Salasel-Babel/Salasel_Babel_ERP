using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح المخزون عبر السلك</b> — الصنف، وحركته، ورصيده، وتقييمه، ومرتجع مشترياته.
/// <para>
/// وما تُثبته هذه المجموعة ليس أن وحدة المخزون تحسب صحيحاً — لذلك مجموعتها الخاصّة
/// في <c>Babel.Inventory.Tests</c> — بل أن <b>ما تحسبه يمكن طلبه من الشبكة</b>:
/// باعتماد وعنوان وعقد منشور، بلا مرجع مشروع وبلا معرفة بأي نوع داخلي.
/// </para>
/// <para>
/// <b>والكمّية تعبر بوحدتها دائماً</b>: كائنٌ فيه مقدارٌ نصّاً ورمز وحدة، لا عددٌ عارٍ.
/// وكرتونان من صنفٍ كرتونه اثنتا عشرة حبّة يصلان أربعاً وعشرين حبّة في الرصيد —
/// وهو التحويل الذي لو غاب لصار الرصيد رقماً صحيحاً بمقياسٍ خاطئ.
/// </para>
/// <para>
/// <b>ولماذا الشركة «ب» لا «أ»:</b> هذه المجموعة <b>تُرحّل</b>، وميزان الشركة «أ»
/// مقسومٌ بين اختبارات تحجز فتراتٍ بعينها وتؤكّد عدد صفوفها بالضبط. والشركة «ب» لا
/// يُؤكَّد على ميزانها إلا أنّه خالٍ في 2026-12، وهذه المجموعة لا تقترب منها.
/// </para>
/// </summary>
public sealed class InventorySurfaceTests
{
    /// <summary>تاريخ تقييمٍ بعد كل حركات هذه المجموعة — تُقرأ عنده الأرصدة مكتملة.</summary>
    private const string AsOf = "2026-03-31";

    [Fact]
    public async Task الصنف_يُسجَّل_من_الشبكة_ثم_يُقرأ_بمعرّفه_ويظهر_في_قائمته()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;
        string code = Documents.Number("ITEM");

        // ── ١ · التسجيل: 201 وعنوانٌ يوجّه إلى مورد القراءة ─────────────────
        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post, Documents.Items(company), ApiFixture.TokenB, Documents.Item(code)));

        (string createdText, JsonElement item) = await Http.BodyAsync(created);
        Console.WriteLine("الصنف: " + createdText);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(code, item.GetProperty("code").GetString());
        Assert.Equal("EACH", item.GetProperty("baseUnit").GetString());

        string itemId = item.GetProperty("id").GetString()!;
        Assert.Equal(Documents.Item(company, itemId), created.Headers.Location?.OriginalString);

        // والمعامل **نسبيّ** — بسطٌ ومقام صحيحان، لا عائم يقرّب الكرتون إلى 11.999.
        JsonElement unit = Assert.Single(item.GetProperty("units").EnumerateArray().ToList());
        Assert.Equal("CTN", unit.GetProperty("unitCode").GetString());
        Assert.Equal(12L, unit.GetProperty("numerator").GetInt64());
        Assert.Equal(1L, unit.GetProperty("denominator").GetInt64());

        // ── ٢ · القراءة بالمعرّف تُعيد الصنف نفسه ───────────────────────────
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Documents.Item(company, itemId), ApiFixture.TokenB));

        (string readText, JsonElement readBody) = await Http.BodyAsync(read);
        Assert.True(read.StatusCode == HttpStatusCode.OK, readText);
        Assert.Equal(code, readBody.GetProperty("code").GetString());

        // ── ٣ · القائمة تحويه، وهي **غلافٌ بعدّاد** لا مصفوفة عارية ─────────
        using HttpResponseMessage listed = await api.Call(Http.Request(
            HttpMethod.Get, Documents.Items(company), ApiFixture.TokenB));

        (string listText, JsonElement list) = await Http.BodyAsync(listed);
        Assert.True(listed.StatusCode == HttpStatusCode.OK, listText);

        List<JsonElement> items = [.. list.GetProperty("items").EnumerateArray()];
        Assert.Equal(items.Count, list.GetProperty("itemCount").GetInt32());
        Assert.Contains(items, entry => string.Equals(entry.GetProperty("code").GetString(), code, StringComparison.Ordinal));

        // ورمزٌ مكرّر يُرفض برمز ثابت، لا بانفجار فريدٍ من القاعدة.
        using HttpResponseMessage duplicate = await api.Call(Http.Request(
            HttpMethod.Post, Documents.Items(company), ApiFixture.TokenB, Documents.Item(code)));

        (_, JsonElement problem) = await Http.BodyAsync(duplicate);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("inventory.duplicate_item_code", Http.CodeOf(problem));
    }

    [Fact]
    public async Task ترحيل_حركة_المخزون_مرّتين_يُعيد_القيد_ذاته_ويُعلن_أنه_مُرحَّل_من_قبل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        (string code, _) = await Documents.AddItemAsync(api, company, ApiFixture.TokenB);

        // ── ١ · مسوّدة: كرتونان بـ240 — ولا قيد ولا رصيد بعد ────────────────
        using HttpResponseMessage drafted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.StockMovements(company), ApiFixture.TokenB,
            Documents.StockMovementIn(Documents.Number("STK"), code)));

        (string draftText, JsonElement draft) = await Http.BodyAsync(drafted);
        Console.WriteLine("مسوّدة الحركة: " + draftText);

        Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
        Assert.Equal("DRAFT", draft.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("entryId").ValueKind);
        Assert.False(draft.GetProperty("alreadyPosted").GetBoolean());

        // والكمّية تعبر **بوحدتها كما سُلّمت**: كرتونان، لا أربعٌ وعشرون حبّة.
        Assert.Equal("2.000000", draft.GetProperty("quantity").GetProperty("magnitude").GetString());
        Assert.Equal("CTN", draft.GetProperty("quantity").GetProperty("unit").GetString());
        Assert.Equal("DEFAULT", draft.GetProperty("locationId").GetString());

        string movementId = draft.GetProperty("id").GetString()!;

        // ── ٢ · الترحيل الأول: 201 وقيدٌ في الدفتر ──────────────────────────
        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.StockMovementPosting(company, movementId), ApiFixture.TokenB));

        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);
        Console.WriteLine("الترحيل الأول: " + firstText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("POSTED", firstBody.GetProperty("state").GetString());
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());

        string entryId = firstBody.GetProperty("entryId").GetString()!;
        Assert.NotEqual(Guid.Empty, Guid.Parse(entryId));

        // ── ٣ · الترحيل الثاني: 200 والقيدُ **نفسه** ────────────────────────
        // والحكم من بوّابة الترحيل لا من مقارنة حالة: «كانت الحالة POSTED» جوابٌ
        // يُصدّق سطراً كُتب ثم فُقد قيده، والبوّابة تقرأ الهوية في سجلّ المحاولات.
        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.StockMovementPosting(company, movementId), ApiFixture.TokenB));

        (string secondText, JsonElement secondBody) = await Http.BodyAsync(second);
        Console.WriteLine("الترحيل الثاني: " + secondText);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(entryId, secondBody.GetProperty("entryId").GetString());

        // ── ٤ · الرصيد: 2 كرتون × 12 = 24 حبّة بـ240 ⇒ متوسط 10 للحبّة ──────
        JsonElement balance = await Documents.BalanceOfAsync(api, company, ApiFixture.TokenB, code);
        Assert.Equal("24.000000", balance.GetProperty("quantity").GetProperty("magnitude").GetString());
        Assert.Equal("EACH", balance.GetProperty("quantity").GetProperty("unit").GetString());
        Assert.Equal("240.0000", balance.GetProperty("value").GetString());
        Assert.Equal("10.000000", balance.GetProperty("unitCost").GetString());
        Assert.True(balance.GetProperty("hasCostBasis").GetBoolean());
        Assert.Equal("DEFAULT", balance.GetProperty("locationId").GetString());

        // ── ٥ · والحركة تظهر في قائمتها مُرحَّلةً ───────────────────────────
        using HttpResponseMessage listed = await api.Call(Http.Request(
            HttpMethod.Get, Documents.StockMovements(company), ApiFixture.TokenB));

        (string listText, JsonElement list) = await Http.BodyAsync(listed);
        Assert.True(listed.StatusCode == HttpStatusCode.OK, listText);

        List<JsonElement> movements = [.. list.GetProperty("movements").EnumerateArray()];
        Assert.Equal(movements.Count, list.GetProperty("movementCount").GetInt32());

        JsonElement mine = Assert.Single(
            movements.Where(m => string.Equals(m.GetProperty("id").GetString(), movementId, StringComparison.Ordinal)).ToList());
        Assert.Equal("POSTED", mine.GetProperty("state").GetString());
        Assert.Equal(entryId, mine.GetProperty("entryId").GetString());
    }

    [Fact]
    public async Task تقييم_المخزون_يبلغ_الرقم_نفسه_من_ثلاثة_طرق_مستقلّة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        (string code, _) = await Documents.AddItemAsync(api, company, ApiFixture.TokenB);

        using HttpResponseMessage drafted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.StockMovements(company), ApiFixture.TokenB,
            Documents.StockMovementIn(Documents.Number("STK"), code)));

        (string draftText, JsonElement draft) = await Http.BodyAsync(drafted);
        Assert.True(drafted.StatusCode == HttpStatusCode.Created, draftText);

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Documents.StockMovementPosting(company, draft.GetProperty("id").GetString()!),
            ApiFixture.TokenB));

        (string postedText, _) = await Http.BodyAsync(posted);
        Assert.True(posted.StatusCode == HttpStatusCode.Created, postedText);

        using HttpResponseMessage valued = await api.Call(Http.Request(
            HttpMethod.Get, Documents.InventoryValuation(company, AsOf), ApiFixture.TokenB));

        (string valuedText, JsonElement valuation) = await Http.BodyAsync(valued);
        Console.WriteLine("التقييم: " + valuedText);

        Assert.True(valued.StatusCode == HttpStatusCode.OK, valuedText);
        Assert.Equal(AsOf, valuation.GetProperty("asOf").GetString());

        // ثلاثة طرق مستقلّة إلى الرقم نفسه: مجموع حركات الدفتر المساعد، ورصيد
        // الحساب الضابط، ومجموع أرصدة الأصناف. وتساويها هو المطابقة نفسها —
        // ولذلك تُقرأ الثلاثة، لا `isReconciled` وحده الذي قد يقارن رقماً بنفسه.
        string subledger = valuation.GetProperty("subledgerTotal").GetString()!;
        string control = valuation.GetProperty("controlTotal").GetString()!;
        string balances = valuation.GetProperty("balanceTotal").GetString()!;

        Assert.Equal(subledger, control);
        Assert.Equal(subledger, balances);
        Assert.Equal("0.0000", valuation.GetProperty("divergence").GetString());
        Assert.True(valuation.GetProperty("isReconciled").GetBoolean());
        Assert.Empty(valuation.GetProperty("divergences").EnumerateArray().ToList());

        // ولا رقم على السلك عائم: كلها نصوص.
        Assert.Equal(JsonValueKind.String, valuation.GetProperty("subledgerTotal").ValueKind);
        Assert.Equal(JsonValueKind.String, valuation.GetProperty("controlTotal").ValueKind);
        Assert.Equal(JsonValueKind.String, valuation.GetProperty("balanceTotal").ValueKind);
    }

    /// <summary>
    /// <b>الواقعة ذات الوجهين تُفحص على وجهيها.</b>
    /// <para>
    /// مرتجع المشتريات يفعل شيئين معاً: يُخرج البضاعة من المخزن، ويُنقص ذمّة المورد.
    /// واختبارٌ يقيس الذمّة وحدها يبقى أخضر وإن لم تُخرج حبّةٌ واحدة — وهو بالضبط
    /// شكل العطل الذي وُجد هذا الملف لأجله (‏<c>traps.md#fakh-a-two-sided-fact-tested-on-one-side-only</c>).
    /// </para>
    /// <para>
    /// والأرقام: أربع وحدات بمئة ⇒ صافٍ 400 وضريبة 60 وإجمالي 460 على المورد،
    /// ورصيدٌ 4 حبّات بـ400. ثم مرتجعٌ بوحدة واحدة ⇒ صافيه <b>يُحسب</b> بتكلفة
    /// الاستلام الأصلي (400 ÷ 4 = 100) ولا يُملى، فتنقص الذمّة 115 ويصير الرصيد
    /// 3 حبّات بـ300.
    /// </para>
    /// </summary>
    [Fact]
    public async Task مرتجع_المشتريات_يُخرج_البضاعة_ويُنقص_الذمة_معاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyB;

        // ── ٠ · ملفّ القدرات — **يبذره هذا الاختبار بنفسه** ─────────────────
        // سلسلة الأمر والاستلام والفاتورة المخزنية كلّها خلف قدرة «المطابقة
        // الثلاثية»، وغياب الملفّ رفضٌ لا فتح (‏ADR-0023). واعتمادُ الاختبار على
        // ملفٍّ كتبه اختبار آخر هو «أخضر بترتيب التشغيل لا ببنائه» بعينه.
        await ThreeWayMatchAsync(api, company);

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenB);
        string costCenterId = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenB);
        (string code, _) = await Documents.AddItemAsync(api, company, ApiFixture.TokenB);

        // ── ١ · أمر شراء ────────────────────────────────────────────────────
        using HttpResponseMessage ordered = await api.Call(Http.Request(
            HttpMethod.Post, Documents.PurchaseOrders(company), ApiFixture.TokenB,
            Documents.StockPurchaseOrder(Documents.Number("PO"), supplierId, code, costCenterId)));

        (string orderText, JsonElement order) = await Http.BodyAsync(ordered);
        Console.WriteLine("الأمر: " + orderText);
        Assert.Equal(HttpStatusCode.Created, ordered.StatusCode);

        string orderId = order.GetProperty("id").GetString()!;
        string orderLineId = await FirstLineAsync(api, company, Documents.PurchaseOrder(company, orderId));

        // ── ٢ · استلام البضاعة وترحيله: هنا تدخل البضاعة المخزن ─────────────
        using HttpResponseMessage receipted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.GoodsReceipts(company), ApiFixture.TokenB,
            Documents.StockGoodsReceipt(Documents.Number("GRN"), orderId, orderLineId)));

        (string receiptText, JsonElement receipt) = await Http.BodyAsync(receipted);
        Console.WriteLine("الاستلام: " + receiptText);
        Assert.Equal(HttpStatusCode.Created, receipted.StatusCode);

        string receiptId = receipt.GetProperty("id").GetString()!;
        string receiptLineId = await FirstLineAsync(api, company, Documents.GoodsReceiptLines(company, receiptId));

        using HttpResponseMessage receiptPosted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.GoodsReceiptPosting(company, receiptId), ApiFixture.TokenB));

        (string receiptPostedText, _) = await Http.BodyAsync(receiptPosted);
        Console.WriteLine("ترحيل الاستلام: " + receiptPostedText);
        Assert.Equal(HttpStatusCode.Created, receiptPosted.StatusCode);

        JsonElement afterReceipt = await Documents.BalanceOfAsync(api, company, ApiFixture.TokenB, code);
        Assert.Equal("4.000000", afterReceipt.GetProperty("quantity").GetProperty("magnitude").GetString());
        Assert.Equal("400.0000", afterReceipt.GetProperty("value").GetString());

        // ── ٣ · فاتورة المورد المخزنية وترحيلها ─────────────────────────────
        using HttpResponseMessage billed = await api.Call(Http.Request(
            HttpMethod.Post, Documents.StockBills(company), ApiFixture.TokenB,
            Documents.StockBill(Documents.Number("BILL"), receiptId, receiptLineId)));

        (string billText, JsonElement bill) = await Http.BodyAsync(billed);
        Console.WriteLine("الفاتورة: " + billText);

        Assert.Equal(HttpStatusCode.Created, billed.StatusCode);
        Assert.Equal("400.0000", bill.GetProperty("net").GetString());
        Assert.Equal("60.0000", bill.GetProperty("tax").GetString());
        Assert.Equal("460.0000", bill.GetProperty("gross").GetString());

        string billId = bill.GetProperty("id").GetString()!;

        // والعنوان يوجّه إلى مورد **فاتورة المورد**: مستندٌ واحد وعنوانٌ واحد.
        Assert.Equal(Documents.SupplierBill(company, billId), billed.Headers.Location?.OriginalString);

        using HttpResponseMessage billPosted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierBillPosting(company, billId), ApiFixture.TokenB));

        (string billPostedText, _) = await Http.BodyAsync(billPosted);
        Console.WriteLine("ترحيل الفاتورة: " + billPostedText);
        Assert.Equal(HttpStatusCode.Created, billPosted.StatusCode);

        decimal payableBefore = await PayableOfAsync(api, company, supplierId);

        // ── ٤ · المرتجع: كمّيةٌ بلا مبلغ — والمبلغ يُحسب من تكلفة الاستلام ──
        using HttpResponseMessage returned = await api.Call(Http.Request(
            HttpMethod.Post, Documents.PurchaseReturns(company), ApiFixture.TokenB,
            Documents.PurchaseReturn(Documents.Number("DBN"), billId, receiptLineId)));

        (string returnText, JsonElement returnDraft) = await Http.BodyAsync(returned);
        Console.WriteLine("المرتجع: " + returnText);

        Assert.Equal(HttpStatusCode.Created, returned.StatusCode);
        Assert.Equal("DRAFT", returnDraft.GetProperty("state").GetString());

        // وصافيه **صفر في المسوّدة**: لم يُحسب بعد، ولا يُملى من العميل.
        Assert.Equal("0.0000", returnDraft.GetProperty("net").GetString());

        string returnId = returnDraft.GetProperty("id").GetString()!;
        Assert.Equal(Documents.PurchaseReturn(company, returnId), returned.Headers.Location?.OriginalString);

        using HttpResponseMessage returnPosted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.PurchaseReturnPosting(company, returnId), ApiFixture.TokenB));

        (string returnPostedText, JsonElement returnBody) = await Http.BodyAsync(returnPosted);
        Console.WriteLine("ترحيل المرتجع: " + returnPostedText);

        Assert.Equal(HttpStatusCode.Created, returnPosted.StatusCode);
        Assert.Equal("POSTED", returnBody.GetProperty("state").GetString());
        Assert.Equal("100.0000", returnBody.GetProperty("net").GetString());
        Assert.Equal("15.0000", returnBody.GetProperty("tax").GetString());
        Assert.Equal("115.0000", returnBody.GetProperty("gross").GetString());
        Assert.NotEqual(JsonValueKind.Null, returnBody.GetProperty("entryId").ValueKind);

        // ── ٥ · الوجهان معاً ────────────────────────────────────────────────
        decimal payableAfter = await PayableOfAsync(api, company, supplierId);
        JsonElement afterReturn = await Documents.BalanceOfAsync(api, company, ApiFixture.TokenB, code);

        Assert.Equal(115.0000m, payableBefore - payableAfter);
        Assert.Equal("3.000000", afterReturn.GetProperty("quantity").GetProperty("magnitude").GetString());
        Assert.Equal("300.0000", afterReturn.GetProperty("value").GetString());

        // ── ٦ · والمطابقة تبقى صفراً بعده ───────────────────────────────────
        // خروج البضاعة كُتب في دفتر المخزون **وفي** حسابه الضابط بالمبلغ نفسه.
        // ولو نقصت الذمّة وحدها لبقي الرقمان متساويين ولكان هذا التأكيد أخضر —
        // ولذلك لا يقوم وحده: الرصيد أعلاه هو ما يشهد أن حبّةً خرجت فعلاً.
        using HttpResponseMessage valued = await api.Call(Http.Request(
            HttpMethod.Get, Documents.InventoryValuation(company, AsOf), ApiFixture.TokenB));

        (string valuedText, JsonElement valuation) = await Http.BodyAsync(valued);
        Assert.True(valued.StatusCode == HttpStatusCode.OK, valuedText);
        Assert.Equal("0.0000", valuation.GetProperty("divergence").GetString());
        Assert.True(valuation.GetProperty("isReconciled").GetBoolean(), valuedText);
    }

    /// <summary>
    /// يبذر ملفّ قدرات المنشأة بـ«المطابقة الثلاثية» مُشغَّلة — وهي ما يرخّص حقل
    /// الاستلام، فيقع أمر الشراء والاستلام والفاتورة المخزنية.
    /// </summary>
    private static async Task ThreeWayMatchAsync(ApiProcess api, Guid company)
    {
        const string payload = """
            {"documents":[{"documentType":"purchasing.supplier_bill",
              "capabilities":[{"capability":"three_way_match","enabled":true}]}],
             "withdrawalReason":"بذر حالة اختبار — الملفّ يُستبدل بالكامل"}
            """;

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/capability-profile"),
            ApiFixture.TokenB,
            payload));

        (string text, _) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "تعذّر حفظ ملفّ القدرات: " + text);
    }

    /// <summary>معرّف السطر الأول من مستند مشتريات — مدخلُ المستند التالي في السلسلة.</summary>
    private static async Task<string> FirstLineAsync(ApiProcess api, Guid company, string path)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, path, ApiFixture.TokenB));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "قراءة المستند: " + text);

        JsonElement line = Assert.Single(body.GetProperty("lines").EnumerateArray().ToList());
        return line.GetProperty("id").GetString()!;
    }

    /// <summary>ذمّة المورد كما يقرؤها تقرير الأعمار — من السلك لا من القاعدة.</summary>
    private static async Task<decimal> PayableOfAsync(ApiProcess api, Guid company, string supplierId)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Documents.PayablesAging(company, AsOf), ApiFixture.TokenB));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "قراءة الأعمار: " + text);

        foreach (JsonElement party in body.GetProperty("parties").EnumerateArray())
        {
            if (string.Equals(party.GetProperty("partyId").GetString(), supplierId, StringComparison.Ordinal))
            {
                return decimal.Parse(
                    party.GetProperty("bands").GetProperty("total").GetString()!,
                    CultureInfo.InvariantCulture);
            }
        }

        return 0m;
    }
}
