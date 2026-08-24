using Babel.SharedKernel;
using Xunit;

namespace Babel.Assets.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class AssetsModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Assets, AssetsModuleInfo.Module);
        Assert.True(AssetsModuleInfo.Name.IsAssigned);
    }
}
