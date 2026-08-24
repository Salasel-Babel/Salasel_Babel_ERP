using Babel.SharedKernel;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class ComplianceModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Compliance, ComplianceModuleInfo.Module);
        Assert.True(ComplianceModuleInfo.Name.IsAssigned);
    }
}
