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
public sealed record SpokenSlotValue(
    string Name,
    string Text,
    string? Unit,
    string Heard,
    FieldProvenance Provenance);
