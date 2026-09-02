using Babel.Ai.Agent;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ai.Workspace;

/// <summary>
/// تركيب مساحة العمل الجانبية فوق حلقة الوكيل.
/// <para>
/// <b>ودالّةٌ منفصلة عن <c>AddBabelAgentLoop</c> عمداً</b>، بنفس حجّة انفصال الحلقة عن
/// <c>AddBabelAi</c>: الحلقة تعمل بلا مساحة (مسارٌ صوتيّ أو دفعة)، والمساحة تحتاج مخزن
/// جلساتٍ ومنفّذ مسوّداتٍ ومصادر أوراق. فمن ركّبها قصدها.
/// </para>
/// <para>
/// <b>وما لا تسجّله هي كذلك: <see cref="IAgentQuestionSheets"/>.</b> راسمُ الأوراق يحتاج
/// منفذ جَرد الأسماء، و<c>TheNameSheetIsNeverReachableFromTheAgent</c> يمنع
/// <c>src/Babel.Ai/</c> من أن تسمّيه بحرفٍ واحد — فالراسم يعيش في الجذر التركيبي
/// ويُسجَّل هناك. ومن ركّب المساحة بلا راسمٍ يسقط عند حلّ <c>AgentTurnService</c>، وذلك
/// مقصود: وكيلٌ يلتبس عليه اسمٌ ولا ورقةَ له يخترع مِقبضاً.
///
/// <b>وما تسجّله:</b> <see cref="IAgentDraftSubmitter"/> يصير
/// <see cref="AgentDraftConfirmationGate"/> ملفوفاً حول منفّذٍ حقيقي — ومن لم يسجّل
/// منفّذاً حقيقياً يأخذ <see cref="UnavailableAgentDraftSubmitter"/> الذي يرفض بجملةٍ
/// تسمّي ما ينقص، ولا يبتلع الخطوة بصمت.
/// </para>
/// </summary>
public static class AgentWorkspaceRegistration
{
    /// <summary>يسجّل المساحة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelAgentWorkspace(this IServiceCollection services)
        => services.AddBabelAgentWorkspace(static _ => { });

    /// <summary>يسجّل المساحة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelAgentWorkspace(
        this IServiceCollection services,
        Action<AgentWorkspaceOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AgentWorkspaceOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IAgentWorkspaceStore, InMemoryAgentWorkspaceStore>();

        // ‏**الوجهة الافتراضية ترفض بالاسم** — ولا تُسجَّل بوصفها `IAgentDraftSubmitter`
        // كي لا يلتقطها الباب الملفوف بدل نفسه.
        services.AddSingleton<UnavailableAgentDraftSubmitter>();

        services.AddScoped<IAgentDraftSubmitter>(provider => new AgentDraftConfirmationGate(
            provider.GetService<UnavailableAgentDraftSubmitter>()!,
            provider.GetRequiredService<IAgentWorkspaceStore>(),
            provider.GetRequiredService<AgentWorkspaceOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        // ‏**والمساحة مفردة والحلقة مُنطقة**: الجلسة تعيش عبر الطلبات، والدور يُحلّ من
        // نطاقٍ خاصّ به. ومصنعٌ يفتح نطاقاً لكل دور هو ما يمنع كائناً مُنطقاً من أن يعيش
        // في مفردة — وهو أشهر عطلٍ في التركيب على الإطلاق.
        services.AddSingleton(provider => new AgentWorkspaceService(
            provider.GetRequiredService<IAgentWorkspaceStore>(),
            () => provider.CreateScope().ServiceProvider.GetRequiredService<AgentTurnService>(),
            provider.GetRequiredService<Babel.Ai.Lookup.ILookupHandles>(),
            provider.GetRequiredService<IAgentSpendLedger>(),
            provider.GetRequiredService<IAgentTenantBillingSource>(),
            provider.GetRequiredService<AgentOptions>(),
            provider.GetRequiredService<AgentWorkspaceOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
