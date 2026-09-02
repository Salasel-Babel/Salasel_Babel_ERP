using Babel.ArchitectureTests.Support;
using Babel.Contracts.Lookup;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>«الفصل هو الحارس» — وكان اسماً بلا مفعول.</b>
/// <para>
/// العقد يقول إنّ <see cref="INameCandidateSource"/> و<see cref="INameCandidateSheetSource"/>
/// مفصولان <b>عمداً</b>: «هذا يُعيد أسماءً، وذاك لا يُعيدها أبداً؛ ومن يحقن هذا في مسار
/// النموذج يكون قد فعل ذلك باسمٍ يقول ما يفعل». <b>وكان محوّلٌ واحد يُنفّذ المنفذَين
/// معاً</b> — وهو الكائن الذي تُسجّله تعليمة التركيب منفذَ سبر. فتحويلٌ واحد على متغيّرٍ
/// نوعُه منفذ السبر كان يُعيد الأسماء والصفوف <b>والعدد الدقيق</b>: الثلاثة التي قال
/// المالك إنها لا تعبر إلى النموذج.
/// </para>
/// <para>
/// <b>وحارسان لأن الالتفافين اثنان:</b> كائنٌ يحمل الوجهين (يُمسَك بالانعكاس على كل نوعٍ
/// في الشجرة) · ووحدة الوكيل تُشير إلى منفذ الجَرد بالاسم (يُمسَك بمسح المصدر). ولا يكفي
/// أحدهما: الأوّل يمنع التحويل، والثاني يمنع الحقن المباشر.
/// </para>
/// </summary>
public sealed class TheNameSheetIsNeverReachableFromTheAgent
{
    /// <summary>الوحدة التي يمرّ منها النموذج، ولا يجوز أن تعرف الجَرد.</summary>
    private const string AgentSourcePath = "src/Babel.Ai/";

    /// <summary>
    /// لا نوع في هذه الشجرة يُنفّذ المنفذَين معاً. <b>ونوعٌ واحد يفعل يُبطل الفصل كلّه</b>
    /// بتحويلٍ واحد، بلا سطرٍ يُضاف في أي وحدة.
    /// </summary>
    [Fact]
    public void NoTypeImplementsBothTheProbePortAndTheSheetPort()
    {
        List<string> both = [];

        foreach (Type type in BabelAssemblies.AllTypes())
        {
            if (type.IsAssignableTo(typeof(INameCandidateSource))
                && type.IsAssignableTo(typeof(INameCandidateSheetSource)))
            {
                both.Add(type.FullName ?? type.Name);
            }
        }

        Assert.Empty(both);
    }

    /// <summary>
    /// ووحدة الوكيل <b>لا تسمّي</b> منفذ الجَرد ولا محوّله. فحقنُه هناك لا يقع سهواً
    /// في سطرٍ واحد: لا يوجد سطرٌ واحد يقع فيه.
    /// </summary>
    [Fact]
    public void TheAgentModuleNamesNeitherTheSheetPortNorItsAdapter()
    {
        List<string> offenders = [];

        string root = Path.Combine(RepositoryLayout.Root, AgentSourcePath.Replace('/', Path.DirectorySeparatorChar));

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);

            foreach (string forbidden in new[]
            {
                nameof(INameCandidateSheetSource),
                nameof(NameCandidate) + " ",
                "ListForSheetAsync",
                "PostgresNameSheet",
            })
            {
                if (source.Contains(forbidden, StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetRelativePath(RepositoryLayout.Root, file) + " ⇐ " + forbidden.Trim());
                }
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// <b>والشاهد الموجب:</b> المنفذان موجودان فعلاً وكلٌّ منهما يعلن ما يعلن — فلا يمرّ
    /// الحارسان أعلاه على لا شيء بعد إعادة تسميةٍ صامتة.
    /// </summary>
    [Fact]
    public void BothPortsExistAndOnlyOneOfThemCanReturnAName()
    {
        Assert.True(typeof(INameCandidateSource).IsInterface);
        Assert.True(typeof(INameCandidateSheetSource).IsInterface);

        // ‏**والفحص على النوع لا على نصّه.** كان مكتوباً `Contains("NameCandidate>")`،
        // و`Task<IReadOnlyList<NameCandidate>>` يُطبَع `…NameCandidate]]` — فالتأكيد
        // السالب كان يمرّ **بلا أن يقيس شيئاً**، والموجب كان سيسقط لو كُتب.
        Assert.DoesNotContain(
            typeof(INameCandidateSource).GetMethods(),
            static method => Mentions(method.ReturnType, typeof(NameCandidate)));

        Assert.Contains(
            typeof(INameCandidateSheetSource).GetMethods(),
            static method => Mentions(method.ReturnType, typeof(NameCandidate)));
    }

    /// <summary>هل يذكر هذا النوع — أو أحد وسائطه العامّة بأي عمق — النوعَ المطلوب؟</summary>
    private static bool Mentions(Type candidate, Type wanted)
        => candidate == wanted
        || Array.Exists(candidate.GetGenericArguments(), argument => Mentions(argument, wanted));
}
