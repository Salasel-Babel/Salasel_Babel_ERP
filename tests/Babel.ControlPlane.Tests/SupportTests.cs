using Babel.ControlPlane.Connections;
using Babel.ControlPlane.Metering;
using Babel.ControlPlane.Provisioning;
using Babel.ControlPlane.Support;
using Xunit;

namespace Babel.ControlPlane.Tests;

public class CanonTests
{
    [Fact]
    public void الطابع_الزمني_يُقَصّ_إلى_الميكروثانية()
    {
        // فخ-16: نبضة .NET (100 نانوثانية) أدقّ من timestamptz (ميكروثانية).
        var t = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero).AddTicks(1237);
        var c = Canon.Instant(t);
        Assert.Equal(0, c.Ticks % 10);
        Assert.Equal(Canon.Instant(c), c);          // القصّ ثابت (idempotent)
    }

    [Theory]
    [InlineData("1234.5", "1234.5000")]
    [InlineData("0", "0.0000")]
    [InlineData("-99.12345", "-99.1234")]           // تقريب مصرفي إلى 4
    [InlineData("1000000000000000.0001", "1000000000000000.0001")]
    public void المبلغ_يُنسَّق_بمقياس_أربعة_وثقافة_ثابتة(string input, string expected) =>
        Assert.Equal(expected, Canon.Amount(decimal.Parse(input,
            System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void تنسيق_المبلغ_لا_يتأثّر_بلغة_الخيط()
    {
        // فخ-18: ToString() واعٍ باللغة قرب رقم مالي = خطأ فوترة صامت.
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("1234.5600", Canon.Amount(1234.56m));
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1234.5600", Canon.Amount(1234.56m));
        }
        finally { Thread.CurrentThread.CurrentCulture = before; }
    }

    [Fact]
    public void المحارف_الاتجاهية_غير_المرئية_مرفوضة_عند_الحدّ()
    {
        // فخ-23: تُرفض عند الحدّ، لا تُزال لاحقاً.
        Assert.Throws<ArgumentException>(() => Canon.Text("شركة‏الوادي", "name_ar"));
        Assert.Throws<ArgumentException>(() => Canon.Text("Al‮Wadi", "name_en"));
    }

    [Fact]
    public void التطبيع_إلى_NFC_يقع_مرة_واحدة_عند_الحدّ()
    {
        // فخ-24: نفس النصّ بصورتين يونيكود مختلفتين يصير صورة واحدة.
        var nfd = "أ".Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal("أ", Canon.Text(nfd, "name_ar"));
    }

    [Fact]
    public void الاسم_الثنائي_يرفض_الفراغ()
    {
        Assert.Throws<ArgumentException>(() => BilingualName.Of("", "x"));
        Assert.Throws<ArgumentException>(() => BilingualName.Of("س", "  "));
        var n = BilingualName.Of(" شركة ", " Company ");
        Assert.Equal("شركة", n.Ar);
        Assert.Equal("Company", n.En);
    }
}

public class IdentifierTests
{
    [Theory]
    [InlineData("alwadi")]
    [InlineData("babel_t_x9")]
    public void المعرّفات_المقبولة_تمرّ(string s) => Assert.Equal(s, Db.Ident(s));

    [Theory]
    [InlineData("Alwadi")]                 // حرف كبير
    [InlineData("al-wadi")]                // شرطة
    [InlineData("al wadi")]                // فراغ
    [InlineData("9alwadi")]                // يبدأ برقم
    [InlineData("")]
    [InlineData("x\"; drop database postgres; --")]
    public void المعرّفات_المرفوضة_ترمي(string s) => Assert.Throws<ArgumentException>(() => Db.Ident(s));

    [Fact]
    public void معرّف_المستأجر_مشتقّ_حتمياً_من_رمزه()
    {
        var a = TenantProvisioner.DeterministicTenantId("alwadi");
        var b = TenantProvisioner.DeterministicTenantId("alwadi");
        var c = TenantProvisioner.DeterministicTenantId("alwadi2");
        Assert.Equal(a, b);          // إعادة التزويد تصل إلى نفس المستأجر
        Assert.NotEqual(a, c);
        Assert.NotEqual(Guid.Empty, a);
    }
}

public class CircuitBreakerTests
{
    private static (TenantCircuitBreaker Cb, Func<DateTimeOffset> Clock, Action<TimeSpan> Advance) Make(
        int threshold = 3, int openSeconds = 10)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset Clock() => now;
        void Advance(TimeSpan d) => now += d;
        return (new TenantCircuitBreaker(threshold, TimeSpan.FromSeconds(openSeconds), Clock),
                Clock, Advance);
    }

    [Fact]
    public void يبقى_مغلقاً_تحت_العتبة()
    {
        var (cb, _, _) = Make();
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Closed, cb.State);
        cb.ThrowIfOpen("t");                     // لا يرمي
    }

    [Fact]
    public void يفتح_عند_العتبة_ويرفض_فوراً()
    {
        var (cb, _, _) = Make();
        for (var i = 0; i < 3; i++) cb.RecordFailure();
        Assert.Equal(CircuitState.Open, cb.State);
        Assert.Throws<CircuitOpenException>(() => cb.ThrowIfOpen("t"));
        Assert.Equal(1, cb.Trips);
        Assert.Equal(1, cb.RejectedFast);
    }

    [Fact]
    public void ينتقل_إلى_نصف_مفتوح_بعد_المهلة()
    {
        var (cb, _, advance) = Make();
        for (var i = 0; i < 3; i++) cb.RecordFailure();
        advance(TimeSpan.FromSeconds(11));
        Assert.Equal(CircuitState.HalfOpen, cb.State);
        cb.ThrowIfOpen("t");                     // نصف المفتوح يسمح بمحاولة واحدة
    }

    [Fact]
    public void فشل_واحد_في_نصف_المفتوح_يُعيد_الفتح()
    {
        var (cb, _, advance) = Make();
        for (var i = 0; i < 3; i++) cb.RecordFailure();
        advance(TimeSpan.FromSeconds(11));
        Assert.Equal(CircuitState.HalfOpen, cb.State);
        cb.RecordFailure();
        Assert.Equal(CircuitState.Open, cb.State);
    }

    [Fact]
    public void نجاح_واحد_يُغلق_القاطع_ويصفّر_العدّاد()
    {
        var (cb, _, advance) = Make();
        for (var i = 0; i < 3; i++) cb.RecordFailure();
        advance(TimeSpan.FromSeconds(11));
        cb.RecordSuccess();
        Assert.Equal(CircuitState.Closed, cb.State);
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Closed, cb.State);
    }
}

public class UsageSpoolTests
{
    [Fact]
    public void المخزن_المحلّي_يحفظ_ويسترجع_الحدث_كاملاً()
    {
        var path = Path.Combine(Path.GetTempPath(), $"babel-spool-test-{Guid.NewGuid():N}.jsonl");
        try
        {
            var spool = new UsageSpool(path);
            var tenant = Guid.CreateVersion7();
            var e = new UsageEvent(tenant, "k-1", "2026-04", "POS", "u@x.example",
                "posting.created", 2.5000m,
                new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero), "test");

            Assert.Equal(1, spool.Append([e]));
            Assert.Equal(1, spool.Append([e]));      // الإضافة فقط، بلا تنقيح
            var read = spool.ReadAll();

            Assert.Equal(2, read.Count);
            Assert.Equal(tenant, read[0].TenantId);
            Assert.Equal("k-1", read[0].IdempotencyKey);
            Assert.Equal(2.5000m, read[0].Quantity);      // decimal لا double
            Assert.Equal(e.OccurredAt, read[0].OccurredAt);

            spool.Clear();
            Assert.Empty(spool.ReadAll());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void التطبيع_يقصّ_الطابع_ويقرّب_الكمّية()
    {
        var e = new UsageEvent(Guid.CreateVersion7(), "k", "2026-04", "AR", null, "x",
            1.000049m, new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(7), "t")
            .Normalised();
        Assert.Equal(0, e.OccurredAt.Ticks % 10);
        Assert.Equal(1.0000m, e.Quantity);
    }

    [Fact]
    public void رمز_الفترة_مشتقّ_من_الطابع_بصيغة_واحدة() =>
        Assert.Equal("2026-04",
            UsageEvent.PeriodOf(new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero)));
}

public class BillableUserTests
{
    [Fact]
    public void التعريفات_الثلاثة_كلها_متاحة()
    {
        Assert.Equal(3, BillableUserStrategies.All.Count);
        Assert.Contains(BillableUserStrategies.All, s => s.Code == "Named");
        Assert.Contains(BillableUserStrategies.All, s => s.Code == "Concurrent");
        Assert.Contains(BillableUserStrategies.All, s => s.Code == "ActiveInPeriod");
    }

    [Fact]
    public void الافتراضي_هو_الأكثر_تحفّظاً_تجاه_العميل() =>
        Assert.Equal("ActiveInPeriod", BillableUserStrategies.Default.Code);

    [Fact]
    public void كل_تعريف_يحمل_اسمين()
    {
        foreach (var s in BillableUserStrategies.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.NameAr));
            Assert.False(string.IsNullOrWhiteSpace(s.NameEn));
        }
    }

    [Fact]
    public void تعريف_غير_معروف_يرمي() =>
        Assert.Throws<ArgumentException>(() => BillableUserStrategies.ByCode("Whatever"));
}
