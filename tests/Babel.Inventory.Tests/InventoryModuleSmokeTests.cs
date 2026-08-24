using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class InventoryModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Inventory, InventoryModuleInfo.Module);
        Assert.True(InventoryModuleInfo.Name.IsAssigned);
    }
}
