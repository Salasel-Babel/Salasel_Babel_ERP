using System.Text.RegularExpressions;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// <b>ماسح حدّ الاستحقاق: يقرأ الأعضاء التي تقرّر ما تسمح به حالة الاستحقاق.</b>
/// <para>
/// القاعدة 6 تمنع شيفرة الإنتاج <b>خارج</b> حدّ الاستحقاق من التفرّع على
/// <c>EntitlementState</c>. وهذا الماسح يحرس ما بقي: <b>داخل</b> الحدّ نفسه.
/// فالنسخة الثانية من جدول القرار — وهي بالضبط العطل الذي وُجد الحدّ لمنعه — تمرّ
/// من المسح السابق نظيفةً لأنها في المجلد المسموح به.
/// </para>
/// <para>
/// <b>ولماذا مسح مصدر لا انعكاس:</b> التجميعتان اثنتان بلا مرجعية بينهما ولا مرجعية
/// إليهما من مشروع الاختبارات المعمارية — <c>Babel.ControlPlane</c> مُعلَنة في
/// <see cref="ModuleMap"/> بمجموعة مراجع <b>فارغة في الاتجاهين</b>. فالانعكاس يبلغ
/// النواة وحدها، والقرص يبلغ الاثنتين. والمسح <b>بلا تعليقات وبلا نصوص</b>: التعليقات
/// تشرح الشكل الممنوع عمداً، والنصوص العربية تحمل استكمالاً بأقواس معقوفة يخلّ بعدّ
/// العمق (نفس علّة القاعدة 12).
/// </para>
/// </summary>
internal static partial class EntitlementDecisionScan
{
    /// <summary>القيمة التي يدور عليها كل شيء: «للقراءة فقط» تعني القراءة وحدها.</summary>
    private const string ReadOnlyToken = "EntitlementState.ReadOnly";

    /// <summary>
    /// مفردتا نيّة الوصول في التجميعتين: <c>EntitlementAccess</c> في النواة و
    /// <c>AccessIntent</c> في مستوى التحكّم. اقتران إحداهما بحالةٍ داخل عضو واحد
    /// هو <b>شكل جدول القرار</b>، وهو ما يُعدّ.
    /// </summary>
    private static readonly string[] AccessTokens = ["EntitlementAccess.", "AccessIntent."];

    private static readonly Lazy<IReadOnlyList<EntitlementSeam>> Cached = new(Load);

    /// <summary>حدود الاستحقاق المكتشَفة على القرص — مجلد <c>/Entitlement/</c> في كل مشروع تحت <c>src/</c>.</summary>
    public static IReadOnlyList<EntitlementSeam> Seams => Cached.Value;

    /// <summary>
    /// النصّ المُوحَّد لعضوٍ: بلا فراغات، وباسمَي نوعَي نيّة الوصول موحَّدين، وباسمَي
    /// الوسيطين موحَّدين — كي يُقارَن جدولا التجميعتين <b>قاعدةً بقاعدة</b> لا حرفاً بحرف.
    /// </summary>
    public static string Normalise(EntitlementSeamMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        string text = member.Text;

        // نوعا نيّة الوصول اسمان لشيء واحد.
        text = text.Replace("EntitlementAccess", "ACCESS", StringComparison.Ordinal);
        text = text.Replace("AccessIntent", "ACCESS", StringComparison.Ordinal);

        // وأسماء الوسيطين اصطلاح لا قاعدة.
        foreach (string identifier in member.ParameterNames)
        {
            text = Regex.Replace(
                text,
                @"\b" + Regex.Escape(identifier) + @"\b",
                "ARG",
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(5));
        }

        // واسم العضو نفسه، والفاصلة الأخيرة قبل القوس، والفراغ كلّه.
        text = Regex.Replace(text, @"\b" + Regex.Escape(member.Name) + @"\b", "MEMBER", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"\s+", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        text = text.Replace(",}", "}", StringComparison.Ordinal);

        return text;
    }

    private static List<EntitlementSeam> Load()
    {
        string sourceRoot = Path.Combine(RepositoryLayout.Root, "src");
        Dictionary<string, List<string>> byProject = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/');

            if (relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal)
                || !relative.Contains("/Entitlement/", StringComparison.Ordinal))
            {
                continue;
            }

            // src/<المشروع>/… — اسم المشروع هو التجميعة، وهي وحدة «مرّة واحدة».
            string project = relative.Split('/')[1];
            if (!byProject.TryGetValue(project, out List<string>? files))
            {
                files = [];
                byProject[project] = files;
            }

            files.Add(relative);
        }

        List<EntitlementSeam> seams = [];

        foreach ((string project, List<string> files) in byProject.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            List<EntitlementSeamMember> members = [];
            List<string> blockScoped = [];

            foreach (string relative in files.Order(StringComparer.Ordinal))
            {
                string raw = File.ReadAllText(Path.Combine(RepositoryLayout.Root, relative));

                // فضاء اسم بقوسين يُزيح عمق الأعضاء درجةً فيمرّ المسح فارغاً — يُرفض صراحةً
                // بدل أن يُقرأ صفراً (فخ-68).
                if (!FileScopedNamespace().IsMatch(raw))
                {
                    blockScoped.Add(relative);
                }

                members.AddRange(MembersOf(project, relative, Sanitize(raw)));
            }

            seams.Add(new EntitlementSeam(project, files.Order(StringComparer.Ordinal).ToList(), members, blockScoped));
        }

        return seams;
    }

    /// <summary>
    /// يستبدل محتوى التعليقات والنصوص بفراغات <b>مع حفظ الأطوال</b>: النصّ العربي هنا
    /// يحمل استكمالاً بأقواس معقوفة، ولو بقي لأخلّ بعدّ العمق فانقسم العضو في غير موضعه.
    /// </summary>
    private static string Sanitize(string text)
    {
        char[] output = text.ToCharArray();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    output[i++] = ' ';
                }

                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    if (text[i] != '\n')
                    {
                        output[i] = ' ';
                    }

                    i++;
                }

                for (int k = 0; k < 2 && i < text.Length; k++)
                {
                    output[i++] = ' ';
                }

                continue;
            }

            if (c == '\'')
            {
                i = BlankQuoted(text, output, i, '\'', verbatim: false);
                continue;
            }

            if (c == '"')
            {
                bool verbatim = i > 0 && (text[i - 1] == '@' || (i > 1 && text[i - 1] == '$' && text[i - 2] == '@'));
                i = BlankQuoted(text, output, i, '"', verbatim);
                continue;
            }

            i++;
        }

        return new string(output);
    }

    private static int BlankQuoted(string text, char[] output, int start, char quote, bool verbatim)
    {
        int i = start + 1;

        while (i < text.Length)
        {
            char c = text[i];

            if (verbatim)
            {
                if (c == quote)
                {
                    if (i + 1 < text.Length && text[i + 1] == quote)
                    {
                        output[i] = ' ';
                        output[i + 1] = ' ';
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }
            }
            else
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    output[i] = ' ';
                    output[i + 1] = ' ';
                    i += 2;
                    continue;
                }

                if (c == quote)
                {
                    return i + 1;
                }

                if (c == '\n')
                {
                    return i;
                }
            }

            if (c != '\n')
            {
                output[i] = ' ';
            }

            i++;
        }

        return i;
    }

    /// <summary>
    /// يقسّم الملفّ إلى أعضاء: أجسام الأنواع عند العمق صفر، والأعضاء داخلها عند العمق واحد،
    /// وحدّ العضو إمّا كتلة متوازنة وإمّا فاصلة منقوطة عند عمق العضو.
    /// </summary>
    private static List<EntitlementSeamMember> MembersOf(string project, string relative, string code)
    {
        List<EntitlementSeamMember> members = [];
        int i = 0;

        while (i < code.Length)
        {
            if (code[i] != '{')
            {
                i++;
                continue;
            }

            // جسم نوع: يبدأ هنا وينتهي عند قوسه المقابل.
            int typeEnd = MatchingBrace(code, i);
            CollectMembers(project, relative, code, i + 1, typeEnd, members);
            i = typeEnd + 1;
        }

        return members;
    }

    private static void CollectMembers(string project, string relative, string code, int from, int to, List<EntitlementSeamMember> members)
    {
        int start = from;
        int i = from;

        while (i < to)
        {
            char c = code[i];

            if (c == '{')
            {
                int end = MatchingBrace(code, i);
                Add(project, relative, code[start..Math.Min(end + 1, to)], members);
                start = end + 1;
                i = end + 1;
                continue;
            }

            if (c == ';')
            {
                Add(project, relative, code[start..(i + 1)], members);
                start = i + 1;
                i++;
                continue;
            }

            i++;
        }

        if (start < to && !string.IsNullOrWhiteSpace(code[start..to]))
        {
            Add(project, relative, code[start..to], members);
        }
    }

    private static void Add(string project, string relative, string text, List<EntitlementSeamMember> members)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Match header = MemberHeader().Match(text);
        if (!header.Success)
        {
            return;
        }

        string name = header.Groups["name"].Value;
        bool returnsBool = ReturnsBoolPattern().IsMatch(text[..header.Groups["name"].Index]);

        string[] parameters = [.. header.Groups["params"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty)
            .Where(static p => p.Length > 0)];

        members.Add(new EntitlementSeamMember(project, relative, name, text.Trim(), returnsBool, parameters));
    }

    private static int MatchingBrace(string code, int open)
    {
        int depth = 0;

        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return code.Length - 1;
    }

    /// <summary>ترويسة عضو: اسمٌ ثم قائمة وسائط ثم جسم — أو خاصّية بجسم تعبيري.</summary>
    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\((?<params>[^()]*)\))?\s*(?:=>|\{)", RegexOptions.CultureInvariant)]
    private static partial Regex MemberHeader();

    /// <summary>نوع الإرجاع <c>bool</c> مجرّداً — لا <c>Task&lt;bool&gt;</c> ولا <c>bool?</c>.</summary>
    [GeneratedRegex(@"\bbool\s+$", RegexOptions.CultureInvariant)]
    private static partial Regex ReturnsBoolPattern();

    [GeneratedRegex(@"^\s*namespace\s+[A-Za-z_][A-Za-z0-9_.]*\s*;\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex FileScopedNamespace();

    /// <summary>عضوٌ واحد في حدّ الاستحقاق، مقروءاً من المصدر.</summary>
    /// <param name="Project">اسم المشروع — أي التجميعة.</param>
    /// <param name="RelativePath">مسار الملف من جذر المستودع.</param>
    /// <param name="Name">اسم العضو.</param>
    /// <param name="Text">نصّه بلا تعليقات ولا نصوص.</param>
    /// <param name="ReturnsBool">هل يُعيد <c>bool</c> مجرّداً؟</param>
    /// <param name="ParameterNames">أسماء وسائطه.</param>
    internal sealed record EntitlementSeamMember(
        string Project,
        string RelativePath,
        string Name,
        string Text,
        bool ReturnsBool,
        IReadOnlyList<string> ParameterNames)
    {
        /// <summary>هل يذكر «للقراءة فقط»؟</summary>
        public bool MentionsReadOnly => Text.Contains(ReadOnlyToken, StringComparison.Ordinal);

        /// <summary>هل يذكر قيمةً من نيّة الوصول؟</summary>
        public bool MentionsAccess => AccessTokens.Any(token => Text.Contains(token, StringComparison.Ordinal));

        /// <summary>
        /// <b>يقرن حالةً بنيّة وصول</b> — أي يحمل شكل جدول القرار، قرّر أم سمّى الرفض.
        /// </summary>
        public bool PairsStateWithAccess => MentionsReadOnly && MentionsAccess;

        /// <summary><b>يقرّر</b>: يقرن، ويُعيد <c>bool</c> — أي يجيب «هل يُسمح؟».</summary>
        public bool Decides => PairsStateWithAccess && ReturnsBool;

        /// <summary>اسم مقروء في رسائل الفشل.</summary>
        public string Display => $"{RelativePath}::{Name}";
    }

    /// <summary>حدّ استحقاق واحد: مجلد <c>/Entitlement/</c> في تجميعة واحدة.</summary>
    /// <param name="Project">اسم المشروع.</param>
    /// <param name="Files">ملفاته.</param>
    /// <param name="Members">كل أعضائه المقروءة.</param>
    /// <param name="BlockScopedNamespaceFiles">ملفات بفضاء اسم بقوسين — يُبطل عدّ العمق.</param>
    internal sealed record EntitlementSeam(
        string Project,
        IReadOnlyList<string> Files,
        IReadOnlyList<EntitlementSeamMember> Members,
        IReadOnlyList<string> BlockScopedNamespaceFiles)
    {
        /// <summary>الأعضاء التي تقرّر.</summary>
        public IReadOnlyList<EntitlementSeamMember> Decisions => [.. Members.Where(static m => m.Decides)];

        /// <summary>الأعضاء التي تقرن حالةً بنيّة وصول — أعمّ من التي تقرّر.</summary>
        public IReadOnlyList<EntitlementSeamMember> Pairings => [.. Members.Where(static m => m.PairsStateWithAccess)];
    }
}
