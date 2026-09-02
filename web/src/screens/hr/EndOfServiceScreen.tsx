/* ═══════════════════════════════════════════════════════════════════════════
   /hr/end-of-service — مكافأة نهاية الخدمة  ·  End-of-service
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة تمتنع عن حساب الرقم الذي يظنّ الناس أنها موجودة لتحسبه.**

   مكافأة نهاية الخدمة في النظام السعودي مبنيّةٌ على أساسٍ قياسٍ **غير محسوم
   في هذا المستودع**: أي مكوّنات الأجر تدخل الوعاء، وكيف يُقسَّم نصف الأجر عن
   السنوات الخمس الأولى والأجر الكامل عمّا بعدها، وكيف تُعامَل الاستقالة عن
   الفصل. ولذلك **لا يوجد في السطح المنشور بابٌ يحسب المستحقّ** — والمستند
   يقبل مبلغاً أدخله معتمِده ومعه **مرجعُ الأساس** الذي قِيس به.

   وهذا ليس نقصاً يُعتذَر عنه: رقمٌ يخترعه النظام من قاعدةٍ لم تُعتمَد يُرحَّل
   إلى الدفتر، ويُصرَف إلى عامل، ويُقرأ في تدقيق — ثم لا يجد أحدٌ من يقول من
   أين جاء. والحقل الفارغ ومعه مرجعٌ إلزامي أصدقُ من رقمٍ يبدو محسوباً.

   ── ودورةٌ من مستندين، والفرق بينهما محاسبيٌّ لا شكليّ ─────────────────
   **المخصص** يستحقّ على الفترة حصّةً لكل علاقة عمل، ويُرحَّل **قيداً لكل
   حركة**. و**المخالصة** تُصرَف عند الانتهاء، وتستنفد ما تراكم: فإن طابق فهو
   `exact`، وإن نقص فـ`short`، وإن زاد فـ`excess` — **والسيناريو يصل مُسمّى من
   الخادم**، ولا تستنتجه الشاشة بمقارنة مبلغين (وتلك مقارنةٌ عشرية محاسبية
   لا تقع في متصفّح).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import {
  draftEndOfServiceProvision,
  draftEndOfServiceSettlement,
  postEndOfServiceProvision,
  postEndOfServiceSettlement,
} from "../../api/generated/client";
import type {
  HrProvision,
  HrSettlement,
  HrSettlementRequest,
} from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { PARAM_readTrialBalance_period_RE } from "../../api/generated/formats";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, RefusalPanel, StatCard, useMoment } from "../../ui";
import { useHrFocus } from "./focus";
import {
  ChooseCompanyFirst,
  HrSectionNav,
  DeclaredGap,
  EntryRef,
  HrState,
  OpaqueCode,
  isMoneyText,
  todayIso,
} from "./parts";
import {
  DUPLICATE_NUMBER,
  NOT_TERMINATED,
  POSTED,
  SETTLEMENT_METHODS,
  TREASURY_MISSING,
} from "./contract";
import "./hr.css";

/** حصّةُ علاقة عملٍ من مخصص الفترة، كما تُكتب قبل أن تعبر. */
interface ShareDraft {
  key: string;
  employmentId: string;
  periodShare: string;
}

let sequence = 0;
function newShare(employmentId: string): ShareDraft {
  sequence += 1;
  return { key: "s" + String(sequence), employmentId, periodShare: "" };
}

/** الشاشة كاملةً. */
export function EndOfServiceScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [focus, setFocus] = useHrFocus();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── المخصص ───────────────────────────────────────────────────────── */
  const [provNumber, setProvNumber] = useState("");
  const [provPeriod, setProvPeriod] = useState("");
  const [provAccruedOn, setProvAccruedOn] = useState(todayIso);
  const [provRef, setProvRef] = useState("");
  const [provBy, setProvBy] = useState("");
  const [shares, setShares] = useState<ShareDraft[]>(() => [newShare(focus.employmentId)]);
  const [provision, setProvision] = useState<HrProvision | null>(null);
  const [provBusy, setProvBusy] = useState(false);
  const [provError, setProvError] = useState<unknown>(null);

  /* ── المخالصة ─────────────────────────────────────────────────────── */
  type Method = HrSettlementRequest["settlementMethod"];
  const [settlementNumber, setSettlementNumber] = useState("");
  const [employmentId, setEmploymentId] = useState(focus.employmentId);
  const [settledOn, setSettledOn] = useState(todayIso);
  const [settlementDue, setSettlementDue] = useState("");
  const [measurementRef, setMeasurementRef] = useState("");
  const [method, setMethod] = useState<Method>((SETTLEMENT_METHODS[0] ?? "") as Method);
  const [treasury, setTreasury] = useState("");
  const [settlement, setSettlement] = useState<HrSettlement | null>(null);
  const [setBusy, setSetBusy] = useState(false);
  const [setError, setSetError] = useState<unknown>(null);

  const provPeriodValid = provPeriod === "" || PARAM_readTrialBalance_period_RE.test(provPeriod);

  /* المسوّدة تُقرأ قبل الترحيل: الحصص تعود من الخادم بأرقامها ورموزها المعتمة،
     فيُرى ما سيُرحَّل قبل أن يقع. وفصلُ الفعلين يمنع مسوّدةً يتيمة برقمٍ صار
     مستعملاً حين يسقط الترحيل وحده. */
  const draftProvision = useCallback(async () => {
    setProvBusy(true);
    setProvError(null);
    try {
      const drafted = await draftEndOfServiceProvision(transport, {
        companyId: config.companyId,
        body: {
          number: provNumber,
          periodCode: provPeriod,
          accruedOn: provAccruedOn,
          measurementRef: provRef,
          approvedBy: provBy,
          shares: shares.map((share) => ({
            employmentId: share.employmentId,
            periodShare: Money.wire(share.periodShare),
          })),
        },
      });
      setProvision(drafted);
      fireArrive();
    } catch (failure) {
      setProvError(failure);
      fireRefuse();
    } finally {
      setProvBusy(false);
    }
  }, [
    config.companyId,
    fireArrive,
    fireRefuse,
    provAccruedOn,
    provBy,
    provNumber,
    provPeriod,
    provRef,
    shares,
    transport,
  ]);

  const postProvision = useCallback(async () => {
    if (!provision) return;
    setProvBusy(true);
    setProvError(null);
    try {
      const settled = await postEndOfServiceProvision(transport, {
        companyId: config.companyId,
        provisionId: provision.id,
      });
      setProvision(settled);
      if (settled.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setProvError(failure);
      fireRefuse();
    } finally {
      setProvBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, provision, transport]);

  const draftSettlement = useCallback(async () => {
    setSetBusy(true);
    setSetError(null);
    try {
      const drafted = await draftEndOfServiceSettlement(transport, {
        companyId: config.companyId,
        body: {
          number: settlementNumber,
          employmentId,
          settledOn,
          settlementDue: Money.wire(settlementDue),
          measurementRef,
          settlementMethod: method,
          treasuryPartyId: treasury,
        },
      });
      /* والمسوّدة **تحمل السيناريو ورصيد المخصص والعجز أو الزيادة سلفاً**:
         فيرى معتمِدها ما سيقع على نتيجة الفترة قبل أن يقع. */
      setSettlement(drafted);
      setFocus({ employmentId });
      fireArrive();
    } catch (failure) {
      setSetError(failure);
      fireRefuse();
    } finally {
      setSetBusy(false);
    }
  }, [
    config.companyId,
    employmentId,
    fireArrive,
    fireRefuse,
    measurementRef,
    method,
    setFocus,
    settlementNumber,
    settledOn,
    settlementDue,
    treasury,
    transport,
  ]);

  const postSettlement = useCallback(async () => {
    if (!settlement) return;
    setSetBusy(true);
    setSetError(null);
    try {
      const posted = await postEndOfServiceSettlement(transport, {
        companyId: config.companyId,
        settlementId: settlement.id,
      });
      setSettlement(posted);
      if (posted.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setSetError(failure);
      fireRefuse();
    } finally {
      setSetBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, settlement, transport]);

  const provCode = provError instanceof ProblemError ? provError.code : null;
  const setCode = setError instanceof ProblemError ? setError.code : null;

  const sharesReady =
    shares.length > 0 &&
    shares.every((share) => share.employmentId !== "" && isMoneyText(share.periodShare));
  const provReady =
    provNumber !== "" && provPeriod !== "" && provPeriodValid && provAccruedOn !== "" &&
    provRef !== "" && provBy !== "" && sharesReady;
  const dueBad = settlementDue !== "" && !isMoneyText(settlementDue);
  const setReady =
    settlementNumber !== "" && employmentId !== "" && settledOn !== "" &&
    settlementDue !== "" && !dueBad && measurementRef !== "" && treasury !== "";

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-eos-needs-company" />;

  return (
    <section className="stack" data-testid="hr-eos-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.eosTitle")}</h1>
          <p className="sub">{t("hr.page.eosLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/end-of-service" />

      <DeclaredGap
        title={t("hr.gap.basisTitle")}
        body={t("hr.gap.basisBody")}
        owed={t("hr.gap.basisOwed")}
        testId="hr-gap-basis"
      />

      {/* ═════════════════════════════════════ ١ · مخصص الفترة ═════════ */}
      <Panel title={t("hr.provision.title")} note={t("hr.provision.note")} testId="hr-provision">
        <div className="grid fields-4">
          <Field
            id="hr-prov-number"
            label={t("hr.field.number")}
            hint={t("hr.field.numberHint")}
            source="typed"
            required
          >
            <input id="hr-prov-number" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-provision-number" value={provNumber} onChange={(e) => setProvNumber(e.target.value)}
              placeholder="EOS-P-2026-06" />
          </Field>
          <Field
            id="hr-prov-period"
            label={t("hr.field.periodCode")}
            hint={provPeriodValid ? t("hr.field.periodHint") : t("hr.field.periodBad")}
            error={provPeriodValid ? undefined : t("hr.field.periodBad")}
            source="typed"
            required
          >
            <input id="hr-prov-period" className={"ctl mono" + (provPeriodValid ? "" : " is-invalid")} dir="ltr"
              autoComplete="off" aria-invalid={!provPeriodValid}
              data-testid="hr-provision-period" value={provPeriod} onChange={(e) => setProvPeriod(e.target.value)}
              placeholder="2026-06" />
          </Field>
          <Field
            id="hr-prov-on"
            label={t("hr.field.accruedOn")}
            hint={t("hr.field.accruedOnHint")}
            source="typed"
            required
          >
            <input id="hr-prov-on" className="ctl mono" type="date" dir="ltr"
              data-testid="hr-provision-date" value={provAccruedOn} onChange={(e) => setProvAccruedOn(e.target.value)} />
          </Field>
          <Field
            id="hr-prov-by"
            label={t("hr.field.approvedBy")}
            hint={t("hr.field.approverHint")}
            source="typed"
            required
          >
            <input id="hr-prov-by" className="ctl" autoComplete="off"
              data-testid="hr-provision-by" value={provBy} onChange={(e) => setProvBy(e.target.value)} />
          </Field>
        </div>

        <Field
          id="hr-prov-ref"
          label={t("hr.field.measurementRef")}
          hint={t("hr.field.measurementRefHint")}
          source="typed"
          required
        >
          <input id="hr-prov-ref" className="ctl" autoComplete="off"
            data-testid="hr-provision-ref" value={provRef} onChange={(e) => setProvRef(e.target.value)} />
        </Field>

        <h3 className="hr-subhead">{t("hr.provision.shares")}</h3>
        <p className="muted">{t("hr.provision.sharesNote")}</p>
        <div className="hr-lines" data-testid="hr-provision-shares">
          {shares.map((share) => (
            <div key={share.key} className="hr-line">
              {/* **وصفان بمقاسين لا بمقاسٍ واحد.** عمودا هذا الصفّ 3fr و1fr،
                  فنصٌّ واحد الطول فيهما يلتفّ سطرين هنا وستّةً هناك — وذلك هو
                  «قاعُ الحبر المتعرّج» الذي لا يُصلحه استعارةُ المسارات: الصندوق
                  يتساوى والحبر لا يتساوى. فالوصفان مكتوبان **على قدر عموديهما**
                  ليقعا في سطرين معاً. (مقيسٌ بـ`scripts/align-audit.mjs`.) */}
              <Field
                id={"hr-share-emp-" + share.key}
                label={t("hr.field.employmentId")}
                hint={t("hr.field.shareEmploymentHint")}
                source="typed"
                required
              >
                <input
                  id={"hr-share-emp-" + share.key}
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  data-testid="hr-share-employment"
                  value={share.employmentId}
                  onChange={(e) =>
                    setShares((current) =>
                      current.map((s) => (s.key === share.key ? { ...s, employmentId: e.target.value } : s))
                    )
                  }
                />
              </Field>
              <Field
                id={"hr-share-amount-" + share.key}
                label={t("hr.field.periodShare")}
                hint={t("hr.field.shareAmountHint")}
                error={share.periodShare !== "" && !isMoneyText(share.periodShare) ? t("hr.field.amountBad") : undefined}
                source="typed"
                required
              >
                <input
                  id={"hr-share-amount-" + share.key}
                  className="ctl amt-input"
                  inputMode="decimal"
                  dir="ltr"
                  autoComplete="off"
                  aria-invalid={share.periodShare !== "" && !isMoneyText(share.periodShare)}
                  data-testid="hr-share-amount"
                  value={share.periodShare}
                  onChange={(e) =>
                    setShares((current) =>
                      current.map((s) => (s.key === share.key ? { ...s, periodShare: e.target.value } : s))
                    )
                  }
                  placeholder="0.0000"
                />
              </Field>
              <div className="field">
                <Button
                  label={t("hr.act.removeShare")}
                  kind="danger"
                  size="sm"
                  disabled={shares.length <= 1}
                  onClick={() => setShares((current) => current.filter((s) => s.key !== share.key))}
                  testId="hr-share-remove"
                />
              </div>
            </div>
          ))}
        </div>
        <button
          type="button"
          className="addline"
          data-testid="hr-share-add"
          onClick={() => setShares((current) => [...current, newShare("")])}
        >
          {t("hr.act.addShare")}
        </button>

        <div className="inline-group">
          <Button
            label={t("hr.act.draftProvision")}
            loading={provBusy}
            disabled={!provReady || provBusy || provision !== null}
            onClick={() => void draftProvision()}
            testId="hr-provision-draft"
          />
          <Button
            label={t("hr.act.accrue")}
            kind="primary"
            loading={provBusy}
            disabled={provBusy || provision === null}
            onClick={() => void postProvision()}
            testId="hr-provision-submit"
          />
        </div>

        {provision ? (
          <div
            className={"hr-receipt " + (provision.alreadyPosted ? arriveCls : postCls)}
            data-state={provision.state}
            data-already={String(provision.alreadyPosted)}
            role="status"
            data-testid="hr-provision-receipt"
          >
            <h2>
              {provision.state !== POSTED
                ? t("hr.provision.drafted")
                : provision.alreadyPosted
                  ? t("hr.provision.again")
                  : t("hr.provision.done")}
            </h2>
            <p>
              {provision.state !== POSTED
                ? t("hr.provision.draftedBody")
                : provision.alreadyPosted
                  ? t("hr.provision.againBody")
                  : t("hr.provision.doneBody")}
            </p>
            <div className="kv">
              <div>
                <div className="k">{t("hr.provision.periodShare")}</div>
                <div className="v"><Amount value={provision.periodShare} /></div>
              </div>
              <div>
                <div className="k">{t("common.label.status")}</div>
                <div className="v"><HrState state={provision.state} /></div>
              </div>
              <div>
                <div className="k">{t("hr.provision.movements")}</div>
                <div className="v"><Num value={provision.movements.length} /></div>
              </div>
              <div>
                <div className="k">{t("hr.field.measurementRef")}</div>
                <div className="v mono" dir="ltr">{provision.measurementRef}</div>
              </div>
            </div>
            <div className="hr-table" data-testid="hr-provision-movements">
              <table>
                <caption className="visually-hidden">{t("hr.provision.movements")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("hr.code.label")}</th>
                    <th scope="col" className="n">{t("hr.field.periodShare")}</th>
                    <th scope="col">{t("hr.entry.label")}</th>
                  </tr>
                </thead>
                <tbody>
                  {provision.movements.map((movement) => (
                    <tr key={movement.id}>
                      <td><OpaqueCode code={movement.employeeCode} /></td>
                      <td className="n"><Amount value={movement.periodShare} /></td>
                      <td><EntryRef entryId={movement.entryId} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="hint">{t("hr.provision.entryPerMovement")}</p>
          </div>
        ) : null}

        {provError ? (
          <div className="stack">
            <ProblemPanel error={provError} />
            {provCode === DUPLICATE_NUMBER ? (
              <RefusalPanel
                title={t("hr.refusal.duplicateTitle")}
                titleEn="The document number is already used"
                body={t("hr.refusal.duplicateBody")}
                code={DUPLICATE_NUMBER}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.duplicateNext")}
                moment={refuseCls}
                testId="hr-refusal-duplicate-provision"
              />
            ) : null}
          </div>
        ) : null}
      </Panel>

      {/* ═════════════════════════════════════ ٢ · المخالصة ════════════ */}
      <Panel title={t("hr.settlement.title")} note={t("hr.settlement.note")} testId="hr-settlement">
        <div className="grid fields-4">
          {/* أربعةُ حقولٍ في صفٍّ واحد، **وكلٌّ منها بوصف**: خليّةٌ بلا وصف
              تُنهي حبرها فوق جيرانها بارتفاع كتلة الوصف كاملةً. والأوصاف
              الأربعة مكتوبة على قدرٍ واحد فتلتفّ سطرين معاً. */}
          <Field
            id="hr-set-number"
            label={t("hr.field.number")}
            hint={t("hr.field.numberHint")}
            source="typed"
            required
          >
            <input id="hr-set-number" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-settlement-number" value={settlementNumber} onChange={(e) => setSettlementNumber(e.target.value)}
              placeholder="EOS-S-2026-0001" />
          </Field>
          <Field
            id="hr-set-employment"
            label={t("hr.field.employmentId")}
            hint={t("hr.field.employmentIdHint")}
            source="typed"
            required
          >
            <input id="hr-set-employment" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-settlement-employment" value={employmentId}
              onChange={(e) => setEmploymentId(e.target.value)} />
          </Field>
          <Field
            id="hr-set-on"
            label={t("hr.field.settledOn")}
            hint={t("hr.field.settledOnHint")}
            source="typed"
            required
          >
            <input id="hr-set-on" className="ctl mono" type="date" dir="ltr"
              data-testid="hr-settlement-date" value={settledOn} onChange={(e) => setSettledOn(e.target.value)} />
          </Field>
          <Field
            id="hr-set-due"
            label={t("hr.field.settlementDue")}
            hint={dueBad ? t("hr.field.amountBad") : t("hr.field.settlementDueHint")}
            error={dueBad ? t("hr.field.amountBad") : undefined}
            source="typed"
            required
          >
            <input id="hr-set-due" className={"ctl amt-input" + (dueBad ? " is-invalid" : "")} inputMode="decimal"
              dir="ltr" autoComplete="off" aria-invalid={dueBad}
              data-testid="hr-settlement-due" value={settlementDue}
              onChange={(e) => setSettlementDue(e.target.value)} placeholder="0.0000" />
          </Field>
        </div>
        <div className="grid fields-3">
          <Field
            id="hr-set-ref"
            label={t("hr.field.measurementRef")}
            hint={t("hr.field.measurementRefHint")}
            source="typed"
            required
          >
            <input id="hr-set-ref" className="ctl" autoComplete="off"
              data-testid="hr-settlement-ref" value={measurementRef}
              onChange={(e) => setMeasurementRef(e.target.value)} />
          </Field>
          <Field
            id="hr-set-method"
            label={t("hr.field.settlementMethod")}
            hint={t("hr.field.settlementMethodHint")}
            source="typed"
            required
          >
            <select id="hr-set-method" className="ctl" data-testid="hr-settlement-method"
              value={method} onChange={(e) => setMethod(e.target.value as Method)}>
              {SETTLEMENT_METHODS.map((name) => (
                <option key={name} value={name}>{t("hr.method." + name)}</option>
              ))}
            </select>
          </Field>
          <Field
            id="hr-set-treasury"
            label={t("hr.field.treasuryParty")}
            hint={t("hr.field.treasuryPartyHint")}
            source="typed"
            required
          >
            <input id="hr-set-treasury" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-settlement-treasury" value={treasury} onChange={(e) => setTreasury(e.target.value)}
              placeholder="BANK-0001" />
          </Field>
        </div>

        <div className="inline-group">
          <Button
            label={t("hr.act.draftSettlement")}
            loading={setBusy}
            disabled={!setReady || setBusy || settlement !== null}
            onClick={() => void draftSettlement()}
            testId="hr-settlement-draft"
          />
          <Button
            label={t("hr.act.settle")}
            kind="primary"
            loading={setBusy}
            disabled={setBusy || settlement === null}
            onClick={() => void postSettlement()}
            testId="hr-settlement-submit"
          />
        </div>

        {settlement ? (
          <div
            className={"hr-receipt " + (settlement.alreadyPosted ? arriveCls : postCls)}
            data-state={settlement.state}
            data-already={String(settlement.alreadyPosted)}
            role="status"
            data-testid="hr-settlement-receipt"
          >
            <h2>
              {settlement.state !== POSTED
                ? t("hr.settlement.drafted")
                : settlement.alreadyPosted
                  ? t("hr.settlement.again")
                  : t("hr.settlement.done")}
            </h2>
            <p>
              {settlement.state !== POSTED
                ? t("hr.settlement.draftedBody")
                : settlement.alreadyPosted
                  ? t("hr.settlement.againBody")
                  : t("hr.settlement.doneBody")}
            </p>
            <div className="hr-split">
              <OpaqueCode code={settlement.employeeCode} testId="hr-settlement-code" />
              <span className="pill pill--info" data-testid="hr-settlement-scenario">
                {t("hr.scenario." + settlement.scenarioCode)}
              </span>
              <HrState state={settlement.state} />
              <span className="spacer" />
              <EntryRef entryId={settlement.entryId} testId="hr-settlement-entry" />
            </div>
            <p>{t("hr.scenario." + settlement.scenarioCode + "Body")}</p>
            <div className="stats-row">
              <StatCard label={t("hr.settlement.due")} amount={settlement.settlementDue} tone="debit" />
              <StatCard label={t("hr.settlement.provisionBalance")} amount={settlement.provisionBalance} />
              <StatCard label={t("hr.settlement.utilised")} amount={settlement.provisionUtilised} />
              <StatCard label={t("hr.settlement.shortfall")} amount={settlement.shortfall} tone="bad" />
              <StatCard label={t("hr.settlement.excess")} amount={settlement.excess} tone="good" />
              <StatCard label={t("hr.settlement.paid")} amount={settlement.amountPaid} tone="credit" />
            </div>
            <p className="hint">{t("hr.settlement.refNote", { ref: settlement.measurementRef })}</p>
          </div>
        ) : null}

        {setError ? (
          <div className="stack">
            <ProblemPanel error={setError} />
            {setCode === NOT_TERMINATED ? (
              <RefusalPanel
                title={t("hr.refusal.notTerminatedTitle")}
                titleEn="The employment is still active"
                body={t("hr.refusal.notTerminatedBody")}
                code={NOT_TERMINATED}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.notTerminatedNext")}
                moment={refuseCls}
                testId="hr-refusal-not-terminated"
              />
            ) : null}
            {setCode === TREASURY_MISSING ? (
              <RefusalPanel
                title={t("hr.refusal.treasuryTitle")}
                titleEn="The document carries no treasury party"
                body={t("hr.refusal.treasuryBody")}
                code={TREASURY_MISSING}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.treasuryNext")}
                moment={refuseCls}
                testId="hr-refusal-treasury-eos"
              />
            ) : null}
          </div>
        ) : null}

        {!settlement && !setError ? (
          <EmptyState
            small
            title={t("hr.settlement.emptyTitle")}
            body={t("hr.settlement.emptyBody")}
            testId="hr-settlement-empty"
          />
        ) : null}
      </Panel>
    </section>
  );
}
