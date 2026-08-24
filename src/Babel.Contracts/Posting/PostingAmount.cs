using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// مبلغ مُسمّى في مفردات الحدث (‏<c>net</c>، <c>tax</c>، <c>period_share</c> …).
/// <para>
/// الوحدة تُسلّم المبالغ بأسمائها المحاسبية، والمصفوفة هي التي تقرّر أي تعبير خطي
/// يذهب إلى أي سطر. ولذلك لا تحتاج الوحدة أن تعرف كم سطراً سيُولَّد ولا ترتيبها.
/// </para>
/// </summary>
/// <param name="Name">اسم المبلغ كما هو معرَّف في <c>amounts</c> على الحدث.</param>
/// <param name="Value">القيمة — <see cref="Money"/> يفرض <c>decimal</c> ومقياس 4.</param>
public sealed record PostingAmount(string Name, Money Value);
