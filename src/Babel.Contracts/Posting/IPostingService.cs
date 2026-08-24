namespace Babel.Contracts.Posting;

/// <summary>
/// محرك الترحيل. الجهة الوحيدة التي تكتب قيداً في النظام كله
/// (وثيقة المعمارية §12 الحدّ 4 · CONTRIBUTING §3 بند 1).
/// <para>
/// الواجهة هنا وليست في Babel.Ledger عمداً: الوحدات الأفقية تعتمد على العقد
/// ولا تعتمد على مشروع الدفتر إطلاقاً. لا مرجع = لا احتمال كتابة مباشرة.
/// </para>
/// </summary>
public interface IPostingService
{
    /// <summary>يرحّل طلباً. حصين ضد التكرار بمفتاح <see cref="PostingRequest.IdempotencyKey"/>.</summary>
    /// <param name="request">طلب الترحيل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<SharedKernel.Result<PostingReceipt>> PostAsync(PostingRequest request, CancellationToken cancellationToken = default);
}
