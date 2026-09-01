using Babel.SharedKernel;

namespace Babel.Contracts.Lookup;

/// <summary>
/// سؤالٌ عن اسمٍ داخل منشأةٍ وشركة. <b>النصّ هو كلام المستخدم نفسه</b> — لا معرّف ولا رمز.
/// </summary>
/// <param name="Text">ما كتبه المستخدم، بعد المِصفاة الصادرة.</param>
/// <param name="Tenant">المنشأة — من بيانات الاعتماد لا من كلام النموذج.</param>
/// <param name="CompanyId">الشركة — من مسار الطلب لا من كلام النموذج.</param>
public sealed record NameCandidateRequest(string Text, TenantId Tenant, Guid CompanyId);

/// <summary>
/// <b>منفذ السبر على سجلّ أسماء تملكه وحدةٌ أخرى.</b>
/// <para>
/// موضعه <c>Babel.Contracts</c> بالإكراه لا بالذوق: <c>ModuleMap.AllowedProjectReferences</c>
/// يعطي كل وحدة أفقية <c>{SharedKernel, Contracts, Core}</c> ولا شيء غيرها، فلا تستطيع
/// <c>Babel.Sales</c> أن ترى <c>Babel.Ai</c> ولا العكس. وهو الشكل نفسه الذي يمرّ منه
/// <c>IAttachmentStore</c> و<c>IVoiceIntentCatalogue</c>.
/// </para>
/// <para>
/// <b>وما لا يُعيده هذا المنفذ:</b> لا اسم، ولا صفّ، ولا درجة تشابه، ولا عدد. ثلاث حالات
/// ومعرّفٌ واحد عند الحالة الوسطى — والمعرّف نفسه لا يخرج إلى النموذج إلا داخل مِقبضٍ موقَّع.
/// </para>
/// </summary>
public interface INameCandidateSource
{
    /// <summary>
    /// مفتاح السجلّ كما تعلنه الوحدة المالكة (‏<c>customer</c> · <c>supplier</c> · …).
    /// يُستعمل للتوجيه ولرسائل الرفض، ولا يعبر إلى النموذج.
    /// </summary>
    string RegisterKey { get; }

    /// <summary>
    /// يسبر السجلّ: صفر · واحد · أكثر. <b>ويقف عند صفّين</b> — لا يُحسب عددٌ فيُنسى حذفه.
    /// </summary>
    /// <param name="request">السؤال، ومعه المنشأة والشركة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<NameCandidateProbe> ProbeAsync(NameCandidateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>مرشّحٌ كما يُعرض على <b>الشاشة</b> — ولا يُكتب في نسخة محادثة النموذج أبداً.</summary>
/// <param name="Id">معرّف الصفّ. يُغلَّف بمِقبض قبل أن يعبر أي حدّ.</param>
/// <param name="LabelAr">الاسم كما هو في السجلّ المحلّي.</param>
/// <param name="SubtitleAr">تمييزٌ إضافي — رمز الطرف مثلاً. أقنعةٌ فقط، ولا معرّف.</param>
public sealed record NameCandidate(Guid Id, string LabelAr, string? SubtitleAr);

/// <summary>
/// <b>مصدر ورقة السؤال — من البيانات المحلّية إلى الشاشة مباشرةً.</b>
/// <para>
/// منفذٌ منفصل عن <see cref="INameCandidateSource"/> <b>عمداً</b>: هذا يُعيد أسماءً، وذاك
/// لا يُعيدها أبداً. ومن يحقن هذا في مسار النموذج يكون قد فعل ذلك <b>باسمٍ يقول ما يفعل</b>،
/// لا سهواً في دالّةٍ واحدة تُعيد الاثنين. الفصل هو الحارس.
/// </para>
/// </summary>
public interface INameCandidateSheetSource
{
    /// <summary>مفتاح السجلّ.</summary>
    string RegisterKey { get; }

    /// <summary>
    /// يجرد المرشّحين لعرضهم على المستخدم. <b>لا يُستدعى في بناء أي رسالة تُرسَل إلى نموذج.</b>
    /// </summary>
    /// <param name="request">السؤال، ومعه المنشأة والشركة.</param>
    /// <param name="cap">سقف الصفوف المعروضة — ورقةٌ لا نهاية لها ليست سؤالاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<IReadOnlyList<NameCandidate>> ListForSheetAsync(
        NameCandidateRequest request,
        int cap,
        CancellationToken cancellationToken = default);
}
