using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// (ج) انضباط التوسيع/الانكماش.
///
/// <para>المصفوفة المطلوبة أثناء الطرح: <b>إصداران من الشيفرة × إصداران من
/// المخطط</b>. الخلية الحرجة هي «الشيفرة الجديدة على مستأجر لم يُرقَّ بعد» —
/// وهي التي تُنسى دائماً، ولا تظهر في التطوير لأن قاعدة المطوّر مُرقّاة.</para>
/// </summary>
public static class ProofC_ExpandContract
{
    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        Recorder.Section("(ج) التوسيع/الانكماش — إصداران من الشيفرة × ثلاثة من المخطط");

        var registry = new TenantRegistry(o);

        // مستأجران: أحدهما على الإصدار السابق (2)، والآخر مُوسَّع (3).
        var old = (await Harness.SimulateFleetAsync(o, registry, "xcold", 1,
            TenantSchema.FleetDemoVersion))[0];
        var expanded = (await Harness.SimulateFleetAsync(o, registry, "xcnew", 1,
            TenantSchema.ExpandVersion))[0];

        await using var oldDb = await Db.OpenAsync(o.TenantOwnerConnectionString(old.DatabaseName));
        await using var newDb = await Db.OpenAsync(o.TenantOwnerConnectionString(expanded.DatabaseName));

        foreach (var db in new[] { oldDb, newDb })
        {
            await Babel.ControlPlane.Provisioning.SeedData.SeedChartOfAccountsAsync(db);
            await Babel.ControlPlane.Provisioning.SeedData.SeedPeriodsAsync(db, 2026);
        }

        var oldProbe = await ExpandContract.ProbeAsync(oldDb);
        var newProbe = await ExpandContract.ProbeAsync(newDb);
        rec.Check("C1", "المخططان مختلفان فعلاً: (قديم) عمود واحد، (مُوسَّع) عمودان",
            oldProbe is { HasLegacy: true, HasNew: false }
            && newProbe is { HasLegacy: true, HasNew: true },
            $"v{TenantSchema.FleetDemoVersion}: description_ar={oldProbe.HasLegacy} memo_ar={oldProbe.HasNew}\n"
            + $"v{TenantSchema.ExpandVersion}: description_ar={newProbe.HasLegacy} memo_ar={newProbe.HasNew}");

        // ---- الخلايا الأربع --------------------------------------------------
        var cells = new List<(string Cell, bool Ok, string Detail)>();

        // (شيفرة قديمة × مخطط قديم)
        var id1 = Guid.CreateVersion7();
        await ExpandContract.WriteWithOldCodeAsync(oldDb, id1, "MAIN", 101, "2026-01",
            new DateOnly(2026, 1, 10), "AR", "قيد من الإصدار السابق");
        var r1 = await ExpandContract.ReadWithOldCodeAsync(oldDb, id1);
        cells.Add(("شيفرة قديمة × مخطط v2", r1 == "قيد من الإصدار السابق", $"قرأت: {r1}"));

        // (شيفرة قديمة × مخطط مُوسَّع) — المُشغّل يُظهر القيمة في العمود الجديد
        var id2 = Guid.CreateVersion7();
        await ExpandContract.WriteWithOldCodeAsync(newDb, id2, "MAIN", 102, "2026-01",
            new DateOnly(2026, 1, 11), "AR", "قيد قديم على مخطط مُوسَّع");
        var both2 = await ExpandContract.ReadBothAsync(newDb, id2);
        cells.Add(("شيفرة قديمة × مخطط v3",
            both2.Legacy == "قيد قديم على مخطط مُوسَّع" && both2.Fresh == both2.Legacy,
            $"description_ar={both2.Legacy}  memo_ar={both2.Fresh} ← المُشغّل ملأ العمود الجديد"));

        // (شيفرة جديدة × مخطط قديم) — الارتداد
        var id3 = Guid.CreateVersion7();
        await ExpandContract.WriteWithNewCodeAsync(oldDb, id3, "MAIN", 103, "2026-01",
            new DateOnly(2026, 1, 12), "AR", "قيد جديد على مستأجر لم يُرقَّ");
        var r3 = await ExpandContract.ReadWithNewCodeAsync(oldDb, id3);
        cells.Add(("شيفرة جديدة × مخطط v2", r3 == "قيد جديد على مستأجر لم يُرقَّ",
            $"ارتدّت إلى description_ar وقرأت: {r3}"));

        // (شيفرة جديدة × مخطط مُوسَّع)
        var id4 = Guid.CreateVersion7();
        await ExpandContract.WriteWithNewCodeAsync(newDb, id4, "MAIN", 104, "2026-01",
            new DateOnly(2026, 1, 13), "AR", "قيد جديد على مخطط مُوسَّع");
        var both4 = await ExpandContract.ReadBothAsync(newDb, id4);
        cells.Add(("شيفرة جديدة × مخطط v3",
            both4.Fresh == "قيد جديد على مخطط مُوسَّع" && both4.Legacy == both4.Fresh,
            $"memo_ar={both4.Fresh}  description_ar={both4.Legacy} ← المُشغّل ملأ العمود القديم"));

        rec.Check("C2", "الخلايا الأربع كلها تعمل أثناء الطرح",
            cells.All(x => x.Ok),
            string.Join("\n", cells.Select(x => $"  [{(x.Ok ? "ok" : "FAIL")}] {x.Cell}: {x.Detail}")));

        // ---- الملء الرجعي لم يفقد شيئاً --------------------------------------
        var backfilled = await Db.ScalarAsync<long>(newDb,
            "select count(*) from ledger.journal_entry where memo_ar is not null and description_ar is not null");
        var total = await Db.ScalarAsync<long>(newDb, "select count(*) from ledger.journal_entry");
        rec.Check("C3", "العمودان متطابقان على كل الصفوف أثناء التوسيع",
            backfilled == total && total > 0, $"{backfilled}/{total} صفّاً يحمل القيمتين");

        // ---- الانكماش: ما يحدث حين يُحذف العمود القديم قبل ترقية كل النُسخ ----
        await TenantSchema.MigrateToAsync(newDb, TenantSchema.ContractVersion);
        var afterContract = await ExpandContract.ProbeAsync(newDb);

        var oldCodeBroke = false;
        var breakage = "";
        try
        {
            await ExpandContract.WriteWithOldCodeAsync(newDb, Guid.CreateVersion7(), "MAIN", 105,
                "2026-01", new DateOnly(2026, 1, 14), "AR", "قيد قديم بعد الانكماش");
        }
        catch (PostgresException ex)
        {
            oldCodeBroke = true;
            breakage = $"SQLSTATE {ex.SqlState}: {ex.MessageText}";
        }

        var newCodeStillWorks = false;
        var id6 = Guid.CreateVersion7();
        await ExpandContract.WriteWithNewCodeAsync(newDb, id6, "MAIN", 106, "2026-01",
            new DateOnly(2026, 1, 15), "AR", "قيد جديد بعد الانكماش");
        newCodeStillWorks = await ExpandContract.ReadWithNewCodeAsync(newDb, id6)
            == "قيد جديد بعد الانكماش";

        rec.Check("C4", "بعد الانكماش: الشيفرة القديمة تفشل بصوت عالٍ، والجديدة تعمل",
            afterContract is { HasLegacy: false, HasNew: true } && oldCodeBroke && newCodeStillWorks,
            $"الشيفرة القديمة ⇒ {breakage}\nالشيفرة الجديدة ⇒ تعمل\n"
            + "⇒ الانكماش لا يُطلَق إلا بعد ترقية **كل** نُسخ التطبيق. "
            + "هذا هو سبب وجود مرحلة ثالثة، لا مرحلتين.");

        // ---- القراءة التاريخية بقيت سليمة -----------------------------------
        var survived = await ExpandContract.ReadWithNewCodeAsync(newDb, id2);
        rec.Check("C5", "الصفوف المكتوبة بالشيفرة القديمة قبل الانكماش ما تزال مقروءة بعده",
            survived == "قيد قديم على مخطط مُوسَّع", $"قرأت من memo_ar: {survived}");
    }
}
