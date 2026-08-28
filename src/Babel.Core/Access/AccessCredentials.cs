using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Core.Access;

/// <summary>
/// سكّ الاعتمادات وبصمها — <b>الموضع الوحيد الذي يعرف فيه شيءٌ نصّ اعتماد</b>.
/// <para>
/// وكل ما تحت هذا النوع — الخدمة، والدليل، والمخزن، والجدول — لا يرى إلا بصمة. فليس في
/// المستودع ولا في قاعدة البيانات ولا في أي إعداد قيمةٌ تصلح للانتحال، ومن يقرأ نسخة
/// احتياطية لا ينتحل بها أحداً.
/// </para>
/// </summary>
public static class AccessCredentials
{
    /// <summary>
    /// يسكّ اعتماداً جديداً: <b>عشوائيةٌ قوية تشفيرياً</b> بـ<see cref="AccessLimits.CredentialBytes"/>
    /// بايتاً، مُرمَّزةً <c>base64url</c> بلا حشو — فلا محرف فيه يحتاج هروباً في ترويسة ولا في عنوان.
    /// </summary>
    public static string Mint() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(AccessLimits.CredentialBytes));

    /// <summary>
    /// بصمة الاعتماد: <c>SHA-256</c> بترميز سداسي عشري صغير، بلا ثقافة ولا حالة أحرف متغيّرة.
    /// <para>
    /// وهي الصيغة نفسها التي يستعملها دليل الاعتمادات المُهيَّأ من الإعداد
    /// (<c>Babel.Api.Security.ConfiguredPrincipalResolver.Digest</c>) — صيغةٌ واحدة لبصمةٍ
    /// واحدة، فلا يوجد اعتمادٌ يُقبل في أحد الدليلين ويُرفض في الآخر لفرق ترميز.
    /// </para>
    /// </summary>
    /// <param name="credential">النصّ المُقدَّم.</param>
    public static string Digest(string credential) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));

    /// <summary>
    /// مقارنة بصمتين <b>بزمن ثابت</b>.
    /// <para>
    /// <b>ولماذا هذا مطلوب رغم أن المُقارَن بصمةٌ لا سرّ:</b> لأن أي مقارنة تنتهي عند أول
    /// اختلاف تُسرّب طولَ البادئة المتطابقة، والقاعدة «لا تُقارَن اعتمادات بمشغّل المساواة»
    /// قاعدةٌ لا يجوز أن تحتاج إلى استثناء يُقرأ بعد سنتين فيُقلَّد في موضعٍ لا يصحّ فيه.
    /// وفي مسار PostgreSQL البحث بالمفتاح على بصمة — لا مقارنةَ سرٍّ أصلاً — وهذا مكتوب
    /// في القرار كي لا يُقرأ غيابُ المقارنة هنا نسياناً.
    /// </para>
    /// </summary>
    /// <param name="left">بصمة.</param>
    /// <param name="right">بصمة أخرى.</param>
    public static bool DigestsMatch(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }
}
