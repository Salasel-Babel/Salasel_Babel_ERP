using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>حدُّ المقطع الحرّ — يُقرَّر بسجلٍّ أو يُرفض، ولا يُخمَّن.</b>
/// <para>
/// <b>الجملة التي أنشأت هذا الملفّ</b> قالها صاحبُ المنتج للنظام الحيّ في أوّل يومٍ
/// نُشر فيه: «سجل سند قبض من شركة المسار الامثل <b>فان لم تجدها انشيء لها حسابا</b>
/// ثم سند قبض بقيمة 20000 …». فابتلع اسمُ العميل الشرطَ كلَّه — تسع كلمات — وخرج
/// مستندٌ صحيح الشكل على طرفٍ لا وجود له، بلا سطرٍ أحمر في أي موضع.
/// </para>
/// <para>
/// <b>ولماذا لم يكن العلاج إضافة «فإن» إلى قائمة إيقاف:</b> القائمة تُحصي <b>ما ليس
/// في الاسم</b> — أي متمّمة مجموعةٍ مفتوحة، اللغةَ كلَّها إلا صفّاً واحداً — فأوّلُ
/// أداةٍ لم تُكتب («لو»، «إذا ما»، «لين»، «عشان») تُعيد العطل صامتاً. والمقاييس في
/// هذا الملفّ تُثبت ذلك بأدواتٍ خليجية لم تُكتب في أي قائمة.
/// </para>
/// </summary>
public sealed class SlotSpansAreBoundedOrRefused
{
    private const string BoundaryFault = "ai.voice.slot_boundary_not_found";
    private const string AmbiguousFault = "ai.voice.slot_boundary_ambiguous";
    private const string NotInRegisterFault = "ai.voice.name_not_in_register";
    private const string ResidueFault = "ai.voice.residue_not_understood";

    /// <summary>جملةُ صاحب المنتج كما نطقها حرفاً بحرف — لا مختصرةً ولا مُهذَّبة.</summary>
    private const string OwnerUtterance =
        "سجل سند قبض من شركة المسار الامثل فان لم تجدها انشيء لها حسابا ثم سند قبض بقيمة 20000 ريال سعودي بتاريخ اليوم طبعاً";

    /// <summary>الشرط الذي ابتُلع يومَها في اسم العميل.</summary>
    private const string SwallowedClause = "فان لم تجدها انشيء لها حسابا";

    private static VoiceResolution Read(string transcript, VoiceEntityRegistry? directory = null)
    {
        VoiceReadingOptions options = new(VoiceHarness.Today, "0.15", directory);
        Result<VoiceResolution> read = SpokenCommandReader.Read(transcript, VoiceHarness.Registry, options);
        Assert.True(read.IsSuccess, read.IsFailure ? read.Errors[0].MessageAr : string.Empty);
        return read.Value;
    }

    private static string? ValueOf(VoiceResolution resolution, string slot) =>
        resolution.Slots.FirstOrDefault(value => value.Name == slot)?.Text;

    private static VoiceEntityRegistry Directory(VoiceEntityKind kind, params string[] names)
    {
        Result<VoiceEntityRegistry> built = VoiceEntityRegistry.Build([new FakeDirectory(kind, names)]);
        Assert.True(built.IsSuccess, built.IsFailure ? built.Errors[0].MessageAr : string.Empty);
        return built.Value;
    }

    private sealed record FakeDirectory(VoiceEntityKind Kind, IReadOnlyList<string> Names) : IVoiceEntityDirectory
    {
        public BabelModule Module => BabelModule.Sales;
    }

    // ═══ أوّلاً · الجملة بعينها ═══════════════════════════════════════════

    [Fact]
    public void جملة_صاحب_المنتج_ترفض_ولا_تبتلع_الشرط_في_اسم_العميل()
    {
        VoiceResolution resolution = Read(OwnerUtterance);

        // ‏**لا قيمة**: لا «شركة المسار الامثل فان لم تجدها انشيء لها حسابا» ولا اقتطاعها.
        Assert.Null(ValueOf(resolution, "customer"));
        Assert.Contains("customer", resolution.MissingSlots);
        Assert.Contains(resolution.Faults, fault => fault.Code == BoundaryFault);

        // والرفض **يسمّي ما لم يُفهَم** لا يكتفي بأن يقول «خطأ».
        Error fault = resolution.Faults.Single(f => f.Code == BoundaryFault);
        Assert.Contains("العميل", fault.MessageAr, StringComparison.Ordinal);
        Assert.Contains(SwallowedClause, fault.MessageAr, StringComparison.Ordinal);

        // وما فُهم يبقى مفهوماً: الرفض على شريحةٍ لا يُسقط أخواتها.
        Assert.Equal("20000", ValueOf(resolution, "amount"));
        Assert.Equal(VoiceHarness.Today, ValueOf(resolution, "receivedOn"));
    }

    [Fact]
    public void لا_شريحة_في_جملة_صاحب_المنتج_تحمل_كلمة_من_الشرط()
    {
        VoiceResolution resolution = Read(OwnerUtterance);

        foreach (SpokenSlotValue value in resolution.Slots)
        {
            foreach (string word in VoiceText.Words(SwallowedClause))
            {
                Assert.DoesNotContain(word, value.Text, StringComparison.Ordinal);
            }
        }
    }

    // ═══ ثانياً · العامّية الخليجية — لا الفصحى وحدها ═════════════════════

    /// <summary>
    /// أدواتٌ خليجية <b>ليست في أي قائمة إيقاف</b>، ولا يجوز أن تصير فيها: «إذا ما»،
    /// «لو»، «لين»، «عشان»، «ولا»، «بعد ما». وكلُّها تُرفض هنا <b>بالبنية لا بالمعجم</b>.
    /// </summary>
    public static TheoryData<string, string> Colloquial() => new()
    {
        { "سجل سند قبض من شركة المسار الامثل وإذا ما لقيتها سو لها حساب كاش اليوم", "customer" },
        { "قبضت من العميل شركة المسار الامثل بعد ما راجعت الحساب نقدا اليوم", "customer" },
        { "سجل سند قبض من مؤسسة النور لين تشوف حسابها عندك نقد اليوم", "customer" },
        { "سجل سند قبض من مؤسسة النور عشان نقفل الشهر ونرتاح نقد اليوم", "customer" },
        { "استلمت من العميل مؤسسة النور ولا تنسى تسجلها بالدفتر نقد اليوم", "customer" },
        { "سجل سند صرف للمورد مؤسسة الرياض لو ما كان عندك ملف سوّ له ملف نقد اليوم", "supplier" },
        { "كم رصيد الصنف حديد تسليح لو ما لقيته بالمستودع الرئيسي شوف الفرعي", "item" },
        { "اصرف سلفة للموظف احمد الغامدي وإذا ما عنده رصيد خلها على الشهر الجاي بمبلغ 2000 اليوم", "employee" },
    };

    [Theory]
    [MemberData(nameof(Colloquial))]
    public void العامية_الخليجية_ترفض_كما_ترفض_الفصحى(string transcript, string slot)
    {
        VoiceResolution resolution = Read(transcript);

        Assert.Null(ValueOf(resolution, slot));
        Assert.Contains(resolution.Faults, fault => fault.Code == BoundaryFault);
    }

    // ═══ ثالثاً · جملٌ خصومة — كُتبت لتكسر القاعدة الجديدة ═══════════════

    /// <summary>
    /// <b>خصومة ١:</b> اسمان في مقطعٍ واحد. «ومؤسسة» كلمةٌ واحدة لا حرفَ عطفٍ منفصلاً،
    /// فلا حدَّ نحويّاً بينهما — والعرض وحده يكشفهما.
    /// </summary>
    [Fact]
    public void خصومة_اسمان_في_مقطع_واحد_يرفضان_ولا_يُدمَجان()
    {
        VoiceResolution resolution = Read("سجل سند قبض من شركة المسار الامثل ومؤسسة النور نقد بمبلغ 20000 اليوم");

        Assert.Null(ValueOf(resolution, "customer"));
        Assert.Contains(resolution.Faults, fault => fault.Code == BoundaryFault);
    }

    /// <summary>
    /// <b>خصومة ٢:</b> علامةُ الوقف تُبقى رمزاً. والتفريغ المكتوب — وهو المسار الوحيد
    /// العامل على عنوانٍ غير مؤمَّن — يحملها فعلاً، فحذفُها إتلافُ إشارةٍ موجودة.
    /// </summary>
    [Fact]
    public void خصومة_الفاصلة_تحدّ_الاسم_ولا_تُحذف()
    {
        IReadOnlyList<string> tokens = VoiceText.Words("شركة المسار الامثل، فإن لم تجدها");
        Assert.Contains("،", tokens);

        VoiceResolution resolution = Read("سجل سند قبض من شركة المسار الامثل، فإن لم تجدها أنشئ لها حسابا. نقد بمبلغ 20000 اليوم");

        Assert.Equal("شركة المسار الامثل", ValueOf(resolution, "customer"));
        Assert.Equal("نقد", ValueOf(resolution, "method"));
        Assert.Empty(resolution.MissingSlots);
    }

    /// <summary>
    /// <b>خصومة ٣:</b> صيغةٌ صرفية لقيمةٍ مغلقة («نقداً» لا «نقد»). كان المقطع يبتلعها
    /// <b>وتبقى الشريحة المغلقة فارغة</b> — مستندٌ ناقصٌ باسمٍ ملوَّث. اليوم يُرفض.
    /// </summary>
    [Fact]
    public void خصومة_صيغة_صرفية_لقيمة_مغلقة_ترفض_ولا_تلتصق_بالاسم()
    {
        VoiceResolution resolution = Read("سجل سند قبض من شركة المسار الامثل نقدا بمبلغ 20000 بتاريخ اليوم");

        Assert.Null(ValueOf(resolution, "customer"));
        Assert.Contains(resolution.Faults, fault => fault.Code == BoundaryFault);
    }

    /// <summary>
    /// <b>خصومة ٤ — وهي الأهمّ لأنها تكشف حدّ العلاج نفسه:</b> ذيلٌ عرضُه ثلاث كلمات
    /// <b>يمرّ</b> على الأرضية وحدها. والأرضيةُ لا تدّعي أنها تعرف أين ينتهي الاسم —
    /// ولذلك يُقاس هنا الفرق: بلا سجلٍّ يمرّ، وبسجلٍّ يُحَدّ ويُسمّى ما بقي.
    /// </summary>
    [Fact]
    public void خصومة_ذيل_قصير_يمر_على_الأرضية_ويُحَدّ_بالسجل()
    {
        const string transcript = "سجل سند قبض من مؤسسة النور طبعا نقد بمبلغ 20000 اليوم";

        // بلا سجلّ: النافذة «مؤسسة النور طبعا» ثلاثُ كلمات — دون الحدّ، فتمرّ بذيلها.
        // ‏**وهذا هو الثقب مقيساً ومكتوباً**، لا مفاجأةً تُكتشف على شاشةٍ بعد سنة:
        // الأرضيةُ تقتل الابتلاعَ الطويل ولا تدّعي أنها تعرف أين ينتهي الاسم.
        Assert.Equal("مؤسسة النور طبعا", ValueOf(Read(transcript), "customer"));

        // وبسجلٍّ محقون: ينتهي الاسم حيث ينتهي الصفّ، وما بقي يُسمّى ولا يُبتلع.
        VoiceResolution bounded = Read(transcript, Directory(VoiceEntityKind.Customer, "مؤسسة النور"));
        Assert.Equal("مؤسسة النور", ValueOf(bounded, "customer"));
        SpokenResidue left = Assert.Single(bounded.Residue);
        Assert.Equal("customer", left.SlotName);
        Assert.Equal("طبعا", left.Text);
        Assert.Contains(bounded.Faults, fault => fault.Code == ResidueFault);
    }

    /// <summary>
    /// <b>خصومة ٥ — والحدُّ الذي وجدتُه بالقياس لا بالتوقّع:</b> اسمٌ يحمل قيمةَ قائمةٍ
    /// مغلقة داخله. والمطابقةُ على الحدود <b>مطويّةٌ تامّة لا جزئية</b>، فـ«الشبكة»
    /// ليست «شبكة» وأداةُ التعريف تُنقذ الاسم؛ أمّا «شبكة» عاريةً فتُحَدّ الاسمَ عندها.
    /// <para>
    /// <b>وهذا السلوك سابقٌ لهذا الإصلاح لا ناتجٌ عنه:</b> قيمُ هذه القائمة المغلقة
    /// مُعلَنة دلائلَ للشريحة نفسها، فكانت حدوداً قبل أن تُضاف القوائم إليها. وضمُّ
    /// القوائم يحرس الحال إن سقط دليلٌ يوماً، ولا يُضيّق شيئاً اليوم.
    /// </para>
    /// </summary>
    [Fact]
    public void خصومة_اسم_يحمل_قيمة_مغلقة_يُقاس_ولا_يُفترض()
    {
        VoiceResolution defined = Read("سجل سند قبض من مؤسسة الشبكة بمبلغ 20000 بتاريخ اليوم");
        Assert.Equal("مؤسسة الشبكة", ValueOf(defined, "customer"));
        Assert.Contains("method", defined.MissingSlots);

        VoiceResolution bare = Read("سجل سند قبض من مؤسسة شبكة بمبلغ 20000 بتاريخ اليوم");
        Assert.Equal("مؤسسة", ValueOf(bare, "customer"));
        Assert.Equal("شبكة", ValueOf(bare, "method"));

        // ‏**والحدُّ الذي لا يرفعه السجلّ — مقيسٌ ومكتوب:** النافذة تُحسب قبل أن يُسأل
        // السجلّ، فاسمٌ مسجَّل تقطعه قيمةُ قائمةٍ مغلقة في وسطه لا يستطيع السجلّ أن
        // يستعيده. والنتيجة رفضٌ مُسمّى — لا القيمةَ الناقصة «مؤسسة» صامتةً — وهو
        // الاتجاه الصحيح للفشل، وليس هو الجواب الكامل.
        VoiceResolution bounded = Read(
            "سجل سند قبض من مؤسسة شبكة بمبلغ 20000 بتاريخ اليوم",
            Directory(VoiceEntityKind.Customer, "مؤسسة شبكة"));
        Assert.Null(ValueOf(bounded, "customer"));
        Assert.Contains(bounded.Faults, fault => fault.Code == NotInRegisterFault);
    }

    // ═══ رابعاً · ما يفعله السجلّ حين يُحقن ══════════════════════════════

    [Fact]
    public void بالسجل_يُقرأ_اسم_صاحب_المنتج_ويُسمّى_الشرط_فضلةً()
    {
        VoiceResolution resolution = Read(
            OwnerUtterance,
            Directory(VoiceEntityKind.Customer, "شركة المسار الأمثل"));

        Assert.Equal("شركة المسار الامثل", ValueOf(resolution, "customer"));

        SpokenResidue left = Assert.Single(resolution.Residue);
        Assert.Equal("customer", left.SlotName);
        Assert.Equal(SwallowedClause, left.Text);

        Error fault = resolution.Faults.Single(f => f.Code == ResidueFault);
        Assert.Contains(SwallowedClause, fault.MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public void بالسجل_اسمٌ_غير_مسجل_يُرفض_ولا_يُقارَب_بأقرب_شبيه()
    {
        VoiceResolution resolution = Read(
            "سجل سند قبض من مؤسسة النور نقد بمبلغ 20000 اليوم",
            Directory(VoiceEntityKind.Customer, "مؤسسة النورين"));

        Assert.Null(ValueOf(resolution, "customer"));
        Assert.Contains(resolution.Faults, fault => fault.Code == NotInRegisterFault);
    }

    [Fact]
    public void بالسجل_اسمان_متعادلان_يُرفضان_ولا_يُقترع_بينهما()
    {
        // صفّان يُنطقان سواءً ويُكتبان مختلفَين — والطيّ يوحّدهما، والإملاء يفرّقهما.
        VoiceResolution resolution = Read(
            "سجل سند قبض من مؤسسة النور نقد بمبلغ 20000 اليوم",
            Directory(VoiceEntityKind.Customer, "مؤسسة النور", "مؤسسه النور"));

        Assert.Null(ValueOf(resolution, "customer"));
        Assert.Contains(resolution.Faults, fault => fault.Code == AmbiguousFault);
    }

    [Fact]
    public void بالسجل_الأخص_يفوز_حين_يكون_أحدهما_بادئة_الآخر()
    {
        VoiceResolution resolution = Read(
            "سجل سند قبض من شركة المسار الأمثل نقد بمبلغ 20000 اليوم",
            Directory(VoiceEntityKind.Customer, "شركة المسار", "شركة المسار الأمثل"));

        Assert.Equal("شركة المسار الامثل", ValueOf(resolution, "customer"));
        Assert.Empty(resolution.Residue);
    }

    [Fact]
    public void دليل_يحمل_مقطعاً_رقمياً_يُسقط_البناء_لا_النطق()
    {
        Result<VoiceEntityRegistry> built = VoiceEntityRegistry.Build(
            [new FakeDirectory(VoiceEntityKind.Customer, ["مؤسسة النور 4101"])]);

        Assert.True(built.IsFailure);
        Assert.Equal("ai.voice.entities.name_carries_a_ledger_code", built.Errors[0].Code);
    }

    // ═══ خامساً · النثر لا يُقاس بعرض الاسم ═════════════════════════════

    /// <summary>
    /// بيانُ القيد نثرٌ حرّ: إطالتُه إسهابٌ يقرؤه الإنسان ويقصّره، لا طرفٌ آخر يمرّ
    /// في مستندٍ صحيح الشكل. فاللاتماثل في الضرر هو ما يجعل الحدَّ على الأسماء وحدها.
    /// </summary>
    [Fact]
    public void النثر_الحر_لا_يُحَدّ_بعرض_الاسم()
    {
        VoiceIntent? journal = VoiceHarness.Registry.Find("accounting.journal_entry.draft");
        Assert.NotNull(journal);
        VoiceSlot description = journal.Slots.Single(slot => slot.Name == "description");
        Assert.Equal(VoiceEntityKind.None, description.Entity);

        VoiceResolution resolution = Read("سجل قيد يومية بيان اقفال حسابات فرعية متراكمة قديمة بمبلغ 5000 اليوم");
        string? read = ValueOf(resolution, "description");

        Assert.NotNull(read);
        Assert.True(VoiceText.Words(read).Count > SpokenCommandReader.NameWordLimit, read);
    }

    // ═══ سادساً · الرقم ثلاثةٌ مُشتقٌّ من السجلّ لا مُختار ═══════════════

    /// <summary>
    /// <b>الحدُّ يُشتقّ من المنتج لا من الذوق.</b> هذا الإثبات يقرأ كل قيمةٍ مشروعة في
    /// ملفّ المتجهات المُودَع ويُثبت أن أوسعها لا يتجاوز الحدّ. فإن اتّسع اسمٌ مشروع
    /// غداً، سقط هذا الإثبات <b>قبل</b> أن يُرفض على شاشة إنسان.
    /// </summary>
    [Fact]
    public void حدّ_عرض_الاسم_مُشتقٌّ_من_أوسع_قيمة_مشروعة_في_السجل()
    {
        Dictionary<(string Intent, string Slot), VoiceSlotKind> kinds = [];
        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            foreach (VoiceSlot slot in intent.Slots)
            {
                kinds[(intent.Id, slot.Name)] = slot.Kind;
            }
        }

        int widest = 0;
        int measured = 0;

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            foreach ((string name, string value) in vector.Slots)
            {
                if (!kinds.TryGetValue((vector.Intent, name), out VoiceSlotKind kind) || kind != VoiceSlotKind.Text)
                {
                    continue;
                }

                measured++;
                widest = Math.Max(widest, VoiceText.Words(value).Count);
            }
        }

        Assert.True(measured >= 40, "قيمُ النصّ المقيسة: " + measured.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(SpokenCommandReader.NameWordLimit, widest);
    }

    // ═══ سابعاً · نصف قطر الأثر — كلُّ شريحةٍ لا شريحةَ العميل وحدها ═════

    /// <summary>
    /// <b>يُحقن الشرطُ نفسه بعد قيمةِ كل شريحةِ سجلٍّ في كل نيّة</b>، ويُثبَت أن أيّاً
    /// منها لا تمتدّ لتبتلعه. والعطل لم يكن في «العميل» بل في القاعدة التي تحدّ كل
    /// مقطعٍ حرّ — فالإثبات على القاعدة، لا على الشريحة التي ظهر فيها.
    /// </summary>
    [Fact]
    public void ولا_شريحةَ_سجلٍّ_واحدة_في_السجل_كلِّه_تبتلع_شرطاً_مُحقَناً()
    {
        const string clause = "فان لم تجدها انشيء لها حسابا";
        Dictionary<(string Intent, string Slot), VoiceSlot> slots = [];

        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            foreach (VoiceSlot slot in intent.Slots)
            {
                slots[(intent.Id, slot.Name)] = slot;
            }
        }

        List<string> swallowed = [];
        int exercised = 0;

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            foreach ((string name, string expected) in vector.Slots)
            {
                if (!slots.TryGetValue((vector.Intent, name), out VoiceSlot? slot)
                    || slot.Kind != VoiceSlotKind.Text
                    || slot.Entity == VoiceEntityKind.None
                    || !vector.Transcript.Contains(expected, StringComparison.Ordinal))
                {
                    continue;
                }

                exercised++;

                int at = vector.Transcript.IndexOf(expected, StringComparison.Ordinal) + expected.Length;
                string injected = vector.Transcript[..at] + " " + clause + vector.Transcript[at..];

                Result<VoiceResolution> read = SpokenCommandReader.Read(
                    injected, VoiceHarness.Registry, VoiceHarness.Options);

                if (read.IsFailure)
                {
                    continue;
                }

                string? value = ValueOf(read.Value, name);
                if (value is not null && !string.Equals(value, expected, StringComparison.Ordinal))
                {
                    swallowed.Add(vector.Intent + "/" + name + " = «" + value + "»");
                }
            }
        }

        Assert.True(exercised >= 40, "الشرائح المُختبَرة: " + exercised.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(
            swallowed.Count == 0,
            "شرائحُ ابتلعت الشرط المُحقَن بدل أن ترفضه:\n" + string.Join('\n', swallowed));
    }

    /// <summary>
    /// كلُّ شريحةِ نصٍّ في السجلّ إمّا موسومةٌ بسجلٍّ فتُحَدّ، وإمّا <b>نثرٌ مُسمّى
    /// صراحةً</b>. ولا شريحةَ ثالثة: وسمٌ يُنسى يُعيد الشريحة إلى الابتلاع الصامت.
    /// </summary>
    [Fact]
    public void كل_شريحة_نص_إما_موسومة_بسجل_وإما_نثرٌ_مُسمّى()
    {
        HashSet<string> prose = ["description", "reason"];
        List<string> untagged = [];
        int counted = 0;

        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            foreach (VoiceSlot slot in intent.Slots.Where(static slot => slot.Kind == VoiceSlotKind.Text))
            {
                counted++;
                if (slot.Entity == VoiceEntityKind.None && !prose.Contains(slot.Name))
                {
                    untagged.Add(intent.Id + "/" + slot.Name);
                }
            }
        }

        Assert.True(counted >= 40, "شرائح النصّ المعدودة: " + counted.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(
            untagged.Count == 0,
            "شرائح نصٍّ بلا سجلٍّ ولا إعلانِ نثر — وهي التي تُبتلع بصمت:\n" + string.Join('\n', untagged));
    }
}
