using System.Xml.Linq;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Canonicalization;

/// <summary>نتيجة تحويل التوقيع: البايتات، وكم مجموعة عقد أُزيلت فعلاً.</summary>
public readonly record struct SigningTransformResult(byte[] Canonical, int ExcludedNodeSets, int RemovedNodes);

/// <summary>
/// تحويل التوقيع: يُزيل <b>ثلاث مجموعات عقد بالضبط</b> ثم يُوحّد الباقي قياسياً.
/// <para/>
/// <b>ما ليس مُزالاً، وهو بيت القصيد:</b> عدّاد الفاتورة (ICV) وبصمة الفاتورة السابقة (PIH)
/// يبقيان <b>داخل</b> البايتات المُجزَّأة. لو خرجا لصارت السلسلة عمودين مجاورين يعيد مالك
/// قاعدة البيانات كتابتهما وتبقى كل بصمة صحيحة
/// (‏<c>docs/evidence/traps.md#fakh-decorative-chain-link-outside-the-hash</c>).
/// وهذا هو <b>نفس شكل</b> سلسلة الدفتر في <c>ADR-0007</c>: التسلسل والبصمة السابقة يدخلان
/// البايتات، لا يجاورانها.
/// <para/>
/// <b>حارس اللافراغ مبني في النوع:</b> <see cref="Apply"/> ترفض إن لم تُزل ثلاث مجموعات
/// بالضبط. تحويلٌ لا يُزيل شيئاً يُنتج بصمة «صحيحة» على مستند خاطئ، ويمرّ صامتاً — وهو
/// بالضبط صنف القاعدة التي تمرّ فراغاً لأن المجموعة التي تفحصها لا تحتوي مخالفة بنيوياً.
/// </summary>
public sealed class ZatcaSigningTransform(IZatcaXmlCanonicaliser canonicaliser, SigningExclusionRule? rule = null)
{
    private readonly SigningExclusionRule _rule = rule ?? ZatcaProfile.ExclusionRule;

    public SigningExclusionRule Rule => _rule;

    /// <summary>
    /// يُطبّق الاستبعاد ثم التوحيد القياسي على <b>نسخة</b> من الشجرة — الأصل لا يُمَسّ،
    /// لأن الأصل هو ما سيُرسَل بعد حقن التوقيع ورمز QR فيه.
    /// </summary>
    /// <param name="document">جذر المستند كما بُني، بمواضع التوقيع وQR فارغة.</param>
    /// <param name="requireExactlyThree">
    /// يرفع الاستثناء إن لم تُزل ثلاث مجموعات. يُترك <c>false</c> في اختبار يقيس
    /// <b>ما الذي يقع</b> عند غياب مجموعة، ولا يُترك كذلك في أي مسار إنتاجي.
    /// </param>
    public SigningTransformResult Apply(XElement document, bool requireExactlyThree = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        XElement copy = new(document);
        int excludedSets = 0;
        int removed = 0;

        foreach (string name in _rule.ExcludedElementNames)
        {
            List<XElement> nodes = [.. copy.DescendantsAndSelf().Where(x => x.Name.LocalName == name)];
            if (nodes.Count > 0)
            {
                excludedSets++;
                removed += nodes.Count;
            }

            foreach (XElement node in nodes)
            {
                node.Remove();
            }
        }

        List<XElement> qrReferences =
        [
            .. copy.Descendants()
                .Where(x => x.Name.LocalName == _rule.AdditionalDocumentReferenceElement)
                .Where(x => x.Elements().Any(child =>
                    child.Name.LocalName == _rule.AdditionalDocumentReferenceIdElement
                    && string.Equals(child.Value, _rule.ExcludedAdditionalDocumentReferenceId, StringComparison.Ordinal)))
        ];

        if (qrReferences.Count > 0)
        {
            excludedSets++;
            removed += qrReferences.Count;
        }

        foreach (XElement node in qrReferences)
        {
            node.Remove();
        }

        if (requireExactlyThree && excludedSets != _rule.ExcludedNodeSetCount)
        {
            throw new ZatcaCanonicalisationException(FormattableString.Invariant($"تحويل التوقيع أزال {excludedSets} مجموعة والمطلوب {_rule.ExcludedNodeSetCount} بالضبط. ") +
                "مجموعة غائبة تعني بصمة محسوبة على مستند غير المستند الذي بُنيت له القاعدة، " +
                "وتمرّ صامتة لأن الناتج يبقى بصمة صالحة الشكل. / " +
                FormattableString.Invariant($"the signing transform removed {excludedSets} node sets, exactly {_rule.ExcludedNodeSetCount} are required."));
        }

        return new SigningTransformResult(canonicaliser.Canonicalise(copy), excludedSets, removed);
    }
}
