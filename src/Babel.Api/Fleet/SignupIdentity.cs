using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Api.Fleet;

/// <summary>
/// معرّفات التسجيل الأول، <b>مشتقّةً حتمياً من مفتاح الطلب</b>.
/// <para>
/// <b>وهذا هو شكل الحصانة ضد التكرار هنا</b>، ولم يُختَر تبسيطاً: البديل — جدولُ
/// «مفتاح الطلب ⇐ ما أُنشئ» — يحتاج كتابتين لا واحدة (قبل الإنشاء وبعده)، وانهيارٌ
/// بينهما يترك مفتاحاً بلا نتيجة، فتُنشئ الإعادةُ مستأجراً ثانياً وهي تظنّ أنها أول
/// محاولة. والاشتقاق الحتمي يُلغي النافذة كلّها: <b>المعرّفات هي المفتاح</b>، وكل
/// كتابة بعدها مُحكَمة بمفتاحها الفريد — رمز المستأجر في سجل الأسطول، و(المنشأة،
/// المستخدم) في جدول العضويات. فالإعادة تصل إلى الحالة نفسها مهما انقطعت.
/// </para>
/// <para>
/// <b>وما لا يكشفه هذا الاشتقاق:</b> جوابُ الباب لا يعتمد على وجود مستأجرٍ آخر ولا على
/// اسمه — الأسماء غير فريدة أصلاً ولا تُفحَص — بل على مفتاحٍ <b>يملكه مقدّمه وحده</b>.
/// ومن يريد أن يعرف بمفتاحٍ مخمَّن أن مستأجراً ما موجود عليه أن يخمّن قيمةً من فضاء
/// المفتاح كلّه؛ ولذلك يُشترَط طولٌ أدنى، ويُقال للعميل صراحةً أن يولّده عشوائياً.
/// </para>
/// <para>
/// <b>ولا سرّ في المفتاح ولا في المشتقّ منه:</b> المعرّفات تظهر في المسارات وفي
/// السجلّات، والاعتماد وحده سرٌّ — وهو يُسكّ في النواة ولا يُشتقّ من شيء.
/// </para>
/// </summary>
/// <param name="TenantId">معرّف المستأجر في سجل الأسطول.</param>
/// <param name="TenantCode">رمزه القصير <c>[a-z0-9_]</c> — يدخل في اسم قاعدة بياناته.</param>
/// <param name="CompanyId">معرّف أول منشأة له.</param>
/// <param name="OwnerId">معرّف أول مستخدم مالك فيها.</param>
internal readonly record struct SignupIdentity(Guid TenantId, string TenantCode, Guid CompanyId, Guid OwnerId)
{
    /// <summary>
    /// أدنى طول مقبول لمفتاح الطلب.
    /// <para>
    /// ‏<b>ستّة عشر محرفاً</b>: هو طول معرّف عشوائي معقول، وأقصر منه يجعل تخمين مفتاحٍ
    /// قائم ممكناً — وتخمينُه يعني قراءةَ معرّفات مستأجرٍ ليس لك. والحدّ يُقال في نصّ
    /// الرفض كي لا يخمّنه أحد.
    /// </para>
    /// </summary>
    public const int MinimumKeyLength = 16;

    /// <summary>أقصى طول مقبول — حدٌّ معلن يمنع تجزئة حمولة كبيرة.</summary>
    public const int MaximumKeyLength = 128;

    /// <summary>
    /// يشتقّ المعرّفات الأربعة من مفتاح الطلب.
    /// <para>
    /// أربع تجزئات مستقلّة بأربع بادئات مسمّاة — لا تجزئةٌ واحدة تُقتطع أربع مرّات:
    /// الأخيرة تجعل معرّفات الأربعة تتقاسم بايتات، فمن يعرف أحدها يقرّب الباقي.
    /// </para>
    /// </summary>
    /// <param name="requestKey">مفتاح الطلب كما قدّمه العميل.</param>
    /// <returns>المعرّفات المشتقّة.</returns>
    public static SignupIdentity Of(string requestKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestKey);

        Guid tenant = Derive("tenant", requestKey);

        return new SignupIdentity(
            tenant,
            CodeOf(tenant),
            Derive("company", requestKey),
            Derive("owner", requestKey));
    }

    /// <summary>
    /// رمز المستأجر مشتقّاً من معرّفه: حرفٌ ثابت ثم ستّ عشرة خانة ست عشرية صغيرة.
    /// <para>
    /// <b>ولا يختاره العميل</b>: الرمز يدخل في <b>اسم قاعدة بيانات</b>، ورمزٌ يكتبه
    /// مجهولٌ على باب مفتوح هو نصٌّ يصير معرّف SQL. و<c>Db.Ident</c> يرفض ما يخالف
    /// <c>[a-z0-9_]</c> فعلاً، لكن الحدّ الصحيح ألّا يصل نصّ العميل إلى هناك أصلاً.
    /// وهو أيضاً ما يمنع مستأجراً من أن يحجز رمز منافسه.
    /// </para>
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    private static string CodeOf(Guid tenantId) =>
        "t_" + Convert.ToHexString(tenantId.ToByteArray(bigEndian: true)).ToLowerInvariant()[..16];

    /// <summary>
    /// معرّف مشتقّ حتمياً: <c>SHA-256</c> على «البادئة ‏\n المفتاح»، وأول ستّة عشر بايتاً
    /// منه بترتيب البايتات الكبير، وبإصدار ‏8 (المخصّص في RFC 9562) ونوعه.
    /// </summary>
    /// <param name="label">بادئة المجال — تفصل معرّفات المفتاح الواحد بعضها عن بعض.</param>
    /// <param name="requestKey">مفتاح الطلب.</param>
    private static Guid Derive(string label, string requestKey)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"babel.signup.{label}\n{requestKey}")));

        byte[] bytes = digest[..16];

        // الإصدار 8: «معرّف مخصّص التوليد» — يقول للقارئ إن هذه البايتات مشتقّة لا
        // عشوائية ولا زمنية، فلا يُقرأ منها ترتيبٌ ولا لحظة إنشاء.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
