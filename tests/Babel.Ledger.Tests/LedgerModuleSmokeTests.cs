using Babel.SharedKernel;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز.</summary>
public sealed class LedgerModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Ledger, LedgerModuleInfo.Module);
        Assert.True(LedgerModuleInfo.Name.IsAssigned);
    }
}
