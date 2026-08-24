using Babel.SharedKernel;
using Xunit;

namespace Babel.Projects.Tests;

/// <summary>مشروع اختبار الوحدة: موصول وجاهز. الاختبارات الفعلية تأتي مع منطق الوحدة.</summary>
public sealed class ProjectsModuleSmokeTests
{
    [Fact]
    public void ModuleInfo_IdentifiesTheModule()
    {
        Assert.Equal(BabelModule.Projects, ProjectsModuleInfo.Module);
        Assert.True(ProjectsModuleInfo.Name.IsAssigned);
    }
}
