using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BabelRelationalSpike.Db;

namespace BabelRelationalSpike.Support;

/// <summary>
/// Deterministic canonical byte form of a journal entry, used as the input to
/// the SHA-256 hash chain.
///
/// Rules (all four matter, and all four have burned real projects):
///   1. decimals rendered with a FIXED scale using the invariant culture -
///      "0.0000". Arabic-Indic digits or a ',' decimal separator would make the
///      hash machine-dependent.
///   2. timestamps rendered as UTC, ISO-8601, 7 fractional digits.
///   3. fields emitted in a fixed order with explicit labels; lines ordered by
///      line_no.
///   4. text NFC-normalised, so "أ" written as U+0623 and as U+0627 U+0654
///      hash identically.
///
/// التوحيد القياسي: أرقام بفواصل ثابتة، أوقات UTC، ترتيب حقول ثابت، ونص NFC.
/// </summary>
public static class Canonical
{
    /// <summary>Invisible bidi / formatting controls that are a real trap in Arabic data entry.</summary>
    public static readonly char[] BidiControls =
    [
        '‎', // LEFT-TO-RIGHT MARK
        '‏', // RIGHT-TO-LEFT MARK
        '؜', // ARABIC LETTER MARK
        '‪', '‫', '‬', '‭', '‮', // embeddings / overrides
        '⁦', '⁧', '⁨', '⁩',           // isolates
        '​', // ZERO WIDTH SPACE
        '﻿'  // ZERO WIDTH NO-BREAK SPACE / BOM
    ];

    public enum BidiPolicy
    {
        /// <summary>Hash exactly what is stored. An invisible U+200F therefore changes the hash.</summary>
        Preserve,
        /// <summary>Strip invisible controls before hashing (see README for why this is NOT the default).</summary>
        Strip
    }

    public static bool ContainsBidiControl(string s) => s.AsSpan().IndexOfAny(BidiControls) >= 0;

    public static string StripBidiControls(string s)
    {
        if (!ContainsBidiControl(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (Array.IndexOf(BidiControls, ch) < 0) sb.Append(ch);
        return sb.ToString();
    }

    public static string Text(string s, BidiPolicy policy = BidiPolicy.Preserve)
    {
        var t = s.Normalize(NormalizationForm.FormC);
        return policy == BidiPolicy.Strip ? StripBidiControls(t) : t;
    }

    /// <summary>Fixed-scale invariant money rendering. 100m and 100.0000m render identically.</summary>
    public static string Money(decimal d) => d.ToString("0.0000", CultureInfo.InvariantCulture);

    /// <summary>
    /// PostgreSQL's timestamptz keeps MICROSECONDS; .NET's DateTime keeps 100-ns
    /// ticks. Hash the untruncated value and the chain can never be re-verified
    /// after a round trip, because the database silently drops the last digit.
    /// Truncate BEFORE hashing and before storing.
    /// دقّة PostgreSQL بالميكروثانية بينما .NET بالـ100 نانوثانية: يجب التقريب قبل البصمة.
    /// </summary>
    public static DateTime PgInstant(DateTime dt)
    {
        var utc = dt.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
    }

    public static string Utc(DateTime dt) =>
        dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    public static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    /// <summary>
    /// The canonical string. NOTE that chain_seq and prev_hash are INSIDE the
    /// hashed bytes - that is what makes it a chain rather than a set of
    /// independent row checksums.
    /// </summary>
    public static string Render(JournalEntry e, IEnumerable<JournalLine> lines, BidiPolicy policy = BidiPolicy.Preserve)
    {
        var sb = new StringBuilder();
        sb.Append("babel.journal.v1\n");
        sb.Append("chain_seq=").Append(e.ChainSeq.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("prev_hash=").Append(Hex(e.PrevHash)).Append('\n');
        sb.Append("book_id=").Append(Text(e.BookId, policy)).Append('\n');
        sb.Append("tenant_id=").Append(Text(e.TenantId, policy)).Append('\n');
        sb.Append("entry_id=").Append(e.EntryId.ToString("D", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("entry_no=").Append(e.EntryNo.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("entry_date=").Append(Date(e.EntryDate)).Append('\n');
        sb.Append("posted_at=").Append(Utc(e.PostedAt)).Append('\n');
        sb.Append("actor=").Append(Text(e.Actor, policy)).Append('\n');
        sb.Append("memo=").Append(Text(e.Memo, policy)).Append('\n');
        sb.Append("memo_ar=").Append(Text(e.MemoAr, policy)).Append('\n');
        foreach (var l in lines.OrderBy(x => x.LineNo))
        {
            sb.Append("line=")
              .Append(l.LineNo.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(Text(l.AccountCode, policy)).Append('|')
              .Append(Money(l.Debit)).Append('|')
              .Append(Money(l.Credit)).Append('|')
              .Append(Text(l.Description, policy)).Append('\n');
        }
        sb.Append("end\n");
        return sb.ToString();
    }

    public static byte[] Bytes(JournalEntry e, IEnumerable<JournalLine> lines, BidiPolicy policy = BidiPolicy.Preserve)
        => new UTF8Encoding(false).GetBytes(Render(e, lines, policy));

    public static byte[] Hash(JournalEntry e, IEnumerable<JournalLine> lines, BidiPolicy policy = BidiPolicy.Preserve)
        => SHA256.HashData(Bytes(e, lines, policy));

    public static byte[] HashOf(string s) => SHA256.HashData(new UTF8Encoding(false).GetBytes(s));

    /// <summary>Genesis link for a book's chain.</summary>
    public static byte[] Genesis(string bookId) => HashOf($"babel.genesis.v1|{Text(bookId)}");
}
