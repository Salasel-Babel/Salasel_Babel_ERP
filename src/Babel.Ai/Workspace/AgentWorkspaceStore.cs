using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// أين تعيش جلسات مساحة العمل. <b>منفذ</b>: الافتراضي في الذاكرة على منوال
/// <c>InMemoryAgentSpendLedger</c>، ويُستبدل بمخزنٍ مُستديم بسطرٍ في التركيب.
/// </summary>
public interface IAgentWorkspaceStore
{
    /// <summary>يفتح جلسةً جديدة.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="companyId">الشركة.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="companyNameAr">اسم الشركة بالعربية.</param>
    AgentWorkspaceSession Open(TenantId tenant, Guid companyId, UserId user, string companyNameAr);

    /// <summary>
    /// يجد جلسةً <b>بمعرّفها وبنطاقها معاً</b> — ومعرّفٌ صحيح من منشأةٍ أخرى لا يُوجَد،
    /// ولا يُفرَّق في الرفض بين «لا وجود له» و«ليس لك».
    /// </summary>
    /// <param name="sessionId">معرّف الجلسة.</param>
    /// <param name="tenant">منشأة الطالب.</param>
    /// <param name="companyId">شركة الطالب.</param>
    /// <param name="user">المستخدم الطالب.</param>
    Result<AgentWorkspaceSession> Find(Guid sessionId, TenantId tenant, Guid companyId, UserId user);

    /// <summary>يجد جلسةً بمعرّفها وحده — <b>للحلقة نفسها</b>، وهي التي أصدرت المعرّف.</summary>
    /// <param name="sessionId">معرّف الجلسة.</param>
    AgentWorkspaceSession? FindForLoop(Guid sessionId);
}

/// <summary>
/// مخزنٌ في الذاكرة عمرُه عمر العملية. <b>وهو مُعلَن لا مُخفى</b>: إعادةُ إقلاع الخادم
/// تُنهي كل محادثةٍ جارية، وتُقرأ في اللوحة «الجلسة انقطعت» — وهي حالةٌ للوحة حالٌ
/// تعرضها لا خطأٌ صامت.
/// </summary>
public sealed class InMemoryAgentWorkspaceStore : IAgentWorkspaceStore
{
    private readonly ConcurrentDictionary<Guid, AgentWorkspaceSession> _sessions = new();
    private readonly TimeProvider _clock;
    private readonly TimeSpan _idleLife;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="clock">مصدر الوقت — لا <c>DateTimeOffset.UtcNow</c> مباشرةً.</param>
    /// <param name="options">إعدادات المساحة، ومنها عمر الجلسة الخاملة.</param>
    public InMemoryAgentWorkspaceStore(TimeProvider clock, AgentWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _idleLife = options.IdleSessionLife;
    }

    /// <inheritdoc />
    public AgentWorkspaceSession Open(TenantId tenant, Guid companyId, UserId user, string companyNameAr)
    {
        Sweep();

        AgentWorkspaceSession session = new(
            Guid.NewGuid(), tenant, companyId, user, companyNameAr, _clock.GetUtcNow());

        _sessions[session.SessionId] = session;
        return session;
    }

    /// <inheritdoc />
    public Result<AgentWorkspaceSession> Find(Guid sessionId, TenantId tenant, Guid companyId, UserId user)
    {
        if (!_sessions.TryGetValue(sessionId, out AgentWorkspaceSession? session)
            || session.Tenant != tenant
            || session.CompanyId != companyId
            || session.User != user
            || Expired(session))
        {
            return Result<AgentWorkspaceSession>.Failure(AgentWorkspaceErrors.SessionNotFound);
        }

        session.Touch(_clock.GetUtcNow());
        return Result<AgentWorkspaceSession>.Success(session);
    }

    /// <inheritdoc />
    public AgentWorkspaceSession? FindForLoop(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out AgentWorkspaceSession? session) ? session : null;

    private bool Expired(AgentWorkspaceSession session) =>
        _clock.GetUtcNow() - session.TouchedAt >= _idleLife;

    private void Sweep()
    {
        foreach (KeyValuePair<Guid, AgentWorkspaceSession> entry in _sessions)
        {
            if (Expired(entry.Value))
            {
                _sessions.TryRemove(entry.Key, out _);
            }
        }
    }
}
