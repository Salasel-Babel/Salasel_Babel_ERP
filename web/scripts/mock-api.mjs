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
      /* ولا حقل nameEn: الإنجليزية مدخلٌ في الترجمات كغيرها، وحقلٌ ثابت لها هنا
         يجعل الوهمي يكذب في الشيء نفسه الذي حُذف من العقد. */
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

/* ═══════ الجلسة والتأسيس والترحيل — ما تحتاجه شاشتا الدخول والقيد ═══════
   والوهمي هنا **يُحاكي القواعد التي تقيسها الاختبارات، لا أكثر**: الاعتماد
   المرفوض، والاعتماد المنقضي، والاعتماد بلا شركات، ورفض الرمز الرقمي في حقل
   مالي، وحصانة التكرار. وما عدا ذلك يبقى للخادم الحقيقي — وهو ما تقيسه
   `tests/Babel.Api.Tests` من الشبكة. */

/** الاعتماد الوحيد المقبول في الوهمي. لا سرّ فيه: قيمة اختبار مُعلَنة. */
export const MOCK_TOKEN = "mock-token";

/** اعتماد منقضٍ — ليُرى في الشاشة رمزه الخاصّ لا رمز «مرفوض». */
export const MOCK_EXPIRED_TOKEN = "mock-expired";

/** اعتماد صحيح لا يبلغ شركة — حالة «اشتُرك ولم يُربط بمنشأة». */
export const MOCK_NO_COMPANY_TOKEN = "mock-no-company";

/** المنشأة الجاهزة في الوهمي. */
export const MOCK_COMPANY = "11111111-1111-4111-8111-111111111111";

/** منشأة يبلغها الاعتماد ولم تُؤسَّس بعد — تظهر ولا تُخفى. */
export const MOCK_COMPANY_NOT_SET_UP = "22222222-2222-4222-8222-222222222222";

const SESSION = {
  tenantId: "11111111-1111-4111-8111-111111111110",
  userId: "11111111-1111-4111-8111-11111111110a",
  companyCount: 2,
  companies: [
    {
      companyId: MOCK_COMPANY,
      state: "Ready",
      nameAr: "مؤسسة بابل للتجارة",
      nameTranslations: [
        { name: "en", value: "Babel Trading Est." },
        { name: "hi", value: "बाबेल ट्रेडिंग प्रतिष्ठान" },
        { name: "ur", value: "بابل ٹریڈنگ ادارہ" },
      ],
      decimalPlaces: 2,
      defaultCostCenter: "cc.main",
    },
    {
      companyId: MOCK_COMPANY_NOT_SET_UP,
      state: "NotSetUp",
      nameAr: null,
      nameTranslations: [],
      decimalPlaces: null,
      defaultCostCenter: null,
    },
  ],
};

const SETUP = {
  nameAr: "مؤسسة بابل للتجارة",
  nameTranslations: [
    { name: "en", value: "Babel Trading Est." },
    { name: "hi", value: "बाबेल ट्रेडिंग प्रतिष्ठान" },
    { name: "ur", value: "بابل ٹریڈنگ ادارہ" },
  ],
  decimalPlaces: 2,
  defaultCostCenter: "cc.main",
  costCenters: [
    { code: "cc.main", nameAr: "المركز الرئيسي", nameTranslations: [{ name: "en", value: "Head office" }], state: "Active", isDefault: true, suspensionReason: "" },
    { code: "cc.branch", nameAr: "فرع جدة", nameTranslations: [{ name: "en", value: "Jeddah branch" }], state: "Active", isDefault: false, suspensionReason: "" },
    { code: "cc.closed", nameAr: "فرع مغلق", nameTranslations: [], state: "Suspended", isDefault: false, suspensionReason: "أُغلق الفرع في 2026-01" },
  ],
};

/** ما رُحِّل بالفعل، بمفتاح الحصانة — فالإرسال الثاني لا يُنشئ قيداً ثانياً. */
const posted = new Map();
let entrySequence = 0;

/**
 * رقم قيد بلا فجوات، نصّاً.
 * و**بلا بادئة حرفية**: العقد يُعرّف entryNumber بأنه Int64String بالنمط
 * ^-?(0|[1-9][0-9]*)$ — فرقمٌ مثل «JV-000001» يرفضه الفاكّ المُولَّد عند العميل
 * قبل أن يُعرَض. وقد كتبه هذا الوهمي كذلك أولاً فسقطت شاشة القيد بـTypeError.
 */
function nextEntryNumber() {
  entrySequence += 1;
  return String(1000 + entrySequence);
}

/** بصمة عرضية ثابتة الطول — لا تجزئة حقيقية، وهي ليست دعوى في الوهمي. */
function fakeHash(key) {
  let a = 0x811c9dc5;
  for (const ch of key) {
    a ^= ch.codePointAt(0);
    a = Math.imul(a, 0x01000193) >>> 0;
  }
  return a.toString(16).padStart(8, "0").repeat(8);
}

/** الاعتماد المقدَّم بعد Bearer، أو "" حين لا ترويسة. */
function bearer(req) {
  const header = req.headers.authorization ?? "";
  return header.startsWith("Bearer ") ? header.slice("Bearer ".length).trim() : "";
}

/**
 * يحكم على الاعتماد كما يحكم الخادم: أربع حالات برموزها الأربعة.
 * @param req الطلب.
 * @returns رفضاً بصيغة المشكلة، أو null حين يُقبل.
 */
function refuseCredential(req, path) {
  const token = bearer(req);
  if (token === "") return { status: 401, code: "auth.credential_missing" };
  if (token === MOCK_EXPIRED_TOKEN) return { status: 401, code: "auth.credential_expired" };
  if (token !== MOCK_TOKEN && token !== MOCK_NO_COMPANY_TOKEN) {
    return { status: 401, code: "auth.credential_rejected" };
  }
  void path;
  return null;
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
    "access-control-expose-headers": "x-babel-trace-id",
  });
  res.end(text);
}

/**
 * يقرأ جسم الطلب نصّاً خامّاً.
 * والخام مقصود: فحص «هل وصل المبلغ رمزاً رقمياً؟» لا يمكن أن يقع بعد JSON.parse —
 * فالتحليل نفسه هو ما يُتلف القيمة. النصّ يُفحص قبله.
 * @param req الطلب.
 * @param done ما يُنادى بالنصّ.
 */
function readBody(req, done) {
  const chunks = [];
  req.on("data", (chunk) => chunks.push(chunk));
  req.on("end", () => done(Buffer.concat(chunks).toString("utf8")));
}

/** نحو المال المنشور، منقولاً من العقد. */
const MONEY_RE = /^-?(0|[1-9][0-9]*)(\.[0-9]{1,4})?$/;

/**
 * يجيب عن طلب ترحيل بالقواعد التي تقيسها الشاشة.
 * @param res الاستجابة.
 * @param path المسار.
 * @param raw الجسم نصّاً خامّاً.
 * @param body الجسم مُحلَّلاً.
 */
function respondToPosting(res, path, raw, body) {
  /* ١ · رمز رقمي في حقل مالي: يُرفض من النصّ الخام قبل أي تحليل. */
  if (/"amount"\s*:\s*-?[0-9]/.test(raw)) {
    send(res, 400, problem(400, "wire.money.number_token", path), "application/problem+json");
    return;
  }

  /* ٢ · نحو المال المنشور. */
  for (const line of body.lines ?? []) {
    if (typeof line.amount !== "string" || !MONEY_RE.test(line.amount)) {
      send(res, 400, problem(400, "wire.money.malformed", path), "application/problem+json");
      return;
    }
  }

  /* ٣ · رمز الحدث إلزامي — على المسارين معاً. */
  if (!body.event) {
    send(res, 422, problem(422, "ledger.posting.missing_event_code", path), "application/problem+json");
    return;
  }

  /* ٤ · ما لا يعرفه العقد ويعرفه الدفتر — **منقولٌ من قياس على الخادم الحقيقي**.
     ‏ADR: القاعدة 2 تمنع السطح من رؤية الحساب، فالسطر يحمل دوراً والدفتر يحلّه.
     وثمرةُ ذلك أن حقلَي subledger و scope اختياريان في العقد **بلا ما يقول متى
     يلزمان**. والمقيس على مؤسسة العرض:
       role=Settlement → الحساب 1201 ضابطٌ لدفتر bank_account ⇒ يحتاج طرفاً
       role=NetAmount  → الحساب 4101 له بُعد إلزامي branch      ⇒ يحتاج فرعاً
     ويحاكيهما الوهمي بالرمزين نفسيهما كي تُختبَر الشاشة على المسار الذي يقع
     فعلاً، لا على مسارٍ سهل لا وجود له. */
  for (const line of body.lines ?? []) {
    if (line.role === "Settlement" && !line.subledger) {
      send(res, 422, problem(422, "ledger.posting.missing_subledger", path), "application/problem+json");
      return;
    }
    if (line.role === "NetAmount" && !line.scope?.branchId) {
      send(res, 422, problem(422, "ledger.posting.guard.GR-COA-002", path), "application/problem+json");
      return;
    }
  }

  /* ٥ · حصانة التكرار: المفتاح نفسه يُعيد الإيصال ذاته و200 بدل 201. */
  const key = String(body.idempotencyKey ?? "");
  const seen = posted.get(key);
  if (seen) {
    /* ⚠ الخادم الحقيقي يُعيد lineCount = 0 في إيصال الوصول الثاني بينما قال 2 في
       الأول — لنفس القيد وبنفس البصمة. مقيس، ومُحاكى هنا كي تُختبَر الشاشة على
       ما يقع لا على ما نتمنّاه. */
    send(res, 200, { ...seen, alreadyPosted: true, lineCount: 0 }, "application/json");
    return;
  }

  /* ٦ · التوازن: جمعٌ عشري نصّي بلا فاصلة عائمة — كما يفعل الخادم في SQL. */
  let debit = "0.0000";
  let credit = "0.0000";
  for (const line of body.lines ?? []) {
    if (line.side === "Debit") debit = addDecimal(debit, line.amount);
    else credit = addDecimal(credit, line.amount);
  }
  if (debit !== credit) {
    send(res, 422, problem(422, "ledger.posting.unbalanced", path), "application/problem+json");
    return;
  }

  const receipt = {
    entryId: "00000000-0000-4000-8000-" + String(entrySequence + 1).padStart(12, "0"),
    entryNumber: nextEntryNumber(),
    entryHash: fakeHash(key),
    alreadyPosted: false,
    chainSequence: String(entrySequence),
    periodCode: String(body.documentDate ?? "2026-01-01").slice(0, 7),
    generation: body.generation ?? 1,
    lineCount: (body.lines ?? []).length,
  };
  posted.set(key, receipt);
  send(res, 201, receipt, "application/json");
}

/** ينشئ الخادم بلا إقلاعه. */
export function createMockServer() {
  return http.createServer((req, res) => {
    const url = new URL(req.url ?? "/", "http://localhost");

    /* الفحص المُسبَق: ترويسة Authorization تجعل الطلب **غير بسيط**، فيسبقه
       المتصفّح بـOPTIONS. وكان هذا الوهمي لا يجيب عنه، فكانت نداءات القراءة
       بلا اعتماد تعمل (مصفوفة العرض لا تمرّر رمزاً) بينما أول نداء **باعتماد**
       يسقط بـ«Failed to fetch» بلا رمز ولا رمز حالة — يبدو للواجهة انقطاعَ
       شبكة وهو رفض CORS. ولا وجود له في النشر: الواجهة والخادم على أصل واحد. */
    if (req.method === "OPTIONS") {
      res.writeHead(204, {
        "access-control-allow-origin": "*",
        "access-control-allow-methods": "GET, POST, PUT, OPTIONS",
        "access-control-allow-headers": "authorization, content-type, accept",
        "access-control-max-age": "600",
        "content-length": "0",
      });
      res.end();
      return;
    }

    if (url.pathname === "/health") {
      send(res, 200, { apiVersion: "v1", calendar: "GregorianCalendar", culture: "en-US", status: "ok" }, "application/json");
      return;
    }
    /* ── الجلسة ─────────────────────────────────────────────────────── */
    if (url.pathname === "/api/v1/session" && req.method === "GET") {
      const refused = refuseCredential(req, url.pathname);
      if (refused) {
        send(res, refused.status, problem(refused.status, refused.code, url.pathname), "application/problem+json");
        return;
      }
      if (bearer(req) === MOCK_NO_COMPANY_TOKEN) {
        send(res, 403, problem(403, "session.no_reachable_company", url.pathname), "application/problem+json");
        return;
      }
      send(res, 200, SESSION, "application/json");
      return;
    }

    /* ── التأسيس: مراكز التكلفة تُقرأ منه، ولا تُكتب في الشاشة ──────────── */
    const setupMatch = /^\/api\/v1\/companies\/([^/]+)\/setup$/.exec(url.pathname);
    if (setupMatch && req.method === "GET") {
      const refused = refuseCredential(req, url.pathname);
      if (refused) {
        send(res, refused.status, problem(refused.status, refused.code, url.pathname), "application/problem+json");
        return;
      }
      if (decodeURIComponent(setupMatch[1]) !== MOCK_COMPANY) {
        send(res, 403, problem(403, "tenancy.company_out_of_scope", url.pathname), "application/problem+json");
        return;
      }
      send(res, 200, SETUP, "application/json");
      return;
    }

    /* ── الترحيل: أول كتابة ───────────────────────────────────────────── */
    const postMatch = /^\/api\/v1\/companies\/([^/]+)\/journal-entries$/.exec(url.pathname);
    if (postMatch && req.method === "POST") {
      const refused = refuseCredential(req, url.pathname);
      if (refused) {
        send(res, refused.status, problem(refused.status, refused.code, url.pathname), "application/problem+json");
        return;
      }
      if (decodeURIComponent(postMatch[1]) !== MOCK_COMPANY) {
        send(res, 403, problem(403, "tenancy.company_out_of_scope", url.pathname), "application/problem+json");
        return;
      }
      readBody(req, (raw) => {
        let body;
        try {
          body = JSON.parse(raw);
        } catch {
          send(res, 400, problem(400, "wire.body.malformed", url.pathname), "application/problem+json");
          return;
        }
        respondToPosting(res, url.pathname, raw, body);
      });
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
