namespace Babel.Ai.Workspace;

/// <summary>إعدادات مساحة العمل الجانبية.</summary>
public sealed class AgentWorkspaceOptions
{
    /// <summary>عمر الجلسة الخاملة قبل أن تُطوى. والانقضاء يُقرأ في اللوحة «الجلسة انقطعت».</summary>
    public TimeSpan IdleSessionLife { get; set; } = TimeSpan.FromHours(2);

    /// <summary>أقصى انتظارٍ لجواب إنسان — تأكيداً كان أو اختياراً على ورقة.</summary>
    public TimeSpan HumanWait { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>أقصى انتظارٍ لقراءة أحداثٍ جديدة في نداءٍ واحد.</summary>
    public TimeSpan EventWait { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>سقف الخيارات المعروضة على ورقة السؤال — ورقةٌ بلا نهاية ليست سؤالاً.</summary>
    public int SheetOptionCap { get; set; } = 12;
}
