
namespace BabelDemo.Db;

/// <summary>
/// بيانات العرض. لاحظ أن القيود الافتتاحية تُرحَّل عبر PostingService نفسه —
/// لا يوجد مسار كتابة ثانٍ للبذر.
/// The seed posts through the same PostingService: there is no second write path.
/// </summary>
internal static class Seed
{
    public const string SeedActor = "نظام التهيئة";

    public static readonly PostRequest[] OpeningEntries =
    [
        new(new DateOnly(2026, 8, 1),
            "قيد افتتاحي — إيداع رأس المال في الحساب البنكي",
            "Opening entry — share capital deposited to bank",
            SeedActor,
            [
                new("1201", "الحساب الجاري — البنك الأهلي", 500_000.0000m, 0m),
                new("3101", "رأس المال المدفوع",             0m, 500_000.0000m),
            ]),

        new(new DateOnly(2026, 8, 5),
            "فاتورة خدمات تقنية عن الفترة 08/2026 شاملة ضريبة القيمة المضافة",
            "IT services invoice for 08/2026 including VAT",
            SeedActor,
            [
                new("5520", "اشتراكات برمجية وخدمات تقنية", 10_000.0000m, 0m),
                new("1305", "ضريبة القيمة المضافة المستردة", 1_500.0000m, 0m),
                new("2101", "شركة التقنية المتقدمة",         0m, 11_500.0000m),
            ]),

        new(new DateOnly(2026, 8, 12),
            "مبيعات نقدية عن الأسبوع الثاني من أغسطس",
            "Cash sales for the second week of August",
            SeedActor,
            [
                new("1101", "متحصلات نقدية",                 23_000.0000m, 0m),
                new("4101", "إيرادات المبيعات",              0m, 20_000.0000m),
                new("2131", "ضريبة القيمة المضافة — مخرجات", 0m,  3_000.0000m),
            ]),
    ];

    public static async Task RunAsync(TextWriter log)
    {
        log.WriteLine("• إنشاء قاعدة البيانات والدور إن لزم / ensuring database and role");
        await Bootstrap.EnsureDatabaseAndRoleAsync();

        log.WriteLine("• تطبيق المخطط والصلاحيات / applying schema, triggers and grants");
        await Bootstrap.ApplyDdlAsync();

        log.WriteLine($"• بذر دليل الحسابات ({ChartOfAccounts.Accounts.Length} حساباً، لكلٍّ اسم عربي وإنجليزي)");
        await ChartOfAccounts.SeedAsync();

        log.WriteLine("• تهيئة العدّاد بلا فجوات / initialising the gapless counter");
        await PostingService.EnsureBookAsync();

        foreach (var e in OpeningEntries)
        {
            var r = await PostingService.PostAsync(e);
            if (!r.Ok || r.Entry is null)
                throw new InvalidOperationException(
                    $"فشل بذر قيد افتتاحي: {r.Error?.Stage} {r.Error?.SqlState} {r.Error?.Message}");
            log.WriteLine($"  ↳ قيد رقم {r.Entry.EntryNo} (تسلسل {r.Entry.ChainSeq}) "
                        + $"بصمة {r.Entry.EntryHash[..12]}… — {r.Entry.BalanceRowsAffected} صف رصيد");
        }

        var v = await LedgerQueries.VerifyAsync();
        log.WriteLine($"• التحقق من السلسلة بعد البذر: {(v.Ok ? "سليمة" : "مكسورة")} ({v.Checked} قيوداً)");
        if (!v.Ok) throw new InvalidOperationException("السلسلة مكسورة مباشرة بعد البذر: " + v.ReasonEn);
    }
}
