using Babel.SharedKernel;
using Xunit;

namespace Babel.Pos.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class PosModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Pos, PosModuleInfo.Module);
        Assert.True(PosModuleInfo.Name.IsAssigned);
    }
}
