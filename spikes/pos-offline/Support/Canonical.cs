using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BabelPosOffline.Support;

/// <summary>
/// الشكل القانوني لمستند نقطة البيع، وهو مدخل سلسلة SHA-256 على <b>الجهاز</b>.
///
/// هذا امتداد مباشر لـ <c>spikes/relational-stack/Support/Canonical.cs</c> بالقواعد
/// الأربع نفسها (مقياس ثابت، UTC، ترتيب حقول ثابت، NFC) زائد قاعدة خامسة يفرضها
/// السيناريو دون الاتصال:
///
///   5. <b>لحظة الإنشاء تُقصّ إلى الميكروثانية قبل التجزئة على الجهاز نفسه.</b>
///      دقّة .NET هي 100 نانوثانية، ودقّة PostgreSQL <c>timestamptz</c> هي الميكروثانية.
///      لو جزّأ الجهاز قيمة بدقّة 100ns ثم خزّنها الخادم بدقّة ميكروثانية، فإن البصمة
///      التي حسبها الجهاز <b>لا يمكن إعادة التحقق منها على الخادم أبداً</b> — والسلسلة
///      كلها تصير غير قابلة للتحقق بعد أول مزامنة. القصّ يجب أن يقع على الجهاز، قبل
///      البصمة، لا على الخادم بعدها.
///
/// The creation instant must be truncated to microseconds ON THE DEVICE, before hashing:
/// .NET keeps 100-ns ticks, PostgreSQL timestamptz keeps microseconds. Hash an
/// untruncated instant on the device and the server can never re-verify that hash.
/// </summary>
public static class Canonical
{
    public static DateTime PgInstant(DateTime dt)
    {
        var utc = dt.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
    }

    public static string Utc(DateTime dt) =>
        PgInstant(dt).ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);

    /// <summary>الشكل المخزَّن في SQLite: نص ISO-8601 UTC بميكروثانية، قابل للفرز نصياً.</summary>
    public static string Store(DateTime dt) => Utc(dt);

    public static DateTime Parse(string s) =>
        DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ss.ffffffZ",
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Text(string s) => s.Normalize(NormalizationForm.FormC);
    public static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
    public static byte[] UnHex(string s) => Convert.FromHexString(s);

    public static byte[] HashOf(string s) => SHA256.HashData(new UTF8Encoding(false).GetBytes(s));

    /// <summary>رابط النشأة لسلسلة جهاز. النطاق = الجهاز، لأن الجهاز وحدة إصدار مستقلة.</summary>
    public static byte[] DeviceGenesis(string tenantId, string deviceId) =>
        HashOf($"babel.pos.genesis.v1|{Text(tenantId)}|{Text(deviceId)}");

    /// <summary>
    /// النص القانوني لمستند البيع. لاحظ أن <c>device_seq</c> و<c>prev_hash</c>
    /// <b>داخل</b> البايتات المُجزَّأة — وهذا ما يجعلها سلسلة لا مجموعة بصمات مستقلة.
    /// ولاحظ أن <c>device_clock_at</c> داخلها أيضاً: ساعة الجهاز مدخل <b>ثابت لا يتغيّر</b>
    /// حتى لو ثبت لاحقاً أنها خاطئة. الحقيقة الموثّقة هي «ما ادّعاه الجهاز»، والتصحيح
    /// يقع في حقل منفصل غير مُجزَّأ.
    /// </summary>
    public static string RenderSale(SaleCanonicalView v)
    {
        var sb = new StringBuilder();
        sb.Append("babel.pos.sale.v1\n");
        sb.Append("device_seq=").Append(v.DeviceSeq.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("prev_hash=").Append(Hex(v.PrevHash)).Append('\n');
        sb.Append("tenant_id=").Append(Text(v.TenantId)).Append('\n');
        sb.Append("device_id=").Append(Text(v.DeviceId)).Append('\n');
        sb.Append("sale_id=").Append(v.SaleId).Append('\n');
        sb.Append("doc_type=").Append(v.DocType).Append('\n');
        sb.Append("invoice_no=").Append(v.InvoiceNo.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("business_date=").Append(v.BusinessDate).Append('\n');
        sb.Append("device_clock_at=").Append(v.DeviceClockAt).Append('\n');
        sb.Append("shift_id=").Append(Text(v.ShiftId)).Append('\n');
        sb.Append("original_idem_key=").Append(Text(v.OriginalIdemKey ?? "")).Append('\n');
        sb.Append("currency=").Append(v.Currency).Append('\n');
        sb.Append("total_net=").Append(Money.CanonicalMinor(v.TotalNetMinor)).Append('\n');
        sb.Append("total_vat=").Append(Money.CanonicalMinor(v.TotalVatMinor)).Append('\n');
        sb.Append("total_gross=").Append(Money.CanonicalMinor(v.TotalGrossMinor)).Append('\n');
        foreach (var l in v.Lines.OrderBy(x => x.LineNo))
            sb.Append("line=")
              .Append(l.LineNo.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(Text(l.ItemCode)).Append('|')
              .Append(Money.CanonicalQty(Money.QtyFromMinor(l.QtyMinor))).Append('|')
              .Append(Money.CanonicalMinor(l.UnitPriceMinor)).Append('|')
              .Append(Money.CanonicalMinor(l.LineNetMinor)).Append('|')
              .Append(Money.CanonicalMinor(l.LineVatMinor)).Append('\n');
        foreach (var j in v.JournalLines.OrderBy(x => x.LineNo))
            sb.Append("jl=")
              .Append(j.LineNo.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(Text(j.AccountCode)).Append('|')
              .Append(Money.CanonicalMinor(j.DebitMinor)).Append('|')
              .Append(Money.CanonicalMinor(j.CreditMinor)).Append('\n');
        sb.Append("end\n");
        return sb.ToString();
    }

    public static byte[] HashSale(SaleCanonicalView v) => HashOf(RenderSale(v));

    /// <summary>بصمة الحمولة وحدها (بلا رابط السلسلة): تكشف إعادة استخدام مفتاح الحصانة بمحتوى مختلف.</summary>
    public static byte[] PayloadHash(SaleCanonicalView v)
    {
        var clone = v with { DeviceSeq = 0, PrevHash = [] };
        return HashOf("babel.pos.payload.v1\n" + RenderSale(clone));
    }
}

public readonly record struct CanonLine(int LineNo, string ItemCode, long QtyMinor, long UnitPriceMinor, long LineNetMinor, long LineVatMinor);
public readonly record struct CanonJournalLine(int LineNo, string AccountCode, long DebitMinor, long CreditMinor);

public sealed record SaleCanonicalView
{
    public required string TenantId { get; init; }
    public required string DeviceId { get; init; }
    public required string SaleId { get; init; }
    public required string DocType { get; init; }
    public required long InvoiceNo { get; init; }
    public required long DeviceSeq { get; init; }
    public required byte[] PrevHash { get; init; }
    public required string BusinessDate { get; init; }
    public required string DeviceClockAt { get; init; }
    public required string ShiftId { get; init; }
    public string? OriginalIdemKey { get; init; }
    public required string Currency { get; init; }
    public required long TotalNetMinor { get; init; }
    public required long TotalVatMinor { get; init; }
    public required long TotalGrossMinor { get; init; }
    public required IReadOnlyList<CanonLine> Lines { get; init; }
    public required IReadOnlyList<CanonJournalLine> JournalLines { get; init; }
}
