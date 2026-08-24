namespace Babel.Compliance.Abstractions;

/// <summary>
/// السؤال الذي يشكّل هذا الحدّ كله (02-architecture.md §11.4).
/// <b>الشكلان لا يتحوّل أحدهما إلى الآخر لاحقاً.</b>
/// </summary>
public enum KeyCustody
{
    /// <summary>المزوّد يحوز المفتاح الخاص للمستأجر. العقد معه: «وقّع وأرسل نيابةً عني».</summary>
    ProviderHeld,

    /// <summary>نحن نحوز المفتاح. العقد مع المزوّد: «أعطني البايتات لأوقّعها، ثم خذ الناتج الموقَّع».</summary>
    SelfHeld
}

/// <summary>
/// <b>الفخّ الأول من فخّي هذا المجال:</b> تمرير بصمة base64 إلى دالة توقيع تُجزّئ مرة أخرى
/// يعطي تجزئة مزدوجة، <b>تتحقّق محلياً وتفشل عند الجهة</b>. لذلك لا يُمرَّر «بايتات» مجرّدة
/// عبر هذا الحدّ أبداً — بل بايتات <b>مع تصريح صريح بشكلها</b>، والموقِّع ملزم باحترامه.
/// <para/>
/// Passing a base64 digest STRING to a signing function that hashes again produces a double
/// hash that validates locally and fails at the authority. Bytes therefore never cross this
/// boundary bare; they cross with an explicit declaration of their form.
/// </summary>
public enum SigningInputForm
{
    /// <summary>بايتات خام. على الموقِّع أن يُجزّئ ثم يوقّع.</summary>
    RawBytesToHashThenSign,

    /// <summary>بصمة محسوبة سلفاً بالبايتات الخام (لا base64، لا hex). على الموقِّع ألّا يُجزّئ مرة أخرى.</summary>
    PrecomputedDigestSignDirectly
}

/// <summary>
/// ما يُطلب توقيعه. <see cref="Form"/> ليس تعليقاً توضيحياً — هو جزء من العقد،
/// وكل تنفيذ لـ<see cref="ILocalKeyCustodian"/> ملزم بفحصه ورفض ما لا يفهمه.
/// </summary>
public sealed record SigningInput(
    IssuingUnitId IssuingUnit,
    CredentialRef Credential,
    ReadOnlyMemory<byte> Payload,
    SigningInputForm Form,
    [property: Provisional("اسم خوارزمية التجزئة المطلوبة للختم",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا الهيئة",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "مواصفة الختم التشفيري المنشورة")]
    string HashAlgorithm,
    [property: Provisional("اسم خوارزمية التوقيع وشكل ترميز الناتج (DER أم r||s)",
        DerivedFrom = "منحنى secp256k1 مقيس عملياً على .NET 10؛ شكل الترميز غير مُتحقَّق منه",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة الختم التشفيري المنشورة")]
    string SignatureAlgorithm);

/// <summary>ناتج التوقيع. لا يحمل مفتاحاً خاصاً، ولا يمرّ به شيء سرّي.</summary>
public sealed record SignatureMaterial(
    ReadOnlyMemory<byte> Signature,
    string SignatureAlgorithm,
    ReadOnlyMemory<byte> SignerCertificateDer,
    DateTimeOffset SignedAt)
{
    /// <summary>
    /// <b>الفخّ الثاني:</b> رمز الأمان الثنائي هو <b>base64 لـbase64 لـDER</b> — دورتا فكّ ترميز.
    /// مُتحقَّق منه من تنفيذات مفتوحة المصدر، لا من الهيئة. يُبنى هنا مرة واحدة كي لا
    /// يُعاد ارتكاب الخطأ في كل ناقل.
    /// </summary>
    [Provisional("ترميز رمز الأمان الثنائي (base64 مزدوج فوق DER)",
        DerivedFrom = "تنفيذات مفتوحة المصدر مستقلة — لا الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "توثيق ترويسة رمز الأمان الثنائي في مواصفة الواجهة")]
    public string BinarySecurityTokenDoubleBase64 =>
        Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes(
                Convert.ToBase64String(SignerCertificateDer.Span)));
}

/// <summary>
/// حالة البايتات الجاهزة للإرسال. <b>هذا هو الموضع الذي ينكشف فيه الشكلان.</b>
/// </summary>
public enum SealState
{
    /// <summary>
    /// شكل «المزوّد يحوز»: نسلّم المستند <b>غير مختوم</b> والمزوّد يختمه ثم يرسله.
    /// النتيجة: <b>لا نملك البايتات التي وصلت الجهة فعلاً</b>.
    /// </summary>
    UnsealedForProviderSeal,

    /// <summary>
    /// شكل «نحن نحوز»: البايتات مختومة عندنا، مجمَّدة، ومخزَّنة كما هي.
    /// النتيجة: كل إعادة إرسال <b>مطابقة بايتياً</b> للأولى.
    /// </summary>
    SealedLocally
}

/// <summary>
/// الحمولة الجاهزة للإرسال، مع بصمتها. البصمة هي <b>مفتاح الحصانة الوحيد الذي نملكه</b>:
/// لا يوجد مفتاح حصانة موثَّق من جانب الجهة، فبصمة المحتوى هي كل ما يمكن مطابقته لاحقاً.
/// </summary>
public sealed record SealedPayload(
    SealState State,
    ReadOnlyMemory<byte> Bytes,
    SignatureMaterial? Signature,
    ReadOnlyMemory<byte> Fingerprint)
{
    public string FingerprintHex => Convert.ToHexString(Fingerprint.Span).ToLowerInvariant();

    /// <summary>
    /// هل يمكن ضمان أن إعادة الإرسال ستحمل البايتات نفسها؟
    /// <b>تحت «المزوّد يحوز»: لا.</b> المزوّد يعيد الختم في كل محاولة، وتوقيع ECDSA
    /// عشوائي بطبيعته، فبايتات المحاولة الثانية تختلف عن الأولى حتى لو كان المستند نفسه.
    /// وهذا يُضعف كشف التكرار بعد المهلة الغامضة إضعافاً حقيقياً.
    /// </summary>
    public bool IsByteStableAcrossRetries => State == SealState.SealedLocally;
}

/// <summary>سياق الختم: من نحن، وأيّ وحدة إصدار، وبأيّ مقبض اعتماد.</summary>
public sealed record SealingContext(
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    CredentialRef Credential,
    ComplianceEnvironment Environment);

/// <summary>فصل كامل بين البيئات: شهادات وإعدادات ومسارات مستقلة (04-zatca §4).</summary>
public enum ComplianceEnvironment
{
    Simulation,
    Production
}

/// <summary>
/// <b>النقطة الوحيدة في هذا الحدّ التي يختلف فيها الشكلان.</b>
/// المُنسِّق (Babel.Compliance) لا يعرف أيّ شكل يعمل تحته ولا يتفرّع عليه أبداً؛
/// حقن التبعيات هو الذي يركّب الزوج المتوافق (خاتم + قناة).
/// <para/>
/// The ONE place the two custody shapes differ. The orchestrator never branches on custody;
/// composition wires a matched (sealer, channel) pair.
/// </summary>
[DualCustodyCost(
    "الواجهة نفسها موجودة فقط لأن خطوة الختم قد تقع عندنا أو عند المزوّد. " +
    "لو حُسم «نحن نحوز» لصارت الخطوة دالة محلية بلا واجهة ولا حقن ولا اختبار مزدوج؛ " +
    "ولو حُسم «المزوّد يحوز» لاختفت الخطوة من هذا الحدّ تماماً وانضمّت إلى نداء الإرسال.",
    Kind = CustodyCostKind.ExtraSurface)]
public interface IDocumentSealer
{
    KeyCustody Custody { get; }

    ValueTask<SealedPayload> SealAsync(
        SealingContext context,
        RenderedDocument document,
        CancellationToken cancellationToken);
}

/// <summary>
/// شكل «نحن نحوز» فقط. الحائز المحلي للمفتاح — خزينة مفاتيح أو HSM.
/// <b>لا تنفيذ لهذه الواجهة تحت شكل «المزوّد يحوز»</b>، وهذا بالضبط قياس ميّت من قياسات التعميم.
/// </summary>
[DualCustodyCost(
    "واجهة كاملة لا وجود لها ولا تنفيذ تحت شكل «المزوّد يحوز». تبقى في العقد لأن القرار مؤجَّل.",
    Kind = CustodyCostKind.DeadBranch,
    DeadUnder = DeadUnderShape.ProviderHeld)]
public interface ILocalKeyCustodian
{
    /// <summary>يولّد زوج مفاتيح للوحدة ويعيد مقبضاً. المفتاح الخاص لا يغادر الخزينة.</summary>
    ValueTask<CredentialRef> CreateKeyAsync(
        TenantId tenant,
        IssuingUnitId unit,
        ComplianceEnvironment environment,
        CancellationToken cancellationToken);

    /// <summary>يوقّع بالضبط ما طُلب، بالشكل الذي صُرِّح به، ولا يُجزّئ مرة ثانية.</summary>
    ValueTask<SignatureMaterial> SignAsync(SigningInput input, CancellationToken cancellationToken);

    /// <summary>يربط الشهادة الصادرة بالمقبض. لا مادة سرّية في الاتجاهين.</summary>
    ValueTask AttachCertificateAsync(
        CredentialRef credential,
        ReadOnlyMemory<byte> certificateDer,
        CancellationToken cancellationToken);
}

/// <summary>
/// شكل «المزوّد يحوز» فقط. لا نملك شيئاً نوقّع به؛ نطلب من المزوّد أن يختم ويرسل.
/// <b>لا تنفيذ لهذه الواجهة تحت شكل «نحن نحوز»</b>.
/// </summary>
[DualCustodyCost(
    "واجهة كاملة لا وجود لها ولا تنفيذ تحت شكل «نحن نحوز». تبقى في العقد لأن القرار مؤجَّل.",
    Kind = CustodyCostKind.DeadBranch,
    DeadUnder = DeadUnderShape.SelfHeld)]
public interface IProviderKeyCustodian
{
    /// <summary>
    /// يطلب من المزوّد إنشاء المفتاح لحساب الوحدة وحيازته. نعود بمقبض فقط،
    /// ولا نستطيع أبداً التوقيع بأنفسنا بعدها.
    /// </summary>
    ValueTask<CredentialRef> ProvisionKeyAsync(
        TenantId tenant,
        IssuingUnitId unit,
        ComplianceEnvironment environment,
        CancellationToken cancellationToken);

    /// <summary>
    /// يُبطل حيازة المزوّد للمفتاح. مسار الخروج التعاقدي — وهو المسار الذي
    /// <b>لا يعيد إلينا المفتاح</b>: الخروج يعني إعادة تسجيل كل وحدة إصدار من الصفر.
    /// </summary>
    ValueTask RevokeAsync(CredentialRef credential, CancellationToken cancellationToken);
}
