/* لقطات ثابتة لكل مشهد — أداة مراجعة بصرية أثناء بناء الفيلم، لا اختبار.
   تُشغَّل بنفس إعداد التصوير: npx playwright test --config=../demo/showcase/film.config.ts demo-shots */
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { test, type Page } from "@playwright/test";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const WEB = process.env.BABEL_WEB_URL ?? "http://127.0.0.1:5173";
const TOKEN = process.env.BABEL_DEMO_TOKEN ?? "";
const COMPANY = process.env.BABEL_DEMO_COMPANY_ID ?? "d3305e1e-0000-4000-8000-000000000001";
const OUT = path.join(ROOT, "demo/showcase/out/shots");

/* ── ليس حارس بوّابة ─────────────────────────────────────────────────────
   هذا الملفّ **أداة تصوير** لا فحصاً: يحتاج خادماً حيّاً على 5080، وقاعدة
   `babel_demo_ledger` مبذورة، واعتماداً — ويُنفّذ `psql` يعدّل الدفتر عمداً
   (مشهد كشف العبث). ويُشغَّل بـ`demo/showcase/record.sh` وحده، وهو الذي
   يُصدِّر `BABEL_DEMO_TOKEN`.

   ‏**ولماذا تخطٍّ مُعلَن لا `testIgnore` صامت:** فخ-80 عطلُه أن ما لم يُنفَّذ
   لم يقل أحدٌ إنه لم يُنفَّذ. والتخطّي هنا **يُطبع بسببه** في تقرير التشغيل،
   فيراه من يقرأ. وإخفاؤه من الإعداد كان سيعيد العطل نفسه بصيغة أهدأ.

   A recording tool, not a gate check — skipped with a printed reason. */
test.skip(
  !process.env.BABEL_DEMO_TOKEN,
  "أداة تصوير لا فحص: تحتاج BABEL_DEMO_TOKEN وخادماً وقاعدةً مبذورة — تُشغَّل بـdemo/showcase/record.sh · " +
    "recording tool, not a gate check: needs BABEL_DEMO_TOKEN, a live API and a seeded database; run via demo/showcase/record.sh"
);

async function set(page: Page, next: Record<string, unknown>): Promise<void> {
  await page.evaluate((value) => {
    (globalThis as unknown as { __demo: { set: (v: unknown) => void } }).__demo.set(value);
  }, next);
}

test.use({ viewport: { width: 1920, height: 1080 } });

test("لقطات المشاهد", async ({ page }) => {
  test.setTimeout(300_000);
  await page.goto(`${WEB}/demo?lang=ar&token=${encodeURIComponent(TOKEN)}&companyId=${COMPANY}&book=MAIN`);
  await page.waitForSelector('[data-testid="demo-stage"]');
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(1200);

  const shot = async (name: string) => {
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT, name + ".png") });
  };

  await set(page, { scene: "title", truth: "real", caption: "دفترٌ لا يُعاد كتابته", captionSub: "سبعة مشاهد من نظامٍ عربيِّ الأصل" });
  await shot("0-title");

  await set(page, {
    scene: "tamper", truth: "real",
    caption: "١ · العبث يُكشَف — ولو كان بنيّة حسنة",
    captionSub: "قيدٌ مُرحَّل منذ يناير. سنغيّر رقمه في قاعدة البيانات مباشرةً.",
    bag: {
      entryNo: 1,
      term: [
        { kind: "note", text: "-- الدفتر سليم الآن." },
        { kind: "cmd", text: 'psql "host=127.0.0.1 dbname=babel_demo_ledger user=babel_ledger_app" -c "update ledger.journal_line ..."' },
        { kind: "err", text: "ERROR:  permission denied for table journal_line" },
        { kind: "sql", text: "update ledger.journal_line l set debit_company = debit_company + 5000 ..." },
        { kind: "ok", text: "UPDATE 1" },
      ],
      alteredLines: [1, 2],
      lineOverrides: { d1: "15350.0000", c2: "14000.0000" },
      balanceVerdict: { balanced: true, totalDebit: "2227091.0500", totalCredit: "2227091.0500" },
      chainVerdict: { ok: false, verdict: "CHAIN-CONTENT-TAMPERED", checked: 1, firstDivergentSequence: "1", reasonAr: "بصمة المحتوى المُعاد حسابها لا تطابق البصمة المخزَّنة." },
    },
  });
  await shot("1-tamper");

  await set(page, { scene: "time", truth: "real", caption: "٢ · الدفتر كما كان في أي يوم مضى", captionSub: "دفترٌ يُضاف إليه فقط هو سلسلة زمنية.", bag: { dayIndex: 140 } });
  await shot("2-time");

  for (let step = 0; step <= 4; step += 1) {
    await set(page, { scene: "explain", truth: "real", caption: "٣ · «فسِّر هذا الرقم»", captionSub: "تفكيكٌ لا تفسير", bag: { explainStep: step, focusEntry: 1 } });
    await shot("3-explain-" + step);
  }

  await set(page, { scene: "language", truth: "real", caption: "٤ · اللغة تنقلب في منتصف العمل", captionSub: "الشاشة أدناه هي شاشة المنتج نفسها.", bag: {} });
  await page.waitForTimeout(2500);
  await shot("4-lang-ar");
  for (const code of ["en", "ur", "hi"]) {
    await page.evaluate((c) => (globalThis as unknown as { __demoLocale?: (x: string) => void }).__demoLocale?.(c), code);
    await page.waitForTimeout(1500);
    await shot("4-lang-" + code);
  }
  await page.evaluate(() => (globalThis as unknown as { __demoLocale?: (x: string) => void }).__demoLocale?.("ar"));
  await page.waitForTimeout(1200);

  const vectors = JSON.parse(readFileSync(path.join(ROOT, "tests/golden/zatca-vectors.v1.json"), "utf8")) as { vectors: { id: string; text?: string }[] };
  const golden = vectors.vectors.find((v) => v.id === "qr.phase1.tlv")!.text!;
  const raw = execFileSync("dotnet", ["run", "demo/showcase/read-qr.cs", "--", golden], {
    encoding: "utf8", cwd: ROOT, env: { ...process.env, PATH: (process.env.PATH ?? "") + ":/usr/lib/dotnet" },
  });
  const parsed: Record<string, unknown> = {};
  const tags: { tag: number; bytes: number }[] = [];
  for (const line of raw.split("\n")) {
    const c = line.split("\t");
    if (c.length < 2) continue;
    if (c[0] === "tag") tags.push({ tag: Number(c[1]), bytes: Number(c[2]) });
    else if (c[0] === "refused" || c[0] === "attested") parsed[c[0]] = c[1] === "1";
    else parsed[c[0]] = c[1];
  }
  parsed["tags"] = tags;
  await set(page, { scene: "qr", truth: "real", caption: "٥ · رمز الفاتورة الإلكترونية", captionSub: "متّجه ذهبي مُودَع، قرأه القارئ المشحون.", bag: { qrPayload: golden, qrLabel: "متّجه ذهبي · qr.phase1.tlv", qrResult: parsed } });
  await shot("5-qr");

  await set(page, {
    scene: "voice", truth: "mixed", caption: "٦ · الإدخال المنطوق", captionSub: "التفريغ محقون لا مسموع.",
    bag: {
      transcript: "فاتورة مصروف من مؤسسة البيان للدعاية والإعلان رقم 9345 بمبلغ ألف وخمسمئة ريال وضريبة خمسة عشر بالمئة اليوم",
      dictionary: [{ spoken: "ألف وخمسمئة", value: "1500" }, { spoken: "مئة فاصلة صفر خمسة", value: "100.05" }],
      refusal: null,
    },
  });
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerdown");
  await page.waitForTimeout(1200);
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerup");
  await shot("6-voice");

  await set(page, {
    scene: "voice", truth: "mixed", caption: "٦ب · يرفض بدل أن يخمّن", captionSub: "«تلاتميه» عامّية غير مُعرَّفة.",
    bag: { transcript: "فاتورة مصروف من مؤسسة الرياض للتوريدات بمبلغ تلاتميه ريال اليوم", refusal: "تلاتميه" },
  });
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerdown");
  await page.waitForTimeout(1200);
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerup");
  await shot("6-voice-refuse");

  await set(page, { scene: "opinion", truth: "sim", caption: "٧ · رأيٌ ثانٍ", captionSub: "محاكاة كاملة وموسومة.", bag: { suggestions: 2, decision: "قُبل الاقتراح — وفُتح قيدُ تصحيحٍ جديد" } });
  await shot("7-opinion");

  await set(page, { scene: "closing", truth: "mixed", caption: "سلاسل بابل", captionSub: "خمسة مشاهد حقيقية، ومشهدان موسومان.", bag: {} });
  await shot("8-closing");
});
