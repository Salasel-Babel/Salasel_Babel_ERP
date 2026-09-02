using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// <b>جلسةُ مساحة عملٍ واحدة — سجلُّ أحداثٍ يُقرأ بمؤشّر، وموقفان ينتظران إنساناً.</b>
/// <para>
/// <b>ولماذا سجلٌّ بمؤشّر لا دفقٌ مفتوح:</b> اللوحة تسأل «ما بعد ن؟» وتنتظر جواباً.
/// فانقطاعُ الشبكة — وهو حالةٌ يجب أن تُعالَج لا أن تُنسى — يُستأنف من حيث وقف بلا
/// تكرارٍ ولا فجوة؛ ولا تُبنى آليةُ استئنافٍ ثانية فوق دفقٍ يعرف ترتيبه وينساه.
/// </para>
/// <para>
/// <b>وموقفان ينتظران إنساناً</b>: ورقةُ سؤالٍ حين يلتبس اسم، وتأكيدُ <b>شكل</b> بيانات
/// قبل أن تهبط مسوّدة. وكلاهما يوقف الدور ولا يقتله: الحلقة تنتظر، واللوحة تعرض، ثمّ
/// يمضي الدور من حيث وقف.
/// </para>
/// </summary>
public sealed class AgentWorkspaceSession
{
    private readonly Lock _gate = new();
    private readonly List<AgentWorkspaceEvent> _events = [];
    private readonly List<AgentWorkspaceStep> _steps = [];
    private readonly List<TaskCompletionSource<bool>> _waiters = [];

    private readonly Dictionary<Guid, (string Token, string RegisterKey, string SubjectText)> _raised = [];

    private AgentWorkspaceConfirmation? _confirmation;
    private TaskCompletionSource<Result>? _confirmationAnswer;
    private AgentWorkspaceQuestion? _question;
    private TaskCompletionSource<Result<string>>? _questionAnswer;
    private long _sequence;

    /// <summary>ينشئ جلسةً مربوطةً بمنشأةٍ وشركةٍ ومستخدم.</summary>
    /// <param name="sessionId">معرّف الجلسة — وهو داخل بايتات كل مِقبضٍ تُصدره.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="companyId">الشركة المفتوحة.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="companyNameAr">اسم الشركة بالعربية.</param>
    /// <param name="openedAt">لحظة الفتح.</param>
    public AgentWorkspaceSession(
        Guid sessionId,
        TenantId tenant,
        Guid companyId,
        UserId user,
        string companyNameAr,
        DateTimeOffset openedAt)
    {
        ArgumentNullException.ThrowIfNull(companyNameAr);

        SessionId = sessionId;
        Tenant = tenant;
        CompanyId = companyId;
        User = user;
        CompanyNameAr = companyNameAr;
        OpenedAt = openedAt;
        TouchedAt = openedAt;
        Phase = AgentTurnPhase.Completed;
    }

    /// <summary>معرّف الجلسة.</summary>
    public Guid SessionId { get; }

    /// <summary>المنشأة.</summary>
    public TenantId Tenant { get; }

    /// <summary>الشركة المفتوحة.</summary>
    public Guid CompanyId { get; }

    /// <summary>المستخدم صاحب الجلسة — <b>ولا يقرؤها غيره</b>.</summary>
    public UserId User { get; }

    /// <summary>اسم الشركة بالعربية، كما يُرسَل رسالةَ نظامٍ في وسط الرسائل.</summary>
    public string CompanyNameAr { get; }

    /// <summary>لحظة الفتح.</summary>
    public DateTimeOffset OpenedAt { get; }

    /// <summary>آخر لحظة استُعملت فيها — تُقاس بها الانقضاء.</summary>
    public DateTimeOffset TouchedAt { get; private set; }

    /// <summary>الدور الجاري أو آخر دور.</summary>
    public Guid CurrentTurnId { get; private set; }

    /// <summary>طور الدور.</summary>
    public AgentTurnPhase Phase { get; private set; }

    /// <summary>آخر رقمٍ في سجلّ الأحداث.</summary>
    public long LastSequence
    {
        get
        {
            lock (_gate)
            {
                return _sequence;
            }
        }
    }

    /// <summary>خطوات الخطّة بحالها الآن.</summary>
    public IReadOnlyList<AgentWorkspaceStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    /// <summary>ما ينتظر تأكيداً الآن، أو <c>null</c>.</summary>
    public AgentWorkspaceConfirmation? PendingConfirmation
    {
        get
        {
            lock (_gate)
            {
                return _confirmation;
            }
        }
    }

    /// <summary>ورقة السؤال المعلَّقة الآن، أو <c>null</c>.</summary>
    public AgentWorkspaceQuestion? PendingQuestion
    {
        get
        {
            lock (_gate)
            {
                return _question;
            }
        }
    }

    /// <summary>يُعلم الجلسة أنها استُعملت.</summary>
    /// <param name="now">اللحظة.</param>
    public void Touch(DateTimeOffset now)
    {
        lock (_gate)
        {
            TouchedAt = now;
        }
    }

    /// <summary>يبدأ دوراً جديداً ويعيد معرّفه، أو يرفض إن كان دورٌ يجري.</summary>
    /// <param name="now">اللحظة.</param>
    public Result<Guid> BeginTurn(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (Phase is AgentTurnPhase.Running or AgentTurnPhase.AwaitingHuman)
            {
                return Result<Guid>.Failure(AgentWorkspaceErrors.TurnAlreadyRunning);
            }

            CurrentTurnId = Guid.NewGuid();
            Phase = AgentTurnPhase.Running;
            TouchedAt = now;
            _steps.Clear();
            _confirmation = null;
            _question = null;
            return Result<Guid>.Success(CurrentTurnId);
        }
    }

    /// <summary>يُنهي الدور بطوره الأخير.</summary>
    /// <param name="phase">الطور.</param>
    public void EndTurn(AgentTurnPhase phase)
    {
        lock (_gate)
        {
            Phase = phase;
            _confirmation = null;
            _question = null;
        }

        Wake();
    }

    /// <summary>يقيّد حدثاً في السجلّ ويوقظ من ينتظر.</summary>
    /// <param name="entry">الحدث بلا رقمه — يُسنَد هنا.</param>
    public AgentWorkspaceEvent Append(AgentWorkspaceEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        AgentWorkspaceEvent numbered;

        lock (_gate)
        {
            numbered = entry with { Sequence = ++_sequence };
            _events.Add(numbered);
        }

        Wake();
        return numbered;
    }

    /// <summary>
    /// يقيّد أنّ اسماً غمض في سجلٍّ بعينه — <b>وهي المعلومة التي تُرسَم منها الورقة
    /// محلّياً</b>. ولا شيء منها يعبر إلى النموذج: مفتاح السجلّ نطق به هو، وكلامُ البحث
    /// كلامُه هو.
    /// </summary>
    /// <param name="subject">موضوع المِقبض كما يُفكّ في البوّابة — وهو مفتاح القيد.</param>
    /// <param name="token">نصّ المِقبض المعتِم كما يراه النموذج واللوحة.</param>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    /// <param name="subjectText">كلام البحث.</param>
    public void NoteRaisedQuestion(Guid subject, string token, string registerKey, string subjectText)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(registerKey);
        ArgumentNullException.ThrowIfNull(subjectText);

        lock (_gate)
        {
            _raised[subject] = (token, registerKey, subjectText);
        }
    }

    /// <summary>يقرأ ما قُيّد عن ورقةٍ رُفعت، أو <c>null</c>.</summary>
    /// <param name="subject">موضوع مِقبض الورقة كما فُكّ.</param>
    public (string Token, string RegisterKey, string SubjectText)? RaisedQuestion(Guid subject)
    {
        lock (_gate)
        {
            return _raised.TryGetValue(subject, out (string Token, string RegisterKey, string SubjectText) found)
                ? found
                : null;
        }
    }

    /// <summary>يستبدل خطّة الخطوات كلَّها بما أعلنه النموذج.</summary>
    /// <param name="titles">عناوين الخطوات بترتيبها.</param>
    public IReadOnlyList<AgentWorkspaceStep> ReplacePlan(IReadOnlyList<string> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);

        lock (_gate)
        {
            _steps.Clear();

            for (int index = 0; index < titles.Count; index++)
            {
                _steps.Add(new AgentWorkspaceStep(
                    Guid.NewGuid(), index + 1, titles[index], AgentStepState.Planned, null, null, []));
            }

            return [.. _steps];
        }
    }

    /// <summary>
    /// يفتح خطوةً لأداةٍ بدأت. <b>ويتبنّى أوّل خطوةٍ مُعلَنة لم تبدأ إن وُجدت</b> — فالخطّة
    /// المُعلَنة والخطوات المنفَّذة سلسلةٌ واحدة لا سلسلتان متجاورتان تتباعدان.
    /// </summary>
    /// <param name="toolName">اسم الأداة.</param>
    public AgentWorkspaceStep OpenStep(string toolName)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        lock (_gate)
        {
            int index = _steps.FindIndex(static step => step.State == AgentStepState.Planned);

            if (index >= 0)
            {
                _steps[index] = _steps[index] with { State = AgentStepState.Running, ToolName = toolName };
                return _steps[index];
            }

            AgentWorkspaceStep opened = new(
                Guid.NewGuid(), _steps.Count + 1, toolName, AgentStepState.Running, toolName, null, []);

            _steps.Add(opened);
            return opened;
        }
    }

    /// <summary>يغيّر حال آخر خطوةٍ جارية.</summary>
    /// <param name="state">الحال الجديد.</param>
    /// <param name="screenRoute">مسار الشاشة عند الهبوط.</param>
    /// <param name="errors">أسباب السقوط.</param>
    public AgentWorkspaceStep? CloseStep(
        AgentStepState state,
        string? screenRoute = null,
        IReadOnlyList<Error>? errors = null)
    {
        lock (_gate)
        {
            int index = _steps.FindLastIndex(static step =>
                step.State is AgentStepState.Running or AgentStepState.AwaitingConfirmation
                    or AgentStepState.AwaitingAnswer);

            if (index < 0)
            {
                return null;
            }

            _steps[index] = _steps[index] with
            {
                State = state,
                ScreenRoute = screenRoute ?? _steps[index].ScreenRoute,
                Errors = errors ?? _steps[index].Errors,
            };

            return _steps[index];
        }
    }

    /// <summary>يعلّق طلب تأكيدٍ وينتظر إنساناً.</summary>
    /// <param name="confirmation">البطاقة المعروضة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<Result> AwaitConfirmationAsync(
        AgentWorkspaceConfirmation confirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        TaskCompletionSource<Result> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            _confirmation = confirmation;
            _confirmationAnswer = answer;
            Phase = AgentTurnPhase.AwaitingHuman;

            int index = _steps.FindIndex(step => step.StepId == confirmation.StepId);
            if (index >= 0)
            {
                _steps[index] = _steps[index] with { State = AgentStepState.AwaitingConfirmation };
            }
        }

        Wake();

        await using (cancellationToken.Register(() => answer.TrySetResult(
            Result.Failure(AgentWorkspaceErrors.HumanDidNotAnswerInTime))).ConfigureAwait(false))
        {
            Result verdict = await answer.Task.ConfigureAwait(false);

            lock (_gate)
            {
                _confirmation = null;
                _confirmationAnswer = null;
                Phase = AgentTurnPhase.Running;
            }

            Wake();
            return verdict;
        }
    }

    /// <summary>يقبل تأكيداً من إنسان أو يرفضه.</summary>
    /// <param name="stepId">الخطوة المقصودة — <b>ولا يُقبل تأكيدٌ لغيرها</b>.</param>
    /// <param name="accepted">هل قَبِل شكل البيانات؟</param>
    public Result SettleConfirmation(Guid stepId, bool accepted)
    {
        TaskCompletionSource<Result>? answer;

        lock (_gate)
        {
            if (_confirmation is null || _confirmation.StepId != stepId || _confirmationAnswer is null)
            {
                return Result.Failure(AgentWorkspaceErrors.NothingAwaitsConfirmation);
            }

            answer = _confirmationAnswer;
        }

        answer.TrySetResult(accepted
            ? Result.Success()
            : Result.Failure(AgentWorkspaceErrors.ShapeRefusedByHuman));

        return Result.Success();
    }

    /// <summary>يعلّق ورقة سؤالٍ وينتظر اختيار إنسان، ويعيد رمز الخيار.</summary>
    /// <param name="question">الورقة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<Result<string>> AwaitAnswerAsync(
        AgentWorkspaceQuestion question,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);

        TaskCompletionSource<Result<string>> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            _question = question;
            _questionAnswer = answer;
            Phase = AgentTurnPhase.AwaitingHuman;

            int index = _steps.FindLastIndex(static step => step.State == AgentStepState.Running);
            if (index >= 0)
            {
                _steps[index] = _steps[index] with { State = AgentStepState.AwaitingAnswer };
            }
        }

        Wake();

        await using (cancellationToken.Register(() => answer.TrySetResult(
            Result<string>.Failure(AgentWorkspaceErrors.HumanDidNotAnswerInTime))).ConfigureAwait(false))
        {
            Result<string> chosen = await answer.Task.ConfigureAwait(false);

            lock (_gate)
            {
                _question = null;
                _questionAnswer = null;
                Phase = AgentTurnPhase.Running;

                int index = _steps.FindLastIndex(static step => step.State == AgentStepState.AwaitingAnswer);
                if (index >= 0)
                {
                    _steps[index] = _steps[index] with { State = AgentStepState.Running };
                }
            }

            Wake();
            return chosen;
        }
    }

    /// <summary>
    /// يسلّم اختيار الإنسان. <b>ولا يقرأ نصّ الخيار ولا موضعه</b> — رمزٌ واحد، ويُطابَق
    /// بأنه من هذه الورقة بعينها.
    /// </summary>
    /// <param name="questionId">معرّف الورقة.</param>
    /// <param name="optionToken">رمز الخيار.</param>
    public Result SettleAnswer(string questionId, string optionToken)
    {
        ArgumentNullException.ThrowIfNull(questionId);
        ArgumentNullException.ThrowIfNull(optionToken);

        TaskCompletionSource<Result<string>>? answer;

        lock (_gate)
        {
            if (_question is null
                || !string.Equals(_question.QuestionId, questionId, StringComparison.Ordinal)
                || _questionAnswer is null)
            {
                return Result.Failure(AgentWorkspaceErrors.NoPendingQuestion);
            }

            if (!_question.Options.Any(option =>
                string.Equals(option.OptionToken, optionToken, StringComparison.Ordinal)))
            {
                return Result.Failure(AgentWorkspaceErrors.OptionNotOnThisSheet);
            }

            answer = _questionAnswer;
        }

        answer.TrySetResult(Result<string>.Success(optionToken));
        return Result.Success();
    }

    /// <summary>يقرأ ما بعد مؤشّرٍ، وينتظر إن لم يكن هناك جديد.</summary>
    /// <param name="after">آخر رقمٍ قرأته اللوحة.</param>
    /// <param name="wait">أقصى انتظار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<IReadOnlyList<AgentWorkspaceEvent>> ReadAsync(
        long after,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        List<AgentWorkspaceEvent> found = Since(after);

        if (found.Count > 0 || wait <= TimeSpan.Zero)
        {
            return found;
        }

        TaskCompletionSource<bool> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            // ‏**فحصٌ ثانٍ داخل القفل**: حدثٌ وقع بين القراءة الأولى وتسجيل الانتظار
            // كان سينام عليه القارئ حتى تنقضي المهلة، فتبدو اللوحة معلّقة والحدث موجود.
            if (_sequence > after)
            {
                return Since(after);
            }

            _waiters.Add(waiter);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(wait);

        await using (timeout.Token.Register(() => waiter.TrySetResult(false)).ConfigureAwait(false))
        {
            await waiter.Task.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _waiters.Remove(waiter);
        }

        return Since(after);
    }

    private List<AgentWorkspaceEvent> Since(long after)
    {
        lock (_gate)
        {
            return [.. _events.Where(entry => entry.Sequence > after)];
        }
    }

    private void Wake()
    {
        TaskCompletionSource<bool>[] waiting;

        lock (_gate)
        {
            waiting = [.. _waiters];
            _waiters.Clear();
        }

        foreach (TaskCompletionSource<bool> waiter in waiting)
        {
            waiter.TrySetResult(true);
        }
    }
}
