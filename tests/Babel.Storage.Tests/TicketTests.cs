using System.Security.Cryptography;
using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Storage.Tests;

/// <summary>
/// تذاكر الوصول: قصيرة الأجل، موقّعة، ومستأجرها <b>داخل</b> البايتات الموقّعة.
/// لا تحتاج قاعدة بيانات ولا قرصاً.
/// </summary>
public sealed class TicketTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly TenantId Other = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly AttachmentId Attachment = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly UserId Bearer = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private static (SignedAttachmentTickets Tickets, FakeClock Clock) Build(byte[]? key = null)
    {
        FakeClock clock = new(new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero));
        StorageOptions options = new() { TicketSigningKey = key ?? RandomNumberGenerator.GetBytes(32) };
        return (new SignedAttachmentTickets(options, clock), clock);
    }

    [Fact]
    public void A_ticket_round_trips_with_every_field_intact()
    {
        (SignedAttachmentTickets tickets, _) = Build();

        Result<AttachmentTicket> issued = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(2));
        Assert.True(issued.IsSuccess);

        Result<RedeemedTicket> redeemed = tickets.Redeem(issued.Value.Token);
        Assert.True(redeemed.IsSuccess);
        Assert.Equal(Tenant, redeemed.Value.Tenant);
        Assert.Equal(Attachment, redeemed.Value.Id);
        Assert.Equal(Bearer, redeemed.Value.Bearer);
        Assert.Equal(issued.Value.ExpiresAt, redeemed.Value.ExpiresAt);
    }

    /// <summary>الرمز آمن في مسار URL: لا <c>+</c> ولا <c>/</c> ولا <c>=</c>.</summary>
    [Fact]
    public void The_token_is_url_safe()
    {
        (SignedAttachmentTickets tickets, _) = Build();
        string token = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(1)).Value.Token;

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    /// <summary>
    /// <b>المستأجر داخل التوقيع.</b> تبديل خانة واحدة في أي حقل يُبطل التذكرة —
    /// وهو الفرق بين حقلٍ موقَّع وحقلٍ مجاور للتوقيع (فخ-22 من باب آخر).
    /// </summary>
    [Fact]
    public void Flipping_a_single_bit_anywhere_in_the_payload_invalidates_the_ticket()
    {
        (SignedAttachmentTickets tickets, _) = Build();
        string token = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(1)).Value.Token;

        byte[] raw = Decode(token);
        int flipped = 0;

        for (int index = 0; index < raw.Length; index++)
        {
            byte[] tampered = (byte[])raw.Clone();
            tampered[index] ^= 0x01;

            Result<RedeemedTicket> redeemed = tickets.Redeem(Encode(tampered));
            Assert.True(redeemed.IsFailure, "خانة " + index + " تغيّرت والتذكرة ما زالت مقبولة");
            flipped++;
        }

        // الشاهد على أن الفحص لم يمرّ على مجموعة فارغة.
        Assert.Equal(raw.Length, flipped);
        Assert.True(flipped >= 89, "طول الحمولة الموقّعة أقصر من المتوقّع: " + flipped);
    }

    /// <summary>تذكرة مستأجر لا تفتح مرفق مستأجر آخر: المستأجر يعبر موقَّعاً ويُقارَن.</summary>
    [Fact]
    public void A_ticket_carries_the_tenant_it_was_issued_for_and_nothing_else()
    {
        (SignedAttachmentTickets tickets, _) = Build();
        string token = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(1)).Value.Token;

        RedeemedTicket redeemed = tickets.Redeem(token).Value;

        Assert.Equal(Tenant, redeemed.Tenant);
        Assert.NotEqual(Other, redeemed.Tenant);
    }

    /// <summary>وتذكرة موقّعة بمفتاح آخر لا تُقبل هنا.</summary>
    [Fact]
    public void A_ticket_signed_with_another_key_is_refused()
    {
        (SignedAttachmentTickets mine, _) = Build(RandomNumberGenerator.GetBytes(32));
        (SignedAttachmentTickets theirs, _) = Build(RandomNumberGenerator.GetBytes(32));

        string token = theirs.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(1)).Value.Token;

        Result<RedeemedTicket> redeemed = mine.Redeem(token);
        Assert.True(redeemed.IsFailure);
        Assert.Equal("storage.ticket_signature_invalid", redeemed.Errors[0].Code);
    }

    [Fact]
    public void A_ticket_expires_and_says_so_by_its_own_code()
    {
        (SignedAttachmentTickets tickets, FakeClock clock) = Build();
        string token = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromMinutes(2)).Value.Token;

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(tickets.Redeem(token).IsSuccess);

        clock.Advance(TimeSpan.FromMinutes(1, 1));
        Result<RedeemedTicket> after = tickets.Redeem(token);

        Assert.True(after.IsFailure);
        Assert.Equal("storage.ticket_expired", after.Errors[0].Code);
    }

    /// <summary>
    /// <b>عمرٌ يتجاوز السقف يُرفض ولا يُقصّ.</b> القصّ الصامت يجعل المستدعي يظنّ أنه
    /// أصدر ساعةً وقد أصدر خمس دقائق.
    /// </summary>
    [Fact]
    public void A_lifetime_beyond_the_cap_is_refused_not_silently_trimmed()
    {
        (SignedAttachmentTickets tickets, _) = Build();

        Result<AttachmentTicket> issued = tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromHours(8));

        Assert.True(issued.IsFailure);
        Assert.Equal("storage.ticket_lifetime_refused", issued.Errors[0].Code);
    }

    /// <summary>وعمرٌ صفر أو سالب مرفوض كذلك — تذكرة أبدية بالخطأ أسوأ من غيابها.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void A_zero_or_negative_lifetime_is_refused(int seconds)
        => Assert.True(Build().Tickets.Issue(Tenant, Attachment, Bearer, TimeSpan.FromSeconds(seconds)).IsFailure);

    /// <summary>ونصّ ليس رمزاً أصلاً يُرفض بلا استثناء يتسرّب.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-a-ticket")]
    [InlineData("!!!!")]
    [InlineData("AAAA")]
    public void Rubbish_is_refused_as_an_unsigned_ticket(string token)
    {
        Result<RedeemedTicket> redeemed = Build().Tickets.Redeem(token);

        Assert.True(redeemed.IsFailure);
        Assert.Equal("storage.ticket_signature_invalid", redeemed.Errors[0].Code);
    }

    /// <summary>
    /// <b>مفتاح غائب عطلٌ عند التركيب لا مفتاحٌ يُخترع.</b> مُصدِرٌ يولّد لنفسه مفتاحاً
    /// عند الإقلاع يجعل كل تذكرة تُقبل قبل إعادة التشغيل وتُرفض بعدها، والرسالة
    /// «انتهت الصلاحية» لا «لا مفتاح».
    /// </summary>
    [Fact]
    public void A_missing_or_short_signing_key_fails_at_composition_time()
    {
        FakeClock clock = new(DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(() => new SignedAttachmentTickets(new StorageOptions { TicketSigningKey = [] }, clock));
        Assert.Throws<ArgumentException>(() =>
            new SignedAttachmentTickets(new StorageOptions { TicketSigningKey = RandomNumberGenerator.GetBytes(31) }, clock));
    }

    private static byte[] Decode(string token)
    {
        string padded = token.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - (padded.Length % 4)) % 4));
    }

    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>ساعة تُحرَّك بيد: الانتهاء يُختبر بالتقديم لا بالانتظار.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}
