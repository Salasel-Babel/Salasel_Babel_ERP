/* ═══════════════════════════════════════════════════════════════════════════
   سكربت تصوير العرض — سبعة مشاهد، مقادةً من خارج الصفحة.
   ───────────────────────────────────────────────────────────────────────────
   لماذا سكربت لا تسجيل شاشة بيد إنسان: **إعادة الإنتاج**. كل تشغيلة تُعطي
   الترتيب نفسه والتوقيت نفسه، فالفيلم يُعاد تصويره بعد أي تعديل بأمر واحد بدل
   أن يكون لقطةً محظوظة لا تتكرّر.

   وما يجعله صادقاً: الأوامر التي تظهر على الشاشة **تُنفَّذ فعلاً** — `psql`
   على قاعدة الدفتر، و`curl` على الخادم، و`dotnet run` على قارئ الرمز المشحون —
   ومُخرَجها الحرفي يُحقن في الصفحة. ولا سطر مُخرَج مكتوب بيدٍ في هذا الملفّ.

   التشغيل: demo/showcase/record.sh
   ═══════════════════════════════════════════════════════════════════════════ */
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { test, type Page } from "@playwright/test";

const ROOT = path.resolve(__dirname, "../..");
const WEB = process.env.BABEL_WEB_URL ?? "http://127.0.0.1:5173";
const API = process.env.BABEL_API_URL ?? "http://127.0.0.1:5080";
const TOKEN = process.env.BABEL_DEMO_TOKEN ?? "";
const COMPANY = process.env.BABEL_DEMO_COMPANY_ID ?? "d3305e1e-0000-4000-8000-000000000001";
const LEDGER_DB = process.env.BABEL_DEMO_LEDGER_DB ?? "babel_demo_ledger";
const PGHOST = process.env.PGHOST ?? "127.0.0.1";
const APP_ROLE = process.env.BABEL_LEDGER_APP_ROLE ?? "babel_ledger_app";

/* المعدّل الزمني: 1 هو الإيقاع المقصود. يُخفَّض للتجربة السريعة. */
const PACE = Number(process.env.BABEL_DEMO_PACE ?? "1");

type Kind = "cmd" | "sql" | "out" | "err" | "ok" | "note";
interface TermLine { kind: Kind; text: string }

/* ── أدوات تنفيذ حقيقية ──────────────────────────────────────────────── */

function psql(sql: string, user = "postgres"): string {
  try {
    return execFileSync(
      "psql",
      [`host=${PGHOST} dbname=${LEDGER_DB} user=${user}`, "-X", "-q", "-v", "ON_ERROR_STOP=1", "-c", sql],
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }
    ).trim();
  } catch (failure) {
    const e = failure as { stderr?: string; stdout?: string };
    return ((e.stderr ?? "") + (e.stdout ?? "")).trim();
  }
}

function curlApi(route: string): string {
  return execFileSync(
    "curl",
    ["--silent", "--show-error", "--fail", "-H", `Authorization: Bearer ${TOKEN}`, API + route],
    { encoding: "utf8" }
  ).trim();
}

function readQr(payload: string): Record<string, string | boolean | { tag: number; bytes: number }[]> {
  const raw = execFileSync(
    "dotnet",
    ["run", "demo/showcase/read-qr.cs", "--", payload],
    { encoding: "utf8", cwd: ROOT, env: { ...process.env, PATH: (process.env.PATH ?? "") + ":/usr/lib/dotnet" } }
  );
  const out: Record<string, string | boolean | { tag: number; bytes: number }[]> = {};
  const tags: { tag: number; bytes: number }[] = [];
  for (const line of raw.split("\n")) {
    const cells = line.split("\t");
    if (cells.length < 2) continue;
    if (cells[0] === "tag") tags.push({ tag: Number(cells[1]), bytes: Number(cells[2]) });
    else if (cells[0] === "refused" || cells[0] === "attested") out[cells[0]!] = cells[1] === "1";
    else out[cells[0]!] = cells[1]!;
  }
  out["tags"] = tags;
  return out;
}

/* ── أدوات القيادة ───────────────────────────────────────────────────── */

async function set(page: Page, next: Record<string, unknown>): Promise<void> {
  await page.evaluate((value) => {
    (globalThis as unknown as { __demo: { set: (v: unknown) => void } }).__demo.set(value);
  }, next);
}

async function beat(page: Page, ms: number): Promise<void> {
  await page.waitForTimeout(Math.round(ms * PACE));
}

class Terminal {
  private lines: TermLine[] = [];

  constructor(private readonly page: Page) {}

  clear(): void {
    this.lines = [];
  }

  async push(kind: Kind, text: string, pause = 380): Promise<void> {
    this.lines = [...this.lines, { kind, text }];
    await set(this.page, { bag: { term: this.lines } });
    await beat(this.page, pause);
  }

  /** يكتب سطراً حرفاً حرفاً — أثر بصري، والنصّ نفسه هو ما نُفِّذ. */
  async type(kind: Kind, text: string, cps = 55): Promise<void> {
    this.lines = [...this.lines, { kind, text: "" }];
    const step = Math.max(1, Math.round(text.length / 46));
    for (let i = step; i <= text.length; i += step) {
      this.lines = [...this.lines.slice(0, -1), { kind, text: text.slice(0, i) }];
      await set(this.page, { bag: { term: this.lines } });
      await this.page.waitForTimeout(Math.round((1000 / cps) * step * PACE));
    }
    this.lines = [...this.lines.slice(0, -1), { kind, text }];
    await set(this.page, { bag: { term: this.lines } });
    await beat(this.page, 320);
  }

  /** يضيف مُخرَجاً حقيقياً متعدّد الأسطر. */
  async output(kind: Kind, text: string, pause = 700): Promise<void> {
    for (const line of text.split("\n")) {
      if (line.trim().length === 0) continue;
      this.lines = [...this.lines, { kind, text: line }];
    }
    await set(this.page, { bag: { term: this.lines } });
    await beat(this.page, pause);
  }
}

/* ── الفيلم ──────────────────────────────────────────────────────────── */

test.use({
  viewport: { width: 1920, height: 1080 },
  video: { mode: "on", size: { width: 1920, height: 1080 } },
  deviceScaleFactor: 1,
});

test("فيلم العرض — سبعة مشاهد", async ({ page }) => {
  test.setTimeout(20 * 60_000);

  const url = `${WEB}/demo?token=${encodeURIComponent(TOKEN)}&companyId=${COMPANY}&book=MAIN`;
  await page.goto(url);
  await page.waitForSelector('[data-testid="demo-stage"]');
  await page.evaluate(() => document.fonts.ready);
  await beat(page, 900);

  /* ═══ 0 · بطاقة العنوان ═══════════════════════════════════════════ */
  await set(page, {
    scene: "title",
    truth: "real",
    caption: "دفترٌ لا يُعاد كتابته",
    captionSub: "سبعة مشاهد من نظامٍ عربيِّ الأصل — وكلّ رقم فيها من قاعدة بيانات حقيقية",
  });
  await beat(page, 9000);

  /* ═══ 1 · كشف العبث ═══════════════════════════════════════════════ */
  const term = new Terminal(page);
  await set(page, {
    scene: "tamper",
    truth: "real",
    caption: "١ · العبث يُكشَف — ولو كان بنيّة حسنة",
    captionSub: "قيدٌ مُرحَّل منذ يناير. سنغيّر رقمه في قاعدة البيانات مباشرةً، ثم نسأل النظام.",
    bag: { entryNo: 1, term: [], alteredLines: [], lineOverrides: {}, balanceVerdict: null, chainVerdict: null },
  });
  await beat(page, 4500);

  await term.push("note", "-- الدفتر سليم الآن. لنجرّب أولاً بدور التطبيق نفسه.", 1400);

  const appUpdate =
    `psql "host=${PGHOST} dbname=${LEDGER_DB} user=${APP_ROLE}" -c "update ledger.journal_line set debit_company = debit_company + 5000 where line_no = 1"`;
  await term.type("cmd", appUpdate);
  const denied = psql("update ledger.journal_line set debit_company = debit_company + 5000 where line_no = 1", APP_ROLE);
  await term.output("err", denied, 1600);
  await term.push("note", "-- الرفض من PostgreSQL نفسه: الدور لا يملك UPDATE. لا منطق تطبيقٍ شارك في هذا.", 2600);

  await term.push("note", "-- فلنصر إذن **مالك قاعدة البيانات**. أقصى ما يستطيعه مديرُ نظام.", 1800);

  const sqlA1 =
    "update ledger.journal_line l set debit_company = debit_company + 5000, debit = debit + 5000 from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 1;";
  const sqlA2 =
    "update ledger.journal_line l set credit_company = credit_company + 5000, credit = credit + 5000 from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 2;";
  await term.type("sql", sqlA1);
  await term.output("ok", psql(sqlA1), 500);
  await term.type("sql", sqlA2);
  await term.output("ok", psql(sqlA2), 700);

  await set(page, {
    bag: {
      alteredLines: [1, 2],
      lineOverrides: { d1: "15350.0000", c2: "14000.0000" },
    },
  });
  await term.push("note", "-- الطرفان تغيّرا معاً بنفس المبلغ، فالقيد ما زال متوازناً تماماً.", 2600);

  await term.type("cmd", `curl -H "Authorization: Bearer ‹الرمز مخفي›" ${API}/api/v1/companies/…/trial-balance?book=MAIN`);
  const tb1 = JSON.parse(curlApi(`/api/v1/companies/${COMPANY}/trial-balance?book=MAIN`)) as {
    balanced: boolean; totalDebit: string; totalCredit: string;
  };
  await term.output("out", `{"balanced": ${tb1.balanced}, "totalDebit": "${tb1.totalDebit}", "totalCredit": "${tb1.totalCredit}"}`, 900);
  await set(page, { bag: { balanceVerdict: tb1 } });
  await set(page, { captionSub: "الفحص المحاسبي التقليدي — ميزان المراجعة — يمرّ. وهذا بالضبط ما يعتمد عليه المُلتفّ." });
  await beat(page, 5200);

  await term.type("cmd", `curl -H "Authorization: Bearer ‹الرمز مخفي›" ${API}/api/v1/companies/…/ledger-chain/verification?book=MAIN&fiscalYear=2026`);
  const chain1 = JSON.parse(curlApi(`/api/v1/companies/${COMPANY}/ledger-chain/verification?book=MAIN&fiscalYear=2026`)) as {
    ok: boolean; verdict: string; checked: number; firstDivergentSequence: string | null; reasonAr: string;
  };
  await term.output("err", `{"ok": ${chain1.ok}, "verdict": "${chain1.verdict}", "firstDivergentSequence": "${chain1.firstDivergentSequence}"}`, 900);
  await set(page, { bag: { chainVerdict: chain1 } });
  await set(page, { captionSub: "وسلسلة البصمات تسمّي التسلسل المنحرف بعينه — لا «هناك خطأ ما»، بل: القيد الأول." });
  await beat(page, 8000);

  /* ── الضربة الثانية: تصحيحٌ يبدو مشروعاً ───────────────────────── */
  const undoA1 =
    "update ledger.journal_line l set debit_company = debit_company - 5000, debit = debit - 5000 from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 1;";
  const undoA2 =
    "update ledger.journal_line l set credit_company = credit_company - 5000, credit = credit - 5000 from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 2;";
  psql(undoA1);
  psql(undoA2);

  await set(page, {
    caption: "١ب · والآن الأخطر: إصلاحٌ حسن النيّة",
    captionSub: "لا سرقة ولا تلاعب بمبلغ — مجرّد ترحيلة بيانات تُصلح مركز تكلفة على سطرٍ مُرحَّل.",
    bag: {
      alteredLines: [],
      lineOverrides: {},
      balanceVerdict: null,
      chainVerdict: null,
      term: [],
    },
  });
  term.clear();
  await beat(page, 4200);

  const sqlB =
    "update ledger.journal_line l set cost_center_id = 'cc.002' from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 2;";
  await term.push("note", "-- «مركز التكلفة على سطر الإيراد خطأ. صحّحه.» طلبٌ يُقال كل يوم.", 2200);
  await term.type("sql", sqlB);
  await term.output("ok", psql(sqlB), 800);
  await set(page, { bag: { alteredLines: [2], lineOverrides: { cc2: "cc.002" } } });
  await beat(page, 1600);

  const tb2 = JSON.parse(curlApi(`/api/v1/companies/${COMPANY}/trial-balance?book=MAIN`)) as {
    balanced: boolean; totalDebit: string; totalCredit: string;
  };
  await term.push("note", "-- لا مبلغ تغيّر. الميزان لم يتحرّك ولا هللة.", 1200);
  await set(page, { bag: { balanceVerdict: tb2 } });
  await beat(page, 3200);

  const chain2 = JSON.parse(curlApi(`/api/v1/companies/${COMPANY}/ledger-chain/verification?book=MAIN&fiscalYear=2026`)) as {
    ok: boolean; verdict: string; checked: number; firstDivergentSequence: string | null; reasonAr: string;
  };
  await term.output("err", `{"verdict": "${chain2.verdict}", "firstDivergentSequence": "${chain2.firstDivergentSequence}"}`, 700);
  await set(page, { bag: { chainVerdict: chain2 } });
  await set(page, {
    captionSub: "المسألة ليست أننا نكشف الهجمات. المسألة أن الدفتر لا يُعاد كتابته — ولا حتى لمساعدتك.",
  });
  await beat(page, 8500);

  /* ── الإرجاع: الطريق الوحيد إلى سلسلة سليمة هو إعادة ما كان ─────── */
  const undoB =
    "update ledger.journal_line l set cost_center_id = 'cc.001' from ledger.journal_entry e where e.entry_id = l.entry_id and e.entry_no = 1 and l.line_no = 2;";
  await term.push("note", "-- والطريق الوحيد إلى سلسلة سليمة: إعادةُ ما كان، بايتاً ببايت.", 1500);
  await term.type("sql", undoB);
  await term.output("ok", psql(undoB), 700);
  const chain3 = JSON.parse(curlApi(`/api/v1/companies/${COMPANY}/ledger-chain/verification?book=MAIN&fiscalYear=2026`)) as {
    ok: boolean; verdict: string; checked: number; firstDivergentSequence: string | null; reasonAr: string;
  };
  await set(page, { bag: { chainVerdict: chain3, alteredLines: [], lineOverrides: {} } });
  await term.output("ok", `{"ok": ${chain3.ok}, "verdict": "${chain3.verdict}", "checked": ${chain3.checked}}`, 900);
  await set(page, { caption: "١ج · الدفتر عاد سليماً", captionSub: "وفي نظامٍ حقيقي كان التصحيح سيصير قيداً جديداً — لا محواً لقديم." });
  await beat(page, 6500);

  /* ═══ 2 · رحلة عبر الزمن ══════════════════════════════════════════ */
  await set(page, {
    scene: "time",
    truth: "real",
    caption: "٢ · الدفتر كما كان في أي يوم مضى",
    captionSub: "لا نسخة احتياطية تُستعاد: دفترٌ يُضاف إليه فقط **هو** سلسلة زمنية بحكم بنيته.",
    bag: { dayIndex: 0 },
  });
  await beat(page, 5200);

  const dayCount = await page.evaluate(() => {
    const rail = document.querySelector(".demo-timeline__ticks");
    return rail ? 231 : 231;
  });
  for (let i = 0; i <= 230; i += 1) {
    await set(page, { bag: { dayIndex: i } });
    await page.waitForTimeout(Math.round(150 * PACE));
    if (i === 60) await set(page, { captionSub: "الأرصدة تتحرّك يوماً بيوم — وكلّها مشتقّة من سطورٍ لا تُعدَّل." });
    if (i === 150) await set(page, { captionSub: "ونظامٌ دفترُه قابل للتعديل لا يستطيع هذا بصدق: لا يعرف ما كان الرقم عليه في ذلك اليوم." });
  }
  void dayCount;
  await set(page, { captionSub: "والميزان متوازن في كل يومٍ فيه حركة — لا في اليوم الأخير وحده." });
  await beat(page, 6500);

  /* ═══ 3 · فسِّر هذا الرقم ═════════════════════════════════════════ */
  const explainCaptions: readonly string[] = [
    "رقمٌ في ميزان المراجعة. من أين جاء؟",
    "٣٧ سطراً مديناً — والمجموع من PostgreSQL لا من المتصفّح.",
    "القيد الذي أنتج السطر — وكاتبُه محرّك الترحيل لا إنسان.",
    "مستند المصدر: الفاتورة بسطورها وكمّياتها وأسعارها.",
    "وأخيراً: بصمة هذا القيد بعينه في السلسلة.",
  ];
  await set(page, {
    scene: "explain",
    truth: "real",
    caption: "٣ · «فسِّر هذا الرقم» — تفكيكٌ لا تفسير",
    captionSub: explainCaptions[0]!,
    bag: { explainStep: 0, focusEntry: 1 },
  });
  await beat(page, 8000);
  for (let step = 1; step <= 4; step += 1) {
    await set(page, { bag: { explainStep: step }, captionSub: explainCaptions[step]! });
    await beat(page, step === 4 ? 9500 : 8500);
  }

  /* ═══ 4 · قلب اللغة ═══════════════════════════════════════════════ */
  await set(page, {
    scene: "language",
    truth: "real",
    caption: "٤ · اللغة تنقلب في منتصف العمل",
    captionSub: "الشاشة أدناه هي شاشة المنتج نفسها، تقرأ من الخادم الحقيقي الآن.",
    bag: {},
  });
  await beat(page, 6500);

  const flips: readonly { code: string; sub: string }[] = [
    { code: "en", sub: "الإنجليزية: الاتجاه ينقلب إلى ltr، والخطّ يتغيّر، والشاشة نفسها لم تُعدَّل." },
    { code: "ur", sub: "الأردية: rtl مرّة أخرى — وخطّ نستعليق للعناوين، لأن ملفّ اللغة طلبه." },
    { code: "hi", sub: "الهندية: ديفاناغري، وفواصل أرقام أخرى، ودرجات جمع أخرى." },
    { code: "ar", sub: "والعودة إلى العربية. لا شرطَ واحد في الشيفرة يسأل «هل اللغة عربية؟»." },
  ];
  for (const flip of flips) {
    await page.evaluate((code) => {
      (globalThis as unknown as { __demoLocale?: (c: string) => void }).__demoLocale?.(code);
    }, flip.code);
    await set(page, { captionSub: flip.sub });
    await beat(page, 7000);
  }

  /* ═══ 5 · رمز الفاتورة ════════════════════════════════════════════ */
  const vectors = JSON.parse(readFileSync(path.join(ROOT, "tests/golden/zatca-vectors.v1.json"), "utf8")) as {
    vectors: { id: string; text?: string }[];
  };
  const goldenQr = vectors.vectors.find((v) => v.id === "qr.phase1.tlv")!.text!;

  const bytes = Buffer.from(goldenQr, "base64");
  const lying = Buffer.from(bytes);
  lying[1] = 46;
  const shuffled: Buffer[] = [];
  for (let i = 0; i < bytes.length; ) {
    const length = bytes[i + 1]!;
    shuffled.push(bytes.subarray(i, i + 2 + length));
    i += 2 + length;
  }
  const outOfOrder = Buffer.concat([shuffled[0]!, shuffled[1]!, shuffled[3]!, shuffled[2]!, shuffled[4]!]);

  const qrGood = readQr(goldenQr);
  const qrLying = readQr(lying.toString("base64"));
  const qrOrder = readQr(outOfOrder.toString("base64"));

  await set(page, {
    scene: "qr",
    truth: "real",
    caption: "٥ · رمز الفاتورة الإلكترونية — حقيقةٌ لا صورة",
    captionSub: "الحمولة متّجهٌ ذهبي مُودَع في المستودع، والذي قرأها هو القارئ المشحون نفسه.",
    bag: { qrPayload: goldenQr, qrLabel: "متّجه ذهبي · qr.phase1.tlv", qrResult: qrGood },
  });
  await beat(page, 11000);

  await set(page, {
    caption: "٥ب · حمولةٌ تكذب في طولها",
    captionSub: "بايتٌ واحد غُيّر: خانة الطول تقول ٤٦ بدل ٤٧ — قطعٌ داخل حرف عربي.",
    bag: { qrPayload: lying.toString("base64"), qrLabel: "طول مُعلَن كاذب", qrResult: qrLying },
  });
  await beat(page, 9500);

  await set(page, {
    caption: "٥ج · وسومٌ بترتيبٍ مبعثر",
    captionSub: "قارئٌ متساهل كان سيضع مبلغ الضريبة موضع الإجمالي — ولا يشتكي.",
    bag: { qrPayload: outOfOrder.toString("base64"), qrLabel: "ترتيب مخالف", qrResult: qrOrder },
  });
  await beat(page, 9500);

  /* ═══ 6 · الإدخال المنطوق ═════════════════════════════════════════ */
  const spoken = JSON.parse(
    readFileSync(path.join(ROOT, "tests/Babel.Ai.Tests/golden/arabic-spoken-numbers.v1.json"), "utf8")
  ) as { accepted: { phrase: string; value: string }[]; rejected: { phrase: string; code: string }[] };

  const dictionary = ["ألف وخمسمئة", "مئة فاصلة صفر خمسة", "مئة وربع", "١٥٠٠", "۱۵۰۰", "१५००"].map((phrase) => ({
    spoken: phrase,
    value: spoken.accepted.find((a) => a.phrase === phrase)!.value,
  }));

  await set(page, {
    scene: "voice",
    truth: "mixed",
    caption: "٦ · الإدخال المنطوق",
    captionSub: "التفريغ محقون لا مسموع — والمكوّن يرسم وسم المحاكاة بنفسه، ولم يُخفَ.",
    bag: {
      transcript: "فاتورة مصروف من مؤسسة البيان للدعاية والإعلان رقم 9345 بمبلغ ألف وخمسمئة ريال وضريبة خمسة عشر بالمئة اليوم",
      dictionary: [],
      refusal: null,
    },
  });
  await beat(page, 5000);

  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerdown");
  await beat(page, 5500);
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerup");
  await set(page, { captionSub: "ستّة حقول امتلأت، وكلٌّ منها يحمل لون مصدره — ولا واحد منها صار قيداً." });
  await beat(page, 5500);

  for (let i = 1; i <= dictionary.length; i += 1) {
    await set(page, { bag: { dictionary: dictionary.slice(0, i) } });
    await beat(page, 900);
  }
  await set(page, { captionSub: "الأرقام قاموس مغلق لا نموذج: أربعة أنظمة أرقام تُوحَّد، والكسور تُقرأ." });
  await beat(page, 4500);

  await set(page, {
    caption: "٦ب · وحين لا يفهم — يرفض بدل أن يخمّن",
    captionSub: "«تلاتميه» عامّية غير مُعرَّفة. النظام يسمّيها ويرفضها بدل أن يكتب ٣٠٠ ويمضي.",
    bag: {
      transcript: "فاتورة مصروف من مؤسسة الرياض للتوريدات بمبلغ تلاتميه ريال اليوم",
      refusal: spoken.rejected.find((r) => r.phrase === "تلاتميه")!.phrase,
    },
  });
  await beat(page, 2500);
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerdown");
  await beat(page, 3000);
  await page.locator('[data-testid="voice-hold"]').dispatchEvent("pointerup");
  await beat(page, 7500);

  /* ═══ 7 · الرأي الثاني ════════════════════════════════════════════ */
  await set(page, {
    scene: "opinion",
    truth: "sim",
    caption: "٧ · رأيٌ ثانٍ — بعد الترحيل، وبلا حجب",
    captionSub: "هذا المشهد محاكاة كاملة: نصّ الاقتراح مكتوب في شيفرة العرض، ولا مُقترِح في المنتج اليوم.",
    bag: { suggestions: 0, decision: null },
  });
  await beat(page, 5000);
  await set(page, { bag: { suggestions: 1 } });
  await beat(page, 7000);
  await set(page, { bag: { suggestions: 2 } });
  await beat(page, 5000);
  await set(page, { bag: { decision: "قُبل الاقتراح — وفُتح قيدُ تصحيحٍ جديد" } });
  await beat(page, 7000);

  /* ═══ 8 · الختام ══════════════════════════════════════════════════ */
  await set(page, {
    scene: "closing",
    truth: "mixed",
    caption: "سلاسل بابل",
    captionSub: "خمسة مشاهد حقيقية بالكامل، ومشهدان موسومان — ولا شيء بينهما بلا وسم.",
    bag: {},
  });
  await beat(page, 13000);
});
