/*
 * ‏**الحصيلة — تُقرأ من مُخرَج التشغيل لا من نصّ الأمر.**
 *
 * الحارس الذي سبق هذا كان قائمةَ خياراتٍ يرفضها مُشغّل الاختبارات، وهُزم مرّتين في
 * يوم واحد: بنقل `--nologo` إلى سطر استمرارٍ داخل `run: |` (فلم يعد السطر يحوي
 * ‏`dotnet test`)، وبـ`--diag` الذي يُنتج فشلاً مطابقاً وليس في القائمة. وقائمةُ
 * الخيارات **مفتوحة ولا تُحصى**: كل خيارٍ جديد وكل مطبعةٍ وكل ترقية حزمةِ تطوير
 * تضيف بنداً لم يخطر لأحد.
 *
 * ‏**والنتيجة مغلقة ومفردة:** إن لم يُنفَّذ اختبار، فلا تقرير — أو تقريرٌ فيه صفر.
 * ‏فيُسأل عن ذلك مباشرةً. قِيس على هذه الآلة أن `--nologo` و`--diag` كليهما
 * ‏**لا يُنشئان مجلّد التقارير أصلاً**، وأن التشغيل النظيف يكتب
 * ‏`Counters total="40" executed="40"`. فالسؤال «هل وُجد التقرير وكم نُفِّذ فيه»
 * يميّز الحالتين بلا أن يعرف اسم خيارٍ واحد.
 *
 * وهذا الملفّ يفشل في الاتجاهين:
 *   · سطحٌ مُعلَن بلا تقرير، أو تقريرٌ بصفر منفَّذ، أو دون الأرضية، أو فيه إخفاق.
 *   · تقريرٌ موجود لا يدّعيه أي سطح في السجلّ — أي مشروع اختبارٍ دخل بلا إعلان.
 *   · تقريرٌ **أقدم من ختم بدء التشغيل** — فبقايا تشغيلٍ سابق لا تُثبت شيئاً.
 *   · تقريرٌ لا يُقرأ — الغموض سقوطٌ لا صفر (فخ-43: محلّلٌ يُعيد صفراً يُرضي كل تأكيد).
 */

import { readFileSync, readdirSync, existsSync, statSync, appendFileSync } from "node:fs";
import { join, resolve, basename, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const manifestPath = join(root, "tests", "test-surfaces.json");

/** المُشغّلات المعروفة — مجموعة مغلقة نملكها. سطحٌ بمُشغّل خارجها يسقط. */
const KNOWN_RUNNERS = new Set(["dotnet", "vitest", "playwright"]);

const STAMP = ".tally-begin";

function die(lines) {
  for (const line of lines) process.stderr.write(line + "\n");
  process.exit(1);
}

// ── السجلّ ────────────────────────────────────────────────────────────────────
if (!existsSync(manifestPath)) {
  die([`✗ سجلّ الأسطح مفقود: tests/test-surfaces.json · the test-surface manifest is missing.`]);
}

let manifest;
try {
  manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
} catch (error) {
  die([`✗ سجلّ الأسطح لا يُقرأ: ${error.message} · the manifest does not parse.`]);
}

const surfaces = manifest.surfaces ?? [];
const jobs = manifest.jobs ?? [];
if (surfaces.length === 0) die(["✗ سجلّ الأسطح فارغ — لا شيء يُحصى، والفراغ ليس خُضرة."]);

const byId = new Map();
for (const surface of surfaces) {
  if (byId.has(surface.id)) die([`✗ معرّف سطحٍ مكرّر في السجلّ: ${surface.id}`]);
  if (!KNOWN_RUNNERS.has(surface.runner)) {
    die([
      `✗ السطح «${surface.id}» يُعلن مُشغّلاً لا يعرفه هذا الملفّ: «${surface.runner}».`,
      `  المعروف: ${[...KNOWN_RUNNERS].join("، ")}. أضِف قراءة تقريره هنا — لا تتركه بلا إحصاء.`,
      `  · Unknown runner: this file cannot read its report, so it refuses rather than pass it silently.`,
    ]);
  }
  byId.set(surface.id, surface);
}

const reportDirectory = join(root, manifest.reportDirectory ?? "artifacts/test-reports");

// ── الوسائط ───────────────────────────────────────────────────────────────────
const argv = process.argv.slice(2);
const requestedJobs = [];
const requestedSurfaces = [];
let mode = "verify";

for (let i = 0; i < argv.length; i += 1) {
  const arg = argv[i];
  if (arg === "--begin") mode = "begin";
  else if (arg === "--job") requestedJobs.push(argv[++i] ?? "");
  else if (arg === "--surface") requestedSurfaces.push(argv[++i] ?? "");
  else die([`✗ وسيط غير معروف: ${arg} · unknown argument.`]);
}

// ── وضع البدء: يُفرَّغ المجلّد ويُختم ─────────────────────────────────────────
// ولماذا الختم: تقريرٌ من تشغيلٍ سابق (أو مُودَع في المستودع) يُرضي كل تأكيدٍ أدناه
// بلا أن يكون شيء قد نُفِّذ. فالتقرير الذي لا يثبت أنه من **هذا** التشغيل لا يُقبل.
// ونسيانُ هذه الخطوة **يُحمِّر** ولا يُخضِّر: الختم المفقود سقوطٌ صريح أدناه.
if (mode === "begin") {
  const { rmSync, mkdirSync, writeFileSync } = await import("node:fs");
  rmSync(reportDirectory, { recursive: true, force: true });
  mkdirSync(reportDirectory, { recursive: true });
  writeFileSync(join(reportDirectory, STAMP), new Date().toISOString() + "\n", "utf8");
  process.stdout.write(`· مجلّد التقارير فُرِّغ وخُتم: ${manifest.reportDirectory}\n`);
  process.exit(0);
}

// ── ما الذي يُحصى في هذا النداء ───────────────────────────────────────────────
const selected = new Map();
for (const name of requestedJobs) {
  const [workflow, job] = name.split(":");
  const entry = jobs.find(
    (candidate) => basename(candidate.workflow ?? "") === workflow && candidate.job === job
  );
  if (!entry) {
    die([
      `✗ لا وظيفة اسمها «${name}» في سجلّ الأسطح.`,
      `  المُعلَن: ${jobs.map((j) => basename(j.workflow) + ":" + j.job).join("، ")}`,
      `  · No such job in the manifest; the tally refuses to guess what it should check.`,
    ]);
  }
  if (entry.classification !== "tallied") {
    die([
      `✗ الوظيفة «${name}» مصنّفة «${entry.classification}» لا «tallied»، فلا أسطح تُحصى لها.`,
      `  · The job is not classified as tallied; a tally step here means the manifest and the workflow disagree.`,
    ]);
  }
  for (const id of entry.surfaces ?? []) {
    if (!byId.has(id)) die([`✗ الوظيفة «${name}» تدّعي سطحاً لا وجود له: ${id}`]);
    selected.set(id, byId.get(id));
  }
}
for (const id of requestedSurfaces) {
  if (!byId.has(id)) die([`✗ لا سطح بهذا المعرّف: ${id}`]);
  selected.set(id, byId.get(id));
}

if (selected.size === 0) {
  die([
    "✗ لم يُطلب إحصاء أي سطح — والإحصاء الفارغ يمرّ دائماً ولا يعني شيئاً (فخ-43).",
    "  استعمل --job <ملفّ>:<وظيفة> أو --surface <معرّف>.",
    "  · An empty tally passes vacuously; refuse instead.",
  ]);
}

// ── الختم ─────────────────────────────────────────────────────────────────────
const stampPath = join(reportDirectory, STAMP);
if (!existsSync(stampPath)) {
  die([
    `✗ لا ختم بدء في ${manifest.reportDirectory} — فلا شيء يُثبت أن التقارير من هذا التشغيل.`,
    `  شغّل «tools/test-tally/run.sh --begin» قبل خطوات الاختبار. · No begin stamp: reports`,
    `  present here cannot be shown to come from this run, so they prove nothing.`,
  ]);
}
const stampedAt = statSync(stampPath).mtimeMs;

// ── قراءة التقارير ────────────────────────────────────────────────────────────
function walk(directory) {
  const found = [];
  if (!existsSync(directory)) return found;
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) found.push(...walk(path));
    else found.push(path);
  }
  return found;
}

const files = walk(reportDirectory).filter((path) => basename(path) !== STAMP);

/** ‏TRX: العدّادات سطرٌ واحد، والتجميعة تُقرأ من `storage`. والغموض سقوط لا صفر. */
function readTrx(path) {
  const text = readFileSync(path, "utf8");
  const counters = /<Counters\b[^>]*\bexecuted="(\d+)"[^>]*\bfailed="(\d+)"[^>]*\berror="(\d+)"[^>]*>/.exec(text);
  const storage = /\bstorage="([^"]+)"/.exec(text);
  if (!counters) {
    die([
      `✗ تقريرٌ لا يُقرأ: ${path}`,
      `  لا ‏Counters فيه — ربّما تغيّر شكل TRX. والغموض **سقوط** لا صفر: محلّلٌ يُعيد`,
      `  صفراً يُرضي كل تأكيد ويُخفي كل شيء (فخ-43). · Unreadable report: the parser refuses`,
      `  to return zero, because a silent zero satisfies every assertion below.`,
    ]);
  }
  const executed = Number(counters[1]);

  // ‏**تقريرُ الصفر لا يذكر تجميعته.** مقيس: `--filter-method` لا يطابق شيئاً فيُكتب
  // ‏TRX فيه `total="0" executed="0"` و**بلا `storage` إطلاقاً** — لأن `storage` تأتي
  // من تعريفات الاختبارات، ولا تعريف. فلا يُنسب هذا التقرير إلى سطح، ويُبلَّغ عنه
  // بوصفه ما هو: إثباتٌ مكتوبٌ بأن صفراً نُفِّذ.
  if (!storage) {
    if (executed === 0) return { assembly: null, executed: 0, failed: 0, path };
    die([
      `✗ تقريرٌ يذكر ${executed} اختباراً منفَّذاً ولا يذكر تجميعتَه: ${path}`,
      `  · A report claiming executed tests but naming no assembly: it cannot be attributed.`,
    ]);
  }

  return {
    assembly: basename(storage[1].replace(/\\/g, "/")),
    executed,
    failed: Number(counters[2]) + Number(counters[3]),
    path,
  };
}

function readJson(path) {
  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    die([`✗ تقريرٌ لا يُقرأ: ${path} — ${error.message} · unreadable report.`]);
  }
}

const problems = [];
const rows = [];

// كل تقريرٍ موجود، منسوباً إلى ما يدّعيه — لفحص الاتجاه الثاني.
const trxByAssembly = new Map();
const jsonReports = new Map();
for (const path of files) {
  if (statSync(path).mtimeMs + 1 < stampedAt) {
    problems.push(
      `تقريرٌ أقدم من ختم بدء التشغيل: ${path.slice(root.length + 1)} — بقيّةُ تشغيلٍ سابق ` +
        `لا تُثبت أن شيئاً نُفِّذ الآن. · A report older than the begin stamp proves nothing about this run.`
    );
    continue;
  }
  if (path.endsWith(".trx")) {
    const report = readTrx(path);
    if (report.assembly === null) {
      problems.push(
        `تقريرٌ يشهد بأن **صفراً** نُفِّذ ولا يذكر تجميعةً: ${path.slice(root.length + 1)} — ` +
          `تصفيةٌ لا تطابق شيئاً، أو مجموعةٌ اختفت. والتشغيل يخرج بصفرٍ في بعض هذه الحالات. ` +
          `· A report attesting that zero tests ran.`
      );
      continue;
    }
    const list = trxByAssembly.get(report.assembly) ?? [];
    list.push(report);
    trxByAssembly.set(report.assembly, list);
  } else if (path.endsWith(".json")) {
    jsonReports.set(basename(path), path);
  } else {
    problems.push(
      `ملفٌّ في مجلّد التقارير لا هو TRX ولا JSON: ${path.slice(root.length + 1)} — ` +
        `مجلّد التقارير ليس مكاناً لغير التقارير. · A file that is neither report format.`
    );
  }
}

// ── الاتجاه الأول: كل سطحٍ مطلوب أنتج تقريراً يفي بأرضيته ─────────────────────
for (const surface of selected.values()) {
  let executed = null;
  let failed = 0;
  let where = "";

  if (surface.runner === "dotnet") {
    const reports = trxByAssembly.get(surface.assembly) ?? [];
    if (reports.length > 0) {
      executed = Math.max(...reports.map((report) => report.executed));
      failed = reports.reduce((sum, report) => sum + report.failed, 0);
      where = `${reports.length} TRX`;
    }
  } else {
    const path = jsonReports.get(surface.report);
    if (path) {
      const report = readJson(path);
      where = surface.report;
      if (surface.runner === "vitest") {
        for (const key of ["numPassedTests", "numFailedTests"]) {
          if (typeof report[key] !== "number") {
            die([`✗ تقرير vitest بلا الحقل ${key}: ${path} · missing field, refusing to read zero.`]);
          }
        }
        executed = report.numPassedTests + report.numFailedTests;
        failed = report.numFailedTests;
      } else {
        const stats = report.stats;
        if (!stats || typeof stats.expected !== "number" || typeof stats.unexpected !== "number") {
          die([`✗ تقرير playwright بلا stats مقروءة: ${path} · missing stats, refusing to read zero.`]);
        }
        executed = stats.expected + stats.unexpected + (stats.flaky ?? 0);
        failed = stats.unexpected + (report.errors?.length ?? 0);
      }
    }
  }

  if (executed === null) {
    problems.push(
      `السطح «${surface.id}» لم يُنتج تقريراً إطلاقاً — لم يُنفَّذ منه شيء. ` +
        `وهذا ما يفعله `+"`--nologo`"+` و`+"`--diag`"+` بالضبط: لا مجلّد ولا ملفّ (مقيس). ` +
        `· Surface produced no report at all: nothing ran.`
    );
    rows.push([surface.id, surface.runner, "—", String(surface.minimumExecuted), "✗ لا تقرير"]);
    continue;
  }

  const short = executed < surface.minimumExecuted;
  if (short) {
    problems.push(
      `السطح «${surface.id}» نفّذ ${executed} اختباراً والأرضية المُلتزَمة ${surface.minimumExecuted}. ` +
        `الانكماش الصامت أخطر من الصفر لأنه يخرج بصفر. ` +
        `· ran ${executed}, committed floor ${surface.minimumExecuted}: a silent shrink.`
    );
  }
  if (failed > 0) {
    problems.push(`السطح «${surface.id}» فيه ${failed} إخفاقاً. · ${failed} failure(s).`);
  }

  rows.push([
    surface.id,
    surface.runner,
    String(executed),
    String(surface.minimumExecuted),
    short || failed > 0 ? "✗" : "✔",
  ]);
  void where;
}

// ── الاتجاه الثاني: لا تقرير بلا سطحٍ يدّعيه ──────────────────────────────────
// سطحٌ غير مُعلَن سقوطٌ كسطحٍ مفقود: مشروع اختبارٍ يدخل المستودع فيُشغَّل ولا يحرسه
// أحد، هو الحالة التي بدأ منها كل هذا (فخ-43).
const claimedAssemblies = new Set(
  surfaces.filter((surface) => surface.runner === "dotnet").map((surface) => surface.assembly)
);
const claimedReports = new Set(
  surfaces.filter((surface) => surface.runner !== "dotnet").map((surface) => surface.report)
);

for (const assembly of trxByAssembly.keys()) {
  if (!claimedAssemblies.has(assembly)) {
    problems.push(
      `تقريرٌ لتجميعةٍ لا يدّعيها أي سطح في السجلّ: ${assembly} — ` +
        `مجموعةُ اختباراتٍ تعمل بلا إعلانٍ ولا أرضية. أضِفها إلى tests/test-surfaces.json. ` +
        `· A report for an assembly no surface claims.`
    );
  }
}
for (const name of jsonReports.keys()) {
  if (!claimedReports.has(name)) {
    problems.push(
      `تقرير JSON لا يدّعيه أي سطح في السجلّ: ${name}. · An unclaimed JSON report.`
    );
  }
}

// ── الحصيلة ───────────────────────────────────────────────────────────────────
const width = (index) => Math.max(...rows.map((row) => [...row[index]].length), 8);
process.stdout.write("\n══ الحصيلة — ما نُفِّذ فعلاً · what actually ran\n");
for (const row of rows) {
  process.stdout.write(
    `   ${row[4]} ${row[0].padEnd(width(0))}  ${row[1].padEnd(11)}  نُفِّذ ${row[2].padStart(5)}  الأرضية ${row[3].padStart(5)}\n`
  );
}

if (process.env.GITHUB_STEP_SUMMARY) {
  const lines = [
    "### الحصيلة — ما نُفِّذ فعلاً · what actually ran",
    "",
    "| السطح · surface | المُشغّل · runner | نُفِّذ · executed | الأرضية · floor | |",
    "|---|---|---:|---:|---|",
    ...rows.map((row) => `| ${row[0]} | ${row[1]} | ${row[2]} | ${row[3]} | ${row[4]} |`),
  ];
  if (problems.length > 0) {
    lines.push("", "> ⚠️ **الحصيلة ساقطة · the tally failed**", "");
    lines.push(...problems.map((problem) => "> - " + problem));
  }
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, lines.join("\n") + "\n");
}

if (problems.length > 0) {
  process.stderr.write("\n✗ الحصيلة ساقطة · the tally failed:\n");
  for (const problem of problems) process.stderr.write("   · " + problem + "\n");
  process.exit(1);
}

process.stdout.write(`\n✔ ${rows.length} سطحاً أنتج تقريراً عند أرضيته أو فوقها، بصفر إخفاق.\n`);
