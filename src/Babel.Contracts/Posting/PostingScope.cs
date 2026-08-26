namespace Babel.Contracts.Posting;

/// <summary>
/// النطاق التحليلي للسطر: الفرع والمشروع و<b>مركز التكلفة</b>.
/// معرّفات مبهمة عمداً — Babel.Ledger يتحقق منها، والوحدة لا تعرف شجرتها.
/// <para>
/// <b>ومركز التكلفة ليس اختيارياً هنا، ولا يمكن أن يكون.</b> ‏ADR-0026 يقرّر أن لكل منشأة
/// مركز تكلفة واحداً على الأقل وأن <c>CostCenterId</c> لا يكون فارغاً في أي موضع. وكان
/// النوع يقول <c>string?</c> بينما القرار يقول «لا يكون فارغاً» — أي أن <b>الموضع الذي
/// يُجيب عن السؤال غير الموضع الذي يُعلنه</b>، وهو صنف العطب نفسه الذي بُذل في هذا
/// المستودع أسابيع في إزالته.
/// </para>
/// <para>
/// <b>ولماذا سجلٌّ مرجعي لا <c>record struct</c>:</b> لأن البنية لها <c>default</c> دائماً،
/// ولا مُنشئ يمنعها. فـ<c>default(PostingScope)</c> كان يُعيد تركيب «نطاقٌ بلا مركز
/// تكلفة» مهما شُدِّد المُنشئ — أي أن الثابتة تبقى <b>وعداً</b>. والسجلّ المرجعي بمُنشئ
/// واحدٍ يرمي على الفراغ يجعلها <b>شرط وجودٍ للنوع</b>، ويجعل <see cref="PostingLine.Scope"/>
/// حقلاً <c>required</c> لا يُنسى.
/// </para>
/// <para>
/// <b>وما حلّ محلّ <c>PostingScope.None</c>:</b> <see cref="On"/>. و«لا نطاق» لم يكن
/// حالةً مشروعة في المجال أصلاً — كان اسماً لطيفاً لـ«سطرٌ بلا مركز تكلفة»، وهو ما
/// يمنعه القرار. وأفقرُ نطاقٍ مشروع هو الذي <b>يسمّي مركز التكلفة وحده</b>، والفرع
/// والمشروع غائبان لأنهما فعلاً اختياريان. فالاسم صار يصف ما يمثّله بدقّة: «على هذا
/// المركز» لا «بلا شيء».
/// </para>
/// </summary>
public sealed record PostingScope
{
    /// <summary>ينشئ نطاقاً تحليلياً بمركز تكلفة مُحلّ.</summary>
    /// <param name="costCenterId">مركز التكلفة — <b>مُحلٌّ قبل الوصول إلى هنا</b>، وغير فارغ.</param>
    /// <param name="branchId">الفرع، أو <c>null</c> — اختياري فعلاً.</param>
    /// <param name="projectId">المشروع، أو <c>null</c> — اختياري فعلاً.</param>
    /// <exception cref="ArgumentException">
    /// إن كان مركز التكلفة فارغاً أو خواءً. <b>والرمي هنا مقصود ولا يُستبدل بـ<c>Result</c></b>:
    /// بلوغُ هذه الحالة يعني أن بوّابةً بنت طلباً بلا حلّ مركز التكلفة، وهو <b>خلل برمجي</b>
    /// لا رفضٌ متوقَّع من مستخدم. والرفض المتوقَّع يقع قبل ذلك، في الحلّ نفسه، برمزه المكتوب.
    /// </exception>
    public PostingScope(string costCenterId, string? branchId = null, string? projectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterId);

        CostCenterId = costCenterId.Trim();
        BranchId = Clean(branchId);
        ProjectId = Clean(projectId);
    }

    /// <summary>مركز التكلفة. <b>غير فارغ دائماً بحكم بناء النوع</b> (ADR-0026).</summary>
    public string CostCenterId { get; }

    /// <summary>الفرع، أو <c>null</c>.</summary>
    public string? BranchId { get; }

    /// <summary>المشروع، أو <c>null</c>.</summary>
    public string? ProjectId { get; }

    /// <summary>
    /// أفقر نطاق مشروع: مركز التكلفة وحده، بلا فرع ولا مشروع.
    /// <b>هذا ما حلّ محلّ <c>None</c></b> — انظر شرح النوع.
    /// </summary>
    /// <param name="costCenterId">مركز التكلفة المُحلّ.</param>
    public static PostingScope On(string costCenterId) => new(costCenterId);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
