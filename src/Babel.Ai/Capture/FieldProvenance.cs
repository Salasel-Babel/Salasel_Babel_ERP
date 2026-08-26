namespace Babel.Ai.Capture;

/// <summary>
/// <b>من أين جاء هذا الحقل بعينه.</b> لكل حقل مصدره، لا للمستند كله.
/// <para>
/// <b>لماذا لكل حقل لا للمستند:</b> شاشة تأكيد تتساوى فيها كل الحقول <b>تُدرِّب الإنسان
/// على الضغط دون قراءة</b>، فتُبطل الضمانة التي وُجدت من أجلها. وإجماليٌّ مُصدَّق برمز
/// موقَّع وإجماليٌّ مقروء ضوئياً بثقة 0.94 <b>ليسا حقيقتين من صنف واحد</b>، ولا يوجد
/// رقم واحد يعبّر عن الفرق بينهما — ولذلك حلّ هذا التعداد محلّ «عتبة ثقة واحدة للمستند».
/// </para>
/// </summary>
public enum FieldProvenance
{
    /// <summary>من رمز موقَّع أو فاتورة إلكترونية — الإنسان <b>يلمح</b>.</summary>
    Attested = 1,

    /// <summary>قراءة ضوئية — الإنسان <b>يراجع</b>.</summary>
    Read = 2,

    /// <summary>اقتراح نموذج — الإنسان <b>يقرّر</b>.</summary>
    Inferred = 3,

    /// <summary>من إعدادات المستأجر — الإنسان <b>يلمح</b>.</summary>
    Defaulted = 4,

    /// <summary>أدخله إنسان — الإنسان <b>يملكه</b>.</summary>
    Typed = 5,

    /// <summary>
    /// نُطق فقيل، ثم فُرِّغ نصّاً — الإنسان <b>يراجع</b>.
    /// <para>
    /// <b>ولماذا مصدر سادس لا «مقروء» بمنشأ آخر:</b> القراءة الضوئية تُخطئ في المحرف،
    /// والتفريغ الصوتي يُخطئ في <b>الرقم كلّه</b> — «خمسة عشر» و«خمسين» تفريغان
    /// متجاوران صوتياً وفرقهما 35، ولا تُنتج بينهما درجةُ ثقة واحدة تمييزاً. ومصدرٌ
    /// مستقلّ يجعل هذا الفرق <b>ظاهراً على الشاشة</b> بدل أن يُدفن تحت وسم «مقروء».
    /// </para>
    /// <para>
    /// <b>وما لا يعنيه هذا المصدر البتّة:</b> أن المنطوق صار حقيقة محاسبية. لا يصير.
    /// يملأ مسوّدة يُرقّيها إنسان (‏ADR-0024)، وواجبه <see cref="ProvenanceDuty.Review"/>
    /// كالمقروء تماماً — بل هو أولى به.
    /// </para>
    /// </summary>
    Spoken = 6,
}

/// <summary>ما يفعله الإنسان أمام حقل من كل مصدر. جدول واحد معلن بدل قرار مبعثر في كل شاشة.</summary>
public enum ProvenanceDuty
{
    /// <summary>يلمح — المصدر يحمل ضمانته معه.</summary>
    Glance = 1,

    /// <summary>يراجع — القراءة الضوئية تخطئ بصمت.</summary>
    Review = 2,

    /// <summary>يقرّر — الذكاء الاصطناعي يقترح ولا يعتمد.</summary>
    Decide = 3,

    /// <summary>يملك — هو من كتبه.</summary>
    Own = 4,
}

/// <summary>
/// الواجب البشري المقابل لكل مصدر، و<b>مفتاح المورد</b> الذي تُترجم به تسميته.
/// <para>
/// <b>مفتاح لا سلسلتان (‏ADR-0021):</b> تسمية المصدر <b>عرضٌ</b>، وتعدّد اللغات يعني
/// قابلية الترجمة إلى أيّ عدد من اللغات لا ثنائية عربي/إنجليزي. فحقلان ثابتان في الوحدة
/// <b>لا يستطيعان</b> التعبير عن لغة ثالثة، والوحدة لا تحمل نصّ عرضٍ أصلاً: تحمل مفتاحاً
/// تحلّه الواجهة. وما يدخل السجلّ من هذه الوحدة هو <b>القيمة الملتقطة كما كتبها المُصدِر</b>،
/// لا تسمية مصدرها.
/// </para>
/// <para>
/// والواجب هنا لا في الواجهة: شاشة تختار بنفسها ماذا تُبرز تُنتج تجربتين مختلفتين على
/// مسارَين، وأحدهما يُظهر حقلاً مُستنتَجاً كأنه مُصدَّق.
/// </para>
/// </summary>
public static class FieldProvenanceInfo
{
    /// <summary>الواجب البشري المقابل للمصدر.</summary>
    /// <param name="provenance">المصدر.</param>
    public static ProvenanceDuty DutyOf(FieldProvenance provenance) => provenance switch
    {
        FieldProvenance.Attested => ProvenanceDuty.Glance,
        FieldProvenance.Defaulted => ProvenanceDuty.Glance,
        FieldProvenance.Read => ProvenanceDuty.Review,
        FieldProvenance.Spoken => ProvenanceDuty.Review,
        FieldProvenance.Inferred => ProvenanceDuty.Decide,
        FieldProvenance.Typed => ProvenanceDuty.Own,
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "مصدر حقل غير معروف / unknown field provenance"),
    };

    /// <summary>مفتاح المورد الذي تُترجم به تسمية المصدر. مفتاح لا نصّ.</summary>
    /// <param name="provenance">المصدر.</param>
    public static string ResourceKeyOf(FieldProvenance provenance) => provenance switch
    {
        FieldProvenance.Attested => "ai.capture.provenance.attested",
        FieldProvenance.Read => "ai.capture.provenance.read",
        FieldProvenance.Inferred => "ai.capture.provenance.inferred",
        FieldProvenance.Defaulted => "ai.capture.provenance.defaulted",
        FieldProvenance.Typed => "ai.capture.provenance.typed",
        FieldProvenance.Spoken => "ai.capture.provenance.spoken",
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "مصدر حقل غير معروف / unknown field provenance"),
    };

    /// <summary>مفتاح المورد الذي تُترجم به تسمية الواجب البشري.</summary>
    /// <param name="duty">الواجب.</param>
    public static string ResourceKeyOf(ProvenanceDuty duty) => duty switch
    {
        ProvenanceDuty.Glance => "ai.capture.duty.glance",
        ProvenanceDuty.Review => "ai.capture.duty.review",
        ProvenanceDuty.Decide => "ai.capture.duty.decide",
        ProvenanceDuty.Own => "ai.capture.duty.own",
        _ => throw new ArgumentOutOfRangeException(nameof(duty), duty, "واجب بشري غير معروف / unknown human duty"),
    };

    /// <summary>
    /// هل يحمل هذا المصدر درجة ثقة أصلاً؟ <b>نعم للمقروء والمُستنتَج والمنطوق</b>.
    /// درجة ثقة على حقل مُصدَّق ادّعاءٌ كاذب: مصدره لا يقيس ثقة، ووجود الرقم يوحي بأنه يقيس.
    /// </summary>
    /// <param name="provenance">المصدر.</param>
    public static bool CarriesConfidence(FieldProvenance provenance) =>
        provenance is FieldProvenance.Read or FieldProvenance.Inferred or FieldProvenance.Spoken;
}

/// <summary>
/// مفاتيح الموارد لمنشأ القيمة — <b>من أي مصدر مادّي</b> جاءت، لا من أي صنف.
/// تُحلّ في الواجهة كبقية مفاتيح العرض (‏ADR-0021).
/// </summary>
public static class CaptureOriginKeys
{
    /// <summary>من رمز استجابة سريعة موقَّع على الفاتورة.</summary>
    public const string SignedQr = "ai.capture.origin.signed_qr";

    /// <summary>من رمز استجابة سريعة بلا توقيع (المرحلة الأولى).</summary>
    public const string UnsignedQr = "ai.capture.origin.unsigned_qr";

    /// <summary>من قراءة ضوئية للمستند.</summary>
    public const string Optical = "ai.capture.origin.optical";

    /// <summary>من نموذج مُقترِح.</summary>
    public const string Model = "ai.capture.origin.model";

    /// <summary>من إعدادات المستأجر.</summary>
    public const string TenantSetting = "ai.capture.origin.tenant_setting";

    /// <summary>أدخله إنسان.</summary>
    public const string Human = "ai.capture.origin.human";

    /// <summary>من تفريغ صوتي في متصفّح المستخدم — لم تغادر البايتات الصوتية جهازه.</summary>
    public const string SpokenOnDevice = "ai.capture.origin.spoken_on_device";
}
