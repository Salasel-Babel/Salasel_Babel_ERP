using Babel.Contracts.Capture;

namespace Babel.Ai.Voice;

/// <summary>
/// قيمةُ شريحةٍ كما خرجت من الكلام. <b>نصّ دائماً</b> — المال نصّ، والكمّية نصّ،
/// ولا عائمة في هذا المسار كلّه.
/// </summary>
/// <param name="Name">اسم الشريحة.</param>
/// <param name="Text">القيمة نصّاً.</param>
/// <param name="Unit">رمز الوحدة حين تكون الشريحة كمّية، وفارغ فيما عداها.</param>
/// <param name="Heard">المقطع من الكلام الذي أنتج القيمة — يُعرض كي يرى الإنسان <b>لماذا</b>.</param>
/// <param name="Provenance">
/// المصدر — <see cref="FieldProvenance.Spoken"/> لما نُطق، و<see cref="FieldProvenance.Defaulted"/>
/// لما جاء من الإعدادات (‏ADR-0030).
/// </param>
/// <param name="Dropped">
/// <b>الذيل الذي قُصّ عند كاسر مقطع</b> — أي ما كان قارئٌ بلا كواسرِ مقاطع سيبتلعه في
/// هذه القيمة. وفارغٌ حين لم يقع قصٌّ من هذا النوع.
/// <para>
/// <b>ولماذا يُحمَل بدل أن يُطرح:</b> قصُّ الاسم عند أداة شرطٍ يُصلح الحالة الغالبة
/// ويُخطئ في اسمٍ مشروع يحمل «لو» أو «الا». <b>والقصّ الصامت أسوأ من القصّ الخاطئ</b>:
/// الخاطئُ المعروض يُرى ويُصحَّح، والصامت يُوقَّع عليه. فيُعرض بجانب الحقل نصّاً —
/// «وسقط: …» — ويرى الإنسان <b>لماذا</b> صار الاسم اسمين.
/// </para>
/// </param>
public sealed record SpokenSlotValue(
    string Name,
    string Text,
    string? Unit,
    string Heard,
    FieldProvenance Provenance,
    string? Dropped = null);
