using System.Globalization;
using System.Security.Cryptography;

namespace Babel.Canonicalization;

/// <summary>
/// سجل مقروء من التخزين، جاهز لإعادة التحقق.
/// لاحظ أن <see cref="CanonVersion"/> يأتي <b>من العمود المخزَّن</b>، لا من ثابت
/// في الشيفرة: هذا ما يُبقي سجلات v1 قابلة للتحقق بعد ظهور v2.
/// </summary>
public sealed record ChainRecord
{
    /// <summary>رقم التسلسل المخزَّن.</summary>
    public required long Sequence { get; init; }

    /// <summary>إصدار الشكل القانوني المخزَّن بجوار البصمة.</summary>
    public required string CanonVersion { get; init; }

    /// <summary>المستند كما أُعيد بناؤه من الحقيقة المجالية المخزَّنة.</summary>
    public required CanonicalDocument Document { get; init; }

    /// <summary>البصمة السابقة كما هي مخزَّنة في هذا السجل.</summary>
    public required byte[] StoredPreviousHash { get; init; }

    /// <summary>بصمة هذا السجل كما هي مخزَّنة.</summary>
    public required byte[] StoredHash { get; init; }
}

/// <summary>رمز نتيجة التحقق. ثابت، ومناسب للتسجيل والتنبيه.</summary>
public static class ChainVerdicts
{
    public const string Ok = "CHAIN-OK";
    public const string Empty = "CHAIN-EMPTY";
    public const string SequenceGap = "CHAIN-SEQUENCE-GAP";
    public const string SequenceOutOfOrder = "CHAIN-SEQUENCE-OUT-OF-ORDER";
    public const string GenesisMismatch = "CHAIN-GENESIS-MISMATCH";
    public const string LinkBroken = "CHAIN-LINK-BROKEN";
    public const string ContentTampered = "CHAIN-CONTENT-TAMPERED";
    public const string VersionUnknown = "CHAIN-VERSION-UNKNOWN";
    public const string VersionMismatch = "CHAIN-VERSION-MISMATCH";
    public const string RecomputeFailed = "CHAIN-RECOMPUTE-FAILED";
}

/// <summary>نتيجة إعادة التحقق من سلسلة.</summary>
public sealed record ChainVerification
{
    /// <summary>هل السلسلة سليمة كاملة؟</summary>
    public required bool Ok { get; init; }

    /// <summary>عدد السجلات التي فُحصت (بما فيها السجل المنحرف).</summary>
    public required int Checked { get; init; }

    /// <summary><b>أول رقم تسلسل منحرف</b>، أو <c>null</c> إن كانت السلسلة سليمة.</summary>
    public required long? FirstDivergentSequence { get; init; }

    /// <summary>رمز الحكم من <see cref="ChainVerdicts"/>.</summary>
    public required string Verdict { get; init; }

    /// <summary>شرح عربي صالح للعرض في تقرير تدقيق.</summary>
    public required string ReasonAr { get; init; }

    /// <summary>تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة.</summary>
    public string? Detail { get; init; }

    public override string ToString()
        => Ok
            ? $"{Verdict}: {Checked} سجلاً"
            : $"{Verdict} عند التسلسل {FirstDivergentSequence}: {ReasonAr}{(Detail is null ? "" : " | " + Detail)}";
}

/// <summary>
/// إعادة التحقق من سلسلة كاملة، وإرجاع <b>أول</b> رقم تسلسل منحرف.
///
/// الفحوص، بهذا الترتيب لكل سجل:
///   1. التسلسل متصاعد بخطوة 1 بلا فجوة ولا تكرار — <b>الفجوة تُثبَت إيجاباً</b>.
///      المتحقّق لا يستطيع التمييز بين «لم يحدث شيء» و«حُذفت السجلات» إن اكتفينا
///      بفحص ما هو موجود.
///   2. إصدار الشكل القانوني المخزَّن معروف، ويطابق إصدار مخطّط المستند.
///   3. <c>prev_hash</c> المخزَّن = بصمة السجل السابق (أو بصمة التكوين للسجل الأول).
///   4. إعادة حساب البصمة من الحقيقة المجالية + التسلسل + البصمة السابقة = البصمة المخزَّنة.
///
/// الفحص 4 هو الذي يمسك العبث بالمحتوى؛ والفحص 3 هو الذي يمسك العابث الأذكى الذي
/// أعاد كتابة بصمة السجل المعبوث به أيضاً — عندها ينتقل الانحراف إلى السجل التالي.
/// </summary>
public static class ChainVerifier
{
    /// <summary>
    /// يعيد التحقق من سلسلة مرتّبة تصاعدياً برقم التسلسل.
    /// </summary>
    /// <param name="records">السجلات مرتّبة برقم التسلسل تصاعدياً.</param>
    /// <param name="genesisHash">بصمة التكوين للنطاق، من <see cref="Canonicalizer.Genesis"/>.</param>
    /// <param name="expectedFirstSequence">أول رقم تسلسل متوقّع. الافتراضي 1.</param>
    public static ChainVerification VerifyChain(
        IEnumerable<ChainRecord> records,
        byte[] genesisHash,
        long expectedFirstSequence = 1)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(genesisHash);

        var list = records as IReadOnlyList<ChainRecord> ?? [.. records];
        if (list.Count == 0)
            return new ChainVerification
            {
                Ok = true, Checked = 0, FirstDivergentSequence = null,
                Verdict = ChainVerdicts.Empty,
                ReasonAr = "لا سجلات في النطاق. فترة بلا حركة يجب أن تُثبَت بمصنوع موقَّع خاص بها، " +
                           "لا أن تُستنتج من غياب السجلات."
            };

        var previousHash = genesisHash;
        var expectedSequence = expectedFirstSequence;
        var checkedCount = 0;

        foreach (var record in list)
        {
            checkedCount++;

            // 1) تسلسل بلا فجوات
            if (record.Sequence != expectedSequence)
            {
                var gap = record.Sequence > expectedSequence;
                return new ChainVerification
                {
                    Ok = false, Checked = checkedCount,
                    FirstDivergentSequence = expectedSequence,
                    Verdict = gap ? ChainVerdicts.SequenceGap : ChainVerdicts.SequenceOutOfOrder,
                    ReasonAr = gap
                        ? $"فجوة في التسلسل: المتوقّع {expectedSequence} والموجود {record.Sequence}. " +
                          "المدقّق يقرأ الرقم المفقود مستنداً محذوفاً."
                        : $"التسلسل غير مرتّب أو مكرّر: المتوقّع {expectedSequence} والموجود {record.Sequence}.",
                    Detail = $"expected={expectedSequence} found={record.Sequence}"
                };
            }

            // 2) الإصدار
            ICanonicalizer canonicalizer;
            try
            {
                canonicalizer = CanonRegistry.Resolve(record.CanonVersion);
            }
            catch (CanonicalizationException ex)
            {
                return Fail(checkedCount, record.Sequence, ChainVerdicts.VersionUnknown,
                    "إصدار الشكل القانوني المخزَّن غير معروف لهذه النسخة من المكتبة.", ex.Message);
            }

            if (record.Document.CanonVersion != record.CanonVersion)
                return Fail(checkedCount, record.Sequence, ChainVerdicts.VersionMismatch,
                    "إصدار مخطّط المستند لا يطابق الإصدار المخزَّن بجوار البصمة.",
                    $"stored={record.CanonVersion} schema={record.Document.CanonVersion}");

            // 3) الرابط
            if (!record.StoredPreviousHash.AsSpan().SequenceEqual(previousHash))
                return Fail(checkedCount, record.Sequence, ChainVerdicts.LinkBroken,
                    record.Sequence == expectedFirstSequence
                        ? "بصمة التكوين المخزَّنة لا تطابق بصمة تكوين هذا النطاق."
                        : "البصمة السابقة المخزَّنة لا تطابق بصمة السجل السابق: الرابط مكسور.",
                    $"expected_prev={Canonicalizer.Hex(previousHash)} stored_prev={Canonicalizer.Hex(record.StoredPreviousHash)}");

            if (record.Sequence == expectedFirstSequence &&
                !record.StoredPreviousHash.AsSpan().SequenceEqual(genesisHash))
                return Fail(checkedCount, record.Sequence, ChainVerdicts.GenesisMismatch,
                    "السجل الأول لا يشير إلى بصمة التكوين الصحيحة.", null);

            // 4) إعادة الحساب من الحقيقة المجالية
            byte[] recomputed;
            try
            {
                var bound = record.Document.Unbind().Bind(record.Sequence, record.StoredPreviousHash);
                recomputed = SHA256.HashData(canonicalizer.Canonicalize(bound));
            }
            catch (CanonicalizationException ex)
            {
                return Fail(checkedCount, record.Sequence, ChainVerdicts.RecomputeFailed,
                    "تعذّر إعادة توحيد المستند من البيانات المخزَّنة: " +
                    "القيمة المخزَّنة نفسها لم تعد تمرّ من قواعد الشكل القانوني " +
                    "(تطبيع بحث كُتب فوق حقل موقَّع؟ هجرة عدّلت طابعاً زمنياً؟).",
                    ex.Message);
            }

            if (!recomputed.AsSpan().SequenceEqual(record.StoredHash))
                return Fail(checkedCount, record.Sequence, ChainVerdicts.ContentTampered,
                    "بصمة المحتوى المُعاد حسابها لا تطابق البصمة المخزَّنة: المحتوى تغيّر بعد الترحيل.",
                    $"recomputed={Canonicalizer.Hex(recomputed)} stored={Canonicalizer.Hex(record.StoredHash)}");

            previousHash = record.StoredHash;
            expectedSequence++;
        }

        return new ChainVerification
        {
            Ok = true, Checked = checkedCount, FirstDivergentSequence = null,
            Verdict = ChainVerdicts.Ok,
            ReasonAr = $"{checkedCount.ToString(CultureInfo.InvariantCulture)} سجلاً أُعيد التحقق منها من بصمة التكوين حتى الرأس.",
            Detail = $"head={Canonicalizer.Hex(previousHash)}"
        };
    }

    private static ChainVerification Fail(int checkedCount, long seq, string verdict, string reason, string? detail)
        => new()
        {
            Ok = false, Checked = checkedCount, FirstDivergentSequence = seq,
            Verdict = verdict, ReasonAr = reason, Detail = detail
        };
}
