using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class AiModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Ai, AiModuleInfo.Module);
        Assert.True(AiModuleInfo.Name.IsAssigned);
    }
}
