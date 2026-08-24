using System.Diagnostics;
using System.Globalization;
using Babel.Contracts.Posting;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// قياس، لا ادّعاء.
/// <para>
/// <b>تحفّظ العتاد، وهو جوهري:</b> هذه الأرقام من حاوية مشتركة بأربع أنوية
/// افتراضية، وPostgreSQL يعمل على <b>نفس الجهاز</b> — أي أن زمن الشبكة صفر تقريباً.
/// هذا يقلب اتجاه أهم رقم في المشروع: فارق الـ127× بين «نداء واحد» و«أربع رحلات»
/// قيس عند RTT = 30 مللي‌ثانية، وهنا لا يظهر لأن RTT ≈ 0. أي أن ما يلي يقيس
/// <b>حدّ المعالجة والقفل</b>، لا حدّ الشبكة — والرقم الحقيقي في نشر عبر شبكة
/// سيكون أسوأ للنمط متعدّد الرحلات وأقرب لهذا للنمط ذي النداء الواحد.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class MeasurementTests : IAsyncLifetime
{
    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Throughput_at_one_eight_and_thirty_two_concurrent_writers()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Console.WriteLine("القياس — الإنتاجية (قيد/ثانية) · العتاد: حاوية مشتركة 4 أنوية افتراضية، وقاعدة البيانات محلية (RTT ≈ 0)");
        Console.WriteLine($"عدد الأنوية المرئية: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");

        foreach (int writers in new[] { 1, 8, 32 })
        {
            string book = "TP" + writers.ToString(CultureInfo.InvariantCulture);
            await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantB, book, token);

            const int perWriter = 25;
            int total = writers * perWriter;

            // إحماء: أول ترحيل لكل نطاق يبني صفوف الأرصدة وخطط الاستعلام.
            await PostAsync(book, "warm-" + book, token);

            Stopwatch stopwatch = Stopwatch.StartNew();
            await Parallel.ForAsync(0, writers, token, async (writer, ct) =>
            {
                for (int i = 0; i < perWriter; i++)
                {
                    await PostAsync(book,
                        $"tp-{writer.ToString(CultureInfo.InvariantCulture)}-{i.ToString(CultureInfo.InvariantCulture)}", ct);
                }
            });
            stopwatch.Stop();

            double perSecond = total / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"  {writers,2} كاتباً → {total.ToString(CultureInfo.InvariantCulture),4} قيداً في "
                + $"{stopwatch.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)} ث = "
                + $"{perSecond.ToString("0.0", CultureInfo.InvariantCulture)} قيد/ث");
        }

        // ── والسؤال الذي يقرّر: أين يذهب التزامن؟ ─────────────────────────
        // الكتّاب أعلاه كلهم على **دفتر واحد**، أي على صفّ عدّاد واحد — وهم
        // يتسلسلون عليه بالتصميم. فخ-15 يقول إن هذا يجعل 8 ⇒ 32 كاتباً يضيف صفر
        // إنتاجية. والمقياس أعلاه يُعيد إنتاج ذلك الشكل بالضبط.
        // والمخرج ليس «قفل أذكى» بل **نطاق أضيق**: كل كاتب على نطاقه.
        {
            const int writers = 32;
            const int perWriter = 25;

            for (int writer = 0; writer < writers; writer++)
            {
                await LedgerTestEnvironment.EnsureCounterAsync(
                    LedgerTestEnvironment.TenantB, "SC" + writer.ToString(CultureInfo.InvariantCulture), token);
                await PostAsync("SC" + writer.ToString(CultureInfo.InvariantCulture), "warm", token);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            await Parallel.ForAsync(0, writers, token, async (writer, ct) =>
            {
                string book = "SC" + writer.ToString(CultureInfo.InvariantCulture);
                for (int i = 0; i < perWriter; i++)
                {
                    await PostAsync(book, "sc-" + i.ToString(CultureInfo.InvariantCulture), ct);
                }
            });
            stopwatch.Stop();

            double perSecond = (writers * perWriter) / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"  32 كاتباً على 32 نطاقاً مستقلاً → {(writers * perWriter).ToString(CultureInfo.InvariantCulture)} قيداً في "
                + $"{stopwatch.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)} ث = "
                + $"{perSecond.ToString("0.0", CultureInfo.InvariantCulture)} قيد/ث");
        }

        Proof.Pass("الإنتاجية مقيسة عند 1 و8 و32 كاتباً، وعلى نطاق واحد مقابل نطاقات مستقلة",
            "انظر الأرقام أعلاه");
    }

    [Fact]
    public async Task Latency_percentiles_at_a_rate_limited_fifty_entries_per_second()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string book = "LAT";
        const int target = 50;
        const int seconds = 6;

        await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantB, book, token);
        await PostAsync(book, "warm-lat", token);

        List<double> samples = new(target * seconds);
        TimeSpan interval = TimeSpan.FromSeconds(1.0 / target);
        Stopwatch clock = Stopwatch.StartNew();

        for (int i = 0; i < target * seconds; i++)
        {
            TimeSpan due = interval * i;
            TimeSpan wait = due - clock.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, token);
            }

            long start = Stopwatch.GetTimestamp();
            await PostAsync(book, "lat-" + i.ToString(CultureInfo.InvariantCulture), token);
            samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        clock.Stop();
        samples.Sort();

        double achieved = samples.Count / clock.Elapsed.TotalSeconds;
        Console.WriteLine(
            $"القياس — الكمون عند معدّل مقيَّد {target.ToString(CultureInfo.InvariantCulture)} قيد/ث "
            + $"({samples.Count.ToString(CultureInfo.InvariantCulture)} عيّنة، معدّل محقَّق "
            + $"{achieved.ToString("0.0", CultureInfo.InvariantCulture)} قيد/ث)");
        Console.WriteLine($"  p50 = {Percentile(samples, 0.50).ToString("0.00", CultureInfo.InvariantCulture)} مث");
        Console.WriteLine($"  p95 = {Percentile(samples, 0.95).ToString("0.00", CultureInfo.InvariantCulture)} مث");
        Console.WriteLine($"  p99 = {Percentile(samples, 0.99).ToString("0.00", CultureInfo.InvariantCulture)} مث");
        Console.WriteLine($"  الأسوأ = {samples[^1].ToString("0.00", CultureInfo.InvariantCulture)} مث");

        Proof.Require(achieved >= target * 0.9,
            "المحرك يلاحق معدّل الذروة المعلَن دون تراكم",
            $"المعدّل المحقَّق {achieved.ToString("0.0", CultureInfo.InvariantCulture)} قيد/ث");
    }

    private async Task PostAsync(string book, string documentId, CancellationToken token)
    {
        PostingRequest request = Requests.RentInvoice(
            LedgerTestEnvironment.TenantB, book + "-" + documentId, 100.0000m, 15.0000m,
            new DateOnly(2026, 11, 10)) with
        { Book = book };

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(request, token);
        if (result.IsFailure)
        {
            Proof.Fail("ترحيل قياس فشل", result.Errors[0].Code + ": " + result.Errors[0].MessageAr);
        }
    }

    private static double Percentile(List<double> sorted, double fraction)
    {
        int index = (int)Math.Ceiling(fraction * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
