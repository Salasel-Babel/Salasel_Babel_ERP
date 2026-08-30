using Babel.SharedKernel;

namespace Babel.Hr.Subledger;

/// <summary>سبب انحراف مستند بين دفتر الموظف المساعد ونقطة الضبط.</summary>
public static class DivergenceReason
{
    /// <summary>الوحدة تعدّه مُرحَّلاً ولا حركة له في نقطة الضبط.</summary>
    public const string MissingInControl = "missing_in_control";

    /// <summary>حركة في نقطة الضبط بلا مستند مقابل في الوحدة.</summary>
    public const string MissingInSubledger = "missing_in_subledger";

    /// <summary>المستند موجود على الطرفين والمبلغان مختلفان.</summary>
    public const string AmountMismatch = "amount_mismatch";

    /// <summary>محاولة ترحيل عالقة: سُجّلت النية ولم يُعرف مصيرها بعد.</summary>
    public const string PostingUnresolved = "posting_unresolved";
}

/// <summary>سطر انحراف واحد — <b>بحبيبيّة المستند والطرف معاً</b>.</summary>
/// <param name="DocumentType">نوع المستند كما أرسلته الوحدة.</param>
/// <param name="DocumentId">معرّفه — وهو معرّف القسيمة على قيود الاستحقاق.</param>
/// <param name="PartyId">الطرف: الرمز المعتم للموظف.</param>
/// <param name="SubledgerEffect">أثره كما تعرفه الوحدة.</param>
/// <param name="ControlEffect">أثره كما هو في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
public sealed record EmployeeReconciliationDivergence(
    string DocumentType,
    string DocumentId,
    string PartyId,
    Money SubledgerEffect,
    Money ControlEffect,
    Money Divergence,
    string ReasonCode);

/// <summary>
/// تقرير مطابقة دفتر الموظف — <b>مستنداً بمستند، لا صافياً بصافٍ</b>.
/// <para>
/// <b>ولاحظ ما ليس في هذا النوع: لا رقم واحد اسمه «رصيد الموظف».</b> ‏
/// <c>IControlPointReader</c> يجمّع بلا تفصيل بالحساب ويعيد <c>Net</c> واحداً، ودفتر
/// الموظف يمتدّ على <b>أصلٍ واحد وثلاثة خصوم</b> (سلفة · راتب مستحق · استقطاع محتجَز ·
/// مخصص نهاية خدمة). فصافٍ واحد يقاصّ سلفةً بمخصص خدمة براتب مستحق، <b>ويعلن التطابق
/// وهو أعمى</b>: انحرافان متقابلان يُلغيان بعضهما.
/// </para>
/// <para>
/// ولذلك يُطابَق كل <c>ControlPointMovement</c> بأثر مستنده في جدول الوحدة
/// <b>بالحبيبيّة نفسها على الطرفين</b>، ويُنشر ما اختلف وحده. وسؤال «كم على هذا الموظف
/// من سلفة؟» يُجاب من جداول الوحدة لا من الدفتر.
/// </para>
/// </summary>
/// <param name="AsOf">تاريخ المطابقة.</param>
/// <param name="MatchedDocuments">عدد المستندات التي تطابق طرفاها بالضبط.</param>
/// <param name="IsReconciled">هل خلا التقرير من أي انحراف؟ لا «قريب من الصفر».</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق، مرتَّبة ترتيباً ثابتاً.</param>
public sealed record EmployeeReconciliationReport(
    DateOnly AsOf,
    int MatchedDocuments,
    bool IsReconciled,
    IReadOnlyList<EmployeeReconciliationDivergence> Divergences);
