using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// الفصل الذي تدور عليه القاعدة الثالثة في 02-architecture §11.3:
/// <b>القيد يُرحَّل، والمستند يُعزل</b> عن أعمار الذمم وعن الإقرار الضريبي
/// ما دامت المقاصة معلّقة أو مرفوضة.
/// <para/>
/// لاحظ أن هذا النوع <b>لا يعرف شيئاً عن دفتر الأستاذ</b> ولا يستطيع تعديله.
/// كل ما يفعله أنه يجيب على سؤالين تقرأهما وحدات أخرى.
/// </summary>
public readonly record struct FiscalInclusion(
    bool JournalEntryPosted,
    bool IncludeInVatReturn,
    bool IncludeInReceivablesAging,
    string ReasonAr,
    string ReasonEn)
{
    public bool IsQuarantined => !IncludeInVatReturn || !IncludeInReceivablesAging;
}

public static class FiscalInclusionEvaluator
{
    public static FiscalInclusion Evaluate(ComplianceRecord record, FiscalInclusionPolicy policy)
    {
        // القيد مُرحَّل. هذه القيمة ثابتة، ولا حالة التزام تغيّرها.
        const bool posted = true;

        if (record.IsAccepted)
            return new FiscalInclusion(posted, true, true,
                "مقبولة من الجهة — تدخل الإقرار وأعمار الذمم",
                "accepted by the authority — included in the VAT return and in AR aging");

        if (record.Status == ComplianceStatus.Rejected)
        {
            var include = !policy.QuarantineRejectedForever;
            return new FiscalInclusion(posted, include, include,
                "مرفوضة — القيد قائم كما هو، والمستند معزول عن الإقرار وعن أعمار الذمم حتى يصدر مستند تصحيحي",
                "rejected — the journal entry stands untouched; the document is quarantined from the VAT return and AR aging until a corrective document is issued");
        }

        var quarantine = record.Flow == ComplianceFlow.Clearance
            ? policy.QuarantineClearanceUntilAccepted
            : policy.QuarantineReportingUntilAcknowledged;

        return new FiscalInclusion(posted, !quarantine, !quarantine,
            $"حالة الالتزام «{ComplianceStatusText.Ar(record.Status)}» — القيد مُرحَّل، والمستند معزول حتى الحسم",
            $"compliance status '{ComplianceStatusText.En(record.Status)}' — the journal entry is posted; the document is quarantined until settled");
    }
}
