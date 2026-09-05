namespace Babel.Core.Persistence;

/// <summary>
/// <b>ترويسة إصدار معامِلات — الصفّ الذي يُضاف ولا يُعدَّل.</b>
/// <para>
/// وحقولُه الأربعة الأخيرة منقولةٌ بأسمائها من <c>PayrollSettingsRow</c>: تاريخُ
/// السريان، ومن اعتمد، ومتى، ومرجعُ المصدر. وذلك النموذج هو ما يُعمَّم هنا.
/// </para>
/// <para>
/// <b>ومعرّف المستأجر ليس قابلاً للعدم:</b> صفوف المنصّة تحمل المعرّف الصفري، وصفوف
/// المنشآت تحمل معرّفها. والسبب عملي لا جمالي: الفهرس الفريد على
/// (المستوى · المستأجر · المجموعة · تاريخ السريان) هو ما يمنع إصدارين متعارضين،
/// و<c>null</c> في PostgreSQL <b>لا يساوي null</b> في فهرسٍ فريد افتراضاً — فصفّان
/// للمنصّة بالمجموعة والتاريخ نفسيهما كانا سيمرّان معاً.
/// </para>
/// </summary>
internal sealed class ParameterVersionRow
{
    public Guid Id { get; set; }

    /// <summary>معرّف المنشأة، أو المعرّف الصفري لصفّ المنصّة.</summary>
    public Guid TenantId { get; set; }

    /// <summary>رمز المجموعة — من الفهرس المغلق في <c>ParameterCatalogue</c>.</summary>
    public string SetCode { get; set; } = string.Empty;

    /// <summary>‏<c>platform</c> · <c>tenant</c>.</summary>
    public string Scope { get; set; } = string.Empty;

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>‏<c>platform_default</c> · <c>tenant_approved</c> · <c>auditor_signed</c>.</summary>
    public string Approval { get; set; } = string.Empty;

    /// <summary>
    /// من اعتمد الصفّ — <b>إنسان، لا نظام</b>؛ وهو نصّ <c>PayrollSettingsRow</c> نفسه.
    /// <para>
    /// ولذلك يبقى <b>فارغاً</b> في صفّ المنصّة: افتراضٌ يشحنه المنتج لم يعتمده إنسان،
    /// وكتابةُ اسمٍ فيه — ولو اسمَ نظام — ادّعاءُ اعتمادٍ لم يقع. وحالتُه هي تمثيله.
    /// </para>
    /// </summary>
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>تاريخ الاعتماد، أو غيابه في صفّ المنصّة.</summary>
    public DateOnly? ApprovedOn { get; set; }

    /// <summary>مرجع المصدر الذي أُخذت منه القيم — نصٌّ يقرؤه مراجع، وغير فارغ.</summary>
    public string SourceRef { get; set; } = string.Empty;

    /// <summary>لحظة الإيداع — واقعةٌ لا اعتماد.</summary>
    public DateTimeOffset DepositedAt { get; set; }
}

/// <summary>قيمةٌ في إصدار. المفتاح والصنف والقيمة — ولا عملة هنا، فالعملة على المستند.</summary>
internal sealed class ParameterValueRow
{
    public Guid VersionId { get; set; }

    public string Key { get; set; } = string.Empty;

    /// <summary>‏<c>rate</c> · <c>money</c> · <c>count</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// القيمة بمقياسٍ واحد يسع الأصناف الثلاثة. والحارس على النسبة في المخطّط نفسه:
    /// نسبةٌ أكبر من واحد تُرفض بـ<c>23514</c> ولو تجاوز أحدٌ الخدمة.
    /// </summary>
    public decimal Value { get; set; }
}

/// <summary>
/// <b>سجلّ الاستعمال — فهرسُ المراجع لا سجلُّ المستند.</b>
/// <para>
/// المستند يحمل لقطته في قاعدة وحدته؛ وهذا الصفّ يقول «الإصدار الفلاني استعمله
/// المستند الفلاني» في قاعدةٍ واحدة، فتصير قائمةُ المراجعة استعلاماً واحداً بدل
/// مسحٍ على تسع قواعد. ولو ضاع هذا السجلّ لبقيت المستندات مقروءة.
/// </para>
/// </summary>
internal sealed class ParameterUsageRow
{
    public long SequenceNo { get; set; }

    public Guid TenantId { get; set; }

    public Guid VersionId { get; set; }

    /// <summary>الوحدة المالكة للمستند — قيمة <c>BabelModule</c>.</summary>
    public int Module { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }

    public DateOnly PostedOn { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}
