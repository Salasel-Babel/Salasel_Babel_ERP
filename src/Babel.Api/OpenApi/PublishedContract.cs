using System.Reflection;

namespace Babel.Api.OpenApi;

/// <summary>
/// العقد المنشور كما أُودع — <b>بايتاته، لا إعادة بنائه</b>.
/// <para>
/// <b>القرار الذي لا يُنقض هنا:</b> ما يُخدَم على <c>/openapi/v1.json</c> هو
/// <c>contracts/openapi/v1.json</c> نفسه، مضمَّناً في التجميعة وقت البناء. ولا يُبنى
/// شيء وقت التشغيل.
/// </para>
/// <para>
/// <b>ولماذا هذا هو الفارق كلّه:</b> هذه الوثيقة يحرسها اليوم حارسان —
/// <c>PublishedContractTests</c> يقارن المُودَع بما يولّده السطح بايتاً بايت، و
/// <c>Rule18</c> يقارن العميل المُولَّد بالمُودَع. خادمٌ يبني وثيقةً ثالثة عند كل طلب
/// يضع <b>طرفاً ثالثاً خارج الحارسَين</b>، وهو الشكل الذي كلّف هذا المستودع مرّة عند
/// <c>2a34cc9</c>: اتّسع العقد، وخضِرت حرّاس .NET أربعةً من أربعة، ونزل عميلٌ يخالف
/// عقده المنشور. وواجهةُ توثيقٍ تعرض عقداً لم يولّده أحد ليست نقصاً في ميزة — هي
/// <b>مرجعٌ كاذب يبدو مرجعاً</b>.
/// </para>
/// <para>
/// <b>والغياب يُصرَخ به عند أول طلب، لا يُبتلع:</b> مورد مفقود يعني تجميعةً بُنيت بلا
/// عقد، وخدمةُ <c>{}</c> أو مصفوفةٍ فارغة حينها تُقرأ «سطحٌ بلا أبواب» — وهو أسوأ ما
/// يمكن أن تقوله وثيقة.
/// </para>
/// </summary>
internal static class PublishedContract
{
    /// <summary>اسم المورد المضمَّن، مثبَّت في ملفّ المشروع بـ<c>LogicalName</c>.</summary>
    public const string ResourceName = "Babel.Api.contract.v1.json";

    private static readonly Lazy<byte[]> Loaded = new(Read, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>بايتات العقد المُودَع كما هي — بلا إعادة ترميز وبلا إعادة تنسيق.</summary>
    public static byte[] Bytes => Loaded.Value;

    private static byte[] Read() => EmbeddedBytes.Read(ResourceName);
}

/// <summary>
/// صفحة استعراض العقد — <b>قائمة بذاتها بالكامل</b>.
/// <para>
/// لا خطّ خارجي، ولا نصّ برمجي من شبكة توصيل، ولا صورة بعيدة: كل ما تحتاجه الصفحة
/// داخلها، وما تعرضه تقرؤه من <c>/openapi/v1.json</c> على الخادم نفسه. <b>ولماذا هذا
/// شرطٌ لا زينة:</b> الخادم التجريبي قد يقف خلف خروجٍ مقيَّد، وصفحةُ توثيق تُخفق في
/// العرض لأن شبكة التوصيل غير مبلوغة هي فخ-83 نفسه — فحصٌ ينجح على مسارٍ لا تسلكه
/// الحركة الحقيقية، فيبدو الأخضر أوسع مما يقيس.
/// </para>
/// </summary>
internal static class DocsPage
{
    /// <summary>اسم المورد المضمَّن، مثبَّت في ملفّ المشروع بـ<c>LogicalName</c>.</summary>
    public const string ResourceName = "Babel.Api.docs.html";

    private static readonly Lazy<byte[]> Loaded = new(
        () => EmbeddedBytes.Read(ResourceName), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>بايتات الصفحة كما أُودعت.</summary>
    public static byte[] Bytes => Loaded.Value;
}

/// <summary>قراءة موردٍ مضمَّن، أو صراخٌ مسمّى — لا ارتدادٌ صامت إلى فراغ.</summary>
internal static class EmbeddedBytes
{
    /// <summary>يقرأ مورداً مضمَّناً بالاسم المنطقي.</summary>
    /// <param name="resourceName">الاسم المنطقي للمورد.</param>
    public static byte[] Read(string resourceName)
    {
        Assembly assembly = typeof(EmbeddedBytes).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"مورد مضمَّن مفقود: «{resourceName}». تجميعةٌ بُنيت ناقصةً لا تخدم بديلاً فارغاً. / "
                + $"Missing embedded resource '{resourceName}'. An assembly built incomplete serves no empty substitute.");

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
