using System.Security.Cryptography;
using System.Text;

namespace Babel.Canonicalization.Golden;

/// <summary>نوع المتجه الذهبي.</summary>
public enum GoldenKind
{
    /// <summary>مُدخل -> بايتات قانونية محدّدة -> بصمة SHA-256 محدّدة.</summary>
    Bytes,
    /// <summary>مُدخل يجب أن يُرفض برمز خطأ محدّد.</summary>
    Reject,
    /// <summary>عدّة مُدخلات مختلفة نصّياً يجب أن تعطي البصمة نفسها.</summary>
    SameHash,
    /// <summary>عدّة مُدخلات يجب أن تعطي بصمات مختلفة كلها.</summary>
    DifferentHash,
    /// <summary>قيمة نصية محسوبة (تطبيع بحث مثلاً) يجب أن تساوي نصّاً محدّداً.</summary>
    Value
}

/// <summary>نتيجة تنفيذ متجه ذهبي.</summary>
public sealed record GoldenResult
{
    public required string Id { get; init; }
    public required string DescriptionAr { get; init; }
    public required GoldenKind Kind { get; init; }
    public string? CanonicalText { get; init; }
    public string? CanonicalBytesHex { get; init; }
    public string? CanonicalSha256 { get; init; }
    public string? ErrorCode { get; init; }
    public IReadOnlyList<string>? Hashes { get; init; }
    public string? Value { get; init; }
    public string? Note { get; init; }
}

/// <summary>تعريف متجه ذهبي: معرّف ثابت، وصف عربي، ودالة تنتج النتيجة.</summary>
public sealed record GoldenVector(string Id, string DescriptionAr, Func<GoldenResult> Run)
{
    public GoldenResult Execute()
    {
        var r = Run();
        return r with { Id = Id, DescriptionAr = DescriptionAr };
    }
}

/// <summary>مصانع مختصرة لبناء النتائج.</summary>
public static class Golden
{
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>متجه بايتات: يوحّد ويجزّئ ويسجّل البايتات كاملة.</summary>
    public static GoldenResult Bytes(CanonicalDocument doc, long sequence, byte[] previousHash, string? note = null)
    {
        var link = Canonicalizer.Compute(doc, sequence, previousHash);
        return new GoldenResult
        {
            Id = "", DescriptionAr = "", Kind = GoldenKind.Bytes,
            CanonicalText = link.CanonicalText,
            CanonicalBytesHex = Convert.ToHexString(link.CanonicalBytes).ToLowerInvariant(),
            CanonicalSha256 = link.HashHex,
            Note = note
        };
    }

    /// <summary>متجه بايتات على بايتات جاهزة (يُستخدم لبصمة التكوين).</summary>
    public static GoldenResult RawBytes(byte[] bytes, string? note = null) => new()
    {
        Id = "", DescriptionAr = "", Kind = GoldenKind.Bytes,
        CanonicalText = Utf8.GetString(bytes),
        CanonicalBytesHex = Convert.ToHexString(bytes).ToLowerInvariant(),
        CanonicalSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        Note = note
    };

    /// <summary>متجه رفض: يجب أن يرمي <see cref="CanonicalizationException"/> برمز محدّد.</summary>
    public static GoldenResult Reject(Action action, string? note = null)
    {
        try
        {
            action();
        }
        catch (CanonicalizationException ex)
        {
            return new GoldenResult
            {
                Id = "", DescriptionAr = "", Kind = GoldenKind.Reject,
                ErrorCode = ex.Code, Note = note
            };
        }
        return new GoldenResult
        {
            Id = "", DescriptionAr = "", Kind = GoldenKind.Reject,
            ErrorCode = "NOT-REJECTED", Note = note
        };
    }

    /// <summary>متجه تطابق: كل المُدخلات يجب أن تعطي البصمة نفسها.</summary>
    public static GoldenResult SameHash(IEnumerable<Func<ChainLink>> variants, string? note = null)
    {
        var hashes = variants.Select(v => v().HashHex).ToList();
        var all = hashes.Distinct(StringComparer.Ordinal).ToList();
        return new GoldenResult
        {
            Id = "", DescriptionAr = "", Kind = GoldenKind.SameHash,
            CanonicalSha256 = all.Count == 1 ? all[0] : "DIVERGED",
            Hashes = hashes,
            Note = note
        };
    }

    /// <summary>متجه اختلاف: كل المُدخلات يجب أن تعطي بصمات مختلفة.</summary>
    public static GoldenResult DifferentHash(IEnumerable<Func<ChainLink>> variants, string? note = null)
    {
        var hashes = variants.Select(v => v().HashHex).ToList();
        return new GoldenResult
        {
            Id = "", DescriptionAr = "", Kind = GoldenKind.DifferentHash,
            Hashes = hashes,
            CanonicalSha256 = hashes.Distinct(StringComparer.Ordinal).Count() == hashes.Count ? "ALL-DISTINCT" : "COLLISION",
            Note = note
        };
    }

    /// <summary>متجه قيمة نصية (مثال: ناتج تطبيع البحث).</summary>
    public static GoldenResult Value(string value, string? note = null) => new()
    {
        Id = "", DescriptionAr = "", Kind = GoldenKind.Value,
        Value = value,
        CanonicalSha256 = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(value))).ToLowerInvariant(),
        Note = note
    };
}
