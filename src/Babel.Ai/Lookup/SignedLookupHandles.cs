using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Babel.SharedKernel;

namespace Babel.Ai.Lookup;

/// <summary>غرض المِقبض. <b>داخل البايتات الموقَّعة</b>، فلا يُقرأ معرّفُ سؤالٍ كِياناً.</summary>
public enum LookupHandlePurpose : byte
{
    /// <summary>كِيانٌ محلول — يُفكّ إلى معرّف صفٍّ يُسكب في جسم <c>draft…</c> ولا يُعرض.</summary>
    Entity = 1,

    /// <summary>ورقة سؤال — لا تُفكّ إلى كِيان أبداً.</summary>
    Question = 2,

    /// <summary>
    /// خيارٌ واحد على ورقة سؤال — <b>يُفتدى إلى كِيانٍ قائم</b>. رمزٌ لكل خيار كي
    /// لا يعبر <b>موضعُ</b> الخيار: من يرى <c>{"choice":3}</c> يعلم أن الخيارات كانت
    /// أربعةً على الأقل، وثلاثُ محاولاتٍ بأسماءٍ متدرّجة تمسح السجلّ.
    /// </summary>
    Option = 3,

    /// <summary>
    /// خيار «جديد» — <b>لا يُفتدى إلى كِيانٍ قائم أبداً</b>، بل يفتح ورقة إنشاء.
    /// وفصلُه عن <see cref="Option"/> هو الفرق بين «اخترتُ هذا» و«لا شيء منها».
    /// </summary>
    CreateSheet = 4,
}

/// <summary>مِقبضٌ فُكّ بعد أن تحقّق توقيعه ونطاقه.</summary>
/// <param name="Purpose">الغرض كما كُتب داخل التوقيع.</param>
/// <param name="Tenant">المنشأة.</param>
/// <param name="CompanyId">الشركة.</param>
/// <param name="SessionId">الجلسة التي أُصدر فيها.</param>
/// <param name="Subject">الموضوع: معرّف الصفّ أو معرّف الورقة.</param>
/// <param name="ExpiresAt">لحظة الانتهاء.</param>
public sealed record RedeemedLookupHandle(
    LookupHandlePurpose Purpose,
    TenantId Tenant,
    Guid CompanyId,
    Guid SessionId,
    Guid Subject,
    DateTimeOffset ExpiresAt);

/// <summary>
/// <b>مصدِر المقابض ومُستردّها.</b> المِقبض سلطةُ <b>تسمية</b> لا سلطةُ <b>فعل</b>:
/// فكُّه يعطي معرّفاً يُكتب في جسم مسوّدة، ويبقى فحص الاستحقاق في الوحدة المالكة قائماً بعده.
/// </summary>
public interface ILookupHandles
{
    /// <summary>يُصدر مِقبضاً.</summary>
    /// <param name="purpose">الغرض.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="companyId">الشركة.</param>
    /// <param name="sessionId">الجلسة.</param>
    /// <param name="subject">الموضوع.</param>
    /// <param name="lifetime">المدّة. تجاوز السقف يُرفض ولا يُقصّ.</param>
    Result<string> Issue(
        LookupHandlePurpose purpose,
        TenantId tenant,
        Guid companyId,
        Guid sessionId,
        Guid subject,
        TimeSpan lifetime);

    /// <summary>يفكّ مِقبضاً ويقارن غرضه ونطاقه بنطاق الجلسة.</summary>
    /// <param name="token">المِقبض كما ورد.</param>
    /// <param name="expected">الغرض المطلوب.</param>
    /// <param name="tenant">منشأة <b>الجلسة</b> — لا منشأة المِقبض.</param>
    /// <param name="companyId">شركة <b>الجلسة</b>.</param>
    /// <param name="sessionId">معرّف <b>الجلسة</b>.</param>
    Result<RedeemedLookupHandle> Redeem(
        string token,
        LookupHandlePurpose expected,
        TenantId tenant,
        Guid companyId,
        Guid sessionId);
}

/// <summary>
/// <b>مقابض <u>معمّاة</u> ثم موقَّعة، بلا حالة — على منوال <c>Babel.Storage.SignedAttachmentTickets</c>
/// في هيكله، وأشدّ منه في محتواه.</b>
/// <para>
/// <b>ولماذا التعمية لا التوقيع وحده — وهو العطل الذي كان هنا:</b> كان المِقبض
/// ‏<c>payload ‖ HMAC</c>، و<c>payload</c> <b>نصٌّ صريح</b>. وفكُّ base64url بلا مفتاحٍ
/// واحد كان يُخرج المنشأة والشركة والجلسة <b>ومعرّف صفّ العميل بعينه</b> — مقيس. والتوقيع
/// يُثبت أن البايتات لم تُبدَّل؛ <b>ولا يُخفيها</b>. وقاعدة المالك أن ما شكلُه معرّف لا
/// يعبر إلى النموذج، وADR هذا المسار يعد بـ«نعم/لا ومِقبضٍ <b>معتم</b>» — فكان الوعد
/// مكسوراً في كل حلٍّ ناجح، وبصورةٍ يقرؤها كل من يملك نسخة المحادثة.
/// </para>
/// <para>
/// <b>فصار: عمِّ ثم وثِّق</b> (‏AES-256-GCM بمفتاحٍ مشتقّ بـHKDF، ورقم النسخة بياناً
/// مُوثَّقاً مصاحباً). والحقول كلّها داخل المُعمّى، والمِقبض من خارجه <b>عشوائيٌّ محض</b>:
/// لا يُقرأ منه غرضٌ ولا منشأةٌ ولا صفّ، ولا يُقارَن مِقبضان ليُعرف أنهما لصفٍّ واحد
/// (‏nonce عشوائي لكل إصدار).
/// </para>
/// <para>
/// <b>والحقول داخل البايتات المُوثَّقة لا بجانبها</b> — نفس درس فخ-22 الذي يضع
/// <c>prev_hash</c> داخل بايتات القيد لا في عمودٍ مجاور: حقلٌ خارج التوقيع يُبدَّل بلا أن
/// يبطل التوقيع. فالمنشأة والشركة والجلسة والغرض ولحظةُ الانتهاء كلّها موقَّعة.
/// </para>
/// <para>
/// <b>وطبقتان مستقلّتان تمنعان العبور بين المنشآت، لا واحدةٌ مكرَّرة:</b>
/// ‏(١) الاسترداد يقارن ما في البايتات بمنشأة <b>الجلسة</b> وشركتها وجلستها فيرفض عند
/// الاختلاف؛ ‏(٢) ثم يُنادى سجلّ الوحدة المالكة بمنشأة <b>الجلسة</b> — والمنشأة جزءٌ من
/// المفتاح هناك، فلا يوجد الصفّ أصلاً. فمِقبضٌ من منشأةٍ أُخرى يسقط عند (١)، ولو سقطت (١)
/// سهواً لسقط عند (٢).
/// </para>
/// <para>
/// <b>وطول المِقبض ثابتٌ دائماً</b> — <see cref="TokenLength"/> محرفاً — فلا يُقاس منه شيء.
/// </para>
/// <para>
/// <b>ويُكتب مجموعاتٍ من ثمانية يفصلها <c>~</c> — وذلك حارسٌ لا زينة.</b> المِقبض نصٌّ
/// عشوائيّ يعبر المِصفاة الخارجة مثل أي نصّ، و<c>base64url</c> عُشرُ أبجديته خانات:
/// فمِقبضٌ من كل ‎مئة ألفٍ تقريباً كان يحمل صدفةً تسع خاناتٍ متتالية فتُرفض دورةٌ سليمة
/// ‏<b>مرّةً لا تتكرّر ولا تُشخَّص</b> (وهو فخٌّ مسجَّل في هذا المستودع باسمه). و<c>~</c>
/// ليست فاصلاً عند أي درجةٍ من درجات اللمّ، فلا يتجاوز أي مسارٍ رقميّ في المِقبض
/// <b>ثماني خانات</b> — دون الشامل (تسع) ودون الهوية (عشر) ودون الضريبي (خمس عشرة)،
/// وتعذُّرُ الآيبان أصلاً لأنه يطلب اثنتين وعشرين خانة متّصلة. <b>فالاستحالة بنيويّة
/// لا احتمالية.</b>
/// </para>
/// <para>
/// <b>وما لا يفعله:</b> لا إبطال قبل الانتهاء — لا قائمة ولا صفّ. وذلك ثمن كونه بلا حالة،
/// ولذلك المدّة عشر دقائق.
/// </para>
/// </summary>
public sealed class SignedLookupHandles : ILookupHandles
{
    /// <summary>أدنى طول مقبول لمفتاح التوقيع: 32 بايتاً.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>طول المِقبض بالمحارف — ثابتٌ لكل مِقبض مهما كان غرضه أو موضوعه.</summary>
    public const int TokenLength = 152;

    /// <summary>حجم المجموعة الواحدة بالمحارف قبل الفاصل.</summary>
    public const int GroupLength = 8;

    /// <summary>فاصل المجموعات — <b>ليس فاصلاً عند المِصفاة عند أي درجة لمّ</b>.</summary>
    public const char GroupSeparator = '~';

    private const byte Version = 2;

    /// <summary>غرض + منشأة + شركة + جلسة + موضوع + انتهاء — <b>كلّها مُعمّاة</b>.</summary>
    private const int PayloadBytes = 1 + 16 + 16 + 16 + 16 + 8;

    private const int NonceBytes = 12;

    private const int TagBytes = 16;

    /// <summary>نسخة (صريحة ومُوثَّقة) + nonce + المُعمّى + العلامة.</summary>
    private const int TokenBytes = 1 + NonceBytes + PayloadBytes + TagBytes;

    private static readonly byte[] EncryptionInfo = "babel.agent.lookup.handle.v2.enc"u8.ToArray();

    private readonly byte[] _encryptionKey;
    private readonly LookupOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المُصدِر بمفتاحٍ صريح.</summary>
    /// <param name="signingKey">مفتاح التوقيع — 32 بايتاً فأكثر.</param>
    /// <param name="options">الإعدادات.</param>
    /// <param name="clock">مصدر الوقت.</param>
    /// <exception cref="ArgumentException">إن كان المفتاح غائباً أو أقصر من الحدّ.</exception>
    public SignedLookupHandles(byte[] signingKey, LookupOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        // ‏**مفتاحٌ غائب عطلٌ يُعلَن عند التركيب، لا مفتاحٌ يُخترع.** مُصدِرٌ يولّد لنفسه
        // مفتاحاً عند الإقلاع يُنتج نظاماً تُقبل فيه كل المقابض قبل إعادة التشغيل وتُرفض
        // كلها بعدها — والفشل يُقرأ «انتهت الصلاحية» لا «لا مفتاح». نفس نصّ
        // ‏SignedAttachmentTickets ولنفس السبب.
        if (signingKey.Length < MinimumKeyBytes)
        {
            throw new ArgumentException(
                "مفتاح توقيع المقابض غائب أو أقصر من " + MinimumKeyBytes
                + " بايتاً — اضبط " + options.HandleSigningKeyVariable
                + ". / the handle signing key is missing or shorter than "
                + MinimumKeyBytes + " bytes.",
                nameof(signingKey));
        }

        // ‏**مفتاح التعمية مشتقّ لا هو المفتاح نفسه.** المفتاح الخام قد يُستعمل غداً
        // لغرضٍ ثانٍ، والاشتقاق بالغرض يمنع أن يُنتج غرضان مفتاحاً واحداً.
        _encryptionKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, signingKey, outputLength: 32, salt: null, info: EncryptionInfo);

        _options = options;
        _clock = clock;
    }

    /// <summary>
    /// يبني المُصدِر من متغيّر البيئة الذي تسمّيه الإعدادات — <b>ويرمي عند غيابه</b>.
    /// </summary>
    /// <param name="options">الإعدادات، ومنها اسم المتغيّر.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public static SignedLookupHandles FromEnvironment(LookupOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? value = Environment.GetEnvironmentVariable(options.HandleSigningKeyVariable);
        byte[] key = string.IsNullOrEmpty(value) ? [] : Encoding.UTF8.GetBytes(value);
        return new SignedLookupHandles(key, options, clock);
    }

    /// <inheritdoc />
    public Result<string> Issue(
        LookupHandlePurpose purpose,
        TenantId tenant,
        Guid companyId,
        Guid sessionId,
        Guid subject,
        TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > _options.HandleLifetimeCap)
        {
            return Result<string>.Failure(new Error(
                "ai.lookup.handle_lifetime_refused",
                "مدّة المِقبض المطلوبة خارج السقف — ولا تُقصّ إلى السقف بصمت.",
                "the requested handle lifetime is outside the cap; it is refused, not silently clamped."));
        }

        if (!Enum.IsDefined(purpose))
        {
            return Result<string>.Failure(LookupErrors.HandlePurposeUndefined);
        }

        DateTimeOffset expiresAt = _clock.GetUtcNow() + lifetime;

        Span<byte> payload = stackalloc byte[PayloadBytes];
        payload[0] = (byte)purpose;
        tenant.Value.TryWriteBytes(payload.Slice(1, 16));
        companyId.TryWriteBytes(payload.Slice(17, 16));
        sessionId.TryWriteBytes(payload.Slice(33, 16));
        subject.TryWriteBytes(payload.Slice(49, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.Slice(65, 8), expiresAt.ToUnixTimeMilliseconds());

        byte[] token = new byte[TokenBytes];
        token[0] = Version;

        Span<byte> nonce = token.AsSpan(1, NonceBytes);
        RandomNumberGenerator.Fill(nonce);

        // ‏**رقم النسخة بيانٌ مُوثَّق مصاحب**: صريحٌ كي يُقرأ قبل الفكّ، ومُوثَّقٌ كي لا
        // يُبدَّل فيُقرأ مِقبضُ نسخةٍ بأخرى.
        using AesGcm cipher = new(_encryptionKey, TagBytes);
        cipher.Encrypt(
            nonce,
            payload,
            token.AsSpan(1 + NonceBytes, PayloadBytes),
            token.AsSpan(1 + NonceBytes + PayloadBytes, TagBytes),
            token.AsSpan(0, 1));

        return Result<string>.Success(Grouped(Base64Url(token)));
    }

    /// <inheritdoc />
    public Result<RedeemedLookupHandle> Redeem(
        string token,
        LookupHandlePurpose expected,
        TenantId tenant,
        Guid companyId,
        Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!TryDecode(token, out byte[] raw)
            || raw.Length != TokenBytes
            || raw[0] != Version)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleNotSigned);
        }

        byte[] payload = new byte[PayloadBytes];

        try
        {
            // ‏**الفكّ يتحقّق من العلامة أوّلاً ويرمي إن لم تصحّ** — فلا يُقرأ بايتٌ واحد
            // من مِقبضٍ مبدَّل، ولا يحتاج الأمر مقارنةً بزمنٍ ثابت مكتوبةً باليد.
            using AesGcm cipher = new(_encryptionKey, TagBytes);
            cipher.Decrypt(
                raw.AsSpan(1, NonceBytes),
                raw.AsSpan(1 + NonceBytes, PayloadBytes),
                raw.AsSpan(1 + NonceBytes + PayloadBytes, TagBytes),
                payload,
                raw.AsSpan(0, 1));
        }
        catch (CryptographicException)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleNotSigned);
        }

        // التوثيق أولاً ثم الحقول: قيمةٌ غير موثَّقة لا تقود منطقاً.
        LookupHandlePurpose purpose = (LookupHandlePurpose)payload[0];
        if (purpose != expected)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandlePurposeMismatch(expected, purpose));
        }

        TenantId carriedTenant = new(new Guid(payload.AsSpan(1, 16)));
        Guid carriedCompany = new(payload.AsSpan(17, 16));
        Guid carriedSession = new(payload.AsSpan(33, 16));

        // ‏**الطبقة الأولى من طبقتَي منع العبور:** المقارنة بنطاق الجلسة، لا بما في المِقبض.
        // والرسالة واحدة في الحالات الثلاث: رسالةٌ تفرّق بين «منشأة أخرى» و«جلسة أخرى»
        // تُخبر من يجرّب أيَّ نصفٍ أصاب.
        if (carriedTenant != tenant || carriedCompany != companyId || carriedSession != sessionId)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleOutOfScope);
        }

        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(
            BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(65, 8)));

        if (expiresAt <= _clock.GetUtcNow())
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleExpired);
        }

        return Result<RedeemedLookupHandle>.Success(new RedeemedLookupHandle(
            purpose,
            carriedTenant,
            carriedCompany,
            carriedSession,
            new Guid(payload.AsSpan(49, 16)),
            expiresAt));
    }

    /// <summary>‏base64url بلا حشو — يعبر مسار URL وسلسلة استعلام بلا ترميز ثانٍ.</summary>
    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>يقطّع النصّ مجموعاتٍ من ثمانية يفصلها <see cref="GroupSeparator"/>.</summary>
    private static string Grouped(string encoded)
    {
        StringBuilder grouped = new(encoded.Length + (encoded.Length / GroupLength));

        for (int index = 0; index < encoded.Length; index += GroupLength)
        {
            if (index > 0)
            {
                grouped.Append(GroupSeparator);
            }

            grouped.Append(encoded.AsSpan(index, Math.Min(GroupLength, encoded.Length - index)));
        }

        return grouped.ToString();
    }

    /// <summary>
    /// يفكّ الترميز <b>ويرفض كل كتابةٍ غير الكتابة الوحيدة</b>.
    /// <para>
    /// <b>ولماذا الرفض لا التسامح:</b> ‏106 بايتاً ليست من مضاعفات الثلاثة، فآخر محرفٍ
    /// من base64 يحمل بتّاتٍ مهملة — <b>وستّة عشر نصّاً مختلفاً كانت تُفكّ إلى المحتوى
    /// نفسه</b> (مقيس). ولا شيء اليوم يُفهرس بنصّ المِقبض؛ لكن أوّل قائمة إبطالٍ أو نافذة
    /// تكرارٍ أو سجلّ تدقيقٍ يُبنى على النصّ يُهزَم بإعادة تهجئته. والعلاج سطران: يُعاد
    /// الترميز ويُطابَق بالوارد.
    /// </para>
    /// </summary>
    private static bool TryDecode(string token, out byte[] value)
    {
        value = [];

        if (token.Length != TokenLength)
        {
            return false;
        }

        StringBuilder ungrouped = new(token.Length);
        int sinceSeparator = 0;

        foreach (char character in token)
        {
            if (character == GroupSeparator)
            {
                // فاصلٌ في غير موضعه: كتابةٌ أخرى لا مِقبض.
                if (sinceSeparator != GroupLength)
                {
                    return false;
                }

                sinceSeparator = 0;
                continue;
            }

            sinceSeparator++;
            ungrouped.Append(character);
        }

        // ‏**المقارنة على صورة base64url لا على الصورة القياسية**: الأخيرة تحمل
        // ‏`+` و`/` حيث تحمل الأولى `-` و`_`، فمقارنتهما تفشل على كل مِقبضٍ فيه أيّهما.
        string flat = ungrouped.ToString();

        StringBuilder standard = new(flat.Length + 3);
        foreach (char character in flat)
        {
            standard.Append(character switch { '-' => '+', '_' => '/', _ => character });
        }

        standard.Append('=', (4 - (flat.Length % 4)) % 4);

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(standard.ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        if (!string.Equals(Base64Url(decoded), flat, StringComparison.Ordinal))
        {
            return false;
        }

        value = decoded;
        return true;
    }
}
