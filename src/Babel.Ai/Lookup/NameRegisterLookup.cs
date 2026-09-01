using Babel.Contracts.Lookup;
using Babel.SharedKernel;

namespace Babel.Ai.Lookup;

/// <summary>الجلسة التي يجري فيها البحث. كلّها من بيانات الاعتماد والمسار، ولا شيء منها من النموذج.</summary>
/// <param name="Tenant">المنشأة.</param>
/// <param name="CompanyId">الشركة.</param>
/// <param name="SessionId">الجلسة — مربوطةٌ بالمستخدم والمنشأة عند إنشائها.</param>
public sealed record LookupSession(TenantId Tenant, Guid CompanyId, Guid SessionId);

/// <summary>
/// <b>قاعدة المطابقة — وهي قاعدة عدٍّ لا قاعدة ترجيح.</b>
/// <para>
/// صفرٌ ⇒ <c>none</c> · واحدٌ بالضبط ⇒ <c>resolved</c> · اثنان فأكثر ⇒ <c>needs_question</c>.
/// <b>ولا قاعدة «أفضل تطابق» ولا فضّ تعادل، أبداً.</b> واختيار الأعلى درجةً هو بعينه
/// التخمين الذي يرفضه <c>VoiceRefusals.Ambiguous</c> في هذا المستودع: «واختيارُ أحدهما
/// بالصدفة يُنفّذ عمليةً لم تُطلَب».
/// </para>
/// <para>
/// <b>ولا يعرف هذا النوع كم كان المرشّحون.</b> المحوّل يُعيد
/// <see cref="NameCandidateProbe"/> وهو نوعٌ بثلاث حالات وقيمةٍ واحدة، والاستعلام تحته
/// يقف عند صفّين. فالعدد لا يُحذف من الجواب — <b>هو لا يُحسب</b>.
/// </para>
/// <para>
/// <b>وما يعبر إلى النموذج مِقبضٌ لا معرّف.</b> معرّف الصفّ يُسكب في مِقبضٍ موقَّع يحمل
/// المنشأة والشركة والجلسة داخل بايتاته، فمِقبضٌ صحيحٌ في محادثةٍ لا يُفكّ في أخرى.
/// </para>
/// </summary>
public sealed class NameRegisterLookup
{
    private readonly Dictionary<string, INameCandidateSource> _sources;
    private readonly ILookupHandles _handles;
    private readonly LookupOptions _options;

    /// <summary>ينشئ البحث من السجلّات المسجَّلة.</summary>
    /// <param name="sources">مصادر السجلّات كما سجّلتها الوحدات المالكة.</param>
    /// <param name="handles">مُصدِر المقابض.</param>
    /// <param name="options">الإعدادات.</param>
    /// <exception cref="ArgumentException">إن سجّلت وحدتان المفتاح نفسه — سجلٌّ معتلّ فلا يُركَّب.</exception>
    public NameRegisterLookup(
        IEnumerable<INameCandidateSource> sources,
        ILookupHandles handles,
        LookupOptions options)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(options);

        _sources = new Dictionary<string, INameCandidateSource>(StringComparer.Ordinal);
        foreach (INameCandidateSource source in sources)
        {
            // ‏**مفتاحٌ مكرَّر يُسقط التركيب** — نفس ما يفعله سجلّ النيّات المنطوقة
            // في <c>AiModuleRegistration</c>: «سجلٌّ نصفُه صالح يعمل تسعاً وتسعين مرّة
            // ثم يُرحّل مرّةً إلى حدثٍ لا وجود له». وهنا: يبحث في سجلّ وحدةٍ أخرى.
            if (!_sources.TryAdd(source.RegisterKey, source))
            {
                throw new ArgumentException(
                    "سجلّان بمفتاح واحد «" + source.RegisterKey + "» — فلا يُركَّب البحث. "
                    + "/ two name registers claim the key '" + source.RegisterKey + "'.",
                    nameof(sources));
            }
        }

        _handles = handles;
        _options = options;
    }

    /// <summary>مفاتيح السجلّات المسجَّلة، مرتَّبةً — للتشخيص ولاختبارات التركيب.</summary>
    public IReadOnlyList<string> RegisterKeys => [.. _sources.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// يبحث عن اسمٍ في سجلٍّ بعينه داخل منشأة الجلسة وشركتها.
    /// </summary>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    /// <param name="text">كلام المستخدم نفسه.</param>
    /// <param name="session">الجلسة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<Result<NameLookupResult>> ResolveAsync(
        string registerKey,
        string text,
        LookupSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registerKey);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(session);

        if (!_sources.TryGetValue(registerKey, out INameCandidateSource? source))
        {
            return Result<NameLookupResult>.Failure(LookupErrors.NoRegisterSource(registerKey));
        }

        // نصٌّ يطوى إلى لا شيء ليس سؤالاً: «  » و«ـــ» و«ً» كلّها تطابق السجلّ كلّه.
        if (ArabicNameFold.Fold(text).Length == 0)
        {
            return Result<NameLookupResult>.Failure(LookupErrors.EmptyText);
        }

        NameCandidateProbe probe = await source
            .ProbeAsync(new NameCandidateRequest(text, session.Tenant, session.CompanyId), cancellationToken)
            .ConfigureAwait(false);

        return probe.Cardinality switch
        {
            NameCandidateCardinality.None => Result<NameLookupResult>.Success(NameLookupResult.None),

            NameCandidateCardinality.One => Mint(
                LookupHandlePurpose.Entity, probe.Only, session, NameLookupResult.Resolved),

            // ‏**ومعرّف الورقة مِقبضٌ كذلك، وموضوعه معرّفٌ جديد لا معرّف صفّ.**
            // فلو ردّه النموذج في موضع كِيان لسقط عند الغرض، ولو حُلّ لما دلّ على صفّ.
            NameCandidateCardinality.Many => Mint(
                LookupHandlePurpose.Question, Guid.NewGuid(), session, NameLookupResult.NeedsQuestion),

            _ => throw new ArgumentOutOfRangeException(
                nameof(registerKey), probe.Cardinality, "حالةُ سبرٍ خارج المفردات المغلقة."),
        };
    }

    private Result<NameLookupResult> Mint(
        LookupHandlePurpose purpose,
        Guid subject,
        LookupSession session,
        Func<string, NameLookupResult> wrap)
    {
        Result<string> token = _handles.Issue(
            purpose,
            session.Tenant,
            session.CompanyId,
            session.SessionId,
            subject,
            _options.HandleLifetime);

        return token.IsSuccess
            ? Result<NameLookupResult>.Success(wrap(token.Value))
            : Result<NameLookupResult>.Failure(token.Errors);
    }
}
