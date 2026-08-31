using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace Babel.Compliance.Zatca.Canonicalization;

/// <summary>
/// التوحيد القياسي لـXML بمعنى W3C — <b>وهو مشكلة أخرى تماماً غير الشكل القانوني الذي
/// تملكه هذه المنظومة في <c>Babel.Canonicalization</c>.</b> الخلط بينهما ينتج سلسلة
/// تتحقّق محلياً وتُرفض عند الجهة، والفرق يُلخَّص هكذا:
/// <list type="table">
///   <listheader><term>الشكل</term><description>ما هو، ومن يملك مواصفته</description></listheader>
///   <item>
///     <term><c>Babel.Canonicalization</c> (‏<c>babel.canon/v2</c>)</term>
///     <description>
///       تسلسل <b>سطري بمفاتيح صريحة</b> لحقيقة مجالية نملكها نحن. مواصفته
///       <c>SPEC.v2.md</c> عندنا، ومجموعة استثنائه مُعلَنة ومُبصَّمة، و<b>لا يُدخِل
///       بنية XML في الحساب إطلاقاً</b>. غرضه: سلسلة بصمات القيود.
///     </description>
///   </item>
///   <item>
///     <term>هذا الملف (‏C14N)</term>
///     <description>
///       تحويل <b>شجرة XML</b> إلى بايتات وحيدة: ترتيب سمات، وتصريحات مساحات أسماء،
///       ووسوم فارغة تُكتب مفتوحة/مغلقة، ومسافات بيضاء <b>تُحفظ</b>. مواصفته عند W3C،
///       ومن يفرضها هي الهيئة. غرضه: بايتات التوقيع.
///     </description>
///   </item>
/// </list>
/// <b>موضع الخلط الأخطر عملياً:</b> كلاهما يُنتِج «بايتات قانونية» ويُجزَّأ بـSHA-256،
/// فالنوعان متطابقان (<c>byte[]</c>) والأسماء متشابهة. ولذلك لا يُمرَّر ناتج أحدهما إلى
/// الآخر أبداً في هذا المشروع: بصمتنا تعيش في <c>RenderedDocument.DomainChainDigest</c>،
/// وبصمة الهيئة في <c>RenderedDocument.SigningInputDigest</c>، وهما حقلان مختلفان في
/// السجل نفسه بتعليق يقول ذلك.
/// </summary>
public sealed class ZatcaCanonicalXml : IZatcaXmlCanonicaliser
{
    /// <summary>
    /// الاسم <b>المُصرَّح به داخل المستند</b>: ‏C14N 1.1.
    /// وما تنفّذه المنصّة هو C14N 1.0 — انظر <see cref="Canonicalise(XElement)"/> لشرح
    /// متى يتطابق الاثنان بالبايت، وللحارس الذي يجعل التطابق شرطاً لا رجاءً.
    /// </summary>
    public string AlgorithmName => ZatcaProfile.CanonicalizationAlgorithm;

    /// <summary>
    /// <b>المسافة الوحيدة بين ما يُصرَّح به وما يُنفَّذ في هذا المشروع كله، ومُسيَّجة بحارس.</b>
    /// <para/>
    /// المنصّة تشحن <see cref="XmlDsigC14NTransform"/> وهو <b>C14N 1.0</b>
    /// (‏<c>REC-xml-c14n-20010315</c>)، ولا تشحن تنفيذاً لـ<b>C14N 1.1</b>
    /// (‏<c>2006/12/xml-c14n11</c>) الذي يُصرَّح به في المستند.
    /// <para/>
    /// <b>الفارق بين الإصدارين محصور في معالجة السمات في مساحة الاسم <c>xml:</c></b> —
    /// وعلى رأسها <c>xml:base</c> (‏الإصدار 1.1 يُصحّحه عند قصّ جزء من مستند)
    /// و<c>xml:id</c>، ومعهما وراثة <c>xml:lang</c> و<c>xml:space</c> عبر حدّ القصّ.
    /// <b>فإذا خلا المستند من كل سمة في تلك المساحة، كان ناتج الإصدارين متطابقاً بايتياً.</b>
    /// <para/>
    /// ولذلك لا يُترك التطابق افتراضاً: <b>هذه الدالة ترفض</b> أي عقدة تحمل سمة في مساحة
    /// <c>xml:</c>، فيصير «‏1.0 تكفي» <b>شرطاً مفروضاً</b> لا استنتاجاً في تعليق.
    /// وفاتورة ZATCA لا تحمل تلك السمات أصلاً، فالحارس لا يمنع شيئاً مشروعاً.
    /// <para/>
    /// <b>وسم الإثبات:</b> أن الإصدارين يتفقان في غياب مساحة <c>xml:</c> — <b>مُستنتَج</b>
    /// من نصّ المواصفتين، <b>لا مقيس</b> (لا تنفيذ لـ1.1 على هذه المنصّة نقارن به).
    /// والبند مُسجَّل في <c>docs/evidence/verification-debt.md</c>.
    /// </summary>
    /// <exception cref="ZatcaCanonicalisationException">
    /// حين تحمل الشجرة سمة في مساحة الاسم <c>xml:</c>، فيخرج التوحيد القياسي من النطاق
    /// الذي يتطابق فيه الإصداران.
    /// </exception>
    public byte[] Canonicalise(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        GuardAgainstXmlNamespaceAttributes(element);

        // PreserveWhitespace إلزامي: C14N يحفظ المسافة البيضاء، و XmlDocument يحذفها
        // افتراضياً — أي أن الإعداد الافتراضي يغيّر البايتات الموقَّعة بلا استثناء ولا سطر سجل.
        XmlDocument document = new() { PreserveWhitespace = true };
        using (XmlReader reader = element.CreateReader())
        {
            document.Load(reader);
        }

        XmlDsigC14NTransform transform = new();
        transform.LoadInput(document);
        using MemoryStream output = (MemoryStream)transform.GetOutput(typeof(Stream));
        return output.ToArray();
    }

    /// <summary>
    /// توحيد قياسي <b>لجزء من مستند مع بيئته</b>: العنصر ومعه تصريحات مساحات الأسماء
    /// السارية عليه من أسلافه.
    /// <para/>
    /// <b>لماذا هذا التمييز مهمّ إلى هذا الحدّ:</b> ‏C14N غير الحصري يكتب على العنصر
    /// الأعلى في المجموعة <b>كل</b> تصريحات مساحات الأسماء السارية عليه، لا تصريحاته
    /// وحدها. فالخصائص الموقَّعة (‏XAdES) تُجزَّأ وهي <b>داخل</b> المستند، ومَن يحسب
    /// بصمتها على نسخة مقتطعة يحصل على بايتات أقصر ببضعة تصريحات — <b>بصمة مختلفة
    /// تماماً</b>، وتوقيع يتحقّق عندنا ويُرفض عند الجهة.
    /// <para/>
    /// التنفيذ ينسخ العنصر ثم يُلحق به تصريحات أسلافه الغائبة عنه (الأقرب يغلب)، ثم
    /// يُوحّد النسخة قياسياً. وترتيب التصريحات في المُخرَج مفروض من C14N نفسه (مرتَّب
    /// بالبادئة)، فترتيب الإلحاق لا يؤثّر.
    /// </summary>
    public byte[] CanonicaliseInScope(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        XElement clone = new(element);

        HashSet<string> declared = [.. clone.Attributes()
            .Where(static a => a.IsNamespaceDeclaration)
            .Select(static a => a.Name.LocalName)];

        foreach (XElement ancestor in element.Ancestors())
        {
            foreach (XAttribute declaration in ancestor.Attributes().Where(static a => a.IsNamespaceDeclaration))
            {
                if (declared.Add(declaration.Name.LocalName))
                {
                    clone.Add(new XAttribute(declaration.Name, declaration.Value));
                }
            }
        }

        return Canonicalise(clone);
    }

    /// <summary>
    /// حارس النطاق. يمسح <b>كل</b> العقد لا الجذر وحده، ويسمّي الموضع المخالف —
    /// رسالة «يوجد شيء ما» على شجرة فيها مئات العقد ليست رسالة.
    /// </summary>
    private static void GuardAgainstXmlNamespaceAttributes(XElement root)
    {
        foreach (XElement node in root.DescendantsAndSelf())
        {
            foreach (XAttribute attribute in node.Attributes())
            {
                if (attribute.Name.Namespace == XNamespace.Xml)
                {
                    throw new ZatcaCanonicalisationException(FormattableString.Invariant($"العنصر «{node.Name.LocalName}» يحمل السمة «{attribute.Name}» من مساحة الاسم xml:. ") +
                        "هذه هي المساحة الوحيدة التي يختلف فيها C14N 1.1 عن C14N 1.0، " +
                        "والمنصّة تنفّذ 1.0 فقط — فالتوحيد القياسي هنا يخرج من النطاق المضمون ويُرفض. / " +
                        FormattableString.Invariant($"element '{node.Name.LocalName}' carries '{attribute.Name}' from the xml: namespace; ") +
                        "that is precisely where C14N 1.1 differs from the 1.0 implementation this platform ships.");
                }
            }
        }
    }
}

/// <summary>
/// حدّ التوحيد القياسي — <b>موضع واحد يُركَّب فيه المُنفِّذ</b>، فلا يتسرّب توحيد قياسي
/// بديل إلى مكان آخر.
/// <para/>
/// وهو مُعرَّف <b>هنا</b> لا في عقد الالتزام عمداً: التوحيد القياسي لـXML تفصيلة تخصّ
/// هذا المزوّد وحده، ومزوّد آخر قد لا يبني XML أصلاً. رفع التفصيلة إلى العقد يُدخل
/// مورّداً في عقد يُفترض أنه أنواع فقط.
/// </summary>
public interface IZatcaXmlCanonicaliser
{
    /// <summary>يعيد البايتات القانونية للعقد المُعطى. الناتج هو ما يُجزَّأ ويُوقَّع.</summary>
    byte[] Canonicalise(System.Xml.Linq.XElement element);

    /// <summary>اسم الخوارزمية كما سيُصرَّح به داخل المستند.</summary>
    string AlgorithmName { get; }
}

/// <summary>عطل في التوحيد القياسي لـXML. يخرج من هذا الحدّ بصوت عالٍ دائماً، ولا يُبتلع.</summary>
public sealed class ZatcaCanonicalisationException(string message) : Exception(message);
