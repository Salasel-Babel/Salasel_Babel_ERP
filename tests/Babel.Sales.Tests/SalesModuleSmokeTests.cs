using Babel.SharedKernel;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class SalesModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Sales, SalesModuleInfo.Module);
        Assert.True(SalesModuleInfo.Name.IsAssigned);
    }
}
