#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   خادم وهمي مطابق للعقد — للاختبارات وقياس الأداء وحدهما
   A contract-shaped stub server, for tests and performance measurement only
   ───────────────────────────────────────────────────────────────────────────
   لماذا وُجد ولماذا لا يُغني عن الخادم الحقيقي:
   ٥٠٠ صفّاً في ميزان مراجعة تعني ٥٠٠ حساب عليها حركة، ودليل حسابات هذا
   المستودع فيه ١٨٠ حساباً، وخريطة الأدوار تُحلّ أربعة عشر دوراً إلى حفنة
   حسابات — فلا سبيل إلى ٥٠٠ صفّ من الخادم الحقيقي اليوم. ولذلك: التكامل
   الحيّ يُثبَت على الخادم الحقيقي (tests/live-api)، والكثافة تُقاس هنا.

   وكل ما يخرج من هنا **يمرّ بفاكّ الترميز المُولَّد نفسه** في المتصفّح: مبلغ
   لا يطابق نحو العقد يُسقِط الصفحة، فالوهمي لا يستطيع أن يكذب في شكل المال.

       node scripts/mock-api.mjs [--port 5099]
       GET /health
       GET /api/v1/companies/{id}/trial-balance?book=MAIN&period=2026-05&rows=500
       والشركة 00000000-0000-4000-8000-0000000000ff تُرجع مشكلة 403 دائماً.
   ═══════════════════════════════════════════════════════════════════════════ */
import http from "node:http";

const portArg = process.argv.indexOf("--port");
const PORT = portArg >= 0 ? Number(process.argv[portArg + 1]) : 5099;

/** شركة تُرجع مشكلة دائماً — لاختبار سطح الخطأ. */
export const PROBLEM_COMPANY = "00000000-0000-4000-8000-0000000000ff";

/* الاسم سجلٌّ عربي وترجماتٌ بوسم اللغة (ADR-0021) — لا زوجاً ثابتاً ar/en.
   والوهمي يحمل أربع لغات لا اثنتين عمداً: شاشةٌ تُختبَر على زوج واحد تمرّ وهي
   عاجزة عن الثالثة، وذلك بالضبط العطل الذي جاء القرار ليزيله.

   والصفّ الأخير **بلا ترجمات إطلاقاً**: الارتداد إلى السجلّ حالةٌ مشروعة يجب
   أن تُرى في الوهمي، لا حالةٌ لا يبلغها اختبار. */
const NAMES = [
  ["الصندوق الرئيسي", { en: "Main cash box", ur: "مرکزی نقدی صندوق", hi: "मुख्य नकद पेटी" }],
  ["البنك الأهلي — الحساب الجاري", { en: "National Bank — current account", ur: "نیشنل بینک — کرنٹ اکاؤنٹ" }],
  ["العملاء / شركة النور", { en: "Receivables / Al-Noor Co.", hi: "प्राप्य / अल-नूर कं." }],
  ["مخزون البضاعة — المستودع الرئيسي", { en: "Inventory — main warehouse", ur: "انوینٹری — مرکزی گودام", hi: "इन्वेंटरी — मुख्य गोदाम" }],
  ["الأصول الثابتة — التكلفة", { en: "Fixed assets — cost" }],
  ["مجمع إهلاك الأصول الثابتة", { en: "Accumulated depreciation", hi: "संचित मूल्यह्रास" }],
  ["الدائنون / مؤسسة الإمداد", { en: "Payables / Imdad Est.", ur: "واجب الادا / امداد ادارہ" }],
  ["رواتب مستحقة الدفع", { en: "Accrued payroll" }],
  ["رأس المال", { en: "Share capital", ur: "سرمایہ", hi: "शेयर पूंजी" }],
  ["إيرادات المبيعات", { en: "Sales revenue", ur: "فروخت کی آمدنی" }],
  ["مصروف خدمات تقنية", { en: "IT services expense", hi: "आईटी सेवा व्यय" }],
  ["ضريبة القيمة المضافة — مخرجات", {}],
];

/* جمع نصّي بلا فاصلة عائمة: المجموعان هنا يُبنيان كما يبنيهما الخادم — بحساب
   مضبوط — لا بـNumber. وإلا كان الوهمي يكذب في الشيء الذي يقيسه الاختبار. */
function addDecimal(a, b) {
  const [ai, af = ""] = a.split(".");
  const [bi, bf = ""] = b.split(".");
  const width = Math.max(af.length, bf.length);
  const A = BigInt(ai + af.padEnd(width, "0"));
  const B = BigInt(bi + bf.padEnd(width, "0"));
  const sum = (A + B).toString().padStart(width + 1, "0");
  return width === 0 ? sum : sum.slice(0, sum.length - width) + "." + sum.slice(sum.length - width);
}

/**
 * يبني ميزاناً بعدد صفوف مطلوب. المبالغ نصّ بأربع خانات، كما يفرض العقد.
 * @param rowCount عدد الصفوف.
 * @param book الدفتر.
 * @param period رمز الفترة أو null.
 */
export function buildTrialBalance(rowCount, book, period) {
  const rows = [];
  let totalDebit = "0.0000";
  let totalCredit = "0.0000";
  for (let i = 0; i < rowCount; i++) {
    const name = NAMES[i % NAMES.length];
    const code = String(1010101 + i * 7);
    /* قيم مبنيّة لا عشوائية: التشغيلة نفسها تُنتج البايتات نفسها. وواحدة منها
       تحمل عمداً القيمة التي يفقدها Number: 1000000000000.4013 */
    /* الصفوف تتزاوج: كل مبلغ مدين يقابله المبلغ نفسه دائناً في السطر التالي،
       فالميزان متوازن حين يكون العدد زوجياً وغير متوازن حين يكون فردياً —
       والحالتان مطلوبتان: ميزانٌ لا يتوازن يُرى ولا يُقرَّب. */
    const debitOn = i % 2 === 0;
    const pair = Math.floor(i / 2);
    const magnitude =
      pair === 1
        ? "1000000000000.4013"
        : (1000 + pair * 137) + "." + String(1000 + ((pair * 37) % 9000)).slice(0, 4);
    const debit = debitOn ? magnitude : "0.0000";
    const credit = debitOn ? "0.0000" : magnitude;
    totalDebit = addDecimal(totalDebit, debit);
    totalCredit = addDecimal(totalCredit, credit);
    const suffix = " " + (i + 1);
    rows.push({
      accountCode: code,
      nameAr: name[0] + suffix,
      /* الحقل المهجور: مشتقّ من الترجمة en ومرتدٌّ إلى السجلّ حين لا توجد —
         كما يفعل الخادم بالضبط، فالوهمي لا يكذب في السلوك الذي يُختبَر. */
      nameEn: (name[1].en ?? name[0]) + suffix,
      nameTranslations: Object.keys(name[1])
        .sort()
        .map((tag) => ({ name: tag, value: name[1][tag] + suffix })),
      debit,
      credit,
    });
  }
  return {
    balanced: totalDebit === totalCredit,
    book,
    periodCode: period,
    rowCount: rows.length,
    rows,
    totalCredit,
    totalDebit,
  };
}

/** مشكلة بصيغة RFC 9457 كما تنشرها الخلفية. */
export function problem(status, code, path) {
  return {
    code,
    detail: "The credential does not reach this company, or entitlement forbids this access.",
    detailAr: "الاعتماد لا يبلغ هذه الشركة، أو الاستحقاق يمنع هذا الوصول.",
    errors: [
      {
        code,
        field: "companyId",
        messageAr: "الشركة المطلوبة خارج نطاق هذا الاعتماد.",
        messageEn: "The requested company is outside this credential's scope.",
      },
      {
        code: "entitlement.module_not_licensed",
        field: null,
        messageAr: "وحدة الحسابات العامة غير مشمولة بالاشتراك الحالي.",
        messageEn: "The general ledger module is not covered by the current subscription.",
      },
    ],
    instance: path,
    status,
    title: "Forbidden",
    titleAr: "ممنوع",
    traceId: "00-mock000000000000000000000000-0000000000000000-01",
    type: "https://salasel-babel.example/problems/" + code,
  };
}

function send(res, status, body, contentType) {
  const text = JSON.stringify(body);
  res.writeHead(status, {
    "content-type": contentType,
    "content-length": Buffer.byteLength(text),
    "access-control-allow-origin": "*",
  });
  res.end(text);
}

/** ينشئ الخادم بلا إقلاعه. */
export function createMockServer() {
  return http.createServer((req, res) => {
    const url = new URL(req.url ?? "/", "http://localhost");
    if (url.pathname === "/health") {
      send(res, 200, { apiVersion: "v1", calendar: "GregorianCalendar", culture: "en-US", status: "ok" }, "application/json");
      return;
    }
    const match = /^\/api\/v1\/companies\/([^/]+)\/trial-balance$/.exec(url.pathname);
    if (match && req.method === "GET") {
      const companyId = decodeURIComponent(match[1]);
      if (companyId === PROBLEM_COMPANY) {
        send(res, 403, problem(403, "auth.company_out_of_scope", url.pathname), "application/problem+json");
        return;
      }
      const book = url.searchParams.get("book") ?? "MAIN";
      const period = url.searchParams.get("period");
      const rows = Number(url.searchParams.get("rows") ?? "24");
      send(res, 200, buildTrialBalance(rows, book, period), "application/json");
      return;
    }
    send(res, 404, problem(404, "http.not_found", url.pathname), "application/problem+json");
  });
}

if (import.meta.url === "file://" + process.argv[1]) {
  createMockServer().listen(PORT, "127.0.0.1", () => {
    console.log("خادم وهمي مطابق للعقد على http://127.0.0.1:" + PORT);
  });
}
