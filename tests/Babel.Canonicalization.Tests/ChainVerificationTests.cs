using Babel.Canonicalization.Schemas;
using Xunit;

namespace Babel.Canonicalization.Tests;

/// <summary>إعادة التحقق من السلسلة في الذاكرة: كل نوع انحراف يُسمّى بأول تسلسل منحرف.</summary>
public sealed class ChainVerificationTests
{
    private static readonly byte[] Genesis = Fixtures.Genesis;

    /// <summary>معرّف حتمي حتى يمكن إعادة بناء نفس القيد بالضبط في اختبار العبث.</summary>
    private static Guid Id(long seq)
    {
        var b = new byte[16];
        BitConverter.TryWriteBytes(b.AsSpan(8), seq);
        b[6] = 0x70; b[8] = 0x80;
        return new Guid(b);
    }

    private static List<ChainRecord> BuildChain(int n, Func<long, CanonicalDocument>? make = null)
    {
        var records = new List<ChainRecord>();
        var previous = Genesis;
        for (var seq = 1; seq <= n; seq++)
        {
            var doc = make?.Invoke(seq) ?? Fixtures.Entry(entryNo: seq, amount: 100.0000m * seq,
                idempotencyKey: "k-" + seq, entryId: Id(seq));
            var link = Canonicalizer.Compute(doc, seq, previous);
            records.Add(new ChainRecord
            {
                Sequence = seq,
                CanonVersion = link.CanonVersion,
                Document = doc,
                StoredPreviousHash = link.PreviousHash,
                StoredHash = link.Hash
            });
            previous = link.Hash;
        }
        return records;
    }

    [Fact]
    public void AnIntactChainVerifies()
    {
        var v = ChainVerifier.VerifyChain(BuildChain(20), Genesis);
        Assert.True(v.Ok, v.ToString());
        Assert.Equal(20, v.Checked);
        Assert.Null(v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.Ok, v.Verdict);
    }

    [Fact]
    public void AnEmptyChainIsReportedNotAssumedIntact()
    {
        var v = ChainVerifier.VerifyChain([], Genesis);
        Assert.Equal(ChainVerdicts.Empty, v.Verdict);
        Assert.Equal(0, v.Checked);
    }

    [Fact]
    public void TamperedContentIsCaughtAtItsOwnSequence()
    {
        var chain = BuildChain(10);
        chain[4] = chain[4] with
        {
            Document = Fixtures.Entry(amount: 999.0000m, entryNo: 5, idempotencyKey: "k-5", entryId: Id(5))
        };

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(5, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.ContentTampered, v.Verdict);
    }

    [Fact]
    public void RewritingTheRecordsOwnHashMovesDetectionToTheNextSequence()
    {
        var chain = BuildChain(10);

        var replaced = Fixtures.Entry(amount: 999.0000m, entryNo: 5, idempotencyKey: "k-5", entryId: Id(5));
        var recomputed = Canonicalizer.Compute(replaced, 5, chain[4].StoredPreviousHash);
        chain[4] = chain[4] with { Document = replaced, StoredHash = recomputed.Hash };

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(6, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.LinkBroken, v.Verdict);
    }

    [Fact]
    public void AGapIsDetectedAtTheMissingSequence()
    {
        var chain = BuildChain(8);
        chain.RemoveAt(3); // التسلسل 4

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(4, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.SequenceGap, v.Verdict);
    }

    /// <summary>
    /// تبديل موضعَي سجلين: المتحقّق يقف عند أول موضع مخالف ويسمّي التسلسل المتوقّع.
    /// التبديل الأمامي يظهر أولاً كفجوة — والمهم أن الرقم المُبلَّغ هو 3، أي أول
    /// موضع لم يعد الترتيب فيه صحيحاً.
    /// </summary>
    [Fact]
    public void ReorderingIsDetectedAtTheFirstOutOfPlaceSequence()
    {
        var chain = BuildChain(6);
        (chain[2], chain[3]) = (chain[3], chain[2]);

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(3, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.SequenceGap, v.Verdict);
    }

    /// <summary>تسلسل مكرّر أو راجع للخلف يُصنَّف «غير مرتّب».</summary>
    [Fact]
    public void ADuplicatedSequenceIsDetectedAsOutOfOrder()
    {
        var chain = BuildChain(5);
        chain[3] = chain[3] with { Sequence = 3 };

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(4, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.SequenceOutOfOrder, v.Verdict);
    }

    [Fact]
    public void AWrongGenesisIsDetectedAtSequenceOne()
    {
        var chain = BuildChain(4);
        var other = JournalEntrySchema.Genesis("acme", "OTHER", 2026);

        var v = ChainVerifier.VerifyChain(chain, other);
        Assert.False(v.Ok);
        Assert.Equal(1, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.LinkBroken, v.Verdict);
    }

    [Fact]
    public void AnUnknownStoredCanonVersionIsRefusedNotGuessed()
    {
        var chain = BuildChain(3);
        chain[1] = chain[1] with { CanonVersion = "v7" };

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(2, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.VersionUnknown, v.Verdict);
    }

    [Fact]
    public void RewritingTheStoredPreviousHashAloneIsDetected()
    {
        // «الرابط في عمود مجاور»: العابث يعيد كتابة prev_hash فقط.
        var chain = BuildChain(5);
        var forged = (byte[])chain[3].StoredPreviousHash.Clone();
        forged[0] ^= 0xFF;
        chain[3] = chain[3] with { StoredPreviousHash = forged };

        var v = ChainVerifier.VerifyChain(chain, Genesis);
        Assert.False(v.Ok);
        Assert.Equal(4, v.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.LinkBroken, v.Verdict);
    }

    [Fact]
    public void ChainsStartingAtANonOneSequenceAreSupportedExplicitly()
    {
        var records = new List<ChainRecord>();
        var previous = Genesis;
        for (var seq = 100; seq < 105; seq++)
        {
            var doc = Fixtures.Entry(entryNo: seq, idempotencyKey: "k-" + seq);
            var link = Canonicalizer.Compute(doc, seq, previous);
            records.Add(new ChainRecord
            {
                Sequence = seq, CanonVersion = link.CanonVersion, Document = doc,
                StoredPreviousHash = link.PreviousHash, StoredHash = link.Hash
            });
            previous = link.Hash;
        }

        Assert.True(ChainVerifier.VerifyChain(records, Genesis, expectedFirstSequence: 100).Ok);
        Assert.False(ChainVerifier.VerifyChain(records, Genesis).Ok);
    }
}
