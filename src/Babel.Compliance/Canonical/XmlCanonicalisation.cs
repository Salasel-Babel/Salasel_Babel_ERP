using System.Text;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Canonical;

/// <summary>
/// حدّ التوحيد القياسي لـXML. <b>موضع «اشترِ ولا تكتب»</b> (02-architecture §12، التحذير 2):
/// التوحيد القياسي لـXML مشكلة محلولة جيداً في مكتبات ناضجة، وكتابتها يدوياً فخّ
/// «يبدو أسبوعاً ويصير ستة أشهر».
/// <para/>
/// الواجهة هنا كي يكون هناك <b>موضع واحد</b> يُركَّب فيه المُنفِّذ الحقيقي (C14N)،
/// ولا يتسرّب توحيد قياسي بديل إلى مكان آخر.
/// </summary>
public interface IXmlCanonicaliser
{
    /// <summary>يعيد البايتات القانونية للعقد المُعطى. الناتج هو ما يُجزَّأ ويُوقَّع.</summary>
    byte[] Canonicalise(XElement element);

    /// <summary>اسم الخوارزمية كما سيُصرَّح به داخل المستند.</summary>
    string AlgorithmName { get; }
}

/// <summary>
/// <b>تنفيذ مؤقَّت للتشغيل والاختبار فقط.</b> ليس C14N، ولا يدّعي أنه كذلك:
/// هو تسلسل حتمي (ترتيب سمات ثابت، بلا تنسيق، بلا BOM) يكفي لتشغيل خط الأنابيب
/// كاملاً بلا اعتمادية خارجية، ويكفي لإثبات أن قاعدة الاستبعاد تعمل.
/// <para/>
/// <b>لا يُشحن هذا إلى الإنتاج.</b> استبداله بمُنفِّذ C14N حقيقي هو بند في طابور التحقق.
/// </summary>
[Provisional("التوحيد القياسي لـXML: يجب استبدال هذا التسلسل الحتمي بمُنفِّذ C14N حقيقي بالخوارزمية التي تنص عليها المواصفة",
    DerivedFrom = "لا مصدر — تنفيذ محلي للتشغيل فقط",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "خوارزمية التوحيد القياسي المنصوص عليها في مواصفة التوقيع، ثم تركيب مكتبة ناضجة تنفّذها")]
public sealed class DeterministicXmlSerialiser : IXmlCanonicaliser
{
    public string AlgorithmName => "babel.provisional.deterministic-xml.v1";

    public byte[] Canonicalise(XElement element)
    {
        var sb = new StringBuilder();
        Write(element, sb);
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    private static void Write(XElement e, StringBuilder sb)
    {
        sb.Append('<').Append(e.Name.LocalName);
        if (!string.IsNullOrEmpty(e.Name.NamespaceName))
            sb.Append(" xmlns=\"").Append(e.Name.NamespaceName).Append('"');

        foreach (var a in e.Attributes()
                     .Where(a => !a.IsNamespaceDeclaration)
                     .OrderBy(a => a.Name.NamespaceName, StringComparer.Ordinal)
                     .ThenBy(a => a.Name.LocalName, StringComparer.Ordinal))
        {
            sb.Append(' ').Append(a.Name.LocalName).Append("=\"").Append(Escape(a.Value)).Append('"');
        }
        sb.Append('>');

        foreach (var n in e.Nodes())
        {
            switch (n)
            {
                case XElement child: Write(child, sb); break;
                case XText t: sb.Append(Escape(t.Value)); break;
            }
        }
        sb.Append("</").Append(e.Name.LocalName).Append('>');
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>
/// تطبيق قاعدة الاستبعاد قبل التوحيد القياسي.
/// <para/>
/// <b>مُتحقَّق منه من تنفيذات مفتوحة المصدر (لا من الهيئة):</b> تحويل التوقيع يستبعد
/// <b>ثلاث مجموعات عقد بالضبط</b>، و<b>العدّاد والبصمة السابقة ليسا منها</b> —
/// وهذا بالذات ما يجعل السلسلة رابطة تشفيرياً بدل أن تكون بيانات مجاورة.
/// </summary>
public sealed class SigningInputExtractor(IXmlCanonicaliser canonicaliser, SigningExclusionRule? rule = null)
{
    private readonly SigningExclusionRule _rule = rule ?? SigningExclusionRule.Default;

    public SigningExclusionRule Rule => _rule;

    /// <summary>عدد المجموعات المستبعدة فعلاً في آخر استخراج — يُتحقَّق منه في الاختبار.</summary>
    public int LastExcludedNodeSets { get; private set; }

    public byte[] Extract(XElement document)
    {
        var copy = new XElement(document);
        var excluded = 0;

        foreach (var name in _rule.ExcludedElementNames)
        {
            var nodes = copy.Descendants().Where(x => x.Name.LocalName == name).ToList();
            if (nodes.Count > 0) excluded++;
            foreach (var n in nodes) n.Remove();
        }

        var qrRefs = copy.Descendants()
            .Where(x => x.Name.LocalName == _rule.AdditionalDocumentReferenceElement)
            .Where(x => x.Elements()
                .Any(c => c.Name.LocalName == _rule.AdditionalDocumentReferenceIdElement &&
                          c.Value == _rule.ExcludedAdditionalDocumentReferenceId))
            .ToList();
        if (qrRefs.Count > 0) excluded++;
        foreach (var n in qrRefs) n.Remove();

        LastExcludedNodeSets = excluded;
        return canonicaliser.Canonicalise(copy);
    }
}
