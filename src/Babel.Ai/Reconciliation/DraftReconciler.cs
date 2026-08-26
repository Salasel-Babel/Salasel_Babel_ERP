using System.Globalization;
using Babel.Ai.Capture;
using Babel.SharedKernel;

namespace Babel.Ai.Reconciliation;

/// <summary>
/// <b>المطابقة الحسابية قبل أن يرى الإنسان المسوّدة.</b>
/// <para>
/// هذا حساب رخيص لا ذكاء اصطناعي، ويلتقط أكثر أخطاء الاستخراج: رقم مقروء خطأً يكسر
/// معادلةً من ثلاث. والقاعدة: <b>لا يُطلب من الإنسان أن يكتشف الفرق</b> — يُقال له أي
/// رقم اختلف وبكم.
/// </para>
/// <para>
/// <b>والحساب كله <c>decimal</c>.</b> قيس في هذا المستودع انحراف ضريبة عند الخانة
/// العشرية الرابعة من <c>double</c> واحد، والتقريب هنا على مستوى السطر ثم الجمع —
/// لا الجمع ثم التقريب — لأن القلب يُنتج فرق هللة على كل فاتورة تقريباً.
/// </para>
/// </summary>
public static class DraftReconciler
{
    /// <summary>خانتان: الهللة أصغر وحدة نقدية، والتقريب سياسة معلنة لا صدفة.</summary>
    public const int Halalas = 2;

    /// <summary>يقرّب إلى الهللة، والنصف يبتعد عن الصفر — نفس سياسة وحدة المشتريات.</summary>
    public static decimal Round(decimal value) => decimal.Round(value, Halalas, MidpointRounding.AwayFromZero);

    /// <summary>
    /// رتبة الثقة بالمصدر. تُستعمل لتسمية <b>المشتبه به</b> حين يختلف رقمان:
    /// المُصدَّق برمز موقَّع يغلب المقروء ضوئياً، لا العكس.
    /// </summary>
    /// <param name="provenance">المصدر.</param>
    public static int TrustRank(FieldProvenance provenance) => provenance switch
    {
        // ⚠ الأرقام <b>متباعدة عمداً</b>: الترتيب وحده هو المعنى، ولا يقارَن أيٌّ منها
        // بثابت. وحين أُضيف المصدر السادس (المنطوق) لم يكن بين «مقروء» و«مُستنتَج»
        // عددٌ صحيح شاغر في السلّم القديم 1..5، فكان لا بدّ من إعادة ترقيم كامل —
        // وهو ما يُغني عنه التباعد إلى الأبد.
        FieldProvenance.Attested => 50,
        FieldProvenance.Typed => 40,
        FieldProvenance.Defaulted => 30,
        FieldProvenance.Read => 20,
        FieldProvenance.Spoken => 15,
        FieldProvenance.Inferred => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "مصدر حقل غير معروف / unknown field provenance"),
    };

    /// <summary>
    /// يطابق المسوّدة حسابياً ويعيد ملاحظاتها مرتَّبة. القائمة الفارغة تعني: الحساب متّسق.
    /// </summary>
    /// <param name="draft">المسوّدة.</param>
    public static IReadOnlyList<ReconciliationFinding> Reconcile(CapturedInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        List<ReconciliationFinding> findings = [];

        // ── 1 · كل سطر: الكمية × السعر مقابل صافي السطر المقروء ────────────────
        foreach (CapturedInvoiceLine line in draft.Lines)
        {
            decimal extension = Round(line.Quantity.Value * line.UnitPrice.Value);
            if (extension != line.LineNet.Value)
            {
                findings.Add(LineExtension(line, extension));
            }
        }

        // ── 2 · مجموع السطور مقابل الصافي ──────────────────────────────────────
        decimal lineSum = Round(draft.Lines.Sum(static line => line.LineNet.Value));
        if (draft.Lines.Count > 0 && lineSum != draft.Net.Value)
        {
            string suspect = Weakest(
                (draft.Net.Provenance, CapturedInvoiceDraft.NetField),
                (FieldProvenance.Read, "lines"));

            findings.Add(new ReconciliationFinding
            {
                Code = "capture.line_sum_disagrees_with_net",
                SuspectField = suspect,
                Expected = lineSum,
                Observed = draft.Net.Value,
                Message = new LocalizedName(
                    "مجموع صوافي السطور " + Show(lineSum) + " والصافي المستخرَج " + Show(draft.Net.Value)
                    + " — الفرق " + Show(draft.Net.Value - lineSum) + "، والمشتبه به «" + suspect + "» لأنه أضعف الطرفين مصدراً.",
                    "The line net total is " + Show(lineSum) + " while the extracted net is " + Show(draft.Net.Value)
                    + " — a difference of " + Show(draft.Net.Value - lineSum) + "; the suspect is '" + suspect + "', the weaker-sourced side."),
            });
        }

        // ── 3 · الضريبة عند النسبة المُعلنة ────────────────────────────────────
        decimal taxAtRate = Round(draft.Net.Value * draft.TaxRate.Value);
        if (taxAtRate != draft.TaxTotal.Value)
        {
            string suspect = Weakest(
                (draft.Net.Provenance, CapturedInvoiceDraft.NetField),
                (draft.TaxRate.Provenance, CapturedInvoiceDraft.TaxRateField),
                (draft.TaxTotal.Provenance, CapturedInvoiceDraft.TaxTotalField));

            findings.Add(new ReconciliationFinding
            {
                Code = "capture.tax_disagrees_with_rate",
                SuspectField = suspect,
                Expected = taxAtRate,
                Observed = draft.TaxTotal.Value,
                Message = new LocalizedName(
                    "الضريبة عند النسبة " + ShowRate(draft.TaxRate.Value) + " على صافٍ " + Show(draft.Net.Value)
                    + " تساوي " + Show(taxAtRate) + "، والمستخرَج " + Show(draft.TaxTotal.Value)
                    + " — الفرق " + Show(draft.TaxTotal.Value - taxAtRate) + "، والمشتبه به «" + suspect + "» لأنه أضعف الأطراف مصدراً.",
                    "VAT at " + ShowRate(draft.TaxRate.Value) + " on a net of " + Show(draft.Net.Value)
                    + " is " + Show(taxAtRate) + " while the extracted amount is " + Show(draft.TaxTotal.Value)
                    + " — a difference of " + Show(draft.TaxTotal.Value - taxAtRate) + "; the suspect is '" + suspect + "', the weakest-sourced figure."),
            });
        }

        // ── 4 · الصافي + الضريبة مقابل الإجمالي ────────────────────────────────
        decimal computedGross = Round(draft.Net.Value + draft.TaxTotal.Value);
        if (computedGross != draft.GrossTotal.Value)
        {
            string suspect = Weakest(
                (draft.Net.Provenance, CapturedInvoiceDraft.NetField),
                (draft.TaxTotal.Provenance, CapturedInvoiceDraft.TaxTotalField),
                (draft.GrossTotal.Provenance, CapturedInvoiceDraft.GrossTotalField));

            findings.Add(new ReconciliationFinding
            {
                Code = "capture.net_plus_tax_disagrees_with_gross",
                SuspectField = suspect,
                Expected = computedGross,
                Observed = draft.GrossTotal.Value,
                Message = new LocalizedName(
                    "الصافي " + Show(draft.Net.Value) + " زائد الضريبة " + Show(draft.TaxTotal.Value)
                    + " يساوي " + Show(computedGross) + "، والإجمالي المستخرَج " + Show(draft.GrossTotal.Value)
                    + " — الفرق " + Show(draft.GrossTotal.Value - computedGross)
                    + "، والمشتبه به «" + suspect + "» لأنه أضعف الأطراف مصدراً.",
                    "Net " + Show(draft.Net.Value) + " plus VAT " + Show(draft.TaxTotal.Value)
                    + " is " + Show(computedGross) + " while the extracted gross is " + Show(draft.GrossTotal.Value)
                    + " — a difference of " + Show(draft.GrossTotal.Value - computedGross)
                    + "; the suspect is '" + suspect + "', the weakest-sourced figure."),
            });
        }

        return findings;
    }

    private static ReconciliationFinding LineExtension(CapturedInvoiceLine line, decimal extension) => new()
    {
        Code = "capture.line_extension_disagrees",
        SuspectField = FormattableString.Invariant($"line[{line.LineNo}].net"),
        Expected = extension,
        Observed = line.LineNet.Value,
        Message = new LocalizedName(
            "السطر " + Number(line.LineNo) + ": الكمية " + ShowQuantity(line.Quantity.Value)
            + " × السعر " + Show(line.UnitPrice.Value) + " تساوي " + Show(extension)
            + "، وصافي السطر المقروء " + Show(line.LineNet.Value)
            + " — الفرق " + Show(line.LineNet.Value - extension) + ".",
            "Line " + Number(line.LineNo) + ": quantity " + ShowQuantity(line.Quantity.Value)
            + " × unit price " + Show(line.UnitPrice.Value) + " is " + Show(extension)
            + " while the read line net is " + Show(line.LineNet.Value)
            + " — a difference of " + Show(line.LineNet.Value - extension) + "."),
    };

    /// <summary>
    /// المشتبه به هو <b>أضعف الأطراف مصدراً</b> بين الحقول الداخلة في المعادلة. إجماليٌّ
    /// مُصدَّق برمز موقَّع لا يُتَّهم بسبب رقم مقروء ضوئياً — والعكس هو ما يدفع الإنسان
    /// إلى تصحيح الرقم الصحيح ويترك الخطأ قائماً.
    /// <para>
    /// وعند التعادل يفوز <b>الأول في القائمة</b>، والقوائم مرتَّبة بالأرجحية. فالحكم حتمي
    /// ولا يتغيّر بين تشغيلين.
    /// </para>
    /// </summary>
    private static string Weakest(params (FieldProvenance Provenance, string Field)[] candidates)
    {
        (FieldProvenance Provenance, string Field) weakest = candidates[0];

        foreach ((FieldProvenance Provenance, string Field) candidate in candidates)
        {
            if (TrustRank(candidate.Provenance) < TrustRank(weakest.Provenance))
            {
                weakest = candidate;
            }
        }

        return weakest.Field;
    }

    private static string Show(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string ShowQuantity(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string ShowRate(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
