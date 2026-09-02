/* ═══════════════════════════════════════════════════════════════════════════
   /hr/social-insurance — سداد اشتراك التأمينات  ·  The social insurance payment
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة تضع رقمين متجاورين ولا تطرح أحدهما من الآخر.**

   المبلغ المسدَّد يصل من المستدعي: فاتورة الجهة قد تخالف ما استحقّته
   المسيّرات لأسبابٍ مشروعة — التحاقٌ متأخّر، أو تسويةُ شهرٍ سابق، أو خلافٌ
   على تصنيف. والقراءة تُعيد `accruedForPeriod` **إلى جانبه للمقارنة لا
   للإملاء**، فيُرى الفارق قبل الاعتماد بدل أن يُكتشَف عند المطابقة.

   **ولا يُحسب الفارق هنا.** طرحُ مبلغين عمليةٌ عشرية محاسبية، والمتصفّح ليس
   مكانها: `Number` يفقد الخانة الرابعة، والفرق المعروض يصير رقماً ثالثاً لا
   مصدر له في العقد. فالرقمان يُعرضان كما وصلا، والعين تقارن.

   ── دورةٌ من خطوتين، والثانية وحدها هي التي لا رجعة فيها ───────────────
   المسوّدة تُنشأ، فتُقرأ بمبلغها وباستحقاق فترتها، **ثم** تُرحَّل. وإعادةُ
   الترحيل آمنة: يعود الإيصال نفسه بـ`alreadyPosted = true` — وذلك **ليس
   خطأً**، ويُعرَض بلوحٍ يقول «لم يقع ترحيلٌ جديد» لا بلوح نجاحٍ ثانٍ، لأن
   «رُحِّل مرّتين» جملةٌ محاسبية كاذبة.

   ── وقيدٌ مفروضٌ بحكم البيانات يُقال قبل أن يُكتشف ──────────────────────
   الرواتب تُرحَّل بالريال السعودي حصراً: حساب التأمينات المستحقة معلَنٌ في
   دليل الحسابات بعملةٍ واحدة، فأي عملة أخرى يرفضها المخطِّط. ولا حقل عملةٍ
   في هذه الشاشة أصلاً — والغياب مشروحٌ لا مسكوتٌ عنه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import {
  draftSocialInsurancePayment,
  postSocialInsurancePayment,
  readSocialInsurancePayment,
} from "../../api/generated/client";
import type {
  HrSocialInsurancePayment,
  HrSocialInsurancePaymentRequest,
} from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { PARAM_readTrialBalance_period_RE } from "../../api/generated/formats";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, RefusalPanel, StatCard, useMoment } from "../../ui";
import {
  ChooseCompanyFirst,
  EntryRef,
  HrSectionNav,
  HrState,
  isMoneyText,
  todayIso,
} from "./parts";
import { DUPLICATE_NUMBER, POSTED, SOCIAL_INSURANCE_METHODS, TREASURY_MISSING } from "./contract";
import "./hr.css";

/** طريقة التسوية كما يقبلها العقد. */
type Method = HrSocialInsurancePaymentRequest["settlementMethod"];

/** الشاشة كاملةً. */
export function SocialInsuranceScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const [arriveCls, fireArrive] = useMoment("arrive");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [postCls, firePost] = useMoment("post");

  const [number, setNumber] = useState("");
  const [periodCode, setPeriodCode] = useState("");
  const [amount, setAmount] = useState("");
  const [paidOn, setPaidOn] = useState(todayIso);
  const [method, setMethod] = useState<Method>((SOCIAL_INSURANCE_METHODS[0] ?? "") as Method);
  const [treasury, setTreasury] = useState("");
  const [lookup, setLookup] = useState("");

  const [payment, setPayment] = useState<HrSocialInsurancePayment | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postFailure, setPostFailure] = useState<unknown>(null);

  const periodValid = periodCode === "" || PARAM_readTrialBalance_period_RE.test(periodCode);

  const ready =
    number.trim() !== "" &&
    PARAM_readTrialBalance_period_RE.test(periodCode) &&
    isMoneyText(amount) &&
    paidOn !== "" &&
    treasury.trim() !== "";

  const draft = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    setPostFailure(null);
    try {
      const created = await draftSocialInsurancePayment(transport, {
        companyId: config.companyId,
        body: {
          number: number.trim(),
          periodCode,
          /* المبلغ نصٌّ محتجَز بنحو العقد، ولا يمرّ برقمٍ عائم في أي خطوة. */
          amount: Money.wire(amount),
          paidOn,
          settlementMethod: method,
          treasuryPartyId: treasury.trim(),
        },
      });
      setPayment(created);
      fireArrive();
    } catch (problem) {
      setFailure(problem);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [amount, config.companyId, fireArrive, fireRefuse, method, number, paidOn, periodCode, transport, treasury]);

  const open = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    setPostFailure(null);
    try {
      const found = await readSocialInsurancePayment(transport, {
        companyId: config.companyId,
        paymentId: lookup.trim(),
      });
      setPayment(found);
      fireArrive();
    } catch (problem) {
      setFailure(problem);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, lookup, transport]);

  const post = useCallback(async () => {
    if (!payment) return;
    setPostBusy(true);
    setPostFailure(null);
    try {
      const settled = await postSocialInsurancePayment(transport, {
        companyId: config.companyId,
        paymentId: payment.id,
      });
      setPayment(settled);
      firePost();
    } catch (problem) {
      setPostFailure(problem);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, firePost, fireRefuse, payment, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-si-needs-company" />;

  const duplicate = failure instanceof ProblemError && failure.code === DUPLICATE_NUMBER;
  const treasuryMissing =
    postFailure instanceof ProblemError && postFailure.code === TREASURY_MISSING;

  return (
    <section className="stack" data-testid="hr-social-insurance-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.socialTitle")}</h1>
          <p className="sub">{t("hr.page.socialLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/social-insurance" />

      <Panel title={t("hr.si.title")} note={t("hr.si.note")} testId="hr-si-new">
        <div className="grid fields-4">
          <Field id="hr-si-number" label={t("hr.field.number")} hint={t("hr.field.numberHint")} source="typed" required>
            <input
              id="hr-si-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-si-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
              placeholder="GOSI-2026-06"
            />
          </Field>
          <Field
            id="hr-si-period"
            label={t("hr.field.periodCode")}
            hint={t("hr.si.periodHint")}
            error={periodValid ? undefined : t("hr.field.periodBad")}
            source="typed"
            required
          >
            <input
              id="hr-si-period"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              aria-invalid={!periodValid}
              data-testid="hr-si-period"
              value={periodCode}
              onChange={(e) => setPeriodCode(e.target.value)}
              placeholder="2026-06"
            />
          </Field>
          <Field
            id="hr-si-amount"
            label={t("hr.field.amount")}
            hint={t("hr.si.amountHint")}
            error={amount !== "" && !isMoneyText(amount) ? t("hr.field.amountBad") : undefined}
            source="typed"
            required
          >
            <input
              id="hr-si-amount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={amount !== "" && !isMoneyText(amount)}
              data-testid="hr-si-amount"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="0.0000"
            />
          </Field>
          <Field id="hr-si-paid" label={t("hr.field.paidOn")} hint={t("hr.si.paidOnHint")} source="typed" required>
            <input
              id="hr-si-paid"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="hr-si-paid-on"
              value={paidOn}
              onChange={(e) => setPaidOn(e.target.value)}
            />
          </Field>
        </div>

        <div className="grid fields-2">
          <Field
            id="hr-si-treasury"
            label={t("hr.field.treasuryParty")}
            hint={t("hr.field.treasuryPartyHint")}
            source="typed"
            required
          >
            <input
              id="hr-si-treasury"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-si-treasury"
              value={treasury}
              onChange={(e) => setTreasury(e.target.value)}
            />
          </Field>
          <Field
            id="hr-si-method"
            label={t("hr.field.settlementMethod")}
            hint={t("hr.field.settlementMethodHint")}
            source="typed"
            required
          >
            <select
              id="hr-si-method"
              className="ctl"
              data-testid="hr-si-method"
              value={method}
              onChange={(e) => setMethod(e.target.value as Method)}
            >
              {SOCIAL_INSURANCE_METHODS.map((entry) => (
                <option key={entry} value={entry}>
                  {t("hr.method." + entry)}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="inline-group">
          <Button
            label={t("hr.act.draftSiPayment")}
            kind="primary"
            loading={busy}
            disabled={!ready || busy}
            onClick={() => void draft()}
            testId="hr-si-draft"
          />
        </div>
        <p className="hint" data-testid="hr-si-currency">{t("hr.si.currencyNote")}</p>
      </Panel>

      <Panel title={t("hr.si.lookup")} note={t("hr.si.lookupNote")} testId="hr-si-lookup">
        <div className="grid fields-2">
          <Field id="hr-si-open" label={t("hr.field.paymentId")} hint={t("hr.field.paymentIdHint")} source="typed">
            <input
              id="hr-si-open"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-si-lookup-id"
              value={lookup}
              onChange={(e) => setLookup(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && lookup.trim() !== "") void open();
              }}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.read")}
              disabled={lookup.trim() === "" || busy}
              onClick={() => void open()}
              testId="hr-si-open"
            />
          </div>
        </div>
      </Panel>

      {duplicate ? (
        <RefusalPanel
          title={t("hr.refusal.duplicateTitle")}
          body={t("hr.refusal.duplicateBody")}
          code={DUPLICATE_NUMBER}
          codeLabel={t("common.problem.code")}
          next={t("hr.refusal.duplicateNext")}
          moment={refuseCls}
          testId="hr-si-duplicate"
        />
      ) : failure ? (
        <div className={refuseCls}>
          <ProblemPanel error={failure} />
        </div>
      ) : null}

      {payment === null ? (
        <EmptyState title={t("hr.si.emptyTitle")} body={t("hr.si.emptyBody")} testId="hr-si-empty" />
      ) : (
        <Panel
          title={t("hr.si.card")}
          note={t("hr.si.cardNote")}
          aside={<HrState state={payment.state} testId="hr-si-state" />}
          testId="hr-si-card"
        >
          <div className="kv">
            <div>
              <div className="k">{t("hr.field.number")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-si-card-number">{payment.number}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.periodCode")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-si-card-period">{payment.periodCode}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.paidOn")}</div>
              <div className="v mono" dir="ltr">{payment.paidOn}</div>
            </div>
            <div>
              <div className="k">{t("hr.entry.label")}</div>
              <div className="v">
                <EntryRef entryId={payment.entryId} testId="hr-si-entry" />
              </div>
            </div>
          </div>

          {/* الرقمان متجاوران — ولا ثالث بينهما محسوبٌ في المتصفّح. */}
          <div className={"stats-row " + arriveCls}>
            <StatCard
              label={t("hr.si.paidAmount")}
              amount={payment.amount}
              tone="debit"
              hint={t("hr.si.paidAmountHint")}
              testId="hr-si-amount-out"
            />
            <StatCard
              label={t("hr.si.accrued")}
              amount={payment.accruedForPeriod}
              tone="credit"
              hint={t("hr.si.accruedHint")}
              testId="hr-si-accrued"
            />
          </div>
          <p className="hint" data-testid="hr-si-compare">{t("hr.si.compareNote")}</p>

          <h3 className="hr-split">{t("hr.si.postTitle")}</h3>
          <p className="muted">{t("hr.si.postNote")}</p>
          <div className="inline-group">
            <Button
              label={payment.state === POSTED ? t("hr.act.postAgain") : t("hr.act.postSiPayment")}
              kind="primary"
              loading={postBusy}
              disabled={postBusy}
              onClick={() => void post()}
              testId="hr-si-post"
            />
          </div>

          {treasuryMissing ? (
            <RefusalPanel
              title={t("hr.refusal.treasuryTitle")}
              body={t("hr.refusal.treasuryBody")}
              code={TREASURY_MISSING}
              codeLabel={t("common.problem.code")}
              next={t("hr.refusal.treasuryNext")}
              moment={refuseCls}
              testId="hr-si-treasury-refusal"
            />
          ) : postFailure ? (
            <div className={refuseCls}>
              <ProblemPanel error={postFailure} />
            </div>
          ) : null}

          {payment.state === POSTED ? (
            <div
              className={"hr-receipt " + postCls}
              data-already={payment.alreadyPosted ? "true" : "false"}
              data-state={payment.state}
              data-testid="hr-si-receipt"
            >
              <h2>{payment.alreadyPosted ? t("hr.si.again") : t("hr.si.done")}</h2>
              <p>{payment.alreadyPosted ? t("hr.si.againBody") : t("hr.si.doneBody")}</p>
              <div className="kv">
                <div>
                  <div className="k">{t("hr.entry.label")}</div>
                  <div className="v">
                    <EntryRef entryId={payment.entryId} testId="hr-si-receipt-entry" />
                  </div>
                </div>
                <div>
                  <div className="k">{t("hr.payslip.alreadyPosted")}</div>
                  <div className="v" data-testid="hr-si-already">
                    {payment.alreadyPosted ? t("hr.payslip.alreadyYes") : t("hr.payslip.alreadyNo")}
                  </div>
                </div>
              </div>
              <p className="hint">{t("hr.run.idempotencyNote")}</p>
            </div>
          ) : null}
        </Panel>
      )}
    </section>
  );
}
