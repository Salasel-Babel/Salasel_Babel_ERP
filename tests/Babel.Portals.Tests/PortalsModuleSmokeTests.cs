using Babel.SharedKernel;
using Xunit;

namespace Babel.Portals.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class PortalsModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Portals, PortalsModuleInfo.Module);
        Assert.True(PortalsModuleInfo.Name.IsAssigned);
    }
}
