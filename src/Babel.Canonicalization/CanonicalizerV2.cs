using System.Globalization;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>
/// ثوابت الشكل القانوني <c>v2</c>.
///
/// <para>
/// <b>لماذا v2 أصلاً</b> (SPEC §12، عمود «مبرَّر»): «حقل جديد <b>يجب</b> أن يدخل
/// الحقيقة المُوقَّعة». مخطّط v1 لم يكن يغطّي <c>role_code</c>، ولا الأبعاد عدا
/// <c>cost_center</c>، ولا المبالغ بعملة الشركة. أي أن مالك قاعدة البيانات كان
/// يستطيع نقل حركة من عقار إلى عقار — فتنقلب ربحية عقارين ويتغيّر كشف مالك —
/// <b>والسلسلة تبقى خضراء</b>. الرقم لا يتغيّر، والتوازن لا ينكسر، والمعنى
/// المحاسبي ينقلب.
/// </para>
/// <para>
/// <b>والشكل السلكي لم يتغيّر</b>: نفس الترويسة بترقيم مختلف، ونفس سطر الحقل
/// بسابقة الطول، ونفس محارف الأنواع مضافاً إليها <c>R</c> (سعر صرف بمقياس 8).
/// v2 ليست «شكلاً أجمل» بل <b>تغطية أوسع</b>؛ وإبقاء الشكل السلكي كما هو هو ما
/// يُبقي قطع <c>CanonicalSplit</c> ودالة <c>ledger.post_entry</c> صالحتين بلا
/// حرف واحد من التغيير فيهما.
/// </para>
/// </summary>
public static class CanonicalV2
{
    /// <summary>معرّف الإصدار كما يُخزَّن في عمود <c>canon_version</c>.</summary>
    public const string Version = "v2";

    /// <summary>ترويسة الشكل السلكي لـv2.</summary>
    public const string Magic = "babel.canon/v2";
}

/// <summary>
/// تنفيذ الشكل القانوني v2.
///
/// <para>
/// <b>الشيفرة مكرّرة عن <c>CanonicalizerV1</c> عمداً، ولا تُشارَك معه.</b> مُوحِّد
/// مشترك بين إصدارين يعني أن أي تحسين أو إصلاح أو إعادة صياغة في المسار المشترك
/// يحرّك بايتات v1 بأثر رجعي — وهو بالضبط ما تمنعه المواصفة. الازدواج هنا ليس
/// إهمالاً بل <b>عزلاً</b>: كل إصدار يملك مسار بايتاته كاملاً، ويموت مجمَّداً معه.
/// </para>
///
/// ═══════════════ الشكل السلكي، إصدار v2 ═══════════════
/// <code>
///   babel.canon/v2\n
///   kind\tK\t&lt;len&gt;\t&lt;نوع المستند&gt;\n
///   chain_seq\tI\t&lt;len&gt;\t&lt;رقم التسلسل&gt;\n
///   prev_hash\tB\t64\t&lt;64 محرف hex&gt;\n
///   &lt;سطر حقل&gt;*
///   end\tC\t&lt;len&gt;\t&lt;عدد سطور الحقول&gt;\n
/// </code>
/// سطر الحقل: <c>&lt;المسار&gt;\t&lt;النوع&gt;\t&lt;طول الحمولة بالبايت&gt;\t&lt;الحمولة&gt;\n</c>
///
/// و<c>chain_seq</c> و<c>prev_hash</c> <b>داخل</b> البايتات كما في v1: الرابط في
/// عمود مجاور يجعل السلسلة زينة (ADR-0007).
/// </summary>
internal sealed class CanonicalizerV2 : ICanonicalizer
{
    internal static readonly CanonicalizerV2 Instance = new();

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static CanonicalizerV2() => CanonicalRuntime.EnsureSupported();

    public string Version => CanonicalV2.Version;

    public byte[] Canonicalize(CanonicalDocument document)
    {
        CanonicalRuntime.EnsureSupported();

        if (document.Chain is not { } chain)
            throw new CanonicalizationException(CanonErrors.DocumentUnbound,
                "المستند غير مرتبط بموقع في السلسلة. لا يجوز الحصول على بايتات قانونية بلا " +
                "chain_seq و prev_hash داخلها: الرابط في عمود مجاور يجعل السلسلة زينة. " +
                "استخدم Canonicalizer.Compute(doc, sequence, previousHash).");

        var sb = new StringBuilder(2048);
        sb.Append(CanonicalV2.Magic).Append('\n');

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
