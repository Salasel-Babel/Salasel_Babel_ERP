using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests;

/// <summary>بطاقة الوحدة: هويتها واسمها ثنائي اللغة موجودان — وهو ما تفحصه القاعدة 9.</summary>
public sealed class AiModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Ai, AiModuleInfo.Module);
        Assert.True(AiModuleInfo.Name.IsAssigned);
    }
}
