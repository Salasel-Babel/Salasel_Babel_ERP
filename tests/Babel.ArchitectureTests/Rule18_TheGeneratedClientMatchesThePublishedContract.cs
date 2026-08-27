using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 18 — العميل المُولَّد يطابق العقد المنشور، ويُفشل البناء إن لم يطابقه.</b>
/// <para>
/// <b>العقد له طرفان، وكان محروساً من طرف واحد.</b> جهة .NET تحرس أن
/// <c>contracts/openapi/v1.json</c> يطابق ما يبعثه الخادم (<c>PublishedContractTests</c>).
/// وجهة الواجهة تحرس أن <c>web/src/api/generated/</c> يطابق ذلك الملفّ
/// (<c>npm run gen:check</c>). فمن غيّر العقد ورأى حرّاس .NET <b>خضراء</b> ظنّ التغيير
/// نازلاً كاملاً — وهو نصفه.
/// </para>
/// <para>
/// <b>وقد وقع، ومقيس:</b> الإيداع <c>2a34cc9</c> أعاد توليد العقد المنشور بعد دخول
/// <c>purchasing.supplier_bill</c> إلى كتالوج القدرات — فاتّسع تعدادان
/// (<c>documentType</c> و<c>capabilities</c>، ومعهما <c>landed_cost</c> و
/// <c>three_way_match</c>) — و<b>لم يُعَد توليد عميل TypeScript معه</b>. فبقيت الملفّات
/// الستّة تحت <c>web/src/api/generated/</c> تحمل بصمة العقد القديم <c>e678dc2c…</c>
/// بينما صار العقد <c>8a33528a…</c>. و<c>PublishedContractTests</c> خضراء 4/4 طوال ذلك،
/// لأنها تحرس الطرف الآخر.
/// </para>
/// <para>
/// <b>ولماذا هذه القاعدة لا تُعيد التوليد:</b> على شاكلة القاعدتين 15 و16 — لا تُشغّل
/// <c>npm run gen</c> ولا تحتاج Node أصلاً. تقرأ البصمة المُسجَّلة في ترويسة كل ملفّ
/// مُولَّد وتقارنها بـ<c>sha256</c> العقد على القرص. فالانحراف يصير <b>عطل بناء في
/// بوّابة الخلفية</b> لا سطراً في سكربت لا يُشغّله إلا سير الواجهة.
/// </para>
/// <para>
/// <b>وهو فخ-80 في موضع ثانٍ:</b> إشارةٌ خضراء تغطّي أقلّ ممّا يفترضه قارئها.
/// (‏<c>docs/evidence/traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only</c>)
/// </para>
/// </summary>
public sealed class Rule18_TheGeneratedClientMatchesThePublishedContract
{
    private const string Contract = "contracts/openapi/v1.json";
    private const string GeneratedFolder = "web/src/api/generated";

    /// <summary>عددٌ أدنى معلوم وقت كتابة القاعدة — حارس لافراغ.</summary>
    private const int MinimumGeneratedFiles = 6;

    /// <summary>البصمة كما تكتبها الترويسة: سطرٌ وحده فيه أربعٌ وستّون خانة ست عشرية.</summary>
    private static readonly Regex HeaderHash =
        new(@"^\s{4,}(?<hash>[0-9a-f]{64})\s*$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    /// <summary>البصمة كما يكتبها ثابت العقد في <c>contract.ts</c>.</summary>
    private static readonly Regex ConstantHash =
        new(@"sourceSha256:\s*""(?<hash>[0-9a-f]{64})""", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// كل ملفّ مُولَّد يحمل بصمة العقد <b>الذي على القرص الآن</b>. وانحرافُ أيٍّ منها
    /// يُفشل البناء هنا، لا في سير الواجهة وحده.
    /// </summary>
    [Fact]
    public void EveryGeneratedFileRecordsTheHashOfThePublishedContractOnDisk()
    {
        string expected = ContractHash();
        List<string> drifted = [];

        foreach (string file in GeneratedFiles())
        {
            string text = File.ReadAllText(file);
            Match match = HeaderHash.Match(text);

            if (!match.Success)
            {
                drifted.Add($"{Relative(file)}: لا بصمة في الترويسة · no hash recorded");
                continue;
            }

            string recorded = match.Groups["hash"].Value;
            if (!string.Equals(recorded, expected, StringComparison.Ordinal))
            {
                drifted.Add(
                    FormattableString.Invariant(
                        $"{Relative(file)}: مُسجَّلة {recorded[..8]}… · العقد {expected[..8]}…"));
            }
        }

        Assert.True(
            drifted.Count == 0,
            "العميل المُولَّد لا يطابق العقد المنشور — غُيِّر العقد ولم يُعَد التوليد.\n"
            + "أعِد التوليد: cd web && npm run gen\n"
            + "وهو ما وقع في 2a34cc9: اتّسع العقد بـpurchasing.supplier_bill ولم يُعَد توليد "
            + "العميل، وحرّاس .NET خضراء لأنها تحرس الطرف الآخر (فخ-80 في موضع ثانٍ).\n"
            + "The generated client no longer matches the published contract; run `npm run gen`.\n"
            + string.Join("\n", drifted));
    }

    /// <summary>
    /// وثابت العقد في <c>contract.ts</c> يوافق ترويسته. اختلافُهما يعني ملفّاً حُرِّر بيد
    /// بين توليدين — وهو ما تمنعه الترويسة بالنصّ ولا يمنعه شيء بالبناء.
    /// </summary>
    [Fact]
    public void TheContractConstantAgreesWithItsOwnHeader()
    {
        string path = Path.Combine(RepositoryLayout.Root, GeneratedFolder, "contract.ts");
        Assert.True(File.Exists(path), $"{GeneratedFolder}/contract.ts غير موجود · missing.");

        string text = File.ReadAllText(path);
        Match header = HeaderHash.Match(text);
        Match constant = ConstantHash.Match(text);

        Assert.True(header.Success, "لا بصمة في ترويسة contract.ts · no header hash.");
        Assert.True(constant.Success, "لا sourceSha256 في contract.ts · no sourceSha256 constant.");

        Assert.True(
            string.Equals(header.Groups["hash"].Value, constant.Groups["hash"].Value, StringComparison.Ordinal),
            "ترويسة contract.ts وثابت sourceSha256 يقولان بصمتين مختلفتين — الملفّ حُرِّر بيد. · "
            + "The header and the sourceSha256 constant disagree: the file was hand-edited.");
    }

    /// <summary>
    /// حارس اللافراغ. مسحٌ لا يجد ملفّاً يمرّ دائماً — وهو عطل فخ-43 بعينه.
    /// </summary>
    [Fact]
    public void TheComputationIsNotVacuous()
    {
        string contract = Path.Combine(RepositoryLayout.Root, Contract);
        Assert.True(File.Exists(contract), $"{Contract} غير موجود — وهو مصدر التوليد. · missing.");
        Assert.True(
            new FileInfo(contract).Length > 4096,
            $"{Contract} أصغر من أن يكون عقداً منشوراً · implausibly small.");

        List<string> files = GeneratedFiles();
        Assert.True(
            files.Count >= MinimumGeneratedFiles,
            FormattableString.Invariant(
                $"لم يُقرأ من {GeneratedFolder} إلا {files.Count} ملفّاً (الحدّ الأدنى {MinimumGeneratedFiles}) — المسح ضامر فالقاعدة تمرّ على لا شيء. · vacuous scan."));

        Assert.All(files, file => Assert.Matches(HeaderHash, File.ReadAllText(file)));

        // شاهد إيجابي: البصمة المحسوبة ليست فارغة ولا ثابتة مصطنعة.
        Assert.Equal(64, ContractHash().Length);
    }

    private static string ContractHash()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(RepositoryLayout.Root, Contract));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLower(CultureInfo.InvariantCulture);
    }

    private static List<string> GeneratedFiles()
    {
        string folder = Path.Combine(RepositoryLayout.Root, GeneratedFolder);
        Assert.True(Directory.Exists(folder), $"{GeneratedFolder} غير موجود · missing.");
        return [.. Directory.EnumerateFiles(folder, "*.ts").OrderBy(static x => x, StringComparer.Ordinal)];
    }

    private static string Relative(string full) =>
        Path.GetRelativePath(RepositoryLayout.Root, full).Replace('\\', '/');
}
