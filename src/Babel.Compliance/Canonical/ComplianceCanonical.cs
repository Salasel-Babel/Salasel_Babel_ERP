using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Canonical;

/// <summary>
/// <b>الطريق الوحيد إلى دالة التجزئة في هذا الحدّ.</b> لا مسار ثانٍ، ولا حتى مؤقتاً
/// (02-architecture §8.1).
/// <para/>
/// هذه <b>ليست</b> صيغة التوحيد القياسي التي تطلبها الهيئة. هما شيئان مختلفان تماماً:
/// <list type="bullet">
///   <item><b>هذه</b> — بصمتنا نحن على الحقيقة المجالية. مواصفتها كاملة هنا، ونملكها، ونثبّتها بمتجهات ذهبية.</item>
///   <item><b>تلك</b> — تحويل XSLT ثم توحيد قياسي لـXML قبل الختم. مواصفتها عند الهيئة، وتنفيذها في مُولِّد المستند.</item>
/// </list>
/// الخلط بينهما ينتج سلسلة تتحقق محلياً وتُرفض عند الجهة.
/// <para/>
/// <b>العدّاد والبصمة السابقة داخل البايتات المُجزَّأة</b> — لا بجوارها. لو خُزِّن الرابط
/// في عمود مجاور فقط لصارت السلسلة زينة يعيد المهاجم كتابتها.
/// </summary>
public static class ComplianceCanonical
{
    /// <summary>معرّف الصيغة. أي تغيير في التمثيل يوجب رقماً جديداً وسلسلة جديدة.</summary>
    public const string FormatId = "babel.compliance.doc.v1";

    /// <summary>المقياس القانوني للمال في هذا النطاق كله: 4 خانات، مطابقاً numeric(19,4).</summary>
    public const int MoneyScale = 4;

    /// <summary>
    /// تنسيق مالي ثابت المقياس وثابت اللغة. 100m و100.00m و100.0000m تعطي النص نفسه.
    /// لا مُشغّل قاعدة بيانات ولا مُسلسِل JSON ولا ToString واعية باللغة تقترب من هذه البايتات.
    /// </summary>
    public static string Money(decimal value)
    {
        var scale = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (scale > MoneyScale)
            throw new CanonicalisationException(
                $"قيمة مالية بمقياس {scale} تتجاوز المقياس القانوني {MoneyScale}: {value.ToString(CultureInfo.InvariantCulture)}. " +
                "التقريب قرار محاسبي يقع قبل هذا الحدّ، لا داخل دالة التوحيد القياسي. / " +
                "Rounding is an accounting decision taken before this boundary, never inside canonicalisation.");
        return value.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// PostgreSQL يخزّن timestamptz بالميكروثانية، و.NET بـ100 نانوثانية.
    /// القصّ يقع <b>قبل</b> التجزئة و<b>قبل</b> التخزين، وإلا لم تعد السلسلة قابلة للتحقق
    /// بعد أول دورة ذهاب وإياب.
    /// </summary>
    public static DateTimeOffset PgInstant(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % 10), TimeSpan.Zero);
    }

    public static string Instant(DateTimeOffset value) =>
        PgInstant(value).ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);

    public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>معرّف صيغة اشتقاق الهوية من مستند المصدر. تغييره يعني هويات جديدة لكل شيء.</summary>
    public const string DerivedIdFormatId = "babel.compliance.derived-id.v1";

    /// <summary>
    /// <b>هوية مشتقّة من مستند المصدر لا مولَّدة عشوائياً.</b> وهذا هو ما يجعل استقبال
    /// الحقيقة نفسها مرّتين حصيناً بلا جدول حصانة إضافي: النداء الثاني يشتقّ المعرّف
    /// نفسه، فيجد السجل قائماً بدل أن يبني ثانياً.
    /// <para/>
    /// لو كان المعرّف <c>Guid.CreateVersion7()</c> لأنتج كل نداء مستنداً جديداً بعدّاد
    /// جديد في السلسلة — أي مستندَين نظاميَّين لبيعة واحدة، وهو أسوأ ما يمكن أن يقع
    /// في هذا المسار.
    /// <para/>
    /// A UUID derived from the source document identity rather than generated. This is
    /// what makes receiving the same fact twice idempotent without a second table.
    /// </summary>
    /// <param name="purpose">غرض الاشتقاق — يفصل فضاءات المعرّفات فلا يتصادم غرضان.</param>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="sourceDocumentType">نوع مستند المصدر كما تسمّيه وحدته.</param>
    /// <param name="sourceDocumentId">معرّف مستند المصدر داخل وحدته.</param>
    public static Guid DerivedId(string purpose, TenantId tenant, string sourceDocumentType, string sourceDocumentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDocumentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDocumentId);

        var digest = SHA256.HashData(Utf8(
            $"{DerivedIdFormatId}|{purpose}|{T(tenant.Value)}|{T(sourceDocumentType)}|{T(sourceDocumentId)}"));

        Span<byte> bytes = stackalloc byte[16];
        digest.AsSpan(0, 16).CopyTo(bytes);

        // شكل UUID سليم: الإصدار 8 (مشتقّ من تجزئة مخصّصة، RFC 9562) والمتغيّر RFC 4122.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>بداية سلسلة وحدة إصدار. لكل وحدة سلسلتها المستقلة — لا سلسلة عالمية واحدة.</summary>
    public static byte[] Genesis(TenantId tenant, IssuingUnitId unit) =>
        SHA256.HashData(Utf8($"babel.compliance.genesis.v1|{tenant.Value}|{unit.Value}"));

    /// <summary>
    /// الصيغة القانونية للمستند. الترتيب ثابت، والتسميات صريحة، والسطور مرتّبة برقم السطر.
    /// </summary>
    public static string Render(ComplianceDocument d, ChainSlot chain)
    {
        var sb = new StringBuilder();
        sb.Append(FormatId).Append('\n');

        // العدّاد والبصمة السابقة أولاً وداخل البايتات — هذا ما يجعلها سلسلة.
        sb.Append("counter=").Append(chain.Counter.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("prev_hash=").Append(Hex(chain.PreviousHash.Span)).Append('\n');

        sb.Append("tenant=").Append(T(d.Tenant.Value)).Append('\n');
        sb.Append("issuing_unit=").Append(T(d.IssuingUnit.Value)).Append('\n');
        sb.Append("document_id=").Append(d.DocumentId.Value.ToString("D", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("document_uuid=").Append(d.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("kind=").Append(d.Kind.ToString()).Append('\n');
        sb.Append("flow=").Append(d.Flow.ToString()).Append('\n');
        sb.Append("document_number=").Append(T(d.DocumentNumber)).Append('\n');
        sb.Append("issued_at=").Append(Instant(d.IssuedAt)).Append('\n');
        sb.Append("currency=").Append(T(d.CurrencyCode)).Append('\n');
        sb.Append("journal_entry=").Append(d.JournalEntry.Value.ToString("D", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("original_document=").Append(d.OriginalDocument?.Value.ToString("D", CultureInfo.InvariantCulture) ?? "-").Append('\n');
        sb.Append("correction_reason_ar=").Append(T(d.CorrectionReasonAr ?? "")).Append('\n');
        sb.Append("correction_reason_en=").Append(T(d.CorrectionReasonEn ?? "")).Append('\n');

        AppendParty(sb, "seller", d.Seller);
        if (d.Buyer is null) sb.Append("buyer=-\n"); else AppendParty(sb, "buyer", d.Buyer);

        foreach (var l in d.Lines.OrderBy(x => x.LineNo))
        {
            sb.Append("line=")
              .Append(l.LineNo.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(Money(l.Quantity)).Append('|')
              .Append(Money(l.UnitPrice)).Append('|')
              .Append(Money(l.NetAmount)).Append('|')
              .Append(Money(l.TaxRate)).Append('|')
              .Append(Money(l.TaxAmount)).Append('|')
              .Append(Money(l.GrossAmount)).Append('|')
              .Append(T(l.DescriptionAr)).Append('|')
              .Append(T(l.DescriptionEn)).Append('\n');
        }

        sb.Append("net_total=").Append(Money(d.Totals.NetTotal)).Append('\n');
        sb.Append("tax_total=").Append(Money(d.Totals.TaxTotal)).Append('\n');
        sb.Append("gross_total=").Append(Money(d.Totals.GrossTotal)).Append('\n');
        sb.Append("end\n");
        return sb.ToString();
    }

    public static byte[] Bytes(ComplianceDocument d, ChainSlot chain) => Utf8(Render(d, chain));

    public static byte[] Hash(ComplianceDocument d, ChainSlot chain) => SHA256.HashData(Bytes(d, chain));

    private static void AppendParty(StringBuilder sb, string prefix, PartyRef p)
    {
        sb.Append(prefix).Append("_name_ar=").Append(T(p.NameAr)).Append('\n');
        sb.Append(prefix).Append("_name_en=").Append(T(p.NameEn)).Append('\n');
        sb.Append(prefix).Append("_trn=").Append(T(p.TaxRegistrationNumber ?? "")).Append('\n');
        sb.Append(prefix).Append("_address_ar=").Append(T(p.AddressAr ?? "")).Append('\n');
        sb.Append(prefix).Append("_address_en=").Append(T(p.AddressEn ?? "")).Append('\n');
        if (p.AddressParts is null) return;
        foreach (var kv in p.AddressParts.OrderBy(k => k.Key, StringComparer.Ordinal))
            sb.Append(prefix).Append("_addr.").Append(T(kv.Key)).Append('=').Append(T(kv.Value)).Append('\n');
    }

    /// <summary>
    /// النص هنا <b>مطبَّع سلفاً</b>: <see cref="ComplianceText.Normalise"/> يقع عند الدخول
    /// ويُخزَّن ناتجه. هذه الدالة تُعيد التطبيع دفاعياً فقط، ولا تُغني عنه.
    /// </summary>
    private static string T(string s) => s.Normalize(NormalizationForm.FormC);

    private static byte[] Utf8(string s) => new UTF8Encoding(false).GetBytes(s);
}
