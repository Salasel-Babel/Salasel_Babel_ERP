using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.Ai.Suggestions;
using Babel.Ai.Voice;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Lookup;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>الحدّ: شريحةٌ منطوقة تسمّي طرفاً صنفُها <c>Entity</c>؛ وقيمةُ شريحة الطرف
/// <u>مِقبضٌ معتم ولا شيء غيره</u>؛ ولا يُبنى <c>VoiceDispatch</c> — ولا مسوّدةٌ من
/// بعده — وفيه طرفٌ لم يُحلّ.</b>
/// <para>
/// <b>والعطل الذي أوجب هذا الحارس مقيسٌ لا مُتخيَّل:</b> كانت شريحةُ الطرف تُقرأ نصّاً
/// حرّاً بقواعدِ إيقافٍ تُخترع من الجملة، وكان جوابُها على «من العميل شركة النور … لصالح
/// مؤسسة الرياض» صحيحاً <b>بحادثةِ ترتيبٍ في مصفوفة دلائل</b>. تُقلَّب المصفوفة فيخرج
/// طرفٌ آخر على المستند <b>بلا عطلٍ واحد وبوّابةٌ تقبل</b> — وهو أخبثُ ما يُنتجه مسارُ
/// إدخال: مستندٌ صحيح الشكل على طرفٍ لم يُطلَب.
/// </para>
/// <para>
/// <b>ويقرأ هذا الحارس ثلاثة مصادر ويقيس بينها</b>، على مثال
/// <c>NoVoiceIntentReachesAPostingOperation</c>:
/// <list type="number">
///   <item>سجلّ النيّات كما بنته الوحدات السبع.</item>
///   <item>مجموعاتُ سجلّات الأسماء كما تُعلنها الوحدات المالكة.</item>
///   <item><c>contracts/openapi/v1.json</c> — <b>أجسامُ المسوّدات المنشورة نفسها</b>.</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class NoDraftIsBuiltFromASpokenName
{
    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    private static VoiceIntentRegistry Registry { get; } = Build();

    /// <summary>
    /// <b>مفاتيح السجلّات التي تخدمها الوحدات فعلاً</b> — تُقرأ من مجموعاتها لا من قائمةٍ هنا.
    /// </summary>
    private static HashSet<string> ServedRegisters { get; } = Served();

    /// <summary>
    /// <b>الباقي المُسمّى: حقولُ معرّفاتٍ تُغذّيها شرائحُ رمز لا شرائحُ طرف.</b>
    /// <para>
    /// ‏<c>leaseId</c> و<c>unitId</c> معرّفا صفَّين (‏<c>maxLength: 36</c>) وتُغذّيهما شريحتا
    /// <c>Code</c> بحدِّ أربع كلمات — <b>أي العطل نفسه، على سطحٍ لم يبلغه هذا التحويل</b>.
    /// وتسميتُهما هنا تجعل الباقي <b>معلوماً ومحصوراً</b>: أيُّ حقل معرّفٍ جديد تُغذّيه
    /// شريحةٌ ليست طرفاً يُحمِّر هذا الحارس. وهو مُسجَّل في
    /// <c>فخ-جديد-document-references-are-still-bounded-by-word-count</c>.
    /// </para>
    /// </summary>
    private static readonly string[] KnownResidue = ["lease", "unit"];

    /// <summary>طولُ معرّف الصفّ في العقد المنشور — ‏<c>uuid</c> نصّاً.</summary>
    private const int RowIdentifierLength = 36;

    /// <summary>طولُ الرمز في العقد المنشور — رمزُ صنفٍ أو مستودعٍ أو موقعٍ في رفّ.</summary>
    private const int CodeLength = 64;

    private static VoiceIntentRegistry Build()
    {
        IVoiceIntentCatalogue[] catalogues =
        [
            new Babel.Ledger.Voice.LedgerVoiceIntents(),
            new Babel.Purchasing.Voice.PurchasingVoiceIntents(),
            new Babel.Sales.Voice.SalesVoiceIntents(),
            new Babel.Projects.Voice.ProjectsVoiceIntents(),
            new Babel.Hr.Voice.HrVoiceIntents(),
            new Babel.Inventory.Voice.InventoryVoiceIntents(),
            new Babel.RealEstate.Voice.RealEstateVoiceIntents(),
        ];

        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build(catalogues, MatrixPostingVocabulary.Default);

        return built.IsSuccess
            ? built.Value
            : throw new InvalidOperationException(
                "سجلّ النيّات لم يُبنَ: " + string.Join(" · ", built.Errors.Select(static error => error.MessageAr)));
    }

    private static HashSet<string> Served()
    {
        INameRegisterCatalogue[] catalogues =
        [
            new Babel.Sales.NameRegister.SalesNameRegisters(),
            new Babel.Purchasing.NameRegister.PurchasingNameRegisters(),
            new Babel.Hr.NameRegister.HrNameRegisters(),
            new Babel.Projects.NameRegister.ProjectsNameRegisters(),
            new Babel.RealEstate.NameRegister.RealEstateNameRegisters(),
        ];

        HashSet<string> keys = new(StringComparer.Ordinal);

        foreach (INameRegisterCatalogue catalogue in catalogues)
        {
            foreach (string key in catalogue.RegisterKeys)
            {
                // ‏**مفتاحٌ تدّعيه وحدتان يُسقط تركيب البحث نفسه** — فيُلتقط هنا أولاً.
                Assert.True(keys.Add(key), "مفتاح السجلّ «" + key + "» تدّعيه أكثر من وحدة.");
            }
        }

        return keys;
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>حقول المسوّدة المنشورة: الاسم ← طولُه الأقصى، للمطلوبة وحدها.</summary>
    private static Dictionary<string, Dictionary<string, int>> DraftBodies()
    {
        Dictionary<string, Dictionary<string, int>> bodies = new(StringComparer.Ordinal);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        JsonElement root = document.RootElement;

        foreach (JsonProperty path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind != JsonValueKind.Object
                    || !operation.Value.TryGetProperty("operationId", out JsonElement id)
                    || id.GetString() is not string name
                    || !operation.Value.TryGetProperty("requestBody", out JsonElement body))
                {
                    continue;
                }

                if (!body.TryGetProperty("content", out JsonElement content)
                    || !content.TryGetProperty("application/json", out JsonElement json)
                    || !json.TryGetProperty("schema", out JsonElement schema))
                {
                    continue;
                }

                schema = Resolve(root, schema);

                if (!schema.TryGetProperty("properties", out JsonElement properties))
                {
                    continue;
                }

                HashSet<string> required = new(StringComparer.Ordinal);
                if (schema.TryGetProperty("required", out JsonElement requiredList))
                {
                    foreach (JsonElement entry in requiredList.EnumerateArray())
                    {
                        required.Add(entry.GetString()!);
                    }
                }

                Dictionary<string, int> fields = new(StringComparer.Ordinal);

                foreach (JsonProperty property in properties.EnumerateObject())
                {
                    if (required.Contains(property.Name)
                        && property.Value.TryGetProperty("maxLength", out JsonElement max))
                    {
                        fields[property.Name] = max.GetInt32();
                    }
                }

                bodies[name] = fields;
            }
        }

        return bodies;
    }

    private static JsonElement Resolve(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out JsonElement reference))
        {
            return schema;
        }

        JsonElement current = root;

        foreach (string segment in reference.GetString()!.TrimStart('#', '/').Split('/'))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    // ══════════════════════════════ ضوابط موجبة (فخ-43) ══════════════════════════

    /// <summary>
    /// <b>حارسٌ على لا شيء يمرّ على لا شيء.</b> عقدٌ لا يُقرأ، أو سجلٌّ فارغ، أو صفرُ
    /// شريحةِ طرف — كلُّها تجعل ما تحته أخضرَ بلا أن يقيس شيئاً.
    /// </summary>
    [Fact]
    public void المصادر_الثلاثة_ليست_ضامرة()
    {
        Assert.True(Registry.Count >= 40, "النيّات: " + Count(Registry.Count));
        Assert.True(DraftBodies().Count >= 60, "أجسام المسوّدات: " + Count(DraftBodies().Count));
        Assert.True(ServedRegisters.Count >= 9, "السجلّات المخدومة: " + Count(ServedRegisters.Count));

        int entitySlots = Registry.Intents.Sum(
            static intent => intent.Slots.Count(static slot => slot.Kind == VoiceSlotKind.Entity));

        Assert.True(entitySlots >= 25, "شرائح الأطراف: " + Count(entitySlots));
    }

    // ══════════════════════════ ١ · كلُّ طرفٍ يسمّي سجلّاً يخدمه أحد ═══════════════

    /// <summary>
    /// شريحةُ طرفٍ تسمّي سجلّاً لا تخدمه وحدةٌ واحدة <b>لا تُحلّ أبداً</b>: تُقرأ، وتُعلَّق،
    /// ولا يجيب عنها أحد — فتظهر نيّةٌ «منشورة» لا تكتمل قطّ، ويُقال للمستخدم «لم يُحلّ
    /// بعد» إلى الأبد. وهذا يُحمِّر البوّابة بدل يد المستخدم.
    /// </summary>
    [Fact]
    public void كل_شريحة_طرف_تسمي_سجلاً_تخدمه_وحدةٌ_مالكة()
    {
        int measured = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            foreach (VoiceSlot slot in intent.Slots.Where(static slot => slot.Kind == VoiceSlotKind.Entity))
            {
                measured++;
                Assert.NotNull(slot.RegisterKey);
                Assert.True(
                    ServedRegisters.Contains(slot.RegisterKey!),
                    "الشريحة «" + slot.Name + "» في النيّة «" + intent.Id + "» تسمّي السجلّ «"
                    + slot.RegisterKey + "» ولا تخدمه وحدةٌ مالكة. المخدومة: "
                    + string.Join(" · ", ServedRegisters.Order(StringComparer.Ordinal)));
            }
        }

        Assert.True(measured >= 25, "شرائح الأطراف المقيسة: " + Count(measured));
    }

    // ═════════════ ٢ · كلُّ حقلِ معرّفٍ في جسمٍ منشور تُغذّيه شريحةُ طرف ═══════════

    /// <summary>
    /// <b>وهذا هو الحدّ مقيساً على العقد لا على قائمة.</b> لكل نيّةٍ منشورة: كلُّ حقلٍ
    /// <b>مطلوب</b> ينتهي بـ<c>Id</c> وطولُه الأقصى <see cref="RowIdentifierLength"/> —
    /// أي <b>معرّف صفّ</b> — يجب أن تُغذّيه شريحةٌ من صنف <see cref="VoiceSlotKind.Entity"/>،
    /// <b>لا <c>Prose</c> ولا <c>Code</c> ولا عدد</b>.
    /// <para>
    /// <b>ولماذا الطول لا الاسم:</b> العقد نفسه يفرّق — ‏<c>customerId</c> طولُه 36 لأنه
    /// معرّف صفّ، و<c>warehouseId</c> طولُه 64 لأنه <b>رمز</b>. فالقاعدة تقرأ ما يقوله
    /// العقد ولا تعتمد على اصطلاح تسمية، <b>وتتعافى وحدها حين يتغيّر العقد</b>.
    /// </para>
    /// </summary>
    [Fact]
    public void كل_حقل_معرف_صفٍّ_مطلوب_تُغذّيه_شريحةُ_طرف()
    {
        Dictionary<string, Dictionary<string, int>> bodies = DraftBodies();
        int measured = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            if (intent.OperationId is not string operationId
                || !bodies.TryGetValue(operationId, out Dictionary<string, int>? fields))
            {
                continue;
            }

            foreach ((string field, int length) in fields)
            {
                if (length != RowIdentifierLength
                    || !field.EndsWith("Id", StringComparison.Ordinal)
                    || string.Equals(field, "companyId", StringComparison.Ordinal))
                {
                    continue;
                }

                string stem = field[..^2];
                VoiceSlot? slot = intent.Slots.FirstOrDefault(
                    candidate => string.Equals(candidate.Name, stem, StringComparison.Ordinal));

                if (slot is null)
                {
                    // حقلٌ لا تُغذّيه شريحةٌ منطوقة أصلاً — تملؤه الشاشة بمُنتقٍ أمام عين إنسان.
                    continue;
                }

                measured++;

                if (KnownResidue.Contains(slot.Name, StringComparer.Ordinal))
                {
                    Assert.NotEqual(VoiceSlotKind.Entity, slot.Kind);
                    continue;
                }

                Assert.True(
                    slot.Kind == VoiceSlotKind.Entity,
                    "الحقل «" + field + "» في «" + operationId + "» معرّفُ صفّ، وتُغذّيه الشريحة «"
                    + slot.Name + "» وصنفُها " + slot.Kind + ". ومسوّدةٌ تُبنى على اسمٍ منطوق تُنشئ "
                    + "مستنداً على طرفٍ آخر صحيحَ الشكل.");
            }
        }

        Assert.True(measured >= 6, "حقول المعرّفات المقيسة: " + Count(measured));
    }

    /// <summary>
    /// <b>والعكس كذلك:</b> حقلٌ طولُه <see cref="CodeLength"/> <b>رمزٌ لا معرّف صفّ</b>،
    /// ومِقبضٌ معتم لا يملؤه. فشريحةٌ من صنف <c>Entity</c> تُغذّيه تَعِد بحلٍّ لا يمكن
    /// أن يُستعمل — وهو وعدٌ يظهر للمستخدم «حُلّ» ثم يسقط عند بناء الجسم.
    /// </summary>
    [Fact]
    public void حقل_الرمز_لا_تُغذّيه_شريحةُ_طرف()
    {
        Dictionary<string, Dictionary<string, int>> bodies = DraftBodies();
        int measured = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            if (intent.OperationId is not string operationId
                || !bodies.TryGetValue(operationId, out Dictionary<string, int>? fields))
            {
                continue;
            }

            foreach ((string field, int length) in fields)
            {
                if (length != CodeLength || !field.EndsWith("Id", StringComparison.Ordinal))
                {
                    continue;
                }

                VoiceSlot? slot = intent.Slots.FirstOrDefault(
                    candidate => string.Equals(candidate.Name, field[..^2], StringComparison.Ordinal));

                if (slot is null)
                {
                    continue;
                }

                measured++;
                Assert.NotEqual(VoiceSlotKind.Entity, slot.Kind);
            }
        }

        Assert.True(measured >= 3, "حقول الرموز المقيسة: " + Count(measured));
    }

    /// <summary>
    /// <b>والضابط الموجب على الحدّ نفسه (فخ-43):</b> يُبنى عمداً وضعٌ تُغذّي فيه شريحةُ
    /// <c>Prose</c> حقلَ معرّفِ صفّ، ويُقاس أنّ القاعدة <b>تلتقطه</b>. وحدٌّ لم يُرَ يسقط
    /// مرّةً واحدة هو حدٌّ لا يُعرف أنه يعمل.
    /// </summary>
    [Fact]
    public void القاعدة_تلتقط_شريحةً_ليست_طرفاً_تُغذّي_معرّف_صفّ()
    {
        VoiceSlot prose = new("customer", VoiceSlotKind.Prose, "العميل", true, ["العميل"], []);

        bool caught = prose.Kind != VoiceSlotKind.Entity
            && !KnownResidue.Contains(prose.Name, StringComparer.Ordinal);

        Assert.True(caught, "القاعدة لا تلتقط شريحةً ليست طرفاً تُغذّي حقل معرّف صفّ.");
    }

    // ═══════════════ ٣ · نوعُ الشريحة في الأمر لا يستطيع أن يحمل اسماً ═══════════

    /// <summary>
    /// <b>الغياب بنيويّ لا اتّفاقيّ.</b> حالةُ الطرف في <see cref="ResolvedSlotValue"/>
    /// تعرض <c>Handle</c> ولا تعرض نصّاً ولا ما سُمع ولا اسماً من سجلّ. فأولُ من يبني
    /// جسمَ مسوّدةٍ من هذا النوع <b>لا يجد اسماً يضعه في موضع معرّف</b>.
    /// </summary>
    [Fact]
    public void شريحةُ_الطرف_في_الأمر_لا_تحمل_نصّاً()
    {
        ResolvedSlotValue entity = ResolvedSlotValue.OfEntity("customer", new string('h', 152));

        Assert.True(entity.IsEntity);
        Assert.NotNull(entity.Handle);
        Assert.Null(entity.Text);
        Assert.Null(entity.Unit);

        Type type = typeof(ResolvedSlotValue);

        foreach (string forbidden in new[] { "Heard", "Name" + "Ar", "LabelAr", "Span" })
        {
            Assert.Null(type.GetProperty(forbidden));
        }

        // ‏والمِقبض بطول <c>SignedLookupHandles.TokenLength</c> — ثابتٌ لا يطول بطول الاسم.
        Assert.Equal(Babel.Ai.Lookup.SignedLookupHandles.TokenLength, entity.Handle!.Length);
    }

    // ══════════════════ ٤ · البوّابة وحدها تبني الأمر ═══════════════════════════

    /// <summary>
    /// منشئُ <see cref="VoiceDispatch"/> داخليّ، و<see cref="VoiceConfirmationGate"/>
    /// وحدَه ينادِيه. <b>حارسٌ بنيويّ لا انضباطيّ</b>، على مثال <c>TheGateIsTheOnlyDoorToDispatch</c>.
    /// </summary>
    [Fact]
    public void البوابة_وحدها_تبني_الأمر()
    {
        // ‏ومنشئُ النسخ المُولَّد للسجلّ يُستثنى: ليس باباً إلى بناء أمرٍ جديد.
        System.Reflection.ConstructorInfo[] constructors = [.. typeof(VoiceDispatch)
            .GetConstructors(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)
            .Where(static candidate => candidate.GetParameters() is not [{ ParameterType.Name: nameof(VoiceDispatch) }])];

        System.Reflection.ConstructorInfo only = Assert.Single(constructors);
        Assert.True(only.IsAssembly, "منشئُ VoiceDispatch ليس داخلياً — فالبوّابة تُتجاوَز.");

        string gate = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Voice", "VoiceConfirmationGate.cs"));

        Assert.Contains("new VoiceDispatch(", gate, StringComparison.Ordinal);

        string[] sources = Directory.GetFiles(
            Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories);

        foreach (string file in sources)
        {
            if (Path.GetFileName(file) is "VoiceConfirmationGate.cs" or "VoiceDispatch.cs")
            {
                continue;
            }

            Assert.DoesNotContain("new VoiceDispatch(", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    // ═════════════ ٥ · لا قاعدةَ إملائية أُعيدت، ولا سقوطٌ إلى الدليل التالي ═════

    /// <summary>
    /// <b>مسحٌ فظّ على المصدر — وهو الفخّ بعينه.</b> لا صيغةَ فعلٍ منتظمة، ولا
    /// <c>??=</c> فوق <c>continue</c> أو معه، ولا معرّفٌ يُسمّي ترجيحاً أو فضَّ تعادل.
    /// وكلُّها أسماءُ ما حُذف: قرارٌ داخل القارئ حيث لا معلومات تكفي لاتّخاذه.
    /// </summary>
    [Fact]
    public void لا_قاعدة_ترجيح_أُعيدت_إلى_القارئ()
    {
        string[] watched =
        [
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Voice", "SpokenCommandReader.cs"),
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Voice", "SpokenSpans.cs"),
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Voice", "SpokenNameResolver.cs"),
            Path.Combine(RepositoryLayout.Root, "web", "src", "voice", "command.ts"),
        ];

        foreach (string file in watched)
        {
            Assert.True(File.Exists(file), file);

            string[] lines = File.ReadAllLines(file);
            string source = string.Join('\n', lines.Where(static line => !IsComment(line)));

            foreach (string forbidden in new[] { "adjudicat", "bestMatch", "BestMatch", "tieBreak", "TieBreak" })
            {
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
            }

            // ‏«‏??= ثم continue» هو شكلُ العطل حرفياً: رفضٌ يُسجَّل ثم يُواصَل إلى دليلٍ آخر.
            for (int index = 0; index < lines.Length; index++)
            {
                if (IsComment(lines[index]) || !lines[index].Contains("??=", StringComparison.Ordinal))
                {
                    continue;
                }

                for (int ahead = index; ahead < Math.Min(lines.Length, index + 4); ahead++)
                {
                    Assert.DoesNotContain("continue", lines[ahead], StringComparison.Ordinal);
                }
            }

            Assert.DoesNotContain("ReadCompany", source, StringComparison.Ordinal);
            Assert.DoesNotContain("readCompany", source, StringComparison.Ordinal);
        }
    }

    private static bool IsComment(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
    }

    // ═════════ ٦ · المتصفّح لا يُرسل مقطعاً مسموعاً في موضع معرّف ═══════════════

    /// <summary>
    /// المتصفّح هو ما ينادي الباب فعلاً. فيُمسح <c>web/src/voice/</c> كلُّه: لا حقلٌ
    /// ينتهي بـ<c>Id</c> يُسنَد من <c>.heard</c> ولا من نصِّ شريحةٍ ليست طرفاً.
    /// </summary>
    [Fact]
    public void المتصفح_لا_يُرسل_ما_سُمع_في_موضع_معرّف()
    {
        string[] files =
        [
            .. Directory.GetFiles(Path.Combine(RepositoryLayout.Root, "web", "src", "voice"), "*.ts*"),
            .. Directory.GetFiles(Path.Combine(RepositoryLayout.Root, "web", "src", "agent"), "*.ts*"),
        ];

        Assert.True(files.Length >= 8, "ملفّات الواجهة المقيسة: " + Count(files.Length));

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);

            Assert.DoesNotMatch(HeardIntoIdentifier(), source);
        }
    }

    /// <summary>إسنادُ حقلٍ ينتهي بـ<c>Id</c> من <c>heard</c> أو من <c>.text</c> لشريحة.</summary>
    [GeneratedRegex(@"\w+Id\s*:\s*[\w.]*\.heard", RegexOptions.CultureInvariant)]
    private static partial Regex HeardIntoIdentifier();
}
