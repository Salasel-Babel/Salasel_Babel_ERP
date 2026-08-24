using Babel.SharedKernel;
using Xunit;

namespace Babel.RealEstate.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class RealEstateModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.RealEstate, RealEstateModuleInfo.Module);
        Assert.True(RealEstateModuleInfo.Name.IsAssigned);
    }
}
