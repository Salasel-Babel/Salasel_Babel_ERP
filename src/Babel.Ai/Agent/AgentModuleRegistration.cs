using Babel.Ai.Agent.Anthropic;
using Babel.Ai.Lookup;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ai.Agent;

/// <summary>
/// تركيب حلقة الوكيل. <b>دالّةٌ منفصلة عن <c>AddBabelAi</c> عمداً</b>: الوحدة تعمل بلا
/// وكيل، والوكيل يحتاج مفتاحاً ومنافذَ لا يملكها كل ناشر. فمن ركّبها قصدها.
/// <para>
/// <b>وما لا تسجّله:</b> لا <see cref="IAgentQuestionSheets"/> ولا
/// <see cref="IAgentDraftSubmitter"/> ولا <c>INameCandidateSource</c>. أوّلها يملكه سطح
/// أوراق السؤال، وثانيها الجذر التركيبي، وثالثها <b>الوحدات المالكة للأسماء</b> —
/// والقاعدة 3 تمنع هذه الوحدة من معرفة أيٍّ منها. فمن ركّب الحلقة بلا منافذها يسقط عند
/// حلّ <see cref="AgentTurnService"/>، <b>وذلك مقصود</b>: وكيلٌ بلا سجلّ أسماء يخترع
/// المقابض، ووكيلٌ بلا منفّذ يُنتج مسوّداتٍ لا تُحفظ. وهي سابقة
/// <c>InvoiceCaptureService</c> نفسها في هذا المستودع.
/// </para>
/// </summary>
public static class AgentModuleRegistration
{
    /// <summary>يسجّل الحلقة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelAgentLoop(this IServiceCollection services)
        => services.AddBabelAgentLoop(static _ => { });

    /// <summary>يسجّل الحلقة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    /// <exception cref="InvalidOperationException">إن اعتلّت الإعدادات أو اعتلّ الكتالوج.</exception>
    public static IServiceCollection AddBabelAgentLoop(
        this IServiceCollection services,
        Action<AgentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AgentOptions options = new();
        configure(options);

        // ‏**إعدادٌ معتلّ لا يُركَّب** — والرمي عند الإقلاع أرخص من نداءٍ يسقط في الإنتاج.
        Result validation = options.Validate();
        if (validation.IsFailure)
        {
            throw new InvalidOperationException(
                "إعدادات حلقة الوكيل معتلّة فلا تُركَّب: "
                + string.Join(" · ", validation.Errors.Select(static error => error.MessageAr)));
        }

        services.AddSingleton(options);

        // ‏**الكتالوج يُقرأ ويُرشَّح ويُتحقَّق منه هنا** — ويرمي إن بقي فيه ما ترفضه
        // البوّابة. فلا يعرض النموذجُ باباً لا يُعكَس ولو لمرّة.
        services.AddSingleton(AgentToolCatalogue.Embedded);

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IAgentModelGateway, AnthropicAgentGateway>();
        services.AddSingleton<IAgentSpendLedger, InMemoryAgentSpendLedger>();
        services.AddSingleton<IAgentTenantBillingSource, OwnerKeyBillingSource>();

        services.AddSingleton<LookupOptions>();
        services.AddSingleton<ILookupHandles>(provider => SignedLookupHandles.FromEnvironment(
            provider.GetRequiredService<LookupOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddSingleton<NameRegisterLookup>();
        services.AddScoped<AgentTurnService>();

        return services;
    }
}
