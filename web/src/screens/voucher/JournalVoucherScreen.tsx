/* ═══════════════════════════════════════════════════════════════════════════
   قيد يومية يدوي — أول شاشة تكتب في هذا المنتج
   A manual journal voucher — the first screen in this product that writes
   ───────────────────────────────────────────────────────────────────────────
   وكل شاشة قبلها تقرأ. والاتجاه غير المُجرَّب هو الاتجاه الصعب: **مالٌ يغادر
   يد المستخدم ويعبر السلك**. وستّة قرارات تحكم هذا الملف، وكلها مقيسة لا
   مفترَضة:

   ١ · المال نصّ في الاتجاهين، ولا حساب واحد عليه هنا. المبلغ يُكتب في حقل
       نصّي، ويصير Money بـMoney.wire، ويخرج بـ.text — والمُرمِّز المُولَّد
       يرفض أن يعبر حقلٌ مالي بغير Money. والقيمة المقيسة في هذا المستودع:
           1000000000000.4013  →(Number)→  1000000000000.4012
       خانةٌ واحدة في المنزلة الرابعة، وهي **أسوأ** من خطأ كبير: لا تُرى في
       عمود من خمسمئة صفّ.

   ٢ · **لا حكم توازن عند العميل.** المقارنة بين مبلغين قرارٌ عشري محاسبي،
       وإجراؤه هنا يُعيد الفخّ من بابه الثاني. الطلب يُرسَل، والدفتر يحكم،
       ويصل الرفض برمز ledger.posting.unbalanced — والشاشة تتصرّف على **الرمز**
       لا على نصّ رسالة.

   ٣ · الأدوار والجوانب **مقروءة من العقد** وقت التشغيل، لا مكتوبة هنا. قائمةٌ
       مكتوبة بيد تنحرف عند أول إضافة، فتُرسل دوراً لا يعرفه الخادم.

   ٤ · مركز التكلفة يُقرأ من تأسيس المنشأة نفسها (ADR-0026): تركُه محذوفاً
       يعني «الافتراضي»، وهو افتراض معلن في العقد لا صمت. وPostingScope.CostCenterId
       غير قابل للعدم في النواة (ADR-0029)، فلا يوجد سطر بلا مركز أصلاً.

   ٥ · الحدث إلزامي وهو ledger.manual_voucher.posted — مُعرَّف في مصفوفة
       الترحيل (data/posting-matrix/events/ledger.json). يُعرَض ولا يُخترَع.

   ٦ · مفتاح الحصانة يُسكّ مرّة لكل قيد ويُعرَض. والإرسال الثاني بالمفتاح نفسه
       يُعيد الإيصال ذاته وalreadyPosted = true ورمز 200 — والشاشة تقول ذلك
       صراحةً بدل أن تُظهر نجاحاً ثانياً يُقرأ «رُحِّل مرّتين».
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { Link, useNavigate } from "@tanstack/react-router";
import { postJournalEntry, readCompanySetup } from "../../api/generated/client";
import { useQuery } from "@tanstack/react-query";
import { SCHEMAS } from "../../api/generated/runtime-schema";
import { SCHEMA_Money_RE } from "../../api/generated/formats";
import { Money } from "../../api/money";
import type { CostCenter, PostingLine, PostingReceipt } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT, Num } from "../../i18n/react";

/* ── ما يُقرأ من العقد وقت التشغيل، لا يُكتب هنا ───────────────────────── */

/** أعضاء مجموعة مغلقة كما ينشرها العقد لحقل بعينه. */
function members(schema: string, field: string): readonly string[] {
  const shape = SCHEMAS[schema];
  const found = shape ? shape.fields[field]?.e : undefined;
  if (!found || found.length === 0) {
    throw new TypeError(
      "الحقل " + schema + "." + field + " ليس مجموعة مغلقة في العقد المُولَّد. " +
        "/ is not a closed set in the generated contract."
    );
  }
  return found;
}

const ROLES = members("PostingLine", "role");
const SIDES = members("PostingLine", "side");
const SUBLEDGER_KINDS = members("Subledger", "kind");

/** «لا دفتر مساعد» — العضو الذي يعني الغياب، فيُحذف الحقل كلّه بدله. */
const NO_SUBLEDGER = "None";

/**
 * تسمية كل جانب. والحارس تحتها ليس زينة: عضوٌ جديد في المجموعة المغلقة يكسر
 * الشاشة **بصوت عالٍ عند الإقلاع** بدل أن يُعرَض بلا اسم أو باسم جاره.
 */
const SIDE_LABEL: Readonly<Record<string, string>> = { Debit: "acct.debit", Credit: "acct.credit" };

for (const side of SIDES) {
  if (!SIDE_LABEL[side]) {
    throw new TypeError("جانب في العقد بلا تسمية · a published side with no label: " + side);
  }
}

/** الجانب المدين والجانب الدائن — بأسمائهما في العقد لا بترتيبهما فيه. */
const DEBIT = "Debit";
const CREDIT = "Credit";

/** الدور الافتراضي لكل جانب في مسوّدة جديدة — دورٌ يعرفه العقد، لا نصّ. */
const DEBIT_ROLE = "Settlement";
const CREDIT_ROLE = "NetAmount";

for (const role of [DEBIT_ROLE, CREDIT_ROLE]) {
  if (!ROLES.includes(role)) {
    throw new TypeError("دور افتراضي غير منشور في العقد · unpublished default role: " + role);
  }
}

/**
 * حدث القيد اليدوي في مصفوفة الترحيل.
 * data/posting-matrix/events/ledger.json — ولا يُخترَع رمز حدث في واجهة.
 */
const MANUAL_VOUCHER_EVENT = "ledger.manual_voucher.posted";

/** ما يُطلق الترحيل. القيد اليدوي يُرحَّل عند الاعتماد. */
const TRIGGER = "OnApproval";

/** الوحدة المالكة للمستند المصدر. */
const SOURCE_MODULE = "Ledger";

/** نوع المستند المصدر داخل الدفتر. */
const SOURCE_TYPE = "ManualJournal";

/** «اتركه للافتراضي» — قيمة عرض لا تعبر السلك: الحقل يُحذف كلّه. */
const DEFAULT_CENTER = "";

/** مركز عامل. الموقوف لا يُعرض للاختيار: العرض ثم الرفض إهانة لا خدمة. */
const ACTIVE = "Active";

/** سطر كما يُحرَّر في الشاشة — المبلغ **نصّ** حتى لحظة الإرسال. */
interface DraftLine {
  key: string;
  role: string;
  side: string;
  amount: string;
  costCenter: string;
  branchId: string;
  qualifier: string;
  subledgerKind: string;
  subledgerParty: string;
  narrationAr: string;
  narrationEn: string;
}

let sequence = 0;
function newLine(side: string, role: string): DraftLine {
  sequence += 1;
  return {
    key: "l" + String(sequence),
    role,
    side,
    amount: "",
    costCenter: DEFAULT_CENTER,
    branchId: "",
    qualifier: "",
    subledgerKind: NO_SUBLEDGER,
    subledgerParty: "",
    narrationAr: "",
    narrationEn: "",
  };
}

/** مفتاح حصانة جديد — يُسكّ في المتصفّح ويُعرَض، ولا يُخترَع في الخادم. */
function newIdempotencyKey(): string {
  const random = globalThis.crypto?.randomUUID?.() ?? String(Date.now());
  return "web-" + random.replace(/-/g, "").slice(0, 24);
}

/** اليوم بصيغة yyyy-MM-dd ميلادية — من حقل التاريخ لا من تنسيق ثقافة. */
function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/** الشاشة كاملةً. */
export function JournalVoucherScreen(): ReactNode {
  const { t } = useT();
  const { transport, config, setConfig } = useApi();
  const navigate = useNavigate();

  const [documentDate, setDocumentDate] = useState(todayIso);
  const [narrationAr, setNarrationAr] = useState("");
  const [narrationEn, setNarrationEn] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState(newIdempotencyKey);
  const [lines, setLines] = useState<DraftLine[]>(() => [
    newLine(DEBIT, DEBIT_ROLE),
    newLine(CREDIT, CREDIT_ROLE),
  ]);
  const [receipt, setReceipt] = useState<PostingReceipt | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const setup = useQuery({
    queryKey: ["company-setup", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readCompanySetup(transport, { companyId: config.companyId }, signal),
  });

  const centres: readonly CostCenter[] = useMemo(
    () => (setup.data?.costCenters ?? []).filter((c) => c.state === ACTIVE),
    [setup.data]
  );

  /* شكل المبلغ يُفحص بالنحو **المنشور** لا بنمط مكتوب هنا. */
  const badAmounts = useMemo(
    () => lines.filter((line) => line.amount !== "" && !SCHEMA_Money_RE.test(line.amount)).map((l) => l.key),
    [lines]
  );
  const emptyAmounts = useMemo(() => lines.filter((line) => line.amount === "").map((l) => l.key), [lines]);

  const ready =
    config.companyId !== "" &&
    documentDate !== "" &&
    narrationAr !== "" &&
    narrationEn !== "" &&
    lines.length > 0 &&
    badAmounts.length === 0 &&
    emptyAmounts.length === 0;

  const update = useCallback((key: string, patch: Partial<DraftLine>) => {
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)));
  }, []);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      /* الترميز: كل مبلغ يصير Money هنا — والمُرمِّز المُولَّد يرفض أي شيء
         آخر في حقل مالي، فلا يوجد طريق يمرّ منه رقم إلى السلك. */
      const wireLines: PostingLine[] = lines.map((line) => {
        /* النطاق يُبنى بالحقول المذكورة وحدها: حقلٌ فارغ يُحذف ولا يُرسَل نصّاً
           فارغاً — والعقد يقول إن حذف costCenterId يعني «الافتراضي»، أما ""
           فقيمة يرفضها الخادم. */
        const scope: { branchId?: string; costCenterId?: string } = {};
        if (line.branchId !== "") scope.branchId = line.branchId;
        if (line.costCenter !== DEFAULT_CENTER) scope.costCenterId = line.costCenter;

        /* التحويل الوحيد هنا وعند الحدّ: القيم تأتي من قوائم **مقروءة من العقد
           نفسه** وقت التشغيل، فهي أعضاء المجموعة المغلقة بحكم مصدرها — لكن
           TypeScript لا يعرف ذلك عن نصٍّ قرأه من runtime-schema. */
        return {
          role: line.role,
          side: line.side,
          amount: Money.wire(line.amount),
          ...(Object.keys(scope).length > 0 ? { scope } : {}),
          ...(line.qualifier === "" ? {} : { qualifier: line.qualifier }),
          ...(line.subledgerKind === NO_SUBLEDGER || line.subledgerParty === ""
            ? {}
            : { subledger: { kind: line.subledgerKind, partyId: line.subledgerParty } }),
          ...(line.narrationAr && line.narrationEn
            ? { narration: { ar: line.narrationAr, en: line.narrationEn } }
            : {}),
        } as PostingLine;
      });

      const posted = await postJournalEntry(transport, {
        companyId: config.companyId,
        body: {
          event: MANUAL_VOUCHER_EVENT,
          idempotencyKey,
          source: { module: SOURCE_MODULE, documentType: SOURCE_TYPE, documentId: idempotencyKey },
          trigger: TRIGGER,
          documentDate,
          narration: { ar: narrationAr, en: narrationEn },
          book: config.book,
          lines: wireLines,
        },
      });
      setReceipt(posted);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.book, config.companyId, documentDate, idempotencyKey, lines, narrationAr, narrationEn, transport]);

  const startNew = useCallback(() => {
    setReceipt(null);
    setError(null);
    setIdempotencyKey(newIdempotencyKey());
    setLines([newLine(DEBIT, DEBIT_ROLE), newLine(CREDIT, CREDIT_ROLE)]);
    setNarrationAr("");
    setNarrationEn("");
  }, []);

  /* «شاهده في الميزان»: تُثبَّت فترة الإيصال في الإعداد ثم يُنتقَل — فيفتح
     الميزان على **الفترة التي رُحِّل فيها القيد** لا على كل الفترات. */
  const openInTrialBalance = useCallback(
    (posted: PostingReceipt) => {
      setConfig({ ...config, period: posted.periodCode });
      void navigate({ to: "/" });
    },
    [config, navigate, setConfig]
  );

  /*
   * ── ما لا يعرفه العقد، وتعلّمته الشاشة من الرفض ────────────────────────
   * القاعدة 2 تمنع السطح من رؤية الحساب: السطر يحمل **دوراً**، والدفتر يحلّه.
   * وثمرة ذلك أن الشاشة **لا تستطيع أن تعرف مسبقاً** أن الدور الذي اختاره
   * المستخدم سيُحلّ إلى حساب ضابط يحتاج طرفاً، أو إلى حساب ببُعد إلزامي:
   * subledger و scope حقلان **اختياريان** في العقد بلا ما يقول متى يلزمان.
   * فالطريق الوحيد هو أن تُرسِل، وتقرأ الرمز، وتتصرّف عليه — والرسائل الواصلة
   * **تسمّي الحساب والدفتر المساعد والبُعد الناقص**، فهي صالحة للتصرّف فعلاً.
   */
  const problemCode = error instanceof ProblemError ? error.code : null;
  const unbalanced = problemCode === "ledger.posting.unbalanced";
  const needsParty = problemCode === "ledger.posting.missing_subledger";
  const needsDimension = problemCode?.startsWith("ledger.posting.guard.") ?? false;

  if (config.companyId === "") {
    return <ChooseCompanyFirst />;
  }

  return (
    <section className="stack" data-testid="voucher-screen">
      <header className="statline">
        <h1 style={{ margin: 0, fontSize: "var(--font-size-h1)", fontFamily: "var(--font-display)" }}>
          {t("screen.voucher.title")}
        </h1>
        <span className="pill" data-testid="voucher-event" title={MANUAL_VOUCHER_EVENT}>
          {MANUAL_VOUCHER_EVENT}
        </span>
      </header>

      <p className="muted">{t("screen.voucher.lede")}</p>

      {receipt ? (
        <ReceiptPanel
          receipt={receipt}
          onNew={startNew}
          onRepeat={() => void submit()}
          onOpenTrialBalance={() => openInTrialBalance(receipt)}
          busy={busy}
        />
      ) : null}

      <div className="card card-pad">
        <div className="grid fields-3">
          <div className="field">
            <label htmlFor="jv-date">{t("field.entryDate.label")}</label>
            <input
              id="jv-date"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="voucher-date"
              value={documentDate}
              onChange={(e) => setDocumentDate(e.target.value)}
            />
            <span className="hint">{t("screen.voucher.dateHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="jv-book">{t("field.book.label")}</label>
            <input id="jv-book" className="ctl mono" data-testid="voucher-book" value={config.book} readOnly />
            <span className="hint">{t("screen.voucher.bookHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="jv-key">{t("screen.voucher.idempotencyKey")}</label>
            <input id="jv-key" className="ctl mono" dir="ltr" data-testid="voucher-key" value={idempotencyKey} readOnly />
            <span className="hint">{t("screen.voucher.idempotencyHint")}</span>
          </div>
        </div>

        <div className="grid fields-half" style={{ marginTop: "var(--space-12)" }}>
          <div className="field">
            <label htmlFor="jv-memo-ar">{t("screen.voucher.narrationAr")}</label>
            <input
              id="jv-memo-ar"
              className="ctl"
              lang="ar"
              data-testid="voucher-memo-ar"
              value={narrationAr}
              onChange={(e) => setNarrationAr(e.target.value)}
              placeholder={t("field.memo.ph")}
            />
          </div>
          <div className="field">
            <label htmlFor="jv-memo-en">{t("screen.voucher.narrationEn")}</label>
            <input
              id="jv-memo-en"
              className="ctl"
              lang="en"
              dir="ltr"
              data-testid="voucher-memo-en"
              value={narrationEn}
              onChange={(e) => setNarrationEn(e.target.value)}
              placeholder={t("field.memo.ph")}
            />
            <span className="hint">{t("screen.voucher.narrationHint")}</span>
          </div>
        </div>
      </div>

      <div className="stack" data-testid="voucher-lines">
        {lines.map((line, index) => (
          <fieldset key={line.key} className="card card-pad" data-testid="voucher-line" data-line={line.key}>
            <legend className="k">
              <Num value={index + 1} />
            </legend>
            <div className="grid fields-4">
              <div className="field">
                <label htmlFor={"jv-role-" + line.key}>{t("screen.voucher.role")}</label>
                <select
                  id={"jv-role-" + line.key}
                  className="ctl mono"
                  data-testid="voucher-role"
                  value={line.role}
                  onChange={(e) => update(line.key, { role: e.target.value })}
                >
                  {ROLES.map((role) => (
                    <option key={role} value={role}>
                      {role}
                    </option>
                  ))}
                </select>
                <span className="hint">{t("screen.voucher.roleHint")}</span>
              </div>

              <div className="field">
                <label htmlFor={"jv-side-" + line.key}>{t("screen.voucher.side")}</label>
                <select
                  id={"jv-side-" + line.key}
                  className="ctl"
                  data-testid="voucher-side"
                  value={line.side}
                  onChange={(e) => update(line.key, { side: e.target.value })}
                >
                  {SIDES.map((side) => (
                    <option key={side} value={side}>
                      {t(SIDE_LABEL[side] as string)}
                    </option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor={"jv-amount-" + line.key}>{t("screen.voucher.amount")}</label>
                <input
                  id={"jv-amount-" + line.key}
                  className={
                    "ctl amt-input" +
                    (line.side === DEBIT ? " is-debit" : " is-credit") +
                    (badAmounts.includes(line.key) ? " is-invalid" : "")
                  }
                  inputMode="decimal"
                  autoComplete="off"
                  spellCheck={false}
                  dir="ltr"
                  aria-invalid={badAmounts.includes(line.key)}
                  data-testid="voucher-amount"
                  value={line.amount}
                  onChange={(e) => update(line.key, { amount: e.target.value })}
                  placeholder="0.0000"
                />
                <span className="hint">
                  {badAmounts.includes(line.key)
                    ? t("screen.voucher.amountBad")
                    : t("screen.voucher.amountHint")}
                </span>
              </div>

              <div className="field">
                <label htmlFor={"jv-cc-" + line.key}>{t("field.costCentre.label")}</label>
                <select
                  id={"jv-cc-" + line.key}
                  className="ctl"
                  data-testid="voucher-cost-center"
                  value={line.costCenter}
                  onChange={(e) => update(line.key, { costCenter: e.target.value })}
                >
                  <option value={DEFAULT_CENTER}>{t("screen.voucher.costCentreDefault")}</option>
                  {centres.map((centre) => (
                    <option key={centre.code} value={centre.code}>
                      {centre.nameAr}
                    </option>
                  ))}
                </select>
                <span className="hint">{t("screen.voucher.costCentreHint")}</span>
              </div>
            </div>

            <div className="grid fields-4" style={{ marginTop: "var(--space-10)" }}>
              <div className="field">
                <label htmlFor={"jv-branch-" + line.key}>{t("field.branch.label")}</label>
                <input
                  id={"jv-branch-" + line.key}
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="voucher-branch"
                  value={line.branchId}
                  onChange={(e) => update(line.key, { branchId: e.target.value })}
                  placeholder="BR-01"
                />
                <span className="hint">{t("screen.voucher.branchHint")}</span>
              </div>

              <div className="field">
                <label htmlFor={"jv-qual-" + line.key}>{t("screen.voucher.qualifier")}</label>
                <input
                  id={"jv-qual-" + line.key}
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="voucher-qualifier"
                  value={line.qualifier}
                  onChange={(e) => update(line.key, { qualifier: e.target.value })}
                />
                <span className="hint">{t("screen.voucher.qualifierHint")}</span>
              </div>

              <div className="field">
                <label htmlFor={"jv-sub-" + line.key}>{t("screen.voucher.subledger")}</label>
                <select
                  id={"jv-sub-" + line.key}
                  className="ctl"
                  data-testid="voucher-subledger-kind"
                  value={line.subledgerKind}
                  onChange={(e) => update(line.key, { subledgerKind: e.target.value })}
                >
                  {SUBLEDGER_KINDS.map((kind) => (
                    <option key={kind} value={kind}>
                      {kind}
                    </option>
                  ))}
                </select>
                <span className="hint">{t("screen.voucher.subledgerHint")}</span>
              </div>

              <div className="field">
                <label htmlFor={"jv-party-" + line.key}>{t("screen.voucher.party")}</label>
                <input
                  id={"jv-party-" + line.key}
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="voucher-party"
                  disabled={line.subledgerKind === NO_SUBLEDGER}
                  value={line.subledgerParty}
                  onChange={(e) => update(line.key, { subledgerParty: e.target.value })}
                  placeholder="BANK-0001"
                />
                <span className="hint">{t("screen.voucher.partyHint")}</span>
              </div>
            </div>

            <div className="inline-group" style={{ marginTop: "var(--space-10)" }}>
              <button
                type="button"
                className="btn btn-danger-soft"
                data-testid="voucher-remove-line"
                disabled={lines.length <= 2}
                onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
              >
                {t("common.action.deleteLine")}
              </button>
            </div>
          </fieldset>
        ))}

        <button
          type="button"
          className="addline"
          data-testid="voucher-add-line"
          onClick={() => setLines((current) => [...current, newLine(DEBIT, DEBIT_ROLE)])}
        >
          {t("common.action.addLine")}
        </button>
      </div>

      <div className="card card-pad">
        <p className="muted" data-testid="voucher-balance-note">
          {t("screen.voucher.balanceNote")}
        </p>
        <div className="inline-group">
          <button
            type="button"
            className="btn btn-primary"
            data-testid="voucher-post"
            disabled={!ready || busy}
            onClick={() => void submit()}
          >
            {busy ? t("common.state.loading") : t("common.action.post")}
          </button>
          <button type="button" className="btn" data-testid="voucher-reset" onClick={startNew}>
            {t("screen.voucher.newVoucher")}
          </button>
        </div>
      </div>

      {error ? (
        <>
          <ProblemPanel error={error} />
          {unbalanced ? (
            <p className="alert alert--warning" role="status" data-testid="voucher-unbalanced">
              {t("screen.voucher.unbalancedNext")}
            </p>
          ) : null}
          {needsParty ? (
            <p className="alert alert--warning" role="status" data-testid="voucher-needs-party">
              {t("screen.voucher.needsPartyNext")}
            </p>
          ) : null}
          {needsDimension ? (
            <p className="alert alert--warning" role="status" data-testid="voucher-needs-dimension">
              {t("screen.voucher.needsDimensionNext")}
            </p>
          ) : null}
        </>
      ) : null}
    </section>
  );
}

/** لوحة الإيصال — وتفرّق صراحةً بين ترحيل أول وإرسال ثانٍ بالمفتاح نفسه. */
function ReceiptPanel(props: {
  receipt: PostingReceipt;
  onNew: () => void;
  onRepeat: () => void;
  onOpenTrialBalance: () => void;
  busy: boolean;
}): ReactNode {
  const { t } = useT();
  const { receipt } = props;
  const again = receipt.alreadyPosted;

  return (
    <section
      className={"alert " + (again ? "alert--info" : "alert--success")}
      role="status"
      data-testid="voucher-receipt"
      data-already-posted={String(again)}
    >
      <h2 style={{ marginTop: 0 }}>
        {again ? t("screen.voucher.alreadyPosted") : t("screen.voucher.posted")}
      </h2>
      <p>{again ? t("screen.voucher.alreadyPostedBody") : t("screen.voucher.postedBody")}</p>

      <div className="kv">
        <div>
          <div className="k">{t("field.entryNo.label")}</div>
          <div className="v mono" data-testid="receipt-number">
            {receipt.entryNumber}
          </div>
        </div>
        <div>
          <div className="k">{t("field.periodCode.label")}</div>
          <div className="v mono" data-testid="receipt-period">
            {receipt.periodCode}
          </div>
        </div>
        <div>
          <div className="k">{t("screen.voucher.chainSequence")}</div>
          <div className="v mono" data-testid="receipt-sequence">
            {receipt.chainSequence}
          </div>
        </div>
        <div>
          <div className="k">{t("screen.voucher.lineCount")}</div>
          <div className="v" data-testid="receipt-lines">
            <Num value={receipt.lineCount} />
          </div>
        </div>
      </div>

      <div className="hint mono" dir="ltr" data-testid="receipt-hash">
        {receipt.entryHash}
      </div>

      <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
        <button
          type="button"
          className="btn btn-primary"
          data-testid="receipt-open-trial-balance"
          onClick={props.onOpenTrialBalance}
        >
          {t("screen.voucher.seeInTrialBalance")}
        </button>
        <button
          type="button"
          className="btn"
          data-testid="voucher-submit-again"
          disabled={props.busy}
          onClick={props.onRepeat}
        >
          {t("screen.voucher.submitAgain")}
        </button>
        <button type="button" className="btn" data-testid="voucher-new-after" onClick={props.onNew}>
          {t("screen.voucher.newVoucher")}
        </button>
      </div>
    </section>
  );
}

/** حين لا شركة مختارة: الطريق إلى الاختيار، لا حقل معرّف يُكتب بيد. */
function ChooseCompanyFirst(): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid="voucher-needs-company">
      <strong>{t("screen.voucher.needCompany")}</strong>
      <p className="muted">{t("screen.voucher.needCompanyBody")}</p>
      <Link to="/sign-in" className="btn btn-primary" data-testid="voucher-go-sign-in">
        {t("screen.signIn.action")}
      </Link>
    </section>
  );
}
