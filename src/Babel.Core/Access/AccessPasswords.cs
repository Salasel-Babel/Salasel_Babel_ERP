using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Core.Access;

/// <summary>
/// كلمةُ المرور كعاملِ إثباتٍ أوّل — <b>فوق محرّك الجلسات نفسه لا بديلاً عنه</b>.
/// <para>
/// <b>ولماذا أُضيفت بعد أن رفض <see cref="AccessService"/> فكرةَ كلمة المرور أصلاً:</b>
/// ‏ADR-0045 رفض أن يكون <b>الاعتمادُ</b> كلمةَ مرور، وذلك الرفض قائم كما هو: ما يُقدَّم
/// في كل طلب يبقى مفتاحاً مبهماً قصيرَ العمر يدور ويُبطَل. وما يُضاف هنا شيءٌ آخر —
/// <b>عاملُ إثباتٍ أوّل يُنتج جلسةً ثم ينصرف</b>. فلا يُحمل في ترويسة، ولا يعبر وسيطاً،
/// ولا يُقدَّم إلا على بابٍ واحد. والإبطالُ الفوريّ والتدويرُ وكشفُ إعادة الاستعمال تبقى
/// كلُّها على الجلسة كما كانت — وهي الأشياء التي لا يُتنازل عنها.
/// </para>
/// <para>
/// <b>ولا خوارزميةَ مُودَعةٌ في صفٍّ واحد:</b> الصفُّ يحمل وصفَ اشتقاقه كاملاً
/// (‏<c>pbkdf2-sha256$التكرارات$الملح$البصمة</c>)، فرفعُ التكرارات غداً يعمل على
/// الصفوف الجديدة ويقرأ القديمة كما هي — ولا هجرةَ بيانات تُطلب لتشديد أمنٍ.
/// </para>
/// <para>
/// <b>واختيار PBKDF2 مكتوبٌ لا مسكوتٌ عنه:</b> Argon2id أفضلُ نظرياً، وهو حزمةٌ خارجية
/// تدخل شجرةَ الاعتماديات لأجل دالّةٍ واحدة. و<c>Rfc2898DeriveBytes</c> في المكتبة
/// القياسية، وستّمئةُ ألف تكرارٍ بـSHA-256 هي توصيةُ OWASP المعلنة. والوصفُ في الصفّ
/// يجعل الانتقالَ إلى Argon2id يوماً ما إضافةَ فرعٍ في <see cref="Verify"/> لا هجرة.
/// </para>
/// </summary>
public static class AccessPasswords
{
    /// <summary>اسم الاشتقاق كما يُكتب في الصفّ.</summary>
    public const string Algorithm = "pbkdf2-sha256";

    /// <summary>
    /// شكلُ الإثبات كتعبيرٍ نمطيّ — <b>ويقرؤه قيدُ قاعدة البيانات نفسه</b>.
    /// <para>
    /// ونسخةٌ ثانية منه في ملفّ الخريطة كانت ستنحرف عن هذه بعد تغييرٍ واحد،
    /// فيقبل المخزنُ ما ترفضه الشيفرة أو العكس. والمصدرُ هنا واحد.
    /// </para>
    /// </summary>
    public const string ProofPattern = "^[a-z0-9-]+\\$[0-9]+\\$[A-Za-z0-9+/=]+\\$[A-Za-z0-9+/=]+$";

    /// <summary>عددُ التكرارات للصفوف الجديدة — توصيةُ OWASP لـPBKDF2-HMAC-SHA256.</summary>
    public const int Iterations = 600_000;

    /// <summary>طولُ الملح بالبايتات.</summary>
    public const int SaltBytes = 16;

    /// <summary>طولُ المفتاح المشتقّ بالبايتات.</summary>
    public const int HashBytes = 32;

    /// <summary>
    /// أقصرُ كلمةِ مرورٍ تُقبل.
    /// <para>
    /// اثنا عشر محرفاً، <b>ولا قواعدَ تركيب</b> (حرفٌ كبير ورقمٌ ورمز): ‏NIST 800-63B
    /// يقول إنها تدفع الناسَ إلى أنماطٍ متوقَّعة — <c>Password1!</c> — فتُنقص العشوائية
    /// الفعلية وتزيد النسيان. والطولُ وحده هو ما يُقاس.
    /// </para>
    /// </summary>
    public const int MinimumPasswordLength = 12;

    /// <summary>
    /// أطولُ كلمةِ مرورٍ تُقبل — <b>حدٌّ تشغيليّ لا أمنيّ</b>: الاشتقاق يُكلّف زمناً
    /// بالطول، وحمولةٌ ضخمة على بابٍ بلا اعتماد تصير بابَ إنهاكٍ لا بابَ دخول.
    /// </summary>
    public const int MaximumPasswordLength = 256;

    /// <summary>أطولُ معرّفِ دخولٍ يُقبل — حدُّ RFC 5321 لصندوق البريد.</summary>
    public const int MaximumHandleLength = 254;

    /// <summary>أقصرُ معرّفِ دخولٍ ذي معنى: حرفٌ و<c>@</c> وحرف.</summary>
    public const int MinimumHandleLength = 3;

    /// <summary>
    /// يُسوّي معرّفَ الدخول: يُشذّب الفراغ ويُصغّر الحروف بثقافةٍ ثابتة.
    /// <para>
    /// <b>والتسويةُ هنا لا في قاعدة البيانات</b>: عمودٌ يُسوّي بنفسه (‏<c>citext</c>)
    /// يجعل التسويةَ حكمَ إضافةٍ في محرّك التخزين، فتختلف عن حكم البحث في الذاكرة —
    /// ويصير «‏Ali@x.sa» مستخدماً ثانياً في تنفيذٍ وواحداً في آخر. والمُسوَّى يُكتب
    /// ويُقرأ، فلا موضعان يختلفان.
    /// </para>
    /// <para>
    /// <b>ولا تُمسّ الحروفُ غيرُ اللاتينية بتصغيرٍ ثقافيّ</b>: <c>ToLowerInvariant</c>
    /// لا يعرف تركيّةَ النقطة، فلا يصير <c>I</c> حرفاً آخر بحسب لغة الخادم.
    /// </para>
    /// </summary>
    /// <param name="handle">ما كتبه المستخدم.</param>
    public static string Normalise(string? handle) =>
        (handle ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// هل لهذا المعرّف شكلُ بريدٍ مقبول؟ <b>فحصُ شكلٍ لا فحصُ وجود.</b>
    /// <para>
    /// ولا نحوَ RFC 5322 كاملاً هنا بقصد: تطبيقُه الكامل يقبل ما لا يقبله أي مزوّد
    /// بريدٍ في الدنيا، ويردّ عناوينَ صحيحة حين يُكتب بيدٍ خطأً. والمقصود منعُ الفراغ
    /// والمسافات وغيابِ <c>@</c> — وما بعد ذلك يُثبته إرسالُ رسالةٍ لا تعبيرٌ نمطي.
    /// </para>
    /// </summary>
    /// <param name="normalised">المعرّف بعد التسوية.</param>
    public static bool HasHandleShape(string normalised)
    {
        if (normalised.Length is < MinimumHandleLength or > MaximumHandleLength)
        {
            return false;
        }

        int at = normalised.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != normalised.LastIndexOf('@') || at == normalised.Length - 1)
        {
            return false;
        }

        foreach (char c in normalised)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>هل طولُ كلمة المرور داخل الحدّين؟</summary>
    /// <param name="password">ما كتبه المستخدم، بلا تشذيب — الفراغُ محرفٌ معتبَر.</param>
    public static bool HasPasswordLength(string? password) =>
        password is not null
        && password.Length >= MinimumPasswordLength
        && password.Length <= MaximumPasswordLength;

    /// <summary>
    /// يشتقّ إثباتاً جديداً بملحٍ عشوائيّ.
    /// </summary>
    /// <param name="password">كلمة المرور.</param>
    public static string Derive(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    /// <summary>
    /// يتحقّق من كلمة مرورٍ مقابل إثباتٍ مُودَع، <b>بمقارنةٍ ثابتة الزمن</b>.
    /// <para>
    /// وإثباتٌ مشوَّه يُقرأ <c>false</c> ولا يرمي: صفٌّ تالفٌ في جدول وصولٍ يجب أن
    /// يمنع الدخول لا أن يُسقط الخادمَ بخمسمئة — فيبقى البابُ مغلقاً ويبقى الخادم قائماً.
    /// </para>
    /// </summary>
    /// <param name="password">ما قُدِّم.</param>
    /// <param name="proof">الإثبات المُودَع.</param>
    public static bool Verify(string password, string proof)
    {
        string[] parts = proof.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// يُنفق زمنَ اشتقاقٍ كاملاً ثم يردّ <c>false</c> — <b>لمعرّفٍ لا صفَّ له</b>.
    /// <para>
    /// <b>وهذا ليس احتياطاً نظرياً:</b> بابٌ يردّ في ميلي‌ثانيةٍ على بريدٍ غير مسجَّل
    /// وفي ستّمئةِ ألفِ تكرارٍ على بريدٍ مسجَّل <b>يُعلن قائمةَ عملائك</b> لمن يقيس
    /// الزمن. والردّان يجب أن يتساويا زمناً كما يتساويان نصّاً.
    /// </para>
    /// </summary>
    /// <param name="password">ما قُدِّم — يُشتقّ فعلاً ولا يُهمَل، وإلّا حُذف الفرع بالتحسين.</param>
    public static bool WasteEqualTime(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        /* المقارنةُ مع الملح نفسه: تستهلك النتيجة فلا يُسقطها المُحسِّن، وتردّ false دائماً
           (الأطوال تتساوى والمحتوى لا يتساوى إلا باحتمالٍ لا يقع). */
        return CryptographicOperations.FixedTimeEquals(derived, RandomNumberGenerator.GetBytes(HashBytes));
    }
}
