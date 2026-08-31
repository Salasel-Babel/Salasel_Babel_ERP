using Babel.SharedKernel;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class PurchasingModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Purchasing, PurchasingModuleInfo.Module);
        Assert.True(PurchasingModuleInfo.Name.IsAssigned);
    }
}
