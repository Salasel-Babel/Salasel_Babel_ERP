using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 14-ب — شرحُ وثيقة التصميم يُتّسق ولا يُسقَّف.</b>
/// <para>
/// <b>الصنف الآخر.</b> <see cref="Rule14_TranslationsAreRowsNotColumns"/> يحرس
/// <b>العمود المخزَّن</b>: زوجٌ ثابت في جدول يعجز بنيوياً عن لغةٍ ثالثة، فسقفُه دَينٌ
/// لا يرتفع. وهذا الملفّ يحرس <b>الشرح</b>: قيمةٌ نصّية في وثيقة بيانات — اسمُ حدث
/// الترحيل ومُطلِقه وعكسه في <c>data/posting-matrix/events/*.json</c>. والشرح
/// <b>ليس ديناً</b>: لا هجرة تلزم لإضافة لغةٍ إليه، ولا عمود يُنشأ، ولا بايتة تُبصَم.
/// </para>
/// <para>
/// <b>ولماذا الفصل ليس ترفاً:</b> كان السقف الواحد يعدّ الاثنين معاً — مقيس على
/// <c>develop</c> أنّ <c>data/posting-matrix/</c> وحدها ٦٤١ من ٨٦٢. وكلُّ حدث ترحيلٍ جديد
/// يضيف نحو سبعة مواضع شرحٍ فيكسر سقفاً لا يرتفع. <b>فسُحب حدثٌ حقيقي لأجل ذلك:</b>
/// أضاف أسطولُ المخزون <c>inventory.transfer.between_locations</c> فرأى «866 والسقف
/// 862»، فسحبه ولم يرفع السقف — وكان محقّاً، لأن السقف حارسٌ سليم يقيس الشيء الخطأ.
/// وبعد الفصل: النموّ مفتوح في كل الوحدات، والاتّساق مفروض.
/// </para>
/// <para>
/// <b>والقاعدة المفروضة هنا — اتّساقٌ لا سقف:</b> كلُّ شرحٍ إنجليزي غير فارغ يجب أن
/// يقابله شقيقٌ عربي غير فارغ <b>في الكائن نفسه</b>. والعربية إلزامية والإنجليزية
/// اختيارية: هذا هو ADR-0021 بند 2 مطبَّقاً على الوثيقة — العربية هي السجلّ، وأي لغةٍ
/// أخرى عرضٌ اختياري. ومخطّطٌ <b>يوجب</b> الإنجليزية يمنحها الامتياز البنيوي الذي
/// ينفيه القرار، ويجعل الأردية والهندية طبقةً ثانية في وثيقةٍ تدّعي أن العربية سجلّها.
/// </para>
/// <para>
/// <b>وما لا تفعله هذه القاعدة:</b> لا تعدّ، ولا تسقّف، ولا تمنع حدثاً جديداً. حدثٌ
/// بعربيّةٍ وحدها <b>يمرّ</b>، وحدثٌ بعربيّةٍ وإنجليزية يمرّ، وحدثٌ بإنجليزيةٍ وحدها
/// <b>يُرفض</b> — لأن ذلك سجلٌّ بلا سجلّ.
/// </para>
/// </summary>
public sealed class Rule14_TheDesignGlossIsConsistentNotCapped
{
    /// <summary>
    /// <b>عددُ الشروح الإنجليزية غير الفارغة في وثائق البيانات — مقيس، وليس سقفاً.</b>
    /// <para>يُستعمل <b>أرضيةً</b> وحدها: مسحٌ يقرأ أقلّ من هذا صار نطاقه مكسوراً ويمرّ
    /// على فراغ. ولا حدّ أعلى له بحال — وذلك هو القرار.</para>
    /// <para><b>أمرُ قياسه، يُعاد تشغيله حرفياً من جذر المستودع:</b></para>
    /// <code>
    /// git ls-files -- 'data/*.json' 'data/**/*.json' \
    ///  | xargs -n1 jq '[paths(scalars) as $p
    ///        | select(($p|last|tostring|endswith("_en"))
    ///                 and (getpath($p)|type=="string")
    ///                 and (getpath($p)|gsub("\\s";"")|length&gt;0))] | length' \
    ///  | paste -sd+ | bc          # 894 على develop، منها 890 في data/posting-matrix/
    /// </code>
    /// </summary>
    private const int MeasuredGlossFloor = 850;

    /// <summary>أرضيةُ عدد أحداث الترحيل — مقيسة 89: <c>jq -s '[.[].events|length]|add' data/posting-matrix/events/*.json</c>.</summary>
    private const int MeasuredEventFloor = 85;

    /// <summary>وثيقةٌ ممسوحة: مسارُها ونصّها. النصّ مفصولٌ عن المسار كي يستطيع الشاهد الموجب أن يُطعِم الكاشفَ وثيقةً مصنوعة.</summary>
    private sealed record Document(string Path, string Json);

    /// <summary>مخالفةٌ واحدة: أين، وأي مفتاح، ولماذا.</summary>
    private sealed record Offence(string Path, string Pointer, string Reason)
    {
        public override string ToString() => Path + " · " + Pointer + " — " + Reason;
    }

    private static List<Document> DesignDocuments { get; } = LoadTrackedJsonUnderData();

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · القاعدة على الوثائق الحقيقية
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EveryEnglishGlossHasItsArabicRecordBesideIt()
    {
        List<Offence> offences = [.. DesignDocuments.SelectMany(Scan)];

        Assert.True(
            offences.Count == 0,
            "شرحٌ إنجليزي بلا سجلٍّ عربي بجانبه. العربية هي السجلّ والإنجليزية عرضٌ اختياري "
            + "(‏ADR-0021 بند 2 · ADR-جديد gloss-is-not-column-debt):\n"
            + string.Join('\n', offences.Select(static offence => "  " + offence)));
    }

    /// <summary>
    /// <b>والمسح يقرأ وثائق حقيقية، لا مجموعةً ضامرة.</b> حارسٌ نطاقُه انكسر يمرّ أخضر
    /// على لا شيء — وهذه أرضيةٌ مقيسة لا تقدير.
    /// </summary>
    [Fact]
    public void TheScanReadsTheRealDesignDocuments()
    {
        foreach (string expected in new[]
                 {
                     "data/posting-matrix/events/inventory.json",
                     "data/posting-matrix/events/sales.json",
                     "data/posting-matrix/events/realestate.json",
                     "data/posting-matrix/guard-rules.json",
                 })
        {
            Assert.Contains(DesignDocuments, document => string.Equals(document.Path, expected, StringComparison.Ordinal));
        }

        int glosses = DesignDocuments.Sum(CountEnglishGlosses);

        Assert.True(
            glosses >= MeasuredGlossFloor,
            string.Create(
                CultureInfo.InvariantCulture,
                $"الشروح الإنجليزية المقروءة {glosses} وأرضيتها {MeasuredGlossFloor} — المسح ضامر، ")
            + "والقاعدة تمرّ على فراغ. صحّح النطاق أو اخفض الأرضية بقياسٍ مكتوب.");

        int events = DesignDocuments
            .Where(static document => document.Path.StartsWith("data/posting-matrix/events/", StringComparison.Ordinal))
            .Sum(static document =>
            {
                using JsonDocument parsed = JsonDocument.Parse(document.Json);
                return parsed.RootElement.TryGetProperty("events", out JsonElement events)
                    ? events.GetArrayLength()
                    : 0;
            });

        Assert.True(
            events >= MeasuredEventFloor,
            FormattableString.Invariant($"أحداث الترحيل المقروءة {events} وأرضيتها {MeasuredEventFloor}."));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · الشاهد الموجب — الكاشف يلتقط المخالفة، ولا يلتقط النموّ
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>شاهدٌ موجب: مخالفاتٌ حقيقية تُطعَم للكاشف فيراها.</b>
    /// <para>
    /// حارسٌ لم يُرَ ساقطاً ليس حارساً. والوثائق أدناه <b>مصنوعة</b> بالشكل نفسه الذي
    /// يكتبه كاتب حدثٍ جديد، لا بشكلٍ مُصطنَع لإرضاء الكاشف.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDetectorCatchesEnglishWithoutAnArabicRecord()
    {
        (string Label, string Json)[] violations =
        [
            ("اسم حدث إنجليزي بلا عربي",
             """{"events":[{"event_code":"inventory.x","name_en":"Transfer between locations"}]}"""),
            ("عربيٌّ فارغ والإنجليزي مكتوب",
             """{"events":[{"event_code":"inventory.x","name_ar":"   ","name_en":"Transfer"}]}"""),
            ("مُطلِقُ الحدث إنجليزي وحده",
             """{"events":[{"trigger":{"name_en":"On approval of the transfer"}}]}"""),
            ("تحفّظٌ إنجليزي وحده",
             """{"events":[{"caveats":[{"ref":"A","text_en":"Awaiting the auditor"}]}]}"""),
            ("اشتقاقُ مبلغٍ إنجليزي وحده",
             """{"events":[{"amounts":{"net":{"name_ar":"صافي","name_en":"net","derivation_en":"line total"}}}]}"""),
        ];

        foreach ((string label, string json) in violations)
        {
            IReadOnlyList<Offence> found = Scan(new Document("مصنوع/" + label + ".json", json));

            Assert.True(
                found.Count > 0,
                "الكاشف لم يلتقط مخالفةً حقيقية — " + label + ": " + json);
        }
    }

    /// <summary>
    /// <b>وشاهدٌ سالب هو نصف القرار: النموّ يمرّ.</b>
    /// <para>
    /// حدثٌ <b>بعربيّةٍ وحدها</b> صحيحٌ ولا يُرفض — وهذا بالضبط ما كان ممنوعاً قبل هذا
    /// الفرع، إذ كان المخطّط يوجب <c>name_en</c> فيصير كلُّ حدثٍ جديد سبعةَ مواضع تكسر
    /// السقف. وحارسٌ يرفض كل شيء لا يميّز شيئاً.
    /// </para>
    /// </summary>
    [Fact]
    public void ArabicAloneIsLegalSoANewEventIsNeverBlocked()
    {
        foreach (string legal in new[]
                 {
                     """{"events":[{"event_code":"inventory.x","name_ar":"تحويل بين مواقع المنشأة"}]}""",
                     """{"events":[{"name_ar":"تحويل","name_en":"Transfer","trigger":{"name_ar":"عند الاعتماد"}}]}""",
                     """{"events":[{"name_ar":"تحويل","lines":[{"line_no":1,"note_ar":"","note_en":""}]}]}""",
                     """{"rules":[{"rule_id":"GR-1","name_ar":"قاعدة","message_ar":"نصّ","message_en":"text"}]}""",
                 })
        {
            IReadOnlyList<Offence> found = Scan(new Document("مصنوع/سليم.json", legal));

            Assert.True(
                found.Count == 0,
                "الكاشف رفض وثيقةً سليمة، فهو يمنع النموّ: " + legal
                + "\n" + string.Join('\n', found.Select(static offence => "  " + offence)));
        }
    }

    /// <summary>
    /// <b>ولا سقف — مُثبَتاً بالقياس لا بغياب سطر.</b>
    /// <para>
    /// «لا سقف» دعوى سالبة، ولا يُثبتها أنّ الملفّ لا يحوي ثابتاً. فتُطعَم القاعدةُ
    /// وثيقةً فيها <b>عشرة آلاف</b> شرحٍ متّسق — أكثر بأحد عشر ضعفاً من كل شروح
    /// المستودع — فتمرّ خضراء.
    /// </para>
    /// </summary>
    [Fact]
    public void NoCeilingExistsOnTheGloss()
    {
        StringBuilder builder = new("{\"events\":[");

        for (int index = 0; index < 5_000; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(CultureInfo.InvariantCulture, $$"""{"event_code":"m.e{{index}}","name_ar":"حدث {{index}}","name_en":"Event {{index}}"}""");
        }

        builder.Append("]}");

        Document huge = new("مصنوع/عشرة-آلاف-شرح.json", builder.ToString());

        Assert.Equal(5_000, CountEnglishGlosses(huge));
        Assert.Empty(Scan(huge));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · والمخطّط لا يوجب الإنجليزية — وإلا عاد المنعُ من بابٍ آخر
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>المخطّط يوجب العربية ولا يوجب الإنجليزية.</b>
    /// <para>
    /// هذا هو موضع العطل الأصلي: <c>$defs.bilingual.required = ["name_ar","name_en"]</c>
    /// وسبعُ قوائم <c>required</c> مثلها. وقاعدةُ الاتّساق أعلاه لا تكفي وحدها، لأن
    /// مخطّطاً يوجب الإنجليزية يعيد المنع من بابٍ آخر: يصير حدثٌ بعربيّةٍ وحدها
    /// <b>مخالفاً للمخطّط</b> وإن مرّ على هذا الحارس.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSchemaNeverMakesAnEnglishGlossMandatory()
    {
        string path = Path.Combine(
            RepositoryLayout.Root, "data", "posting-matrix", "schema", "posting-matrix.schema.json");

        Assert.True(File.Exists(path), "مخطّط مصفوفة الترحيل غير موجود: " + path);

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(path));

        List<string> mandatoryEnglish = [];
        int requiredArrays = 0;
        int mandatoryArabic = 0;

        void Walk(JsonElement node, string pointer)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in node.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "required", StringComparison.Ordinal)
                            && property.Value.ValueKind == JsonValueKind.Array)
                        {
                            requiredArrays++;

                            foreach (JsonElement item in property.Value.EnumerateArray())
                            {
                                string? name = item.GetString();

                                if (name is null)
                                {
                                    continue;
                                }

                                if (name.EndsWith("_en", StringComparison.Ordinal))
                                {
                                    mandatoryEnglish.Add(pointer + "/required · " + name);
                                }
                                else if (name.EndsWith("_ar", StringComparison.Ordinal))
                                {
                                    mandatoryArabic++;
                                }
                            }
                        }

                        Walk(property.Value, pointer + "/" + property.Name);
                    }

                    break;

                case JsonValueKind.Array:
                    int index = 0;

                    foreach (JsonElement item in node.EnumerateArray())
                    {
                        Walk(item, pointer + "/" + index.ToString(CultureInfo.InvariantCulture));
                        index++;
                    }

                    break;

                default:
                    break;
            }
        }

        Walk(schema.RootElement, string.Empty);

        Assert.True(
            mandatoryEnglish.Count == 0,
            "المخطّط ما زال يوجب شرحاً إنجليزياً، فحدثٌ بعربيّةٍ وحدها يخالف المخطّط "
            + "وإن مرّ على قاعدة الاتّساق. العربية هي السجلّ والإنجليزية اختيارية "
            + "(‏ADR-0021 بند 2):\n" + string.Join('\n', mandatoryEnglish.Select(static name => "  " + name)));

        // حارسا لافراغ: المخطّط مقروءٌ فعلاً، وما زال يوجب العربية.
        Assert.True(requiredArrays >= 8, FormattableString.Invariant($"قوائم required المقروءة {requiredArrays} — المُحلِّل ضامر."));
        Assert.True(mandatoryArabic >= 8, FormattableString.Invariant($"حقول عربية إلزامية {mandatoryArabic} — العربية فقدت إلزامها، وهي السجلّ."));
    }

    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>الكاشف.</b> يمرّ على كل كائنٍ في الوثيقة، ولكل مفتاح ينتهي بـ<c>_en</c> قيمتُه
    /// نصٌّ غير فارغ يطلب الشقيق <c>_ar</c> في الكائن <b>نفسه</b> غيرَ فارغ.
    /// <para>
    /// <b>ولماذا «غير فارغ» شرطٌ على الطرفين:</b> الفراغ في هذه الوثائق يعني <b>الغياب</b>
    /// لا النقص — مقيس أن <c>note_ar</c>/<c>note_en</c> مكتوبان <c>""</c> في مئات السطور
    /// بوصفهما ملاحظةَ مراجعٍ اختيارية. فعدُّ الفراغ مخالفةً يُنتج ٦٤٢ إحمراراً على شجرة
    /// سليمة (مقيس)، وحارسٌ يحمرّ لما ليس عطلاً يُدرَّب الناس على تجاهله.
    /// </para>
    /// <para>
    /// <b>وقيمةٌ ليست نصّاً ليست شرحاً:</b> في المخطّط نفسه <c>"name_en"</c> مفتاحٌ قيمتُه
    /// <b>كائن</b> يصف نوعاً — بنيةٌ لا ترجمة. فيتخطّاها الكاشف، ويفحصها الاختبار الذي
    /// يخصّ المخطّط أعلاه.
    /// </para>
    /// </summary>
    private static IReadOnlyList<Offence> Scan(Document document)
    {
        List<Offence> offences = [];

        using JsonDocument parsed = JsonDocument.Parse(document.Json);
        Visit(parsed.RootElement, string.Empty);

        return offences;

        void Visit(JsonElement node, string pointer)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in node.EnumerateObject())
                    {
                        if (property.Name.EndsWith("_en", StringComparison.Ordinal)
                            && IsWritten(property.Value))
                        {
                            string sibling = string.Concat(property.Name.AsSpan(0, property.Name.Length - 3), "_ar");

                            if (!node.TryGetProperty(sibling, out JsonElement arabic) || !IsWritten(arabic))
                            {
                                offences.Add(new Offence(
                                    document.Path,
                                    pointer + "/" + property.Name,
                                    "شرحٌ إنجليزي مكتوب و" + sibling + " غائبٌ أو فارغ"));
                            }
                        }

                        Visit(property.Value, pointer + "/" + property.Name);
                    }

                    break;

                case JsonValueKind.Array:
                    int index = 0;

                    foreach (JsonElement item in node.EnumerateArray())
                    {
                        Visit(item, pointer + "/" + index.ToString(CultureInfo.InvariantCulture));
                        index++;
                    }

                    break;

                default:
                    break;
            }
        }
    }

    private static bool IsWritten(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());

    private static int CountEnglishGlosses(Document document)
    {
        using JsonDocument parsed = JsonDocument.Parse(document.Json);
        return Count(parsed.RootElement);

        static int Count(JsonElement node) => node.ValueKind switch
        {
            JsonValueKind.Object => node.EnumerateObject().Sum(property =>
                (property.Name.EndsWith("_en", StringComparison.Ordinal) && IsWritten(property.Value) ? 1 : 0)
                + Count(property.Value)),
            JsonValueKind.Array => node.EnumerateArray().Sum(Count),
            _ => 0,
        };
    }

    /// <summary>
    /// وثائق البيانات المتعقَّبة تحت <c>data/</c> — <b>ما يتعقّبه git لا ما يقع على القرص</b>،
    /// للسبب المشروح في <see cref="Rule14_TranslationsAreRowsNotColumns"/>: مسحُ القرص
    /// يجعل حكم الحارس تابعاً للحظة آخر بناء.
    /// </summary>
    private static List<Document> LoadTrackedJsonUnderData()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("data");

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "‏git ls-files أخفق، فلا سبيل إلى معرفة محتوى المستودع. / git ls-files failed: " + error);
        }

        List<Document> documents = [];

        foreach (string relative in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string absolute = Path.Combine(RepositoryLayout.Root, relative);

            if (File.Exists(absolute))
            {
                documents.Add(new Document(relative.Replace('\\', '/'), File.ReadAllText(absolute)));
            }
        }

        return documents;
    }
}
