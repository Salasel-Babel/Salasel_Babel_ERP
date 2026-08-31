#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   التكامل الحيّ: العميل المُولَّد × الخادم الحقيقي × PostgreSQL حقيقية
   Live integration: the generated client × the real server × real PostgreSQL
   ───────────────────────────────────────────────────────────────────────────
   لا خادم وهمي هنا ولا حمولة مُعدّة: يُرحَّل قيدٌ حقيقي بالمبلغ الذي يفقده
   Number، ثم يُقرأ ميزان المراجعة **بالعميل المُولَّد نفسه** الذي تشحنه
   الواجهة، ويُقارَن نصّ المال بايتاً ببايت.

       BABEL_API=http://127.0.0.1:5080 \
       BABEL_TOKEN=… BABEL_COMPANY=… node scripts/live-api-check.mjs

   وتشغيل الخادم موصوف في web/README.md §التشغيل الحيّ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { createRequire } from "node:module";
import { register } from "node:module";
import { pathToFileURL } from "node:url";

/* الملفّات TypeScript، فنُحمّلها عبر vite في وضع SSR — نفس ما يبنيه المتصفّح. */
const { createServer } = await import("vite");
const vite = await createServer({ server: { middlewareMode: true }, appType: "custom", logLevel: "error" });

const { readTrialBalance, postJournalEntry, health } = await vite.ssrLoadModule("/src/api/generated/client.ts");
const { fetchTransport, ProblemError } = await vite.ssrLoadModule("/src/api/transport.ts");
const { Money, isMoney } = await vite.ssrLoadModule("/src/api/money.ts");
const { CONTRACT } = await vite.ssrLoadModule("/src/api/generated/contract.ts");

const BASE = process.env.BABEL_API ?? "http://127.0.0.1:5080";
const TOKEN = process.env.BABEL_TOKEN ?? "";
const COMPANY = process.env.BABEL_COMPANY ?? "";
const BOOK = process.env.BABEL_BOOK ?? "MAIN";
const HAZARD = "1000000000000.4013";

let failures = 0;
let checks = 0;
function check(what, condition, detail) {
  checks++;
  if (condition) {
    console.log("  ✓ " + what + (detail ? "  — " + detail : ""));
  } else {
    failures++;
    console.log("  ✗ " + what + (detail ? "  — " + detail : ""));
  }
}

const transport = fetchTransport({ baseUrl: BASE, token: TOKEN });

console.log("العقد · contract: " + CONTRACT.version + " · " + CONTRACT.sourceSha256.slice(0, 16) + "…");
console.log("الخادم · server:  " + BASE);
console.log("");

console.log("١ · نقطة الصحّة — بلا اعتماد، وتُعلن ثقافة الخادم وتقويمه");
const h = await health(transport);
check("الخادم يردّ بحالة", h.status === "ok", JSON.stringify(h));
check("التقويم ميلادي", h.calendar === "GregorianCalendar", h.calendar);
check("إصدار السطح يطابق العقد", h.apiVersion === CONTRACT.version, h.apiVersion + " vs " + CONTRACT.version);

console.log("");
console.log("٢ · ترحيل قيد حقيقي بالمبلغ الذي يفقده Number");
/* مفتاح حصانة **ثابت**: إعادة تشغيل هذا الفحص لا تُرحِّل قيداً ثانياً، فيبقى
   صفّ الحساب مساوياً للمبلغ نفسه بالضبط. اختبارٌ يعتمد على قاعدة نظيفة مرّة
   واحدة اختبارٌ ينجح مرّة واحدة. */
const key = "web-live-money-proof";
let receipt;
try {
  receipt = await postJournalEntry(transport, {
    companyId: COMPANY,
    body: {
      event: "ledger.manual_voucher.posted",
      idempotencyKey: key,
      source: { module: "Ledger", documentType: "ManualJournal", documentId: key },
      trigger: "OnApproval",
      documentDate: "2026-08-15",
      narration: { ar: "قيد حيّ من الواجهة", en: "Live entry from the front end" },
      book: BOOK,
      currency: "SAR",
      exchangeRate: "1",
      generation: 1,
      lines: [
        {
          role: "Settlement",
          side: "Debit",
          amount: Money.wire(HAZARD),
          qualifier: "bank",
          subledger: { kind: "Treasury", partyId: "BANK-0001" },
          narration: { ar: "تحصيل بنكي", en: "Bank receipt" },
        },
        {
          role: "NetAmount",
          side: "Credit",
          amount: Money.wire(HAZARD),
          scope: { branchId: "BR-01" },
          narration: { ar: "إيراد", en: "Revenue" },
        },
      ],
    },
  });
  check(
    "القيد رُحِّل أو كان مُرحَّلاً (حصانة التكرار)",
    !!receipt.entryId && receipt.entryHash.length === 64,
    "رقم القيد " + receipt.entryNumber + " · بصمة " + receipt.entryHash.slice(0, 16) + "… · alreadyPosted=" + receipt.alreadyPosted
  );
  /* ⚠ ملاحظة على العقد، مقيسة هنا: lineCount حقل **إلزامي** في PostingReceipt،
     ويصل 0 على إعادة المحاولة بالمفتاح نفسه (alreadyPosted=true) بينما القيد
     له سطران. عميلٌ يعرض «عدد السطور» يعرض صفراً بعد انقطاع شبكة — يعمل خطأً
     ولا يفشل. مرفوع في التقرير. */
  check(
    "عدد السطور صحيح عند الترحيل الأوّل",
    receipt.alreadyPosted ? true : receipt.lineCount === 2,
    "lineCount=" + receipt.lineCount + " · alreadyPosted=" + receipt.alreadyPosted
  );
  if (receipt.alreadyPosted && receipt.lineCount === 0) {
    console.log(
      "  ⚠ ملاحظة على العقد: lineCount=0 على إعادة المحاولة بينما القيد سطران — " +
        "حقلٌ إلزامي يصل بقيمة خاطئة لا غائبة."
    );
  }
} catch (error) {
  failures++;
  console.log("  ✗ تعذّر الترحيل: " + (error instanceof ProblemError ? error.code : String(error)));
  if (error instanceof ProblemError && error.problem) {
    for (const e of error.problem.errors) console.log("      · " + e.code + " — " + e.messageAr);
  }
}

console.log("");
console.log("٣ · قراءة الميزان بالعميل المُولَّد، ومقارنة نصّ المال بايتاً ببايت");
const tb = await readTrialBalance(transport, { companyId: COMPANY, book: BOOK, period: "2026-08" });
check("وصلت صفوف", tb.rows.length > 0, tb.rows.length + " صفّاً");
check("كل مبلغ كائن Money لا رقم", tb.rows.every((r) => isMoney(r.debit) && isMoney(r.credit)));
check("المجموعان كائنا Money", isMoney(tb.totalDebit) && isMoney(tb.totalCredit));

const raw = await fetch(
  BASE + "/api/v1/companies/" + COMPANY + "/trial-balance?book=" + BOOK + "&period=2026-08",
  { headers: { Authorization: "Bearer " + TOKEN } }
);
const rawText = await raw.text();
check(
  "الجسم الخام يحمل المبلغ نصّاً لا رمزاً رقمياً",
  rawText.includes('"' + HAZARD + '"'),
  "طول الجسم " + rawText.length + " بايتاً"
);
const carrier = tb.rows.find((r) => r.debit.text === HAZARD || r.credit.text === HAZARD);
check(
  "العميل يحمل النصّ نفسه بايتاً ببايت",
  !!carrier,
  carrier ? carrier.accountCode + " → " + (carrier.debit.isZero ? carrier.credit.text : carrier.debit.text)
          : "غير موجود · الصفوف: " + tb.rows.map((r) => r.accountCode + "=" + r.debit.text + "/" + r.credit.text).join(" · ")
);
check(
  "ولو مرّ على Number لتغيّر",
  String(Number(HAZARD)) !== HAZARD,
  HAZARD + " → " + String(Number(HAZARD))
);
check("حكم التوازن يصل محسوماً من الدفتر", typeof tb.balanced === "boolean", "balanced=" + tb.balanced);
check(
  "المجموع من الخادم لا من المتصفّح",
  rawText.includes('"totalDebit":"' + tb.totalDebit.text + '"'),
  tb.totalDebit.text
);

console.log("");
console.log("٤ · سطح الخطأ: شركة خارج نطاق الاعتماد");
try {
  await readTrialBalance(transport, {
    companyId: "00000000-0000-4000-8000-0000000000ff",
    book: BOOK,
  });
  check("الطلب خارج النطاق يُرفض", false, "لم يُرفض");
} catch (error) {
  const isProblem = error instanceof ProblemError;
  check("الطلب خارج النطاق يُرفض بمشكلة منشورة", isProblem, isProblem ? error.code : String(error));
  if (isProblem && error.problem) {
    check("المشكلة تحمل رسالة عربية", error.problem.detailAr.length > 0, error.problem.detailAr);
    check("المشكلة تحمل رسالة إنجليزية", error.problem.detail.length > 0, error.problem.detail);
    check("المشكلة تحمل معرّف تتبّع", error.problem.traceId.length > 0, error.problem.traceId);
  }
}

console.log("");
console.log("٥ · حارس اللافراغ على هذا الفحص نفسه");
check("عدد الفحوص المنفَّذة", checks >= 16, String(checks));

console.log("");
console.log(failures === 0 ? "✓ كل الفحوص نجحت · all checks passed" : "✗ فشل " + failures + " فحصاً");
await vite.close();
process.exit(failures === 0 ? 0 : 1);
