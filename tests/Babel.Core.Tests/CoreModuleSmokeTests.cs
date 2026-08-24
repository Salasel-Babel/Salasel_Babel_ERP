using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز.</summary>
public sealed class CoreModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Core, CoreModuleInfo.Module);
        Assert.True(CoreModuleInfo.Name.IsAssigned);
    }
}
