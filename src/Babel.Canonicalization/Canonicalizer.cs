using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>حلقة في السلسلة: البايتات القانونية وبصمتها وموقعها.</summary>
public sealed record ChainLink
{
    /// <summary>إصدار الشكل القانوني — <b>يُخزَّن بجوار البصمة في كل سجل</b>.</summary>
    public required string CanonVersion { get; init; }

    /// <summary>رقم التسلسل داخل نطاق السلسلة.</summary>
    public required long Sequence { get; init; }

    /// <summary>بصمة السجل السابق.</summary>
    public required byte[] PreviousHash { get; init; }

    /// <summary>
    /// البايتات القانونية كاملة. <b>خزّنها للمستندات المختومة</b> — لا تُعِد اشتقاقها
    /// من قاعدة البيانات عند الطلب. أشيع عطل إنتاجي في نظام تركيا هو بالضبط عدم
    /// تطابق بين المخزَّن والمُعتمَد بسبب إعادة التوليد بعد الإرسال.
    /// </summary>
    public required byte[] CanonicalBytes { get; init; }

    /// <summary>SHA-256 على <see cref="CanonicalBytes"/>.</summary>
    public required byte[] Hash { get; init; }

    /// <summary>البصمة بترميز hex صغير.</summary>
    public string HashHex => Convert.ToHexString(Hash).ToLowerInvariant();

    /// <summary>البصمة السابقة بترميز hex صغير.</summary>
    public string PreviousHashHex => Convert.ToHexString(PreviousHash).ToLowerInvariant();

    /// <summary>البايتات القانونية كنصّ — للتشخيص وحده. البصمة تُحسب على البايتات لا على هذا.</summary>
    public string CanonicalText => new UTF8Encoding(false).GetString(CanonicalBytes);
}

/// <summary>مُوحِّد قياسي بإصدار محدّد. يسمح ببقاء v1 قابلاً للتحقق بعد ظهور v2.</summary>
public interface ICanonicalizer
{
    /// <summary>معرّف الإصدار، مثال <c>v1</c>.</summary>
    string Version { get; }

    /// <summary>يحوّل مستنداً مرتبطاً بالسلسلة إلى بايتات.</summary>
    byte[] Canonicalize(CanonicalDocument document);
}

/// <summary>
/// سجلّ الإصدارات.
///
/// <b>قاعدة الإصدارات:</b> إصدار الشكل القانوني يُخزَّن بجوار كل بصمة. عند إدخال
/// v2، يُسجَّل هنا <b>إلى جانب</b> v1، ولا يُمسّ v1 بحرف. المتحقّق يوزّع كل سجل على
/// مُوحِّد إصداره المخزَّن، فتبقى سجلات v1 قابلة للتحقق إلى الأبد.
/// </summary>
public static class CanonRegistry
{
    private static readonly Dictionary<string, ICanonicalizer> Map = new(StringComparer.Ordinal)
    {
        ["v1"] = CanonicalizerV1.Instance
    };

    /// <summary>يسجّل مُوحِّداً جديداً. لا يجوز استبدال إصدار قائم.</summary>
    public static void Register(ICanonicalizer canonicalizer)
    {
        ArgumentNullException.ThrowIfNull(canonicalizer);
        lock (Map)
        {
            if (Map.TryGetValue(canonicalizer.Version, out var existing) && !ReferenceEquals(existing, canonicalizer))
                throw new InvalidOperationException(
                    $"الإصدار {canonicalizer.Version} مسجَّل مسبقاً. الإصدارات لا تُستبدل، تُضاف بجوارها.");
            Map[canonicalizer.Version] = canonicalizer;
        }
    }

    /// <summary>يجلب مُوحِّد إصدار مخزَّن.</summary>
    public static ICanonicalizer Resolve(string version)
    {
        lock (Map)
        {
            if (Map.TryGetValue(version, out var c)) return c;
        }
        throw new CanonicalizationException(CanonErrors.ChainUnknownVersion,
            $"إصدار الشكل القانوني «{version}» غير معروف لهذا الثنائي. " +
            "سجل كُتب بإصدار أحدث لا يمكن التحقق منه بنسخة أقدم من المكتبة.");
    }

    /// <summary>الإصدارات المسجَّلة.</summary>
    public static IReadOnlyCollection<string> Versions
    {
        get { lock (Map) { return [.. Map.Keys]; } }
    }
}

/// <summary>
/// <b>الطريق الوحيد إلى دالة التجزئة.</b>
///
/// <code>
///   byte[]    Canonicalize(CanonicalDocument doc)
///   ChainLink Compute(CanonicalDocument doc, long sequence, byte[] previousHash)
/// </code>
///
/// ولا يوجد ثالث. <c>Compute</c> نفسه يمرّ عبر <c>Canonicalize</c>، و<c>Canonicalize</c>
/// يرفض أي مستند غير مرتبط بموقع في السلسلة — أي أن <b>رقم التسلسل والبصمة السابقة
/// داخل البايتات المُجزَّأة بالبناء، لا بالانضباط</b>.
///
/// ═══════════════ الشكل السلكي، إصدار v1 ═══════════════
/// <code>
///   babel.canon/v1\n
///   kind\tK\t&lt;len&gt;\t&lt;نوع المستند&gt;\n
///   chain_seq\tI\t&lt;len&gt;\t&lt;رقم التسلسل&gt;\n
///   prev_hash\tB\t64\t&lt;64 محرف hex&gt;\n
///   &lt;سطر حقل&gt;*
///   end\tC\t&lt;len&gt;\t&lt;عدد سطور الحقول&gt;\n
/// </code>
/// سطر الحقل: <c>&lt;المسار&gt;\t&lt;النوع&gt;\t&lt;طول الحمولة بالبايت&gt;\t&lt;الحمولة&gt;\n</c>
///
/// <b>لماذا سابقة الطول موجودة، ولماذا هي ليست زينة:</b>
/// بدونها يستطيع بيان قيد يحتوي سطراً جديداً ثم نصّاً على هيئة سطر حقل أن
/// <b>يزوّر حدود الحقول</b> — أي أن مستندين مختلفين يعطيان البايتات نفسها، وهو
/// انهيار تام لمعنى التوحيد القياسي. سابقة الطول تجعل ذلك مستحيلاً بالبناء،
/// وتُبقي الشكل مقروءاً بالعين في الوقت نفسه (بيان متعدّد الأسطر يبقى كما هو).
///
/// الترميز: UTF-8 بلا BOM. أسماء الحقول [a-z0-9_] فالفاصل \t لا يظهر فيها.
/// نهايات الأسطر داخل النصوص LF وحده (CR مرفوض في TextRules).
/// </summary>
public static class Canonicalizer
{
    /// <summary>الإصدار الحالي للشكل القانوني.</summary>
    public const string CurrentVersion = "v1";

    /// <summary>ترويسة الشكل السلكي.</summary>
    public const string Magic = "babel.canon/v1";

    static Canonicalizer() => CanonicalRuntime.EnsureSupported();

    /// <summary>
    /// البايتات القانونية للمستند. <b>يرمي إن لم يكن المستند مرتبطاً بالسلسلة.</b>
    /// </summary>
    public static byte[] Canonicalize(CanonicalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CanonRegistry.Resolve(document.CanonVersion).Canonicalize(document);
    }

    /// <summary>
    /// يربط المستند بموقعه في السلسلة ثم يوحّده ويجزّئه.
    /// <b>هذه هي الدالة التي تُستدعى عند الترحيل.</b>
    /// </summary>
    public static ChainLink Compute(CanonicalDocument document, long sequence, byte[] previousHash)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(previousHash);

        var bound = document.Bind(sequence, previousHash);
        var bytes = Canonicalize(bound);

        return new ChainLink
        {
            CanonVersion = bound.CanonVersion,
            Sequence = sequence,
            PreviousHash = (byte[])previousHash.Clone(),
            CanonicalBytes = bytes,
            Hash = SHA256.HashData(bytes)
        };
    }

    /// <summary>
    /// بصمة التكوين لنطاق سلسلة. تُستخدم كـ<c>prev_hash</c> للسجل رقم 1.
    /// النطاق = نطاق الترقيم نفسه: (مستأجر × دفتر × سنة مالية).
    /// </summary>
    public static byte[] Genesis(string scope)
    {
        CanonicalRuntime.EnsureSupported();
        TextRules.RequireCanonical(scope, "genesis_scope");
        var utf8 = new UTF8Encoding(false);
        var payload = utf8.GetBytes(scope);
        var text = $"{Magic}\ngenesis\tT\t{payload.Length.ToString(CultureInfo.InvariantCulture)}\t{scope}\nend\tC\t1\t0\n";
        return SHA256.HashData(utf8.GetBytes(text));
    }

    /// <summary>hex صغير — الشكل الوحيد المستخدم في التوثيق والمتجهات الذهبية.</summary>
    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}

/// <summary>تنفيذ الشكل القانوني v1. <b>مجمَّد. أي تعديل هنا هو v2.</b></summary>
internal sealed class CanonicalizerV1 : ICanonicalizer
{
    internal static readonly CanonicalizerV1 Instance = new();

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public string Version => "v1";

    public byte[] Canonicalize(CanonicalDocument document)
    {
        CanonicalRuntime.EnsureSupported();

        if (document.Chain is not { } chain)
            throw new CanonicalizationException(CanonErrors.DocumentUnbound,
                "المستند غير مرتبط بموقع في السلسلة. لا يجوز الحصول على بايتات قانونية بلا " +
                "chain_seq و prev_hash داخلها: الرابط في عمود مجاور يجعل السلسلة زينة. " +
                "استخدم Canonicalizer.Compute(doc, sequence, previousHash).");

        var sb = new StringBuilder(1024);
        sb.Append(Canonicalizer.Magic).Append('\n');

        var lines = 0;
        Emit(sb, "kind", 'K', document.Kind); lines++;
        Emit(sb, "chain_seq", 'I', chain.Sequence.ToString(CultureInfo.InvariantCulture)); lines++;
        Emit(sb, "prev_hash", 'B', Convert.ToHexString(chain.PreviousHash).ToLowerInvariant()); lines++;

        foreach (var entry in document.Entries)
        {
            if (entry.IsGroup)
            {
                var items = entry.Items!;
                Emit(sb, entry.Name, 'G', items.Count.ToString(CultureInfo.InvariantCulture));
                lines++;
                for (var i = 0; i < items.Count; i++)
                {
                    var prefix = $"{entry.Name}/{i.ToString(CultureInfo.InvariantCulture)}/";
                    foreach (var (name, value) in items[i].Fields)
                    {
                        Emit(sb, prefix + name, value.Tag, value.Payload);
                        lines++;
                    }
                }
                continue;
            }

            Emit(sb, entry.Name, entry.Value!.Tag, entry.Value.Payload);
            lines++;
        }

        Emit(sb, "end", 'C', lines.ToString(CultureInfo.InvariantCulture));

        return Utf8NoBom.GetBytes(sb.ToString());
    }

    /// <summary>سطر حقل واحد: المسار، النوع، طول الحمولة بالبايت، الحمولة.</summary>
    private static void Emit(StringBuilder sb, string path, char tag, string payload)
    {
        var byteLength = Utf8NoBom.GetByteCount(payload);
        sb.Append(path).Append('\t')
          .Append(tag).Append('\t')
          .Append(byteLength.ToString(CultureInfo.InvariantCulture)).Append('\t')
          .Append(payload).Append('\n');
    }
}
