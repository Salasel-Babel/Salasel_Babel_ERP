namespace Babel.Api.Endpoints;

/// <summary>
/// <b>الأبواب المفتوحة — مُعلَنة في موضعٍ واحد، ويقرؤها الخادم والعقد معاً.</b>
/// <para>
/// «أي المسارات تُفتح بلا اعتماد؟» كان له <b>جوابان</b> في هذا المستودع: قائمةٌ مسمّاةٌ
/// واحداً واحداً في <c>RequestPrincipal.IsAnonymous</c> يقرؤها وسيط المصادقة، و<c>Anonymous</c>
/// على كل عملية في مولّد العقد يقرؤه فريق الواجهة والعميل المُولَّد. وقد رُبطا بحارسٍ
/// يطرق كل باب في العقد ويقارن جواب الخادم بما وعد به
/// (‏<c>traps.md#fakh-the-open-door-list-is-declared-twice-and-guarded-in-neither</c>)،
/// <b>وبقي الإعلان اثنين</b>: الحارس يمسك الانحراف بعد وقوعه، ولا يمنع وقوعه.
/// </para>
/// <para>
/// وهذا الملفّ يُنهي الازدواج من جذره: القائمة <b>هنا</b>، ويقرؤها الاثنان. ومن يفتح
/// باباً جديداً يفتحه في سطر واحد، ومن ينساه لا يجد نصفه مفتوحاً — لأنه لا نصف له.
/// والحارس يبقى حيث هو ولا يُحذف: هو الآن يُثبت أن <b>القراءتين تُنفَّذان</b>، لا أن
/// إعلانين متطابقان.
/// </para>
/// <para>
/// <b>والقائمة مسمّاةٌ واحداً واحداً لا بنمط.</b> نمطٌ فضفاض («ما ليس تحت <c>/api/</c>»)
/// كان سيفتح كل مسار جديد يقع خارجه بلا أن ينتبه أحد — وهو بالضبط ما يجعل حارساً كهذا
/// يمرّ على العطل الذي وُجد لأجله.
/// </para>
/// </summary>
internal static class OpenDoors
{
    /// <summary>
    /// المسارات التي تُخدَم <b>بلا اعتماد</b>، مرتَّبةً ترتيباً حرفياً ثابتاً.
    /// <list type="bullet">
    ///   <item><c>/health</c> — لا يقرأ بيانات مستأجر ولا يكتبها.</item>
    ///   <item><c>/openapi/v1.json</c> — بايتات ملفٍّ مُودَع في المستودع. لا سرّ فيه،
    ///         ولا بيانات مستأجر واحد.</item>
    ///   <item><c>/docs</c> — صفحةٌ ساكنة تقرأ ذلك الملفّ. <b>والمتصفّح لا يستطيع أن يضع
    ///         ترويسة <c>Authorization</c> على تنقّلٍ عُلوي</b>، فصفحةُ توثيق محميّة
    ///         بـ<c>Bearer</c> غير قابلة للفتح أصلاً.</item>
    ///   <item><c>/api/v1/access/sessions</c> و<c>…/renewal</c> — من يطلب اعتماداً لا يملك
    ///         اعتماداً. والاعتماد ليس غائباً عنهما بل <b>منقولاً من الترويسة إلى الجسم</b>.</item>
    ///   <item><c>/api/v1/tenants</c> — <b>التسجيل الأول</b>. ومن ليس عنده حساب هو بالضبط
    ///         من يستعمله، فاشتراط اعتماد عليه يجعل المنتَج غير قابل للشراء. وما لا يُفتح
    ///         بفتحه: لا يقرأ بيانات مستأجرٍ قائم ولا يكشف وجوده، والخطّة <b>لا تُختار
    ///         من جسمه</b> بل هي خطّة الدخول وحدها.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> Paths { get; } =
    [
        .. new[]
        {
            ApiRoutes.Health,
            ApiRoutes.OpenApiDocument,
            ApiRoutes.Docs,
            AccessRoutes.Sessions,
            AccessRoutes.SessionRenewal,
            TenantRoutes.Tenants,
        }.OrderBy(static path => path, StringComparer.Ordinal),
    ];

    /// <summary>
    /// الأبواب المفتوحة التي <b>تبلغ قاعدة البيانات وتُصدر اعتماداً</b>، فتُحرَس بحدّ معدّل.
    /// <para>
    /// وهي فرعٌ من <see cref="Paths"/> لا قائمةٌ ثانية: الثلاثة الساكنة تخدم بايتات
    /// ثابتة أو حالة عملية، فحدُّ معدّلٍ عليها يشتري لا شيء ويكسر صفحة توثيق تُحمَّل
    /// أصولها في دفعة واحدة. وما هنا يفتح صفوفاً ويسكّ أسراراً، وهو ما يُطرَق آلياً.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> RateLimited { get; } =
    [
        .. new[] { AccessRoutes.Sessions, AccessRoutes.SessionRenewal, TenantRoutes.Tenants }
            .OrderBy(static path => path, StringComparer.Ordinal),
    ];

    /// <summary>هل هذا المسار بابٌ مفتوح؟ — المطابقة كاملةٌ وحرفية، لا بادئة.</summary>
    /// <param name="path">مسار الطلب.</param>
    public static bool IsOpen(string path) => Paths.Contains(path, StringComparer.Ordinal);

    /// <summary>هل هذا الباب محروسٌ بحدّ معدّل؟</summary>
    /// <param name="path">مسار الطلب.</param>
    public static bool IsRateLimited(string path) => RateLimited.Contains(path, StringComparer.Ordinal);
}
