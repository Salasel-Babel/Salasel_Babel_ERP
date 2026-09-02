using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Inventory.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>التقريب الصامت مستحيل — لا «غير مُتوقَّع» ولا «مُختبَرٌ بحالتين».</b>
/// <para>
/// إثباتٌ بحالةٍ أو حالتين يقول «هذا المُدخَل لا يُقرَّب»؛ وما يلزم قوله هو <b>«لا مُدخَل
/// يُقرَّب»</b>. والفرق بينهما هو الفرق بين اختبارٍ يمرّ واختبارٍ يحرس، ولذلك يبدأ هذا
/// الملفّ <b>بمسحٍ شامل</b> على فضاء المُدخَلات كلّه في مدىً معلَن، ثم يُتبعه بحالات
/// الحدّ المُسمّاة.
/// </para>
/// <para>
/// <b>ولماذا الكمّية بالذات:</b> الكمّية تُضرب في تكلفة الوحدة. فخانةٌ تُفقَد فيها تدخل
/// <b>المال</b>، وتتراكم على كل حركة، حتى يصير رصيد القيمة لا يساوي مجموع حركاته —
/// و<b>لا يُظهره توازنٌ ولا سلسلةُ بصمات</b>، لأن القيد المبنيّ على الرقم المُقرَّب
/// متوازنٌ تماماً.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class SilentRoundingIsImpossibleTests : IAsyncLifetime
{
    private const string Piece = "EA";
    private const string Carton = "CTN";
    private const string Kilogram = "KG";
    private const string Gram = "G";
    private const string Metre = "M";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · **المسح الشامل**: لا تحويلٍ ناجح يفقد خانة، ولا رفضٍ بلا سبب
    // ═══════════════════════════════════════════════════════════════════════
    //
    // الحُكم على كل زوج (مقدار · معامل) في المدى المُعلَن، **وفي الاتجاهين**:
    //
    //   · **كل نجاح دقيق**: الناتج × المقام = المقدار × البسط **بالضبط**، بمساواة
    //     `decimal` لا بفارقٍ مقبول. فلو قرّب التحويل خانةً واحدة لسقطت المساواة.
    //   · **وكل رفضٍ مستحقّ**: لا يوجد عددٌ عشري بمقياس ستّ خانات يحقّق المساواة أصلاً.
    //     فلو رفض التحويل ما كان يقع بلا باقٍ لسقط هذا الشقّ — وهو الشقّ الذي يمنع
    //     «حارساً يرفض كل شيء فيبدو صحيحاً».
    //
    // والشقّ الثاني هو ما يجعل هذا الإثبات حارساً لا زينة: رفضٌ شامل يجتاز الشقّ الأول
    // وحده اجتيازاً تامّاً.
    [Fact]
    public void لا_تحويلٍ_ناجح_يفقد_خانة_ولا_رفضٍ_بلا_سبب_في_المسح_الشامل()
    {
        const int MaxFactor = 24;

        // مقادير تشمل الصحيح والكسري وما يقع على حدّ المقياس الستّ ودونه.
        decimal[] magnitudes =
        [
            1m, 2m, 3m, 5m, 7m, 11m, 12m, 13m, 24m, 25m, 100m, 999m,
            0.5m, 0.25m, 0.125m, 1.5m, 2.5m, 7.5m,
            0.000001m, 0.0000005m, 0.123456m, 123456.789012m,
        ];

        long successes = 0;
        long refusals = 0;
        List<string> lossy = [];
        List<string> spurious = [];

        for (long numerator = 1; numerator <= MaxFactor; numerator++)
        {
            for (long denominator = 1; denominator <= MaxFactor; denominator++)
            {
                UnitRatio ratio = new(numerator, denominator);

                foreach (decimal magnitude in magnitudes)
                {
                    Result<decimal> result = UnitConversion.ToBase(magnitude, ratio);

                    // ما كان يجب أن يقع: المقسوم، ثم هل يقبل القسمة بلا باقٍ **ضمن
                    // مقياس ستّ خانات**؟
                    decimal scaled = magnitude * numerator;
                    decimal exact = scaled / denominator;
                    decimal atScale = decimal.Round(exact, UnitConversion.QuantityScale, MidpointRounding.ToEven);
                    bool representable = atScale * denominator == scaled;

                    if (result.IsSuccess)
                    {
                        successes++;

                        // ‏**المساواة بالضبط** — لا `Math.Abs(a - b) < ε`. والعتبة هنا
                        // كانت ستقبل بالتعريف ما وُجد الإثبات ليمنعه.
                        if (result.Value * denominator != scaled)
                        {
                            lossy.Add(FormattableString.Invariant(
                                $"{magnitude} × {numerator}/{denominator} ⇒ {result.Value} (وحاصلُه × {denominator} = {result.Value * denominator} ≠ {scaled})"));
                        }
                    }
                    else
                    {
                        refusals++;

                        if (representable)
                        {
                            spurious.Add(FormattableString.Invariant(
                                $"{magnitude} × {numerator}/{denominator} رُفض وهو يقع بلا باقٍ على {atScale}"));
                        }
                    }
                }
            }
        }

        Proof.Require(
            lossy.Count == 0,
            "لا تحويلٍ ناجح واحد يفقد خانة — والمساواة بالضبط لا بفارقٍ مقبول",
            FormattableString.Invariant($"نجاحات مفحوصة={successes} · فاقدة={lossy.Count}")
            + (lossy.Count == 0 ? string.Empty : "\n        " + string.Join("\n        ", lossy.Take(5))));

        Proof.Require(
            spurious.Count == 0,
            "ولا رفضٍ واحد بلا سبب — فلا يمرّ «حارسٌ يرفض كل شيء» بوصفه صحيحاً",
            FormattableString.Invariant($"رفوضات مفحوصة={refusals} · بلا سبب={spurious.Count}")
            + (spurious.Count == 0 ? string.Empty : "\n        " + string.Join("\n        ", spurious.Take(5))));

        // ‏**وكلا الشقّين غير فارغ**: مسحٌ ينجح كلّه أو يُرفض كلّه لا يُثبت شيئاً.
        Proof.Require(
            successes > 0 && refusals > 0,
            "والمسح رأى الحالتين معاً — نجاحاً ورفضاً — فلا يُصادَق على ثابتٍ يقول «نعم» أو «لا» دائماً",
            FormattableString.Invariant(
                $"المفحوص={successes + refusals} زوجاً · نجح={successes} · رُفض={refusals}"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · «‏٧ حبّات ← كرتون» كسرٌ لا يُدوَّر بصمت — على السلك المنطقي نفسه
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task سبع_حبّات_إلى_كرتون_تُرفض_باسمها_واثنتا_عشرة_تمرّ_بواحدٍ_بالضبط()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitRegisterTenant;

        string piece = Harness.Next(Piece);
        string carton = Harness.Next(Carton);

        await RegisterUnitAsync(tenant, piece, "حبّة", "Piece", "COUNT", token);
        await RegisterUnitAsync(tenant, carton, "كرتون", "Carton", "COUNT", token);

        // «الحبّة جزءٌ من اثنتي عشرة من الكرتون» ⇒ 1/12.
        Result<UnitConversionView> factor = await _harness.Units.CreateConversionAsync(
            tenant, Harness.Actor, new UnitConversionDraft(piece, carton, 1L, 12L), token);

        Assert.True(factor.IsSuccess, Describe(factor));

        // ‏١٢ حبّة ⇒ كرتونٌ واحد بالضبط.
        Result<UnitConversionResult> exact = await ConvertAsync(tenant, 12m, piece, carton, token);

        Assert.True(exact.IsSuccess, Describe(exact));

        Proof.Require(
            exact.Value.To.Magnitude == 1m
            && exact.Value.Numerator == 1L && exact.Value.Denominator == 12L,
            "اثنتا عشرة حبّة كرتونٌ واحد بالضبط — والمعامل يخرج مع الجواب فيُراجَع الرقم بلا استعلامٍ ثانٍ",
            "الناتج=" + Quantity(exact.Value.To.Magnitude) + " " + exact.Value.To.Unit
            + " بمعامل " + exact.Value.Numerator.ToString(CultureInfo.InvariantCulture)
            + "/" + exact.Value.Denominator.ToString(CultureInfo.InvariantCulture));

        // ‏**٧ حبّات ⇒ 0.583333… — يُرفض ولا يُجاب بـ«0.583333»**.
        Result<UnitConversionResult> inexact = await ConvertAsync(tenant, 7m, piece, carton, token);

        Assert.True(inexact.IsFailure, "سبع حبّات حُوِّلت إلى كرتون، فقُرِّب الكسر في الخفاء.");

        Proof.Require(
            inexact.Errors[0].Code == "inventory.unit_conversion_not_exact"
            && inexact.Errors[0].MessageAr.Length > 0
            && inexact.Errors[0].MessageEn.Length > 0,
            "وسبعُ حبّات كسرٌ لا يُدوَّر بصمت — يُرفض باسمه وبلغتين، ولا يخرج 0.583333",
            inexact.Errors[0].Code);

        // ‏**والرفض يُسمّي العلاج**: «أرسل مقداراً يقبل القسمة على المقام».
        Proof.Require(
            inexact.Errors[0].MessageAr.Contains("يقبل القسمة", StringComparison.Ordinal),
            "والرسالة تقول ما الذي يجعله يمرّ، لا «مرفوض» وحدها",
            inexact.Errors[0].MessageAr[..Math.Min(120, inexact.Errors[0].MessageAr.Length)]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · «كيلوغرام ← متر» خطأٌ يُرفض، لا معاملٌ ناقص
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task التحويل_بين_صنفَي_كمّية_مختلفين_يُرفض_باسمه()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitRegisterTenant;

        string kilogram = Harness.Next(Kilogram);
        string gram = Harness.Next(Gram);
        string metre = Harness.Next(Metre);

        await RegisterUnitAsync(tenant, kilogram, "كيلوغرام", "Kilogram", "WEIGHT", token);
        await RegisterUnitAsync(tenant, gram, "غرام", "Gram", "WEIGHT", token);
        await RegisterUnitAsync(tenant, metre, "متر", "Metre", "LENGTH", token);

        // وزنٌ إلى وزن: واقعةٌ فيزيائية، فتمرّ.
        Result<UnitConversionView> physical = await _harness.Units.CreateConversionAsync(
            tenant, Harness.Actor, new UnitConversionDraft(kilogram, gram, 1000L, 1L), token);

        Assert.True(physical.IsSuccess, Describe(physical));

        Proof.Require(
            string.Equals(physical.Value.QuantityClass, "WEIGHT", StringComparison.Ordinal),
            "الكيلوغرام ألف غرام — واقعةٌ فيزيائية بين وحدتين من صنفٍ واحد، فتُسجَّل",
            "الصنف=" + physical.Value.QuantityClass);

        // ووزنٌ إلى طول: **كثافةُ مادّة لا معامل**، فيُرفض.
        Result<UnitConversionView> impossible = await _harness.Units.CreateConversionAsync(
            tenant, Harness.Actor, new UnitConversionDraft(kilogram, metre, 1L, 1L), token);

        Assert.True(impossible.IsFailure, "سُجّل معامل بين وزنٍ وطول.");

        Proof.Require(
            impossible.Errors[0].Code == "inventory.unit_class_mismatch"
            && impossible.Errors[0].MessageAr.Contains(kilogram, StringComparison.Ordinal)
            && impossible.Errors[0].MessageAr.Contains(metre, StringComparison.Ordinal),
            "و«كجم ← م» خطأٌ يُرفض باسمه ويُسمّي الوحدتين — لا معاملٌ ناقص يُقرَّب",
            impossible.Errors[0].Code);

        // والمسبار يرفضه كذلك، لا عند التسجيل وحده.
        Result<UnitConversionResult> probed = await ConvertAsync(tenant, 5m, kilogram, metre, token);

        Assert.True(probed.IsFailure, "المسبار حوّل وزناً إلى طول.");

        Proof.Require(
            probed.Errors[0].Code == "inventory.unit_class_mismatch",
            "والمسبار يرفضه أيضاً — الحدّ في القاعدة لا في باب التسجيل وحده",
            probed.Errors[0].Code);

        // ووحدتان من صنفٍ واحد بلا معامل مسجَّل: رفضٌ آخر مُسمّى، **ولا اشتقاق بسلسلة**.
        Result<UnitConversionResult> unlinked = await ConvertAsync(tenant, 1m, gram, kilogram, token);

        Assert.True(unlinked.IsFailure, "اشتُقّ معامل عكسي لم يُسجَّل.");

        Proof.Require(
            unlinked.Errors[0].Code == "inventory.no_conversion_between_units",
            "والمعامل لا يُقلَب تلقائياً ولا يُشتقّ بسلسلة — السلسلة تُنتج تحويلاً لم يقرّه أحد، وكسرُها الوسيط يُقرَّب",
            unlinked.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · الطفح يُمسَك ويُسمّى، ولا يخرج خطأَ خادم
    // ═══════════════════════════════════════════════════════════════════════
    //
    // المقدار يعبر السلك بعشرين خانة صحيحة والبسط يبلغ ملياراً، وحاصلُهما يتجاوز مدى
    // ‏`decimal`. واستثناءٌ غير مُمسَك كان يخرج 500 — أي «عطلٌ عندنا» — وهو في الحقيقة
    // مُدخَلٌ مرفوض له علاجٌ يُقال.
    [Fact]
    public void الطفح_يُرفض_باسمه_ولا_يرمي_استثناءً()
    {
        decimal huge = 99_999_999_999_999_999_999m;
        UnitRatio wide = new(1_000_000_000L, 1L);

        Result<decimal> result = UnitConversion.ToBase(huge, wide);

        Assert.True(result.IsFailure, "تحويلٌ يتجاوز مدى العدد العشري مرّ.");

        Proof.Require(
            result.Errors[0].Code == "inventory.unit_conversion_overflow"
            && result.Errors[0].MessageAr.Length > 0
            && result.Errors[0].MessageEn.Length > 0,
            "الطفح يُمسَك ويُسمّى بلغتين — لا استثناءٌ يخرج 500 فيُقرأ «عطلٌ عندنا» وهو مُدخَل مرفوض",
            result.Errors[0].Code);

        // والمعامل غير الموجب يُرفض قبل أي حساب — والمقام الصفري ليس معاملاً.
        Result<decimal> zeroDenominator = UnitConversion.ToBase(10m, new UnitRatio(1L, 0L));

        Assert.True(zeroDenominator.IsFailure, "معاملٌ بمقامٍ صفري مرّ.");

        Proof.Require(
            zeroDenominator.Errors[0].Code == "inventory.unit_ratio_not_positive",
            "والمقام الصفري ليس معاملاً — يُرفض قبل أي قسمة",
            zeroDenominator.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · الحركة تحفظ **الوحدة المُسلَّمة ومقدارها**، ولا تُنسى بالتحويل
    // ═══════════════════════════════════════════════════════════════════════
    //
    // القرار: الحركة تُخزَّن **بالوحدتين معاً** — المُدخَلة كما وردت، والأساس محسوباً —
    // ولا يُختار أحدهما. ولذلك تُقارَن الإعادة **بما سُلّم** لا بما استقرّ: طلبٌ سُجّل
    // بكرتون يُعاد بـ«اثنتي عشرة حبّة» ليس إعادةً بل طلبٌ آخر يصادف أن أثره واحد اليوم.
    [Fact]
    public async Task الحركة_تحفظ_الوحدة_المُسلَّمة_والإعادة_تُقارَن_بما_سُلّم_لا_بما_استقرّ()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitRegisterTenant;
        string item = Harness.Next("ITEM");

        Result<ItemView> registered = await _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(
                item,
                new LocalizedName("صنف بكرتون", "Item in cartons"),
                "*",
                Piece,
                [new ItemUnitDraft(Carton, 12L, 1L)]),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));

        InventoryMovementSource source = new(
            BabelModule.Inventory,
            "OpeningBalance",
            Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            "OnApproval",
            1,
            "inventory.count_adjustment.posted");

        InventoryItemLocation place = new(item, "WH-UOM-REG", "DEFAULT", "*");

        Result<InventoryMovementCost> byCarton = await _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = source,
                Location = place,
                Quantity = new InventoryQuantity(1m, Carton),
                Cost = Harness.Sar(120.0000m),
                OccurredOn = new DateOnly(2026, 5, 4),
            },
            token);

        Assert.True(byCarton.IsSuccess, Describe(byCarton));

        Proof.Require(
            byCarton.Value.Quantity.Magnitude == 12m
            && UnitConversion.SameUnit(byCarton.Value.Quantity.Unit, Piece),
            "الكرتون دخل الرصيد اثنتي عشرة حبّة — الرصيد يُمسَك بوحدة أساسٍ واحدة",
            "الرصيد=" + Quantity(byCarton.Value.Quantity.Magnitude) + " " + byCarton.Value.Quantity.Unit);

        // ‏**الإعادة بالهوية نفسها وبالمقدار المكافئ لا بالمُسلَّم**: تُرفض.
        Result<InventoryMovementCost> equivalent = await _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = source,
                Location = place,
                Quantity = new InventoryQuantity(12m, Piece),
                Cost = Harness.Sar(120.0000m),
                OccurredOn = new DateOnly(2026, 5, 4),
            },
            token);

        Assert.True(equivalent.IsFailure, "طلبٌ بوحدةٍ أخرى مرّ بوصفه إعادةً للطلب نفسه.");

        Proof.Require(
            equivalent.Errors[0].Code == "inventory.movement_quantity_conflict",
            "والمقارنة بما سُلّم لا بما استقرّ — «كرتون» و«اثنتا عشرة حبّة» طلبان مختلفان يصادف أن أثرهما واحد اليوم",
            equivalent.Errors[0].Code);

        // والإعادة **بالمُسلَّم نفسه** تمرّ بلا حركة ثانية.
        Result<InventoryMovementCost> replay = await _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = source,
                Location = place,
                Quantity = new InventoryQuantity(1m, Carton),
                Cost = Harness.Sar(120.0000m),
                OccurredOn = new DateOnly(2026, 5, 4),
            },
            token);

        Assert.True(replay.IsSuccess, Describe(replay));

        Proof.Require(
            replay.Value.WasAlreadyRecorded && replay.Value.QuantityAfter.Magnitude == 12m,
            "والإعادة بالمُسلَّم نفسه لا تكتب حركة ثانية — الوحدة المُدخَلة محفوظةٌ على الصفّ فتُقارَن حرفياً",
            "مُسجَّلة سلفاً=" + replay.Value.WasAlreadyRecorded.ToString(CultureInfo.InvariantCulture)
            + " · الرصيد=" + Quantity(replay.Value.QuantityAfter.Magnitude));
    }

    // ────────────────────────────────────────────────────────────────────────

    private async Task RegisterUnitAsync(
        TenantId tenant, string code, string arabic, string english, string quantityClass, CancellationToken token)
    {
        Result<UnitOfMeasureView> registered = await _harness.Units.CreateAsync(
            tenant, Harness.Actor, new UnitOfMeasureDraft(code, new LocalizedName(arabic, english), quantityClass), token);

        Assert.True(registered.IsSuccess, Describe(registered));
    }

    private Task<Result<UnitConversionResult>> ConvertAsync(
        TenantId tenant, decimal magnitude, string fromUnit, string toUnit, CancellationToken token)
        => _harness.Units.ConvertAsync(
            tenant,
            Harness.Actor,
            new UnitConversionTrial(new InventoryQuantity(magnitude, fromUnit), toUnit),
            token).AsTask();

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));
}
