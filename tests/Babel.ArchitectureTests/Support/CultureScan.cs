using System.Text;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// ماسح مصدري للتحويلات المعتمدة على الثقافة.
/// <para>
/// <b>لماذا مصدر لا IL:</b> السلسلة المُستكمَلة <c>$"{x:yyyy-MM}"</c> تُترجَم إلى
/// <c>DefaultInterpolatedStringHandler</c>، وما يبقى في IL هو نصّ المُحدِّد وحده بلا أي
/// أثر يميّز «بلا مزوّد تنسيق» عن «بمزوّد»: كلا الشكلين ينتهيان إلى استدعاء واحد.
/// ولهذا لا يلتقطها <c>CA1305</c> أصلاً — فهو يفحص وجود حِمل زائد يقبل
/// <c>IFormatProvider</c> على <c>ToString(string)</c>، ولا شيء من ذلك في المُستكمَلة.
/// السابقة في هذا المشروع قائمة: <see cref="RepositoryLayout"/> يقرأ ملفات
/// <c>csproj</c> نصّاً لسبب من هذا النوع بالضبط.
/// </para>
/// <para>
/// الماسح مُعجَمي كامل لا تعبير نمطي على النصّ الخام: يميّز التعليق من النصّ من الشيفرة،
/// ويفهم <c>@"…"</c> و<c>"""…"""</c> و<c>$$"""…"""</c> والفجوات المتداخلة — لأن مطابقة
/// النص الخام تُنتج إيجابيات كاذبة في قاعدة يكثر فيها SQL خامّ وتعليق عربي.
/// </para>
/// </summary>
internal static class CultureScan
{
    /// <summary>وسم الاستثناء الوحيد المقبول. ضيّق بحكم بنائه: يُعفي سطراً واحداً لا ملفاً ولا مشروعاً.</summary>
    public const string ExemptionMarker = "ثقافة-عرض:";

    /// <summary>أقصر سبب مقبول. وسمٌ بلا سبب مكتوب هو مخالفة في ذاته.</summary>
    public const int MinimumReasonLength = 24;

    /// <summary>صنف المخالفة.</summary>
    internal enum Kind
    {
        /// <summary>‏<c>$"{x:fmt}"</c> — مُحدِّد تنسيق داخل سلسلة مُستكمَلة بلا مزوّد ثابت.</summary>
        InterpolatedFormat,

        /// <summary>‏<c>x.ToString("fmt")</c> بلا <c>IFormatProvider</c>.</summary>
        ToStringFormat,

        /// <summary>‏<c>Parse</c>/<c>TryParse</c> على نوع أوّلي بلا ثقافة ثابتة.</summary>
        Parse,

        /// <summary>‏<c>string.Format</c> بلا مزوّد.</summary>
        StringFormat,

        /// <summary>‏<c>Convert.To…</c> بلا مزوّد.</summary>
        Convert,

        /// <summary>‏<c>ToUpper()</c>/<c>ToLower()</c> بلا <c>Invariant</c> — فخّ الياء التركية.</summary>
        Casing,

        /// <summary>مقارنة نصّية على معرّف بلا <c>StringComparison</c>.</summary>
        Comparison,

        /// <summary>‏<c>DateTime.Now</c> حيث المقصود <c>UtcNow</c>.</summary>
        LocalClock,

        /// <summary>وسم استثناء بلا سبب مكتوب.</summary>
        UnjustifiedExemption,
    }

    /// <summary>موضع واحد مكتشَف.</summary>
    /// <param name="File">المسار النسبي من جذر المستودع.</param>
    /// <param name="Line">رقم السطر (يبدأ من 1).</param>
    /// <param name="Category">صنف المخالفة.</param>
    /// <param name="Snippet">مقتطف مقروء في رسالة الفشل.</param>
    /// <param name="Exempt">هل يحمل وسم استثناء صالحاً؟</param>
    internal sealed record Finding(string File, int Line, Kind Category, string Snippet, bool Exempt)
    {
        /// <summary>سطر واحد في رسالة الفشل.</summary>
        public override string ToString() => $"{File}:{Line} [{Category}] {Snippet}";
    }

    /// <summary>حصيلة مسح: ما وُجد، وكم فُحص — والثاني هو ما يمنع القاعدة من المرور فراغاً.</summary>
    /// <param name="Findings">كل ما وُجد، معفىً وغير معفى.</param>
    /// <param name="FilesScanned">عدد ملفات <c>.cs</c> التي مُسحت.</param>
    /// <param name="ConversionSites">عدد مواضع التحويل المفحوصة، آمنةً كانت أو لا.</param>
    internal sealed record Result(IReadOnlyList<Finding> Findings, int FilesScanned, int ConversionSites)
    {
        /// <summary>المخالفات الفعلية: ما ليس معفىً.</summary>
        public IReadOnlyList<Finding> Violations => [.. Findings.Where(static f => !f.Exempt)];

        /// <summary>الاستثناءات المعلنة الصالحة.</summary>
        public IReadOnlyList<Finding> Exemptions => [.. Findings.Where(static f => f.Exempt)];
    }

    private static readonly string[] ScannedFolders = ["src", "tools", "demo"];

    /// <summary>يمسح <c>src/</c> و<c>tools/</c> و<c>demo/</c>. ‏<c>spikes/</c> خارج النطاق عمداً — تجارب لا منتج، كما في القاعدة 8.</summary>
    /// <returns>حصيلة المسح.</returns>
    public static Result ScanRepository()
    {
        List<Finding> findings = [];
        int files = 0;
        int sites = 0;

        foreach (string folder in ScannedFolders)
        {
            string absolute = Path.Combine(RepositoryLayout.Root, folder);
            if (!Directory.Exists(absolute))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(absolute, "*.cs", SearchOption.AllDirectories)
                         .Where(static p => !IsGenerated(p))
                         .OrderBy(static p => p, StringComparer.Ordinal))
            {
                files++;
                string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
                Result one = ScanText(File.ReadAllText(path), relative);
                findings.AddRange(one.Findings);
                sites += one.ConversionSites;
            }
        }

        return new Result(findings, files, sites);
    }

    private static bool IsGenerated(string path)
    {
        string normalised = path.Replace('\\', '/');
        return normalised.Contains("/obj/", StringComparison.Ordinal)
            || normalised.Contains("/bin/", StringComparison.Ordinal);
    }

    /// <summary>يمسح نصّاً واحداً. مكشوف كي يستطيع اختبار عدم الفراغ أن يُطعِم الماسح مخالفة مصنوعة.</summary>
    /// <param name="text">نصّ الملف.</param>
    /// <param name="file">اسم يُعرض في النتيجة.</param>
    /// <returns>حصيلة المسح.</returns>
    public static Result ScanText(string text, string file) => new Lexer(text, file).Run();

    /// <summary>
    /// هل هذا المُحدِّد يقرأ الثقافة؟ الجواب <b>نعم</b> إلا لمجموعة واحدة يثبت تعريفها
    /// أنها لا تقرؤها: التنسيق الستّ‌عشري <c>X</c>/<c>x</c> — ناتجه محصور في
    /// <c>0-9A-F</c>، ولا فاصلة فيه ولا إشارة ولا تقويم.
    /// <para>
    /// وما عداه يُرفض <b>ولو كان آمناً فعلاً</b>: <c>Guid.ToString("D")</c> لا يقرأ ثقافة،
    /// لكن الماسح مُعجَمي ولا يعرف نوع المستقبِل، و<c>"D"</c> نفسه على <c>DateTime</c>
    /// هو نمط التاريخ الطويل — أي أنه على أخطر الأنواع. الثمن سطرٌ صريح
    /// <c>CultureInfo.InvariantCulture</c> يتجاهله <c>Guid</c> ولا يكلّف شيئاً؛
    /// والمقابل أن لا يمرّ <c>"D"</c> على تاريخ لأن الماسح ظنّه معرّفاً.
    /// </para>
    /// </summary>
    /// <param name="specifier">نصّ المُحدِّد كما ورد.</param>
    /// <returns>‏<c>true</c> إن كان قد يقرأ الثقافة.</returns>
    public static bool IsCultureSensitiveFormat(string specifier)
    {
        string trimmed = specifier.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] is 'X' or 'x')
        {
            return !trimmed.AsSpan(1).ToString().All(char.IsAsciiDigit);
        }

        return true;
    }

    private sealed class Lexer(string text, string file)
    {
        private readonly string _s = text;
        private readonly List<Finding> _findings = [];
        private string[]? _lines;
        private int _sites;

        private string[] Lines => _lines ??= _s.Split('\n');

        public Result Run()
        {
            int i = 0;
            while (i < _s.Length)
            {
                char c = _s[i];

                if (c == '/' && i + 1 < _s.Length && _s[i + 1] == '/')
                {
                    i = SkipLineComment(i);
                }
                else if (c == '/' && i + 1 < _s.Length && _s[i + 1] == '*')
                {
                    i = SkipBlockComment(i);
                }
                else if (c == '\'')
                {
                    i = SkipCharLiteral(i);
                }
                else if (StringStart(i) is int start && start >= 0)
                {
                    i = ScanString(start);
                }
                else
                {
                    i++;
                }
            }

            ScanCodePatterns();
            ScanExemptionMarkers();
            return new Result(_findings, 1, _sites);
        }

        // ————— الحدود المعجمية —————

        private int SkipLineComment(int i)
        {
            while (i < _s.Length && _s[i] != '\n')
            {
                i++;
            }

            return i;
        }

        private int SkipBlockComment(int i)
        {
            i += 2;
            while (i + 1 < _s.Length && !(_s[i] == '*' && _s[i + 1] == '/'))
            {
                i++;
            }

            return Math.Min(_s.Length, i + 2);
        }

        private int SkipCharLiteral(int i)
        {
            i++;
            while (i < _s.Length && _s[i] != '\'')
            {
                i += _s[i] == '\\' ? 2 : 1;
            }

            return Math.Min(_s.Length, i + 1);
        }

        /// <summary>هل يبدأ عند <paramref name="i"/> نصّ حرفي (بسوابقه <c>$</c>/<c>@</c>)؟ يُرجع موضع البدء أو -1.</summary>
        private int StringStart(int i)
        {
            if (_s[i] == '"')
            {
                return i;
            }

            if (_s[i] is not ('$' or '@'))
            {
                return -1;
            }

            int j = i;
            while (j < _s.Length && _s[j] is '$' or '@')
            {
                j++;
            }

            return j < _s.Length && _s[j] == '"' ? i : -1;
        }

        /// <summary>يمسح نصّاً حرفياً كاملاً ويُرجع الموضع بعده.</summary>
        private int ScanString(int start)
        {
            int i = start;
            int dollars = 0;
            bool verbatim = false;

            while (i < _s.Length && _s[i] is '$' or '@')
            {
                if (_s[i] == '$')
                {
                    dollars++;
                }
                else
                {
                    verbatim = true;
                }

                i++;
            }

            int quotes = 0;
            while (i < _s.Length && _s[i] == '"')
            {
                quotes++;
                i++;
            }

            if (quotes >= 3)
            {
                return ScanRaw(start, i, quotes, Math.Max(dollars, 1), dollars > 0);
            }

            if (quotes == 2)
            {
                return i; // نصّ فارغ
            }

            int contentStart = i;
            while (i < _s.Length)
            {
                char c = _s[i];

                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < _s.Length && _s[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }

                        NoteLiteral(start, contentStart, i, dollars == 0);
                        return i + 1;
                    }
                }
                else
                {
                    if (c == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        NoteLiteral(start, contentStart, i, dollars == 0);
                        return i + 1;
                    }

                    if (c == '\n')
                    {
                        return i; // نصّ غير مُغلق — لا يقع في شيفرة تُبنى
                    }
                }

                if (dollars > 0 && c == '{')
                {
                    if (i + 1 < _s.Length && _s[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }

                    i = ScanHole(i + 1, 1, start);
                    continue;
                }

                i++;
            }

            return i;
        }

        private int ScanRaw(int start, int contentStart, int quotes, int braces, bool interpolated)
        {
            int i = contentStart;
            while (i < _s.Length)
            {
                if (_s[i] == '"')
                {
                    int run = 0;
                    int j = i;
                    while (j < _s.Length && _s[j] == '"')
                    {
                        run++;
                        j++;
                    }

                    if (run >= quotes)
                    {
                        return j;
                    }

                    i = j;
                    continue;
                }

                if (interpolated && _s[i] == '{')
                {
                    int run = 0;
                    int j = i;
                    while (j < _s.Length && _s[j] == '{')
                    {
                        run++;
                        j++;
                    }

                    if (run == braces)
                    {
                        i = ScanHole(j, braces, start);
                        continue;
                    }

                    i = j;
                    continue;
                }

                i++;
            }

            return i;
        }

        /// <summary>يمسح فجوة استكمال. يُرجع الموضع بعد قوس/أقواس الإغلاق.</summary>
        private int ScanHole(int i, int braces, int stringStart)
        {
            int depth = 0;
            int formatStart = -1;

            while (i < _s.Length)
            {
                char c = _s[i];

                if (formatStart >= 0)
                {
                    if (c == '}' && depth == 0)
                    {
                        ReportFormat(formatStart, i, stringStart);
                        return Math.Min(_s.Length, i + braces);
                    }

                    i++;
                    continue;
                }

                if (depth == 0 && c == '}')
                {
                    return Math.Min(_s.Length, i + braces);
                }

                if (depth == 0 && c == ':')
                {
                    if (i + 1 < _s.Length && _s[i + 1] == ':')
                    {
                        i += 2; // ‏global::
                        continue;
                    }

                    formatStart = i + 1;
                    i++;
                    continue;
                }

                if (c is '(' or '[' or '{')
                {
                    depth++;
                    i++;
                    continue;
                }

                if (c is ')' or ']' or '}')
                {
                    depth--;
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < _s.Length && _s[i + 1] == '/')
                {
                    i = SkipLineComment(i);
                    continue;
                }

                if (c == '\'')
                {
                    i = SkipCharLiteral(i);
                    continue;
                }

                if (StringStart(i) is int nested && nested >= 0)
                {
                    i = ScanString(nested);
                    continue;
                }

                i++;
            }

            return i;
        }

        // ————— التصنيف —————

        private void ReportFormat(int from, int to, int stringStart)
        {
            _sites++;
            string specifier = _s[from..to];
            if (!IsCultureSensitiveFormat(specifier))
            {
                return;
            }

            if (HasInvariantProviderBefore(stringStart))
            {
                return;
            }

            Add(from, Kind.InterpolatedFormat, $"$\"{{…:{specifier}}}\" — تنسيق يقرأ ثقافة العملية وتقويمها");
        }

        /// <summary>
        /// هل تسبق السلسلةَ المُستكمَلة ثقافةٌ ثابتة صريحة؟ الأشكال المقبولة الثلاثة هي
        /// كل ما تستعمله هذه القاعدة الشيفرية: <c>string.Create(CultureInfo.InvariantCulture, $"…")</c>،
        /// و<c>sb.Append(CultureInfo.InvariantCulture, $"…")</c>، و<c>FormattableString.Invariant($"…")</c>.
        /// </summary>
        private bool HasInvariantProviderBefore(int stringStart)
        {
            int j = stringStart - 1;
            while (j >= 0 && char.IsWhiteSpace(_s[j]))
            {
                j--;
            }

            if (j < 0)
            {
                return false;
            }

            int windowStart = Math.Max(0, j - 80);
            string before = _s[windowStart..(j + 1)];

            return (before.EndsWith(',') && before.Contains("InvariantCulture", StringComparison.Ordinal))
                || (before.EndsWith('(') && before.Contains("Invariant", StringComparison.Ordinal));
        }

        /// <summary>يُسجَّل نصّ حرفي غير مُستكمَل: المُحدِّد في <c>ToString("fmt")</c> والمقارنات على معرّف.</summary>
        private void NoteLiteral(int start, int contentStart, int contentEnd, bool plain)
        {
            if (!plain)
            {
                return;
            }

            string value = _s[contentStart..contentEnd];
            string before = _s[Math.Max(0, start - 48)..start].TrimEnd();

            if (before.EndsWith(".ToString(", StringComparison.Ordinal))
            {
                _sites++;
                if (IsCultureSensitiveFormat(value) && !HasSecondArgument(contentEnd + 1))
                {
                    Add(start, Kind.ToStringFormat, $".ToString(\"{value}\") بلا IFormatProvider");
                }
            }

            foreach (string method in new[] { ".StartsWith(", ".EndsWith(", ".IndexOf(", ".LastIndexOf(" })
            {
                if (!before.EndsWith(method, StringComparison.Ordinal))
                {
                    continue;
                }

                _sites++;
                if (!HasSecondArgument(contentEnd + 1))
                {
                    Add(start, Kind.Comparison, $"{method.Trim('.', '(')}(\"{value}\") بلا StringComparison");
                }
            }
        }

        private bool HasSecondArgument(int afterLiteral)
        {
            int j = afterLiteral;
            while (j < _s.Length && char.IsWhiteSpace(_s[j]))
            {
                j++;
            }

            return j < _s.Length && _s[j] == ',';
        }

        private static readonly string[] ParseReceivers =
        [
            "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
            "decimal", "double", "float", "Int32", "Int64", "Decimal", "Double", "Single",
            "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan",
        ];

        private static readonly string[] ConvertMethods =
        [
            "Convert.ToString(", "Convert.ToDecimal(", "Convert.ToDateTime(",
            "Convert.ToDouble(", "Convert.ToSingle(", "Convert.ToInt32(", "Convert.ToInt64(",
        ];

        /// <summary>
        /// الأنماط التي لا تحتاج نصّاً حرفياً: التحليل، والتنسيق بمزوّد، وحالة الأحرف، والساعة المحلية.
        /// تُفحص على نسخة من الملف مُطفأ فيها التعليق ومحتوى النصوص، فلا تُطابَق كلمة داخل SQL خامّ.
        /// </summary>
        private void ScanCodePatterns()
        {
            string code = BlankNonCode();

            foreach (string receiver in ParseReceivers)
            {
                foreach (string method in new[] { ".Parse(", ".TryParse(", ".ParseExact(", ".TryParseExact(" })
                {
                    string needle = receiver + method;
                    int at = 0;
                    while ((at = code.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
                    {
                        int nameStart = at;
                        bool boundary = nameStart == 0 || !(char.IsLetterOrDigit(code[nameStart - 1]) || code[nameStart - 1] is '_' or '.');
                        _sites++;
                        if (boundary && !ArgumentsMentionCulture(code, at + needle.Length))
                        {
                            Add(at, Kind.Parse, $"{receiver}{method.Trim('.')} بلا CultureInfo.InvariantCulture");
                        }

                        at += needle.Length;
                    }
                }
            }

            ScanSimple(code, "string.Format(", Kind.StringFormat, "string.Format بلا IFormatProvider", checkArguments: true);
            ScanSimple(code, "String.Format(", Kind.StringFormat, "String.Format بلا IFormatProvider", checkArguments: true);

            foreach (string method in ConvertMethods)
            {
                ScanSimple(code, method, Kind.Convert, method + "…) بلا IFormatProvider", checkArguments: true);
            }

            ScanSimple(code, ".ToUpper()", Kind.Casing, ".ToUpper() بلا Invariant — «ID».ToLower() تصير «ıd» تحت tr-TR", checkArguments: false);
            ScanSimple(code, ".ToLower()", Kind.Casing, ".ToLower() بلا Invariant — «ID».ToLower() تصير «ıd» تحت tr-TR", checkArguments: false);
            ScanSimple(code, "DateTime.Now", Kind.LocalClock, "DateTime.Now — الساعة المحلية حيث المقصود UtcNow", checkArguments: false);
            ScanSimple(code, "DateTimeOffset.Now", Kind.LocalClock, "DateTimeOffset.Now — الساعة المحلية حيث المقصود UtcNow", checkArguments: false);
            ScanSimple(code, "DateTime.Today", Kind.LocalClock, "DateTime.Today — الساعة المحلية حيث المقصود UtcNow", checkArguments: false);
        }

        private void ScanSimple(string code, string needle, Kind kind, string message, bool checkArguments)
        {
            int at = 0;
            while ((at = code.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                _sites++;
                if (!checkArguments || !ArgumentsMentionCulture(code, at + needle.Length))
                {
                    Add(at, kind, message);
                }

                at += needle.Length;
            }
        }

        /// <summary>هل تذكر قائمة الوسائط الممتدة من <paramref name="from"/> ثقافةً أو مزوّداً صريحاً؟</summary>
        private static bool ArgumentsMentionCulture(string code, int from)
        {
            int depth = 1;
            int i = from;
            var buffer = new StringBuilder();

            while (i < code.Length && depth > 0)
            {
                char c = code[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }

                buffer.Append(c);
                i++;
            }

            string arguments = buffer.ToString();
            return arguments.Contains("Culture", StringComparison.Ordinal)
                || arguments.Contains("FormatInfo", StringComparison.Ordinal)
                || arguments.Contains("provider", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>نسخة من الملف مُطفأ فيها التعليق ومحتوى النصوص الحرفية، مع حفظ الأطوال وأرقام الأسطر.</summary>
        private string BlankNonCode()
        {
            char[] buffer = _s.ToCharArray();
            int i = 0;

            while (i < _s.Length)
            {
                if (_s[i] == '/' && i + 1 < _s.Length && _s[i + 1] == '/')
                {
                    int end = SkipLineComment(i);
                    Blank(buffer, i, end);
                    i = end;
                }
                else if (_s[i] == '/' && i + 1 < _s.Length && _s[i + 1] == '*')
                {
                    int end = SkipBlockComment(i);
                    Blank(buffer, i, end);
                    i = end;
                }
                else if (_s[i] == '\'')
                {
                    int end = SkipCharLiteral(i);
                    Blank(buffer, i + 1, Math.Max(i + 1, end - 1));
                    i = end;
                }
                else if (StringStart(i) is int start && start >= 0)
                {
                    int end = ScanStringBoundsOnly(start);
                    Blank(buffer, start, end);
                    i = end;
                }
                else
                {
                    i++;
                }
            }

            return new string(buffer);
        }

        private int ScanStringBoundsOnly(int start)
        {
            int saved = _sites;
            int savedFindings = _findings.Count;
            int end = ScanString(start);
            _sites = saved;
            _findings.RemoveRange(savedFindings, _findings.Count - savedFindings);
            return end;
        }

        private static void Blank(char[] buffer, int from, int to)
        {
            for (int k = from; k < to && k < buffer.Length; k++)
            {
                if (buffer[k] != '\n')
                {
                    buffer[k] = ' ';
                }
            }
        }

        // ————— الاستثناءات —————

        private void ScanExemptionMarkers()
        {
            string[] lines = Lines;
            for (int n = 0; n < lines.Length; n++)
            {
                int at = lines[n].IndexOf(ExemptionMarker, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                string reason = lines[n][(at + ExemptionMarker.Length)..].Trim();
                if (reason.Length < MinimumReasonLength)
                {
                    _findings.Add(new Finding(file, n + 1, Kind.UnjustifiedExemption,
                        $"وسم «{ExemptionMarker}» بلا سبب مكتوب (المطلوب {MinimumReasonLength} محرفاً على الأقل)", false));
                }
            }
        }

        private void Add(int offset, Kind kind, string message)
        {
            int line = LineOf(offset);
            _findings.Add(new Finding(file, line, kind, message, IsExempt(line)));
        }

        /// <summary>الاستثناء يُقرأ من السطر نفسه أو من السطر الذي يسبقه مباشرةً. لا نطاق أوسع من ذلك.</summary>
        private bool IsExempt(int line)
        {
            string[] lines = Lines;
            foreach (int candidate in new[] { line - 1, line - 2 })
            {
                if (candidate < 0 || candidate >= lines.Length)
                {
                    continue;
                }

                int at = lines[candidate].IndexOf(ExemptionMarker, StringComparison.Ordinal);
                if (at >= 0 && lines[candidate][(at + ExemptionMarker.Length)..].Trim().Length >= MinimumReasonLength)
                {
                    return true;
                }
            }

            return false;
        }

        private int LineOf(int offset)
        {
            int line = 1;
            for (int k = 0; k < offset && k < _s.Length; k++)
            {
                if (_s[k] == '\n')
                {
                    line++;
                }
            }

            return line;
        }
    }
}
