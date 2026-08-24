using Babel.SharedKernel;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class HrModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Hr, HrModuleInfo.Module);
        Assert.True(HrModuleInfo.Name.IsAssigned);
    }
}
