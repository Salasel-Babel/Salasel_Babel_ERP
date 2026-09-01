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
/// <b>مقابض موقَّعة بلا حالة — على منوال <c>Babel.Storage.SignedAttachmentTickets</c> سطراً بسطر.</b>
/// <para>
/// <b>والحقول داخل البايتات الموقَّعة لا بجانبها</b> — نفس درس فخ-22 الذي يضع
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
/// <b>وما لا يفعله:</b> لا إبطال قبل الانتهاء — لا قائمة ولا صفّ. وذلك ثمن كونه بلا حالة،
/// ولذلك المدّة عشر دقائق.
/// </para>
/// </summary>
public sealed class SignedLookupHandles : ILookupHandles
{
    /// <summary>أدنى طول مقبول لمفتاح التوقيع: 32 بايتاً.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>طول المِقبض بالمحارف — ثابتٌ لكل مِقبض مهما كان غرضه أو موضوعه.</summary>
    public const int TokenLength = 142;

    private const byte Version = 1;

    /// <summary>نسخة + غرض + منشأة + شركة + جلسة + موضوع + انتهاء.</summary>
    private const int PayloadBytes = 1 + 1 + 16 + 16 + 16 + 16 + 8;

    private const int SignatureBytes = 32;

    private readonly byte[] _key;
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

        _key = [.. signingKey];
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

        DateTimeOffset expiresAt = _clock.GetUtcNow() + lifetime;

        byte[] token = new byte[PayloadBytes + SignatureBytes];
        Span<byte> payload = token.AsSpan(0, PayloadBytes);

        payload[0] = Version;
        payload[1] = (byte)purpose;
        tenant.Value.TryWriteBytes(payload.Slice(2, 16));
        companyId.TryWriteBytes(payload.Slice(18, 16));
        sessionId.TryWriteBytes(payload.Slice(34, 16));
        subject.TryWriteBytes(payload.Slice(50, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.Slice(66, 8), expiresAt.ToUnixTimeMilliseconds());

        HMACSHA256.HashData(_key, payload, token.AsSpan(PayloadBytes));

        return Result<string>.Success(Base64Url(token));
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
            || raw.Length != PayloadBytes + SignatureBytes
            || raw[0] != Version)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleNotSigned);
        }

        byte[] expectedSignature = HMACSHA256.HashData(_key, raw.AsSpan(0, PayloadBytes));

        // **مقارنة بزمن ثابت.** مقارنةٌ تخرج عند أول بايت مختلف تسرّب التوقيع الصحيح
        // بايتةً بايتة لمن يقيس الزمن.
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, raw.AsSpan(PayloadBytes)))
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleNotSigned);
        }

        // التوقيع أولاً ثم الحقول: قيمةٌ غير موقَّعة لا تقود منطقاً.
        LookupHandlePurpose purpose = (LookupHandlePurpose)raw[1];
        if (purpose != expected)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandlePurposeMismatch(expected, purpose));
        }

        TenantId carriedTenant = new(new Guid(raw.AsSpan(2, 16)));
        Guid carriedCompany = new(raw.AsSpan(18, 16));
        Guid carriedSession = new(raw.AsSpan(34, 16));

        // ‏**الطبقة الأولى من طبقتَي منع العبور:** المقارنة بنطاق الجلسة، لا بما في المِقبض.
        // والرسالة واحدة في الحالات الثلاث: رسالةٌ تفرّق بين «منشأة أخرى» و«جلسة أخرى»
        // تُخبر من يجرّب أيَّ نصفٍ أصاب.
        if (carriedTenant != tenant || carriedCompany != companyId || carriedSession != sessionId)
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleOutOfScope);
        }

        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(
            BinaryPrimitives.ReadInt64BigEndian(raw.AsSpan(66, 8)));

        if (expiresAt <= _clock.GetUtcNow())
        {
            return Result<RedeemedLookupHandle>.Failure(LookupErrors.HandleExpired);
        }

        return Result<RedeemedLookupHandle>.Success(new RedeemedLookupHandle(
            purpose,
            carriedTenant,
            carriedCompany,
            carriedSession,
            new Guid(raw.AsSpan(50, 16)),
            expiresAt));
    }

    /// <summary>‏base64url بلا حشو — يعبر مسار URL وسلسلة استعلام بلا ترميز ثانٍ.</summary>
    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecode(string token, out byte[] value)
    {
        StringBuilder rebuilt = new(token.Length + 3);
        foreach (char character in token)
        {
            rebuilt.Append(character switch { '-' => '+', '_' => '/', _ => character });
        }

        rebuilt.Append('=', (4 - (token.Length % 4)) % 4);

        try
        {
            value = Convert.FromBase64String(rebuilt.ToString());
            return true;
        }
        catch (FormatException)
        {
            value = [];
            return false;
        }
    }
}
