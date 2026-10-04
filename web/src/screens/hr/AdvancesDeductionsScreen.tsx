/* ═══════════════════════════════════════════════════════════════════════════
   /hr/advances-deductions — السلف والاستقطاعات  ·  Advances and deductions
   ───────────────────────────────────────────────────────────────────────────
   **شاشةٌ واحدة لبابين، لأن العمودين على القسيمة واحدٌ في المعنى.**

   المبالغ الستّة على كل قسيمة فيها عمودان يُنقصان الصافي: `advanceInstalment`
   و`deductions`. ومصدرُهما بابان منفصلان في العقد — السلفة والاستقطاع — لكن
   من يجلس ليكتبهما **إنسانٌ واحد في لحظةٍ واحدة**: قبل إغلاق مسيّر الشهر،
   وهو يمرّ على ما يُقتطع من كل موظف. وفصلُهما شاشتين يجعله يفتح اثنتين
   ليجيب سؤالاً واحداً: «ماذا يُقتطع من هذا الرجل هذا الشهر؟»

   وهما مع ذلك **مستندان مختلفان محاسبياً، فلوحان لا لوحٌ واحد**:
     · السلفة **أصل** يُقسَّط على فتراتٍ لاحقة، وجدولها يُنشأ مرّةً ويُستهلك
       قسطاً قسطاً — و`consumedByPayslipId` على كل قسط يقول أيّ قسيمةٍ أكلته.
     · والاستقطاع **خصمٌ لفترةٍ واحدة** بمعتمِدٍ وتاريخ اعتماد ومفتاح فئة،
       ويُستهلك مرّةً واحدة.

   ── وما ليس على هذه الشاشة ولا يجوز أن يكون ────────────────────────────
   **لا زرَّ ترحيلٍ على أيٍّ منهما، والغياب مُعلَنٌ لا مسكوتٌ عنه.**
     · الاستقطاع يُرحَّل **داخل المسيّر** لا بذاته، ولذلك لا يحمل جوابُه
       `entryId` ولا `alreadyPosted` أصلاً — وشاشةٌ بزرّ ترحيلٍ له كانت
       ستَعِد بفعلٍ لا مورد له (وهو نصّ ما عالجه ADR-0047 في أمر الشراء).
     · والسلفة **لا موردَ ترحيلٍ لها في العقد**، لأن حدث صرفها غير موجود في
       مصفوفة الترحيل — والمحرك يرفض رمزاً لا يعرفه ولا يخترع قالباً. وثمنُ
       ذلك عطلٌ محاسبي حقيقي يُقال هنا صراحةً: السلفة **تُقسَّط ولا تُصرَف**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import {
  draftEmployeeAdvance,
  readEmployeeAdvance,
  readEmployeeDeduction,
  recordEmployeeDeduction,
} from "../../api/generated/client";
import type { HrAdvance, HrAdvanceRequest, HrDeduction } from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { PARAM_readTrialBalance_period_RE } from "../../api/generated/formats";
import { useApi } from "../../app/api-context";
import { EmployeePicker } from "../../app/pickers";
import {
  PRESET,
  useCurrentMemberName,
  useDocumentNumber,
  useFillFrom,
  usePresetFill,
  useSimple,
} from "../../app/presets";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, RefusalPanel, StatCard, useMoment } from "../../ui";
import { useHrFocus } from "./focus";
import {
  ChooseCompanyFirst,
  DeclaredGap,
  HrSectionNav,
  HrState,
  OpaqueCode,
  isMoneyText,
  monthPeriod,
  splitEqualMoney,
  todayIso,
} from "./parts";
import { ADVANCE_METHODS, DUPLICATE_NUMBER } from "./contract";
import "./hr.css";

/** طريقة صرف السلفة كما يقبلها العقد. */
type Method = HrAdvanceRequest["settlementMethod"];

/** قسطٌ كما يعبر: فترته ومبلغه — **والمبلغ نصّ**. */
interface InstalmentLine {
  readonly periodCode: string;
  readonly amount: string;
}

/** قسطٌ كما يُكتب في «كل الشاشات»، بمفتاح صفّه. */
interface InstalmentDraft extends InstalmentLine {
  key: string;
}

/** أقصى عددٍ من الأقساط المتساوية في المبسّطة: سنة. */
const MAX_INSTALMENTS = 12;

let sequence = 0;
function newInstalment(): InstalmentDraft {
  sequence += 1;
  return { key: "i" + String(sequence), periodCode: "", amount: "" };
}

/** الشاشة كاملةً. */
export function AdvancesDeductionsScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [focus, setFocus] = useHrFocus();

  const [arriveCls, fireArrive] = useMoment("arrive");
  const [refuseCls, fireRefuse] = useMoment("refuse");

  /* ── السلفة ───────────────────────────────────────────────────────── */
  const [advNumber, setAdvNumber] = useState("");
  const [advEmployee, setAdvEmployee] = useState(focus.employeeId);
  const [advAmount, setAdvAmount] = useState("");
  const [advIssuedOn, setAdvIssuedOn] = useState(todayIso);
  const [advMethod, setAdvMethod] = useState<Method>((ADVANCE_METHODS[0] ?? "") as Method);
  const [advTreasury, setAdvTreasury] = useState("");
  const [instalments, setInstalments] = useState<InstalmentDraft[]>(() => [newInstalment()]);
  const [advance, setAdvance] = useState<HrAdvance | null>(null);
  const [advBusy, setAdvBusy] = useState(false);
  const [advError, setAdvError] = useState<unknown>(null);
  const [advLookup, setAdvLookup] = useState("");

  /* ── الواجهة المبسّطة (ADR-0095): الرقم من الخادم، والصرف اليوم، والخزينة وطريقتها
     من الثوابت، والأقساط عددٌ يُختار فتُقسم بالهللة من الشهر القادم — بلا عائم. ── */
  const simple = useSimple();
  const advanceNumber = useDocumentNumber("employee_advance");
  const hideAdvMethod = usePresetFill(PRESET.hrSettlementMethod, advMethod, (next) => setAdvMethod(next as Method));
  const hideAdvTreasury = usePresetFill(PRESET.hrTreasury, advTreasury, setAdvTreasury);
  const [instalmentCount, setInstalmentCount] = useState(1);
  /* فترات الأشهر القادمة تُحسب مرّةً عند الفتح — من التاريخ لا من نصّ. */
  const [nextPeriods] = useState(() => Array.from({ length: MAX_INSTALMENTS }, (_, i) => monthPeriod(i + 1).code));
  const autoInstalments = useMemo<readonly InstalmentLine[]>(
    () => splitEqualMoney(advAmount, instalmentCount).map((amount, i) => ({ periodCode: nextPeriods[i] ?? "", amount })),
    [advAmount, instalmentCount, nextPeriods]
  );
  const scheduled: readonly InstalmentLine[] = simple ? autoInstalments : instalments;

  /* ── الاستقطاع ────────────────────────────────────────────────────── */
  const [dedEmployee, setDedEmployee] = useState(focus.employeeId);
  const [dedAmount, setDedAmount] = useState("");
  const [dedPeriod, setDedPeriod] = useState("");
  const [dedCategory, setDedCategory] = useState("");
  const [dedBy, setDedBy] = useState("");
  const [dedOn, setDedOn] = useState(todayIso);
  const [deduction, setDeduction] = useState<HrDeduction | null>(null);
  const [dedBusy, setDedBusy] = useState(false);
  const [dedError, setDedError] = useState<unknown>(null);
  const [dedLookup, setDedLookup] = useState("");
  /* المعتمِد: صاحبُ الجلسة باسمه، والاعتماد اليوم (ADR-0095). */
  const hideDedBy = useFillFrom(useCurrentMemberName(), dedBy, setDedBy);

  const dedPeriodValid = dedPeriod === "" || PARAM_readTrialBalance_period_RE.test(dedPeriod);

  const advanceReady =
    (advNumber.trim() !== "" || advanceNumber.hidden) &&
    advEmployee.trim() !== "" &&
    isMoneyText(advAmount) &&
    advIssuedOn !== "" &&
    advTreasury.trim() !== "" &&
    scheduled.length > 0 &&
    scheduled.every(
      (line) => PARAM_readTrialBalance_period_RE.test(line.periodCode) && isMoneyText(line.amount)
    );

  const deductionReady =
    dedEmployee.trim() !== "" &&
    isMoneyText(dedAmount) &&
    PARAM_readTrialBalance_period_RE.test(dedPeriod) &&
    dedCategory.trim() !== "" &&
    dedBy.trim() !== "" &&
    dedOn !== "";

  const submitAdvance = useCallback(async () => {
    setAdvBusy(true);
    setAdvError(null);
    try {
      const resolvedNumber = await advanceNumber.resolve(advNumber.trim(), advIssuedOn);
      const created = await draftEmployeeAdvance(transport, {
        companyId: config.companyId,
        body: {
          number: resolvedNumber,
          employeeId: advEmployee.trim(),
          /* **المبلغ يعبر نصّاً محتجَزاً بنحو العقد** — ولا يمرّ برقمٍ عائم
             في أي خطوة، ولا يُجمع مجموعُ الأقساط هنا: تساوي المجموع بالمبلغ
             قيدٌ عشريّ يفرضه الخادم، وجمعُه في متصفّح حسابٌ محاسبي في المكان
             الخطأ — ويُخفي رفضاً مشروعاً بدل أن يُظهره. */
          amount: Money.wire(advAmount),
          issuedOn: advIssuedOn,
          settlementMethod: advMethod,
          treasuryPartyId: advTreasury.trim(),
          instalments: scheduled.map((line) => ({
            periodCode: line.periodCode,
            amount: Money.wire(line.amount),
          })),
        },
      });
      setAdvance(created);
      setFocus({ employeeId: created.employeeId });
      fireArrive();
    } catch (problem) {
      setAdvError(problem);
      fireRefuse();
    } finally {
      setAdvBusy(false);
    }
  }, [
    advAmount,
    advEmployee,
    advIssuedOn,
    advMethod,
    advNumber,
    advTreasury,
    advanceNumber,
    config.companyId,
    fireArrive,
    fireRefuse,
    scheduled,
    setFocus,
    transport,
  ]);

  const openAdvance = useCallback(async () => {
    setAdvBusy(true);
    setAdvError(null);
    try {
      const found = await readEmployeeAdvance(transport, {
        companyId: config.companyId,
        advanceId: advLookup.trim(),
      });
      setAdvance(found);
      fireArrive();
    } catch (problem) {
      setAdvError(problem);
      fireRefuse();
    } finally {
      setAdvBusy(false);
    }
  }, [advLookup, config.companyId, fireArrive, fireRefuse, transport]);

  const submitDeduction = useCallback(async () => {
    setDedBusy(true);
    setDedError(null);
    try {
      const created = await recordEmployeeDeduction(transport, {
        companyId: config.companyId,
        body: {
          employeeId: dedEmployee.trim(),
          amount: Money.wire(dedAmount),
          periodCode: dedPeriod,
          categoryKey: dedCategory.trim(),
          approvedBy: dedBy.trim(),
          approvedOn: dedOn,
        },
      });
      setDeduction(created);
      setFocus({ employeeId: created.employeeId });
      fireArrive();
    } catch (problem) {
      setDedError(problem);
      fireRefuse();
    } finally {
      setDedBusy(false);
    }
  }, [
    config.companyId,
    dedAmount,
    dedBy,
    dedCategory,
    dedEmployee,
    dedOn,
    dedPeriod,
    fireArrive,
    fireRefuse,
    setFocus,
    transport,
  ]);

  const openDeduction = useCallback(async () => {
    setDedBusy(true);
    setDedError(null);
    try {
      const found = await readEmployeeDeduction(transport, {
        companyId: config.companyId,
        deductionId: dedLookup.trim(),
      });
      setDeduction(found);
      fireArrive();
    } catch (problem) {
      setDedError(problem);
      fireRefuse();
    } finally {
      setDedBusy(false);
    }
  }, [config.companyId, dedLookup, fireArrive, fireRefuse, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-advances-needs-company" />;

  const advDuplicate = advError instanceof ProblemError && advError.code === DUPLICATE_NUMBER;

  return (
    <section className="stack" data-testid="hr-advances-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.advancesTitle")}</h1>
          <p className="sub">{t("hr.page.advancesLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/advances-deductions" />

      {/* ═══════════════════════════════════════════════ ١ · السلفة ═════ */}
      <Panel title={t("hr.advance.title")} note={t("hr.advance.note")} testId="hr-advance-new">
        <div className="grid fields-4">
          {advanceNumber.hidden ? null : (
          <Field id="hr-a-number" label={t("hr.field.number")} hint={t("hr.field.numberHint")} source="typed" required>
            <input
              id="hr-a-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-advance-number"
              value={advNumber}
              onChange={(e) => setAdvNumber(e.target.value)}
              placeholder="ADV-2026-0001"
            />
          </Field>
          )}
          <Field
            id="hr-a-employee"
            label={t("hr.field.employeeId")}
            hint={t("hr.field.employeeIdHint")}
            source="typed"
            required
          >
            <EmployeePicker id="hr-a-employee" value={advEmployee} onChange={setAdvEmployee} testId="hr-advance-employee" />
          </Field>
          <Field
            id="hr-a-amount"
            label={t("hr.field.amount")}
            hint={t("hr.advance.amountHint")}
            error={advAmount !== "" && !isMoneyText(advAmount) ? t("hr.field.amountBad") : undefined}
            source="typed"
            required
          >
            <input
              id="hr-a-amount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={advAmount !== "" && !isMoneyText(advAmount)}
              data-testid="hr-advance-amount"
              value={advAmount}
              onChange={(e) => setAdvAmount(e.target.value)}
              placeholder="0.0000"
            />
          </Field>
          {simple ? null : (
          <Field
            id="hr-a-issued"
            label={t("hr.field.issuedOn")}
            hint={t("hr.field.issuedOnHint")}
            source="typed"
            required
          >
            <input
              id="hr-a-issued"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="hr-advance-issued"
              value={advIssuedOn}
              onChange={(e) => setAdvIssuedOn(e.target.value)}
            />
          </Field>
          )}
        </div>

        {hideAdvTreasury && hideAdvMethod ? null : (
        <div className="grid fields-2">
          {hideAdvTreasury ? null : (
          <Field
            id="hr-a-treasury"
            label={t("hr.field.treasuryParty")}
            hint={t("hr.advance.treasuryHint")}
            source="typed"
            required
          >
            <input
              id="hr-a-treasury"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-advance-treasury"
              value={advTreasury}
              onChange={(e) => setAdvTreasury(e.target.value)}
            />
          </Field>
          )}
          {hideAdvMethod ? null : (
          <Field
            id="hr-a-method"
            label={t("hr.field.settlementMethod")}
            hint={t("hr.advance.methodHint")}
            source="typed"
            required
          >
            <select
              id="hr-a-method"
              className="ctl"
              data-testid="hr-advance-method"
              value={advMethod}
              onChange={(e) => setAdvMethod(e.target.value as Method)}
            >
              {ADVANCE_METHODS.map((method) => (
                <option key={method} value={method}>
                  {t("hr.method." + method)}
                </option>
              ))}
            </select>
          </Field>
          )}
        </div>
        )}

        <h3 className="hr-split">{t("hr.advance.instalments")}</h3>
        <p className="muted">{t("hr.advance.instalmentsNote")}</p>
        {simple ? (
        <>
        <div className="grid fields-2">
          <Field
            id="hr-a-count"
            label={t("hr.field.instalmentCount")}
            hint={t("hr.field.instalmentCountHint")}
            source="typed"
            required
          >
            <select
              id="hr-a-count"
              className="ctl"
              data-testid="hr-advance-instalment-count"
              value={String(instalmentCount)}
              onChange={(e) => {
                /* القيمة من قائمتنا نحن، فالمطابقة بالنصّ لا بتحويلٍ رقمي. */
                const picked = Array.from({ length: MAX_INSTALMENTS }, (_, i) => i + 1).find(
                  (n) => String(n) === e.target.value
                );
                setInstalmentCount(picked ?? 1);
              }}
            >
              {Array.from({ length: MAX_INSTALMENTS }, (_, i) => i + 1).map((n) => (
                <option key={n} value={String(n)}>
                  {tp("hr.count.instalments", n)}
                </option>
              ))}
            </select>
          </Field>
        </div>
        {autoInstalments.length > 0 ? (
        <div className="hr-table" data-testid="hr-advance-instalments">
          <table>
            <caption className="visually-hidden">{t("hr.advance.instalments")}</caption>
            <thead>
              <tr>
                <th scope="col" className="n">{t("hr.payslip.lineNo")}</th>
                <th scope="col">{t("hr.field.periodCode")}</th>
                <th scope="col" className="n">{t("hr.field.amount")}</th>
              </tr>
            </thead>
            <tbody>
              {autoInstalments.map((line, i) => (
                <tr key={line.periodCode}>
                  <td className="n"><Num value={i + 1} /></td>
                  <td><span className="mono" dir="ltr" data-testid="hr-instalment-period">{line.periodCode}</span></td>
                  <td className="n" data-testid="hr-instalment-amount"><Amount value={Money.wire(line.amount)} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        ) : null}
        </>
        ) : (
        <div className="hr-lines" data-testid="hr-advance-instalments">
          {instalments.map((line) => (
            <div key={line.key} className="hr-line">
              <Field
                id={"hr-a-period-" + line.key}
                label={t("hr.field.periodCode")}
                hint={t("hr.field.periodHint")}
                error={
                  line.periodCode !== "" && !PARAM_readTrialBalance_period_RE.test(line.periodCode)
                    ? t("hr.field.periodBad")
                    : undefined
                }
                source="typed"
                required
              >
                <input
                  id={"hr-a-period-" + line.key}
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  aria-invalid={
                    line.periodCode !== "" && !PARAM_readTrialBalance_period_RE.test(line.periodCode)
                  }
                  data-testid="hr-instalment-period"
                  value={line.periodCode}
                  onChange={(e) =>
                    setInstalments((current) =>
                      current.map((x) => (x.key === line.key ? { ...x, periodCode: e.target.value } : x))
                    )
                  }
                  placeholder="2026-07"
                />
              </Field>
              <Field
                id={"hr-a-inst-" + line.key}
                label={t("hr.field.amount")}
                hint={t("hr.field.amountHint")}
                error={line.amount !== "" && !isMoneyText(line.amount) ? t("hr.field.amountBad") : undefined}
                source="typed"
                required
              >
                <input
                  id={"hr-a-inst-" + line.key}
                  className="ctl amt-input"
                  inputMode="decimal"
                  dir="ltr"
                  autoComplete="off"
                  aria-invalid={line.amount !== "" && !isMoneyText(line.amount)}
                  data-testid="hr-instalment-amount"
                  value={line.amount}
                  onChange={(e) =>
                    setInstalments((current) =>
                      current.map((x) => (x.key === line.key ? { ...x, amount: e.target.value } : x))
                    )
                  }
                  placeholder="0.0000"
                />
              </Field>
              <div className="rowctl hr-act">
                <Button
                  label={t("hr.act.removeInstalment")}
                  size="sm"
                  disabled={instalments.length === 1}
                  onClick={() =>
                    setInstalments((current) => current.filter((x) => x.key !== line.key))
                  }
                  testId="hr-instalment-remove"
                />
              </div>
            </div>
          ))}
        </div>
        )}
        <div className="inline-group">
          {simple ? null : (
          <Button
            label={t("hr.act.addInstalment")}
            onClick={() => setInstalments((current) => [...current, newInstalment()])}
            testId="hr-instalment-add"
          />
          )}
          <Button
            label={t("hr.act.draftAdvance")}
            kind="primary"
            loading={advBusy}
            disabled={!advanceReady || advBusy}
            onClick={() => void submitAdvance()}
            testId="hr-advance-submit"
          />
        </div>

        <DeclaredGap
          title={t("hr.advance.noPostingTitle")}
          body={t("hr.advance.noPostingBody")}
          owed={t("hr.advance.noPostingOwed")}
          testId="hr-gap-advance-posting"
        />
      </Panel>

      <Panel title={t("hr.advance.lookup")} note={t("hr.advance.lookupNote")} testId="hr-advance-lookup">
        <div className="grid fields-2">
          <Field
            id="hr-a-open"
            label={t("hr.field.advanceId")}
            hint={t("hr.field.advanceIdHint")}
            source="typed"
          >
            <input
              id="hr-a-open"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-advance-lookup-id"
              value={advLookup}
              onChange={(e) => setAdvLookup(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && advLookup.trim() !== "") void openAdvance();
              }}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.read")}
              disabled={advLookup.trim() === "" || advBusy}
              onClick={() => void openAdvance()}
              testId="hr-advance-open"
            />
          </div>
        </div>
      </Panel>

      {advDuplicate ? (
        <RefusalPanel
          title={t("hr.refusal.duplicateTitle")}
          body={t("hr.refusal.duplicateBody")}
          code={DUPLICATE_NUMBER}
          codeLabel={t("common.problem.code")}
          next={t("hr.refusal.duplicateNext")}
          moment={refuseCls}
          testId="hr-advance-duplicate"
        />
      ) : advError ? (
        <div className={refuseCls}>
          <ProblemPanel error={advError} />
        </div>
      ) : null}

      {advance ? (
        <Panel
          title={t("hr.advance.card")}
          note={t("hr.advance.cardNote")}
          aside={<HrState state={advance.state} testId="hr-advance-state" />}
          testId="hr-advance-card"
        >
          <div className="kv">
            <div>
              <div className="k">{t("hr.field.number")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-advance-card-number">{advance.number}</div>
            </div>
            <div>
              <div className="k">{t("hr.code.label")}</div>
              <div className="v">
                <OpaqueCode code={advance.employeeCode} testId="hr-advance-code" />
              </div>
            </div>
            <div>
              <div className="k">{t("hr.field.issuedOn")}</div>
              <div className="v mono" dir="ltr">{advance.issuedOn}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.settlementMethod")}</div>
              <div className="v">{t("hr.method." + advance.settlementMethod)}</div>
            </div>
          </div>

          <div className={"stats-row " + arriveCls}>
            <StatCard label={t("hr.field.amount")} amount={advance.amount} tone="debit" testId="hr-advance-amount-out" />
            <StatCard
              label={t("hr.advance.outstanding")}
              amount={advance.outstandingAmount}
              tone="credit"
              hint={t("hr.advance.outstandingHint")}
              testId="hr-advance-outstanding"
            />
          </div>

          <h3 className="hr-split">{t("hr.advance.instalments")}</h3>
          <p className="muted">{tp("hr.count.instalments", advance.instalments.length)}</p>
          <div className="hr-table" data-testid="hr-advance-schedule">
            <table>
              <caption className="visually-hidden">{t("hr.advance.instalments")}</caption>
              <thead>
                <tr>
                  <th scope="col" className="n">{t("hr.payslip.lineNo")}</th>
                  <th scope="col">{t("hr.field.periodCode")}</th>
                  <th scope="col" className="n">{t("hr.field.amount")}</th>
                  <th scope="col">{t("hr.advance.consumed")}</th>
                </tr>
              </thead>
              <tbody>
                {advance.instalments.map((line) => (
                  <tr key={line.lineNo}>
                    <td className="n"><Num value={line.lineNo} /></td>
                    <td><span className="mono" dir="ltr">{line.periodCode}</span></td>
                    <td className="n"><Amount value={line.amount} /></td>
                    <td data-testid="hr-instalment-consumed">
                      {line.consumedByPayslipId === null ? (
                        <span className="muted">{t("hr.advance.notConsumed")}</span>
                      ) : (
                        <span className="mono" dir="ltr">{line.consumedByPayslipId}</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      {/* ═════════════════════════════════════════════ ٢ · الاستقطاع ═══ */}
      <Panel title={t("hr.deduction.title")} note={t("hr.deduction.note")} testId="hr-deduction-new">
        <div className="grid fields-3">
          <Field
            id="hr-d-employee"
            label={t("hr.field.employeeId")}
            hint={t("hr.field.employeeIdHint")}
            source="typed"
            required
          >
            <EmployeePicker id="hr-d-employee" value={dedEmployee} onChange={setDedEmployee} testId="hr-deduction-employee" />
          </Field>
          <Field
            id="hr-d-amount"
            label={t("hr.field.amount")}
            hint={t("hr.deduction.amountHint")}
            error={dedAmount !== "" && !isMoneyText(dedAmount) ? t("hr.field.amountBad") : undefined}
            source="typed"
            required
          >
            <input
              id="hr-d-amount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={dedAmount !== "" && !isMoneyText(dedAmount)}
              data-testid="hr-deduction-amount"
              value={dedAmount}
              onChange={(e) => setDedAmount(e.target.value)}
              placeholder="0.0000"
            />
          </Field>
          <Field
            id="hr-d-period"
            label={t("hr.field.periodCode")}
            hint={t("hr.deduction.periodHint")}
            error={dedPeriodValid ? undefined : t("hr.field.periodBad")}
            source="typed"
            required
          >
            <input
              id="hr-d-period"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              aria-invalid={!dedPeriodValid}
              data-testid="hr-deduction-period"
              value={dedPeriod}
              onChange={(e) => setDedPeriod(e.target.value)}
              placeholder="2026-06"
            />
          </Field>
        </div>

        <div className="grid fields-3">
          <Field
            id="hr-d-category"
            label={t("hr.field.categoryKey")}
            hint={t("hr.field.categoryKeyHint")}
            source="typed"
            required
          >
            <input
              id="hr-d-category"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-deduction-category"
              value={dedCategory}
              onChange={(e) => setDedCategory(e.target.value)}
            />
          </Field>
          {hideDedBy ? null : (
          <Field
            id="hr-d-by"
            label={t("hr.field.approvedBy")}
            hint={t("hr.deduction.approverHint")}
            source="typed"
            required
          >
            <input
              id="hr-d-by"
              className="ctl"
              autoComplete="off"
              data-testid="hr-deduction-by"
              value={dedBy}
              onChange={(e) => setDedBy(e.target.value)}
            />
          </Field>
          )}
          {simple ? null : (
          <Field
            id="hr-d-on"
            label={t("hr.field.approvedOn")}
            hint={t("hr.deduction.approvedOnHint")}
            source="typed"
            required
          >
            <input
              id="hr-d-on"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="hr-deduction-on"
              value={dedOn}
              onChange={(e) => setDedOn(e.target.value)}
            />
          </Field>
          )}
        </div>

        <div className="inline-group">
          <Button
            label={t("hr.act.recordDeduction")}
            kind="primary"
            loading={dedBusy}
            disabled={!deductionReady || dedBusy}
            onClick={() => void submitDeduction()}
            testId="hr-deduction-submit"
          />
        </div>

        <DeclaredGap
          title={t("hr.deduction.noPostingTitle")}
          body={t("hr.deduction.noPostingBody")}
          owed={t("hr.deduction.noPostingOwed")}
          testId="hr-gap-deduction-ceiling"
        />
      </Panel>

      <Panel title={t("hr.deduction.lookup")} note={t("hr.deduction.lookupNote")} testId="hr-deduction-lookup">
        <div className="grid fields-2">
          <Field
            id="hr-d-open"
            label={t("hr.field.deductionId")}
            hint={t("hr.field.deductionIdHint")}
            source="typed"
          >
            <input
              id="hr-d-open"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-deduction-lookup-id"
              value={dedLookup}
              onChange={(e) => setDedLookup(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && dedLookup.trim() !== "") void openDeduction();
              }}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.read")}
              disabled={dedLookup.trim() === "" || dedBusy}
              onClick={() => void openDeduction()}
              testId="hr-deduction-open"
            />
          </div>
        </div>
      </Panel>

      {dedError ? (
        <div className={refuseCls}>
          <ProblemPanel error={dedError} />
        </div>
      ) : null}

      {deduction ? (
        <Panel
          title={t("hr.deduction.card")}
          note={t("hr.deduction.cardNote")}
          testId="hr-deduction-card"
        >
          <div className="kv">
            <div>
              <div className="k">{t("hr.code.label")}</div>
              <div className="v">
                <OpaqueCode code={deduction.employeeCode} testId="hr-deduction-code" />
              </div>
            </div>
            <div>
              <div className="k">{t("hr.field.periodCode")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-deduction-card-period">{deduction.periodCode}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.categoryKey")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-deduction-card-category">{deduction.categoryKey}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.approvedBy")}</div>
              <div className="v" data-testid="hr-deduction-card-by">{deduction.approvedBy}</div>
            </div>
          </div>
          <div className={"stats-row hr-one " + arriveCls}>
            <StatCard
              label={t("hr.field.amount")}
              amount={deduction.amount}
              tone="credit"
              testId="hr-deduction-amount-out"
            />
          </div>
          <p className="hint" data-testid="hr-deduction-consumed">
            {deduction.consumedByPayslipId === null
              ? t("hr.deduction.notConsumed")
              : t("hr.deduction.consumedBy", { payslip: deduction.consumedByPayslipId })}
          </p>
        </Panel>
      ) : null}

      {advance === null && deduction === null ? (
        <EmptyState
          title={t("hr.advance.emptyTitle")}
          body={t("hr.advance.emptyBody")}
          testId="hr-advances-empty"
        />
      ) : null}
    </section>
  );
}
