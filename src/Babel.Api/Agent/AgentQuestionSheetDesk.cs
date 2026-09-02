using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.Ai.Workspace;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;

namespace Babel.Api.Agent;

/// <summary>
/// <b>ورقة السؤال كما يرسمها الخادم — من بياناتٍ محلّية إلى الشاشة مباشرةً.</b>
/// <para>
/// <b>وموضعُه الجذرُ التركيبي لا وحدةُ الذكاء، وذلك حارسٌ قائم لا ذوق:</b>
/// <c>TheNameSheetIsNeverReachableFromTheAgent</c> يفرض أنّ <c>src/Babel.Ai/</c> — وهي
/// الوحدة التي يمرّ منها النموذج — <b>لا تسمّي</b> <see cref="INameCandidateSheetSource"/>
/// ولا <c>ListForSheetAsync</c> بحرفٍ واحد. فحقنُ منفذِ الجَرد في مسار النموذج لا يقع
/// سهواً في سطر: لا يوجد ملفٌّ هناك يستطيع أن يكتبه. والجذر التركيبي هو الموضع الذي
/// يعرف الطرفين ويترجم بينهما، وهو الذي يملك الطرف الإنسانيّ من هذا الحدّ.
/// </para>
/// <para>
/// والحدّ في جملة: النموذج يقول «هذا الاسم ملتبس، اسأل»؛ فيجرد <b>الخادم</b> المرشّحين
/// من سجلّ الوحدة المالكة عبر <see cref="INameCandidateSheetSource"/> — وهو منفذٌ
/// منفصلٌ عن منفذ السبر <b>عمداً</b>: هذا يُعيد أسماءً وذاك لا يُعيدها أبداً — ويرسم
/// الورقة، ويختار الإنسان، ويعود إلى النموذج <b>مِقبضٌ واحد</b>.
/// </para>
/// <para>
/// <b>وما لا يبلغ النموذج:</b> لا اسمٌ من الأسماء، ولا عددُها، ولا موضعُ ما اختير، ولا
/// أنّ اختياراً وقع أصلاً. وشكلُ ما يعود <c>{"handle":"…"}</c> واحدٌ في كل الحالات.
/// </para>
/// <para>
/// <b>ولماذا رمزٌ لكل خيارٍ لا فهرس:</b> الفهرس يُعدّ. من يرى <c>{"choice":3}</c> يعلم
/// أن الخيارات كانت أربعةً على الأقل، وثلاثُ محاولاتٍ بأسماءٍ متدرّجة تمسح السجلّ.
/// والرمز معمّى بطولٍ ثابت، فلا يُعدّ ولا يُقارَن ولا يُزوَّر.
/// </para>
/// </summary>
internal sealed class AgentQuestionSheetDesk : IAgentQuestionSheets
{
    private readonly Dictionary<string, INameCandidateSheetSource> _sheets;
    private readonly ILookupHandles _handles;
    private readonly IAgentWorkspaceStore _store;
    private readonly AgentWorkspaceOptions _options;
    private readonly LookupOptions _lookup;

    /// <summary>يركّب راسم الأوراق.</summary>
    /// <param name="sheets">مصادر الجرد كما سجّلتها الوحدات المالكة.</param>
    /// <param name="handles">مُصدِر المقابض.</param>
    /// <param name="store">مخزن الجلسات.</param>
    /// <param name="options">إعدادات المساحة.</param>
    /// <param name="lookup">إعدادات البحث — ومنها عمر المِقبض.</param>
    /// <exception cref="ArgumentException">إن سجّل مصدران المفتاح نفسه.</exception>
    public AgentQuestionSheetDesk(
        IEnumerable<INameCandidateSheetSource> sheets,
        ILookupHandles handles,
        IAgentWorkspaceStore store,
        AgentWorkspaceOptions options,
        LookupOptions lookup)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lookup);

        _sheets = new Dictionary<string, INameCandidateSheetSource>(StringComparer.Ordinal);

        foreach (INameCandidateSheetSource source in sheets)
        {
            // ‏**مفتاحٌ مكرَّر يُسقط التركيب** — نفس ما يفعله `NameRegisterLookup`: ورقةٌ
            // تُجرد من سجلّ وحدةٍ أخرى تعرض على المستخدم أسماء لا تخصّ سؤاله.
            if (!_sheets.TryAdd(source.RegisterKey, source))
            {
                throw new ArgumentException(
                    "مصدرا ورقةٍ بمفتاح واحد «" + source.RegisterKey + "» — فلا يُركَّب الراسم. "
                    + "/ two sheet sources claim the key '" + source.RegisterKey + "'.",
                    nameof(sheets));
            }
        }

        _handles = handles;
        _store = store;
        _options = options;
        _lookup = lookup;
    }

    /// <inheritdoc />
    public async Task<Result<string>> AwaitAnswerAsync(
        Guid questionId,
        AgentCaller caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        AgentWorkspaceSession? session = _store.FindForLoop(caller.SessionId);
        if (session is null)
        {
            return Result<string>.Failure(AgentWorkspaceErrors.SessionNotFound);
        }

        // ‏**معرّف الورقة يصل هنا مفكوكاً من مِقبضه** (البوّابة فكّته)، وما قُيّد عند
        // رفعها مقيَّدٌ بنصّ المِقبض. فتُقرأ القيود بالمعرّف المفكوك.
        (string Token, string RegisterKey, string SubjectText)? raised = session.RaisedQuestion(questionId);

        if (raised is null)
        {
            return Result<string>.Failure(AgentWorkspaceErrors.NoPendingQuestion);
        }

        if (!_sheets.TryGetValue(raised.Value.RegisterKey, out INameCandidateSheetSource? source))
        {
            return Result<string>.Failure(LookupErrors.NoRegisterSource(raised.Value.RegisterKey));
        }

        IReadOnlyList<NameCandidate> candidates = await source
            .ListForSheetAsync(
                new NameCandidateRequest(raised.Value.SubjectText, caller.Tenant, caller.CompanyId),
                Math.Min(_options.SheetOptionCap, _lookup.QuestionSheetCap),
                cancellationToken)
            .ConfigureAwait(false);

        List<AgentSheetOption> options = [];

        foreach (NameCandidate candidate in candidates)
        {
            Result<string> token = _handles.Issue(
                LookupHandlePurpose.Option,
                caller.Tenant,
                caller.CompanyId,
                caller.SessionId,
                candidate.Id,
                _lookup.HandleLifetime);

            if (token.IsFailure)
            {
                return Result<string>.Failure(token.Errors);
            }

            options.Add(new AgentSheetOption(token.Value, candidate.LabelAr, candidate.SubtitleAr));
        }

        if (options.Count == 0)
        {
            return Result<string>.Failure(LookupErrors.NoRegisterSource(raised.Value.RegisterKey));
        }

        AgentWorkspaceQuestion question = new(
            raised.Value.Token,
            raised.Value.RegisterKey,
            raised.Value.SubjectText,
            options,

            // ‏**«جديد» غير موصولةٍ بعد** — والورقة تقول ذلك بحقلٍ لا بصمت. ووصلُها يحتاج
            // منفذ إنشاءٍ في الوحدة المالكة، وهو سطحٌ آخر لم ينزل. (نقصٌ مُعلَن)
            AllowsCreate: false);

        using CancellationTokenSource waiting =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waiting.CancelAfter(_options.HumanWait);

        Result<string> chosen = await session.AwaitAnswerAsync(question, waiting.Token).ConfigureAwait(false);

        if (chosen.IsFailure)
        {
            return Result<string>.Failure(chosen.Errors);
        }

        // ‏**يُفتدى رمز الخيار في الخادم** — لا يُقرأ ولا يُصدَّق نصُّه: توقيعٌ يحمل غرضه
        // ومنشأته وشركته وجلسته داخل بايتاته، ويُقارَن بنطاق هذه الجلسة بعينها.
        Result<RedeemedLookupHandle> redeemed = _handles.Redeem(
            chosen.Value, LookupHandlePurpose.Option, caller.Tenant, caller.CompanyId, caller.SessionId);

        if (redeemed.IsFailure)
        {
            return Result<string>.Failure(redeemed.Errors);
        }

        // ‏**ويُسكّ مِقبض كِيانٍ جديد** — لا يُعاد رمز الخيار نفسه: غرضُه «خيارٌ على ورقة»
        // لا «كِيان»، ولو أُعيد لسقط عند الغرض في أوّل حقلٍ يُكتب فيه.
        return _handles.Issue(
            LookupHandlePurpose.Entity,
            caller.Tenant,
            caller.CompanyId,
            caller.SessionId,
            redeemed.Value.Subject,
            _lookup.HandleLifetime);
    }
}
