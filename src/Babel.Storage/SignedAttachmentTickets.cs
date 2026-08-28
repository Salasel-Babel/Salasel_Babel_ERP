using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Storage;

/// <summary>
/// <b>تذاكر وصول موقّعة وقصيرة الأجل.</b>
/// <para>
/// المسار على القرص لا يُعطى لأحد، والمعرّف وحده لا يفتح شيئاً. ما يُعطى للمتصفّح هو
/// رمزٌ يحمل <b>المستأجر والمرفق والحامل ولحظة الانتهاء</b>، وعليها جميعاً توقيع
/// ‏HMAC-SHA256. فالحقول <b>داخل</b> البايتات الموقّعة لا بجانبها — وهو الدرس نفسه
/// الذي يجعل <c>prev_hash</c> داخل بايتات القيد لا في عمود مجاور (فخ-22): حقلٌ خارج
/// التوقيع يُبدَّل بلا أن يبطل التوقيع.
/// </para>
/// <para>
/// <b>والتذكرة لا تُغني عن نطاق المستأجر ولا تحلّ محلّه.</b> نقطة النهاية تقارن
/// <see cref="RedeemedTicket.Tenant"/> بمستأجر الجلسة، ثم تنادي المخزن بمستأجر
/// <b>الجلسة</b>. فلو سُرّبت تذكرة كاملة واستُعملت في جلسة مستأجر آخر، سقطت عند
/// المقارنة؛ ولو سقطت المقارنة سهواً، سقط النداء عند المخزن لأن المستأجر جزء من
/// المفتاح هناك. طبقتان مستقلّتان، لا واحدة مكرَّرة.
/// </para>
/// <para>
/// <b>وما لا تفعله:</b> لا تُبطَل تذكرةٌ قبل انتهائها — لا قائمة إبطال ولا حالة في
/// القاعدة. وهذا ثمنُ كونها بلا حالة، ولذلك السقف الافتراضي خمس دقائق: نافذةُ ضررٍ
/// تُقاس بالدقائق لا بالساعات.
/// </para>
/// </summary>
public sealed class SignedAttachmentTickets : IAttachmentTickets
{
    /// <summary>أدنى طول مقبول لمفتاح التوقيع: 32 بايتاً.</summary>
    public const int MinimumKeyBytes = 32;

    private const byte Version = 1;

    /// <summary>طول الحمولة قبل التوقيع: نسخة + مستأجر + مرفق + حامل + انتهاء.</summary>
    private const int PayloadBytes = 1 + 16 + 16 + 16 + 8;

    private readonly StorageOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المُصدِر.</summary>
    /// <param name="options">الإعدادات — ومنها المفتاح والسقف.</param>
    /// <param name="clock">مصدر الوقت.</param>
    /// <exception cref="ArgumentException">إن كان المفتاح غائباً أو أقصر من الحدّ.</exception>
    public SignedAttachmentTickets(StorageOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        // **مفتاحٌ غائب عطلٌ يُعلَن عند التركيب، لا مفتاحٌ يُخترع.** مُصدِر تذاكر
        // يولّد لنفسه مفتاحاً عند الإقلاع يُنتج نظاماً تُقبل فيه كل تذكرة قبل إعادة
        // التشغيل وتُرفض كلها بعدها — والفشل يُقرأ «انتهت الصلاحية» لا «لا مفتاح».
        if (options.TicketSigningKey.Length < MinimumKeyBytes)
        {
            throw new ArgumentException(
                "مفتاح توقيع التذاكر غائب أو أقصر من " + MinimumKeyBytes
                + " بايتاً — اضبط BABEL_STORAGE_TICKET_KEY. / the ticket signing key is missing or shorter than "
                + MinimumKeyBytes + " bytes; set BABEL_STORAGE_TICKET_KEY.",
                nameof(options));
        }

        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public Result<AttachmentTicket> Issue(TenantId tenant, AttachmentId id, UserId bearer, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > _options.TicketLifetimeCap)
        {
            return Result<AttachmentTicket>.Failure(AttachmentErrors.TicketLifetimeRefused(
                lifetime.TotalSeconds,
                _options.TicketLifetimeCap.TotalSeconds));
        }

        DateTimeOffset expiresAt = _clock.GetUtcNow() + lifetime;

        byte[] payload = new byte[PayloadBytes];
        Write(payload, tenant, id, bearer, expiresAt);

        byte[] signature = HMACSHA256.HashData(_options.TicketSigningKey, payload);

        byte[] token = new byte[PayloadBytes + signature.Length];
        payload.CopyTo(token, 0);
        signature.CopyTo(token, PayloadBytes);

        return Result<AttachmentTicket>.Success(new AttachmentTicket
        {
            Token = Base64Url(token),
            Id = id,
            Tenant = tenant,
            ExpiresAt = expiresAt,
        });
    }

    /// <inheritdoc />
    public Result<RedeemedTicket> Redeem(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!TryDecode(token, out byte[]? raw) || raw.Length != PayloadBytes + 32 || raw[0] != Version)
        {
            return Result<RedeemedTicket>.Failure(AttachmentErrors.TicketNotSigned);
        }

        byte[] expected = HMACSHA256.HashData(_options.TicketSigningKey, raw.AsSpan(0, PayloadBytes));

        // **مقارنة بزمن ثابت.** مقارنةٌ تخرج عند أول بايت مختلف تسرّب التوقيع الصحيح
        // بايتةً بايتة لمن يقيس الزمن.
        if (!CryptographicOperations.FixedTimeEquals(expected, raw.AsSpan(PayloadBytes)))
        {
            return Result<RedeemedTicket>.Failure(AttachmentErrors.TicketNotSigned);
        }

        // التوقيع أولاً ثم الانتهاء: قراءة الحقول قبل التحقّق منها تجعل قيمةً غير
        // موقَّعة تقود منطقاً.
        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(
            BinaryPrimitives.ReadInt64BigEndian(raw.AsSpan(49, 8)));

        if (expiresAt <= _clock.GetUtcNow())
        {
            return Result<RedeemedTicket>.Failure(AttachmentErrors.TicketExpired);
        }

        return Result<RedeemedTicket>.Success(new RedeemedTicket
        {
            Tenant = new TenantId(new Guid(raw.AsSpan(1, 16))),
            Id = new AttachmentId(new Guid(raw.AsSpan(17, 16))),
            Bearer = new UserId(new Guid(raw.AsSpan(33, 16))),
            ExpiresAt = expiresAt,
        });
    }

    private static void Write(Span<byte> payload, TenantId tenant, AttachmentId id, UserId bearer, DateTimeOffset expiresAt)
    {
        payload[0] = Version;
        tenant.Value.TryWriteBytes(payload.Slice(1, 16));
        id.Value.TryWriteBytes(payload.Slice(17, 16));
        bearer.Value.TryWriteBytes(payload.Slice(33, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.Slice(49, 8), expiresAt.ToUnixTimeMilliseconds());
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
