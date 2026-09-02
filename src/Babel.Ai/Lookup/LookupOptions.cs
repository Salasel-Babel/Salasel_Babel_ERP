namespace Babel.Ai.Lookup;

/// <summary>
/// إعدادات البحث المحلّي عن الأسماء.
/// <para>
/// <b>ولا مفتاح توقيع في هذا النوع — اسمُ متغيّر البيئة وحده.</b> نفس ما يفعله
/// <c>GitHubModelsOptions.TokenVariable</c> في هذا المستودع وللسبب نفسه: سرٌّ داخل كائن
/// إعدادات يظهر في سطر سجلّ، وفي أثر، وفي رسالة استثناء، وفي مقطع ذاكرة.
/// </para>
/// </summary>
public sealed class LookupOptions
{
    /// <summary>
    /// عتبة التشابه الثلاثي بعد الطيّ.
    /// <para>
    /// <b>‏0.45 مقيسة لا مذوّقة.</b> على هذا الجهاز (‏PostgreSQL 16.13 · pg_trgm 1.6) وبعد
    /// الطيّ: كل زوجٍ يجب أن يتطابق يبلغ 1.000؛ وأقرب زوجٍ يجب <b>ألّا</b> يُخلط —
    /// «محمد القحطاني»~«محمد الغامدي» — يبلغ 0.350. والعتبة بينهما، وفوق ضجيج الثلاثيّات
    /// القصيرة ودون 0.700 التي يبلغها «القحطاني»~«القحطان» — <b>وذانك يبقيان مرشّحَين
    /// فيُسأل عنهما</b>، وهو الصواب: لا قاعدة «أفضل تطابق» في هذا المسار إطلاقاً.
    /// </para>
    /// <para>
    /// و<c>decimal</c> لا <c>double</c>: عتبةٌ تُقارَن بحاصلٍ من القاعدة، وانحرافُ خانةٍ
    /// عشرية يقلب «واحد» إلى «اثنين» فيقلب حلّاً إلى سؤال.
    /// </para>
    /// </summary>
    public decimal SimilarityThreshold { get; set; } = Babel.Core.NameRegister.NameRegisterDefaults.SimilarityThreshold;

    /// <summary>
    /// اسم متغيّر البيئة الذي يحمل مفتاح توقيع المقابض.
    /// <b>الاسم لا القيمة</b>، على نمط <c>GitHubModelsOptions.TokenVariable</c>.
    /// </summary>
    public string HandleSigningKeyVariable { get; set; } = "BABEL_AGENT_HANDLE_KEY";

    /// <summary>
    /// عمر المِقبض الافتراضي: عشر دقائق.
    /// <para>
    /// وهو ثمن كونه بلا حالة — لا قائمة إبطال ولا صفّ في القاعدة، تماماً كما يحتجّ
    /// <c>SignedAttachmentTickets</c> لخمس دقائق. ونافذةُ الضرر تُقاس بالدقائق لا بالساعات.
    /// </para>
    /// </summary>
    public TimeSpan HandleLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>سقف عمر المِقبض. تجاوزه يُرفض عند الإصدار ولا يُقصّ بصمت.</summary>
    public TimeSpan HandleLifetimeCap { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>سقف صفوف ورقة السؤال. ورقةٌ بلا سقف ليست سؤالاً بل جرداً.</summary>
    public int QuestionSheetCap { get; set; } = Babel.Core.NameRegister.NameRegisterDefaults.QuestionSheetCap;
}
