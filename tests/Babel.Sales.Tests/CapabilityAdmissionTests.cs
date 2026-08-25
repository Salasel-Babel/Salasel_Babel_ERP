using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>
/// <b>بوابة القبول موصولة — لا مبنيّة وحدها.</b>
/// <para>
/// النواة تبني ملفّ قدرات مغلقاً مُطابَقاً بالمصفوفة، وتُنتج <c>AdmittedDocument</c>
/// الذي لا يُبنى إلا بالمرور من القبول (ADR-0023). ونوعٌ لا يطلبه أحد في توقيعه ليس
/// حارساً: قبل هذا التغيير لم تكن أي وحدة تستدعي البوابة، فكان الحقل الذي ترخّصه قدرة
/// مُطفأة يمرّ كأن الملفّ لا وجود له.
/// </para>
/// </summary>
[Collection("receivables")]
public sealed class CapabilityAdmissionTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Next(string prefix)
        => prefix + "-ADM-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · مستأجران · شيفرة واحدة · مصفوفة واحدة · صفّا ملفّ مختلفان
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏**هذا هو الإثبات.** المستأجران مبذوران في الدفتر بالكامل — الحسابات نفسها،
    // وخريطة الأدوار نفسها، والفترات نفسها — ولا يفترقان إلا في صفّ ملفّ القدرات.
    // فلو كان الرفض عند المُطفأ ناتجاً عن نقص في الحساب أو في المصفوفة لسقط عند
    // المُشغَّل أيضاً. والمستند المُقدَّم واحد حرفياً: العميل نفسه بالرمز نفسه،
    // والمبالغ نفسها، والاستدعاءات نفسها بالترتيب نفسه.
    [Fact]
    public async Task The_same_advance_is_refused_for_a_tenant_that_disabled_the_capability_and_accepted_for_one_that_enabled_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        TenantId enabled = SalesTestEnvironment.AdvanceEnabledTenant;
        TenantId disabled = SalesTestEnvironment.AdvanceDisabledTenant;

        // صفّا البيانات — وهما كل الفارق بين الحالتين.
        await _harness.SaveProfileAsync(enabled, advance: true, costOfSales: true, token);
        await _harness.SaveProfileAsync(disabled, advance: false, costOfSales: true, token);

        Attempt allowed = await AttemptAdvanceAsync(enabled, token);
        Attempt refused = await AttemptAdvanceAsync(disabled, token);

        // والقدرة الأخرى مُشغَّلة عند الاثنين: الرفض قدرةٌ بعينها لا وحدةٌ كاملة.
        Result<PostingReceipt> costEnabled = await CostOfSalesAsync(enabled, allowed.InvoiceId, token);
        Result<PostingReceipt> costDisabled = await CostOfSalesAsync(disabled, refused.InvoiceId, token);

        Proof.Require(
            allowed.Applied.IsSuccess
            && refused.Recorded.IsFailure
            && refused.Recorded.Errors[0].Code == "document_admission.capability_not_enabled"
            && refused.Recorded.Errors[0].MessageAr.Contains("advanceApplied", StringComparison.Ordinal)
            && costEnabled.IsSuccess
            && costDisabled.IsSuccess,
            "الدفعة المقدمة نفسها تمرّ عند مستأجر شغّل القدرة وتُرفض عند مستأجر أطفأها، والقدرة الأخرى تمرّ عند الاثنين",
            "المُشغَّل: الاستنفاد=" + Describe(allowed.Applied)
            + " · المُطفأ: التسجيل=" + Describe(refused.Recorded)
            + " · تكلفة المبيعات عند المُشغَّل=" + Describe(costEnabled)
            + " وعند المُطفأ=" + Describe(costDisabled));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · مستأجر بلا ملفّ قدرات: رفض لا فتح
    // ═══════════════════════════════════════════════════════════════════════
    //
    // الباب الذي يُلتفّ منه على البوابة كلّها لو فُتح: يكفي ألّا يُحفظ ملفّ لتمرّ
    // كل قدرة مُطفأة. والمستأجر هنا مبذور في الدفتر تماماً — فالرفض من الملفّ لا من نقص.
    [Fact]
    public async Task A_tenant_with_no_capability_profile_at_all_is_refused_not_opened()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // تجهيزة نظيفة بلا أي ملفّ محفوظ: المخزن هنا ملك هذا البند وحده.
        using Harness bare = await Harness.CreateWithoutProfilesAsync(token);

        Result<SalesDocumentView> recorded = await bare.Receipts.RecordAdvanceAsync(
            SalesTestEnvironment.AdvanceEnabledTenant,
            Harness.Actor,
            new CustomerAdvanceDraft(
                Next("ADV"), Guid.CreateVersion7(), March, "bank", "BANK-01", Harness.Sar(100m), Harness.Sar(0m), false),
            token);

        Result<PostingReceipt> cost = await bare.Invoices.PostCostOfSalesAsync(
            SalesTestEnvironment.AdvanceEnabledTenant,
            Harness.Actor,
            Guid.CreateVersion7(),
            new CostOfSalesDraft("ITEM-X", "WH-01", "*", Harness.Sar(10m)),
            token);

        ValidatedCapabilityProfile? stored = await bare.Profiles
            .FindAsync(SalesTestEnvironment.AdvanceEnabledTenant, token);

        Proof.Require(
            stored is null
            && recorded.IsFailure
            && recorded.Errors[0].Code == "sales.capability_profile_missing"
            && cost.IsFailure
            && cost.Errors[0].Code == "sales.capability_profile_missing",
            "غياب الملفّ رفضٌ لا فتح — على المسارين معاً",
            "الملفّ المخزَّن=" + (stored is null ? "(لا شيء)" : "موجود!")
            + " · تسجيل الدفعة=" + Describe(recorded)
            + " · تكلفة المبيعات=" + Describe(cost));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · تذكرة مستند آخر ليست تذكرة هذا المستند
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏<c>AdmittedDocument</c> في التوقيع يمنع «نسيان الفحص»، ولا يمنع وحده تمرير
    // قبولٍ نُشئ لشيء آخر. وهذا البند يُثبت أن الفحص الثاني — «هل يغطّي هذا القبول
    // الحقل الذي أمارسه؟» — ليس زخرفاً: قبولٌ حقيقي تماماً، صادر عن ملفّ صالح، يُرفض
    // لأنه لا يحمل الحقل.
    [Fact]
    public async Task An_admission_issued_for_another_field_does_not_open_this_path()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.AdvanceEnabledTenant;

        await _harness.SaveProfileAsync(tenant, advance: true, costOfSales: true, token);

        ValidatedCapabilityProfile profile = (await _harness.Profiles.FindAsync(tenant, token))!;

        // قبولان حقيقيان من الملفّ نفسه: أحدهما يحمل حقل الدفعة والآخر يحمل المستودع.
        Result<AdmittedDocument> advanceTicket = profile.Admit(new DocumentSubmission(
            new DocumentTypeCode("sales.invoice"), ["customer", "lines", "advanceApplied"]));

        Result<AdmittedDocument> warehouseTicket = profile.Admit(new DocumentSubmission(
            new DocumentTypeCode("sales.invoice"), ["customer", "lines", "warehouse"]));

        Result covers = SalesAdmissionProbe.EnsureCovers(warehouseTicket.Value, "advanceApplied");
        Result coversItself = SalesAdmissionProbe.EnsureCovers(advanceTicket.Value, "advanceApplied");

        Proof.Require(
            advanceTicket.IsSuccess
            && warehouseTicket.IsSuccess
            && covers.IsFailure
            && covers.Errors[0].Code == "sales.admission_does_not_cover_field"
            && coversItself.IsSuccess,
            "قبولٌ صادرٌ لحقل آخر لا يفتح هذا المسار، والقبول الصحيح يفتحه",
            "تذكرة المستودع على حقل الدفعة=" + (covers.IsFailure ? covers.Errors[0].Code : "(قُبلت!)")
            + " · تذكرة الدفعة على حقل الدفعة=" + (coversItself.IsSuccess ? "قُبلت" : "رُفضت!"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · لا باب غير محروس: كل نقطة دخول تبلغ حدثاً تفتحه قدرة تبلغ القبول
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏**الحارس البنيوي.** فحصٌ يؤدّيه مستدعٍ واحد يُنسى في المستدعي الثاني، وهذا
    // بالضبط صنف العطل الذي يتكرّر هنا: «الموضع الذي يجيب عن السؤال ليس الموضع الذي
    // أُصلح». فالحارس لا يعدّ استدعاءات مكتوبة بيد، بل يقرأ **لغة وسيطة** التجميعة:
    //
    //   • الأحداث المحروسة تُقرأ من <c>CapabilityCatalogue</c> نفسه — لا قائمة هنا
    //     تنحرف عنه عند أول إضافة.
    //   • أي دالّة في التجميعة تحمل رمز حدث محروس نصّاً (‏ldstr) تُعدّ «حاملة».
    //   • كل نقطة دخول عامة على خدمة تطبيق تُتبَّع عبر رسم الاستدعاء (بما فيه آلات
    //     الحالة غير المتزامنة والدوالّ المحلّية)، فإن بلغت حاملةً وجب أن تبلغ
    //     <c>SalesAdmission.AdmitInvoiceAsync</c>.
    //
    // ولا يمرّ فراغاً: يُؤكَّد أن الأحداث المحروسة والحاملات ونقاط الدخول المحروسة
    // كلها غير فارغة.
    [Fact]
    public void No_public_entry_point_reaches_a_capability_gated_event_without_reaching_admission()
    {
        AdmissionScan scan = AdmissionScan.Run();

        Proof.Note("الأحداث المحروسة من الكتالوج: " + string.Join(" · ", scan.GatedEvents));
        Proof.Note("الدوالّ الحاملة لرمز محروس: " + string.Join(" · ", scan.Bearers));
        Proof.Note("نقاط الدخول المحروسة فعلاً: " + string.Join(" · ", scan.Guarded));

        Proof.Require(
            scan.GatedEvents.Count > 0
            && scan.Bearers.Count > 0
            && scan.EntryPointCount > 0
            && scan.Guarded.Count > 0
            && scan.Violations.Count == 0,
            "لا نقطة دخول تبلغ حدثاً تفتحه قدرة دون أن تبلغ القبول — والمجموعات المفحوصة غير فارغة",
            "أحداث محروسة=" + scan.GatedEvents.Count.ToString(CultureInfo.InvariantCulture)
            + " · دوالّ حاملة=" + scan.Bearers.Count.ToString(CultureInfo.InvariantCulture)
            + " · نقاط دخول=" + scan.EntryPointCount.ToString(CultureInfo.InvariantCulture)
            + " · محروسة=" + scan.Guarded.Count.ToString(CultureInfo.InvariantCulture)
            + " · مخالفات=" + (scan.Violations.Count == 0 ? "لا شيء" : string.Join(" | ", scan.Violations)));
    }

    private sealed record Attempt(Result<SalesDocumentView> Recorded, Result<PostingReceipt> Applied, Guid InvoiceId);

    /// <summary>يبني الحالة نفسها حرفياً لكل مستأجر، ويُعيد نتيجة كل خطوة.</summary>
    private async Task<Attempt> AttemptAdvanceAsync(TenantId tenant, CancellationToken token)
    {
        string code = Next("CUS");

        Result<CustomerView> customer = await _harness.Customers.CreateAsync(
            tenant,
            Harness.Actor,
            new CustomerDraft(code, new LocalizedName("عميل " + code, "Customer " + code), Harness.Sar(0m), 30),
            token);
        Assert.True(customer.IsSuccess, Describe(customer));

        Result<SalesDocumentView> invoice = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer.Value.Id, March, "BR-01", [Harness.Line(1m, 800m)]),
            null,
            token);
        Assert.True(invoice.IsSuccess, Describe(invoice));

        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, invoice.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted));

        Result<SalesDocumentView> recorded = await _harness.Receipts.RecordAdvanceAsync(
            tenant,
            Harness.Actor,
            new CustomerAdvanceDraft(
                Next("ADV"), customer.Value.Id, March, "bank", "BANK-01", Harness.Sar(300m), Harness.Sar(0m), false),
            token);

        if (recorded.IsFailure)
        {
            return new Attempt(recorded, Result<PostingReceipt>.Failure(recorded.Errors), invoice.Value.Id);
        }

        Result<SalesDocumentView> postedAdvance = await _harness.Receipts
            .PostAdvanceAsync(tenant, Harness.Actor, recorded.Value.Id, token);
        Assert.True(postedAdvance.IsSuccess, Describe(postedAdvance));

        Result<PostingReceipt> applied = await _harness.Receipts.ApplyAdvanceAsync(
            tenant, Harness.Actor, recorded.Value.Id, invoice.Value.Id, Harness.Sar(300m), token);

        return new Attempt(recorded, applied, invoice.Value.Id);
    }

    private Task<Result<PostingReceipt>> CostOfSalesAsync(TenantId tenant, Guid invoiceId, CancellationToken token)
        => _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoiceId,
            new CostOfSalesDraft(Next("ITEM"), "WH-01", "*", Harness.Sar(120m)),
            token).AsTask();

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.Code));
}

/// <summary>
/// مسبار يبلغ <c>SalesAdmission</c> الداخلية — الاختبار يفحص ما يفحصه الإنتاج نفسه،
/// لا نسخةً منه.
/// </summary>
internal static class SalesAdmissionProbe
{
    public static Result EnsureCovers(AdmittedDocument admitted, string field)
        => SalesAdmission.EnsureCovers(admitted, field);
}

/// <summary>
/// <b>ماسح لغة وسيطة</b> يبني رسم الاستدعاء داخل <c>Babel.Sales</c>، ويسأل سؤالاً
/// واحداً: أي نقطة دخول عامة تبلغ رمز حدث تفتحه قدرة، ولا تبلغ القبول؟
/// </summary>
internal sealed class AdmissionScan
{
    private static readonly OpCode[] SingleByte = new OpCode[0x100];
    private static readonly OpCode[] DoubleByte = new OpCode[0x100];

    static AdmissionScan()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode code)
            {
                continue;
            }

            if ((code.Value & 0xFF00) == 0xFE00)
            {
                DoubleByte[code.Value & 0xFF] = code;
            }
            else if (code.Value >= 0 && code.Value < 0x100)
            {
                SingleByte[code.Value] = code;
            }
        }
    }

    private AdmissionScan(
        IReadOnlyList<string> gatedEvents,
        IReadOnlyList<string> bearers,
        IReadOnlyList<string> guarded,
        IReadOnlyList<string> violations,
        int entryPointCount)
    {
        GatedEvents = gatedEvents;
        Bearers = bearers;
        Guarded = guarded;
        Violations = violations;
        EntryPointCount = entryPointCount;
    }

    public IReadOnlyList<string> GatedEvents { get; }

    public IReadOnlyList<string> Bearers { get; }

    public IReadOnlyList<string> Guarded { get; }

    public IReadOnlyList<string> Violations { get; }

    public int EntryPointCount { get; }

    public static AdmissionScan Run()
    {
        Assembly sales = typeof(SalesInvoiceService).Assembly;

        // الأحداث المحروسة من الكتالوج نفسه — لا قائمة مكتوبة هنا تنحرف عنه.
        ImmutableArray<string> gated =
        [
            .. CapabilityCatalogue.DocumentTypes
                .Where(static definition => definition.Module == BabelModule.Sales)
                .SelectMany(static definition => definition.Capabilities)
                .SelectMany(static capability => capability.RequiredEvents)
                .Select(static code => code.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        HashSet<string> gatedSet = new(gated, StringComparer.Ordinal);

        MethodBase admission = typeof(SalesAdmission).GetMethod(
            nameof(SalesAdmission.AdmitInvoiceAsync), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("لم يُعثر على بوابة القبول.");

        Dictionary<MethodBase, Body> bodies = [];
        foreach (Type type in sales.GetTypes())
        {
            foreach (MethodBase method in Methods(type))
            {
                bodies.TryAdd(method, Body.Of(method, sales));
            }
        }

        List<string> bearers = [];
        foreach ((MethodBase method, Body body) in bodies)
        {
            if (body.Literals.Any(gatedSet.Contains))
            {
                bearers.Add(Name(method));
            }
        }

        bearers.Sort(StringComparer.Ordinal);

        List<string> guarded = [];
        List<string> violations = [];
        int entryPoints = 0;

        foreach (Type service in sales.GetTypes()
                     .Where(static type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
                     .Where(static type => typeof(IApplicationService).IsAssignableFrom(type))
                     .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            foreach (MethodInfo entry in service
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(static method => !method.IsSpecialName)
                         .OrderBy(static method => method.Name, StringComparer.Ordinal))
            {
                entryPoints++;

                HashSet<MethodBase> reached = Reach(entry, bodies);
                bool touchesGatedEvent = reached.Any(method =>
                    bodies.TryGetValue(method, out Body? body) && body.Literals.Any(gatedSet.Contains));

                if (!touchesGatedEvent)
                {
                    continue;
                }

                if (reached.Contains(admission))
                {
                    guarded.Add(Name(entry));
                }
                else
                {
                    violations.Add(Name(entry) + " يبلغ حدثاً تفتحه قدرة ولا يبلغ القبول");
                }
            }
        }

        return new AdmissionScan(gated, bearers, guarded, violations, entryPoints);
    }

    private static IEnumerable<MethodBase> Methods(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (MethodInfo method in type.GetMethods(All))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(All))
        {
            yield return constructor;
        }
    }

    /// <summary>يجمع كل ما تبلغه دالّة عبر رسم الاستدعاء داخل التجميعة نفسها.</summary>
    private static HashSet<MethodBase> Reach(MethodBase root, Dictionary<MethodBase, Body> bodies)
    {
        HashSet<MethodBase> seen = [];
        Queue<MethodBase> queue = new();
        queue.Enqueue(root);
        seen.Add(root);

        while (queue.Count > 0)
        {
            MethodBase current = queue.Dequeue();

            if (!bodies.TryGetValue(current, out Body? body))
            {
                continue;
            }

            foreach (MethodBase called in body.Calls)
            {
                if (seen.Add(called))
                {
                    queue.Enqueue(called);
                }
            }
        }

        return seen;
    }

    private static string Name(MethodBase method)
        => (method.DeclaringType?.Name ?? "?") + "." + method.Name;

    /// <summary>جسد دالّة مقروءاً من اللغة الوسيطة: نصوصها الحرفية وما تستدعيه.</summary>
    private sealed class Body
    {
        private Body(ImmutableArray<string> literals, ImmutableArray<MethodBase> calls)
        {
            Literals = literals;
            Calls = calls;
        }

        public ImmutableArray<string> Literals { get; }

        public ImmutableArray<MethodBase> Calls { get; }

        public static Body Of(MethodBase method, Assembly owner)
        {
            List<string> literals = [];
            List<MethodBase> calls = [];

            // الدالّة غير المتزامنة جسدها في آلة حالتها، لا في جسدها الظاهر.
            foreach (Type machine in StateMachines(method))
            {
                MethodInfo? move = machine.GetMethod(
                    "MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (move is not null)
                {
                    calls.Add(move);
                }
            }

            byte[]? il = null;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (InvalidOperationException)
            {
                il = null;
            }

            if (il is null)
            {
                return new Body([.. literals], [.. calls]);
            }

            Type[]? typeArguments = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : null;

            Type[]? methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
            Module module = method.Module;

            int position = 0;
            while (position < il.Length)
            {
                OpCode code;
                if (il[position] == 0xFE && position + 1 < il.Length)
                {
                    code = DoubleByte[il[position + 1]];
                    position += 2;
                }
                else
                {
                    code = SingleByte[il[position]];
                    position += 1;
                }

                int operand = position;
                position += OperandSize(code, il, position);

                if (code.OperandType == OperandType.InlineString)
                {
                    try
                    {
                        literals.Add(module.ResolveString(BitConverter.ToInt32(il, operand)));
                    }
                    catch (ArgumentException)
                    {
                        // رمز لا يُحلّ: لا يُخمَّن ولا يُبتلع أثره — يُتخطّى وحده.
                    }
                }
                else if (code.OperandType is OperandType.InlineMethod or OperandType.InlineTok)
                {
                    try
                    {
                        MethodBase? resolved = module.ResolveMethod(
                            BitConverter.ToInt32(il, operand), typeArguments, methodArguments);

                        if (resolved is not null && resolved.Module.Assembly == owner)
                        {
                            calls.Add(resolved);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // ‏InlineTok قد يشير إلى نوع أو حقل لا إلى دالّة.
                    }
                }
            }

            return new Body([.. literals], [.. calls]);
        }

        private static IEnumerable<Type> StateMachines(MethodBase method)
        {
            if (method.GetCustomAttribute<AsyncStateMachineAttribute>() is { StateMachineType: { } asyncMachine })
            {
                yield return asyncMachine;
            }

            if (method.GetCustomAttribute<IteratorStateMachineAttribute>() is { StateMachineType: { } iterator })
            {
                yield return iterator;
            }
        }

        private static int OperandSize(OpCode code, byte[] il, int position) => code.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
                or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
                or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
            _ => throw new InvalidOperationException("رمز عملية بمُعامل غير معروف: " + code.Name),
        };
    }
}
