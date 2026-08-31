/* ═══════════════════════════════════════════════════════════════════════════
   المقاولون من الباطن — والدفعة المقدمة، وهي الترحيل الوحيد الذي يقع اليوم
   Subcontractors — and the advance, the one posting in this section that lands
   ───────────────────────────────────────────────────────────────────────────
   **الترحيل الوحيد الذي لا يُحجب في هذا القسم هو صرف الدفعة المقدمة.**
   ومبلغها يُدخله المستخدم ولا يشتقّه حاسب، فلا بند معلَّق فيها — بينما
   المستخلص وحركات المحتجز تنتظر أربعة قرارات محاسب. ولذلك هذه الشاشة هي
   الموضع الذي **يُرى فيه إيصالٌ حقيقي**، وفيها تُقال هوية الترحيل كاملة:
   الإرسال الثاني بالهوية نفسها يردّ **القيد الأول** ومعه `alreadyPosted`،
   ولا يُقرأ عملاً جديداً.

   **ولا باب قائمةٍ للمقاولين ولا لعقود الباطن في العقد المنشور.** فالمعرّف
   يُلصَق ثم **يُقرأ من الخادم** قبل أن يُبنى عليه شيء — والشاشة لا تخترع
   قائمةً لا تملكها، ولا تُبقي حقلاً يُكتب فيه معرّفٌ بلا تأكيد.

   **وطريقة التسوية مؤهّل دور لا حساب.** الشاشة تُرسلها كما كتبها المستخدم،
   والمصفوفة وحدها تحوّلها إلى حساب — ولا رمز حسابٍ يظهر هنا (القاعدة 2).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addSubcontract,
  addSubcontractor,
  draftSubcontractorAdvance,
  postSubcontractorAdvance,
  readGuarantee,
  readSubcontract,
  readSubcontractLines,
  readSubcontractor,
} from "../../api/generated/client";
import { asMagnitude, asRate } from "../../api/generated/brands";
import { Money } from "../../api/money";
import type { NameValue, ProjectsDocument, SubcontractLineRequest } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, Field, MOTION, Panel, QuantityValue, RateValue, StatusBadge } from "../../ui";
import {
  ContractingHead,
  DocumentReceipt,
  ExplainedEmpty,
  Foldable,
  isCountText,
  isMagnitudeText,
  isMoneyText,
  isRateText,
  countOf,
  LoadingPanel,
  NeedsCompany,
  PendingPolicyPanel,
  ProjectContractPicker,
  ReadProblem,
  todayIso,
  TranslatedName,
  useProjects,
} from "./shared";
import { selectContracting, useContractingSelection } from "./selection";

/** الأوسمة المعروضة للترجمة — الإنجليزية واحدة من N لا حقلاً ثابتاً. */
const TRANSLATION_TAGS = ["en", "ur", "hi"] as const;

/* ═══════════════════════════════════════════════ تسجيل مقاول من الباطن */

function NewSubcontractorForm(props: { readonly onDone: (id: string) => void }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [names, setNames] = useState<Record<string, string>>({});
  const [vatNumber, setVatNumber] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [done, setDone] = useState<string | null>(null);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const translations: NameValue[] = TRANSLATION_TAGS.filter((tag) => (names[tag] ?? "") !== "").map(
        (tag) => ({ name: tag, value: names[tag] as string })
      );
      const created = await addSubcontractor(transport, {
        companyId: config.companyId,
        body: { code, nameAr, nameTranslations: translations, vatNumber },
      });
      setDone(created.id);
      props.onDone(created.id);
      setCode("");
      setNameAr("");
      setNames({});
      setVatNumber("");
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [code, config.companyId, nameAr, names, props, transport, vatNumber]);

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field id="ns-code" label={t("contracting.common.code")} required>
          <input id="ns-code" className="ctl mono" dir="ltr" value={code} onChange={(e) => setCode(e.target.value)} />
        </Field>
        <Field id="ns-name" label={t("contracting.register.nameAr")} required>
          <input id="ns-name" className="ctl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field
          id="ns-vat"
          label={t("contracting.subcontractor.vat")}
          hint={t("contracting.subcontractor.vatHint")}
        >
          <input
            id="ns-vat"
            className="ctl mono"
            dir="ltr"
            value={vatNumber}
            onChange={(e) => setVatNumber(e.target.value)}
          />
        </Field>
      </div>
      <div className="grid fields-3">
        {TRANSLATION_TAGS.map((tag) => (
          <Field key={tag} id={"ns-name-" + tag} label={t("contracting.register.translation." + tag)}>
            <input
              id={"ns-name-" + tag}
              className="ctl"
              lang={tag}
              value={names[tag] ?? ""}
              onChange={(e) => setNames({ ...names, [tag]: e.target.value })}
            />
          </Field>
        ))}
      </div>
      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.subcontractor.save")}
          kind="primary"
          disabled={busy || code === "" || nameAr === ""}
          onClick={() => void submit()}
          testId="new-subcontractor-save"
        />
        {done ? (
          <span className="pill pill--posted mono" dir="ltr" data-testid="new-subcontractor-done">
            {done}
          </span>
        ) : null}
      </div>
      <p className="muted">{t("contracting.subcontractor.partyNote")}</p>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════ تسجيل عقد باطن */

interface DraftSubLine {
  key: string;
  code: string;
  descriptionAr: string;
  magnitude: string;
  unit: string;
  unitRate: string;
}

let lineSeq = 0;
function newSubLine(): DraftSubLine {
  lineSeq += 1;
  return { key: "s" + String(lineSeq), code: "", descriptionAr: "", magnitude: "", unit: "", unitRate: "" };
}

function subLineReady(line: DraftSubLine): boolean {
  return (
    line.code !== "" &&
    line.descriptionAr !== "" &&
    isMagnitudeText(line.magnitude) &&
    line.unit !== "" &&
    isMoneyText(line.unitRate)
  );
}

function NewSubcontractForm(props: {
  readonly projectId: string;
  readonly onDone: (id: string, number: string) => void;
}): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [subcontractorId, setSubcontractorId] = useState("");
  const [signedOn, setSignedOn] = useState(todayIso);
  const [retentionRate, setRetentionRate] = useState("");
  const [guaranteeMonths, setGuaranteeMonths] = useState("");
  const [lines, setLines] = useState<DraftSubLine[]>(() => [newSubLine()]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const ready =
    number !== "" &&
    subcontractorId !== "" &&
    signedOn !== "" &&
    isRateText(retentionRate) &&
    isCountText(guaranteeMonths) &&
    lines.length > 0 &&
    lines.every(subLineReady);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const wire: SubcontractLineRequest[] = lines.map((line) => ({
        code: line.code,
        descriptionAr: line.descriptionAr,
        contractQuantity: { magnitude: asMagnitude(line.magnitude), unit: line.unit },
        unitRate: Money.wire(line.unitRate),
      }));
      const created = await addSubcontract(transport, {
        companyId: config.companyId,
        body: {
          number,
          projectId: props.projectId,
          subcontractorId,
          signedOn,
          retentionRate: asRate(retentionRate),
          guaranteeMonths: countOf(guaranteeMonths),
          lines: wire,
        },
      });
      props.onDone(created.id, created.number);
      setNumber("");
      setLines([newSubLine()]);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, guaranteeMonths, lines, number, props, retentionRate, signedOn, subcontractorId, transport]);

  const patch = (key: string, change: Partial<DraftSubLine>) =>
    setLines(lines.map((line) => (line.key === key ? { ...line, ...change } : line)));

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field id="sc-number" label={t("contracting.common.number")} required>
          <input id="sc-number" className="ctl mono" dir="ltr" value={number} onChange={(e) => setNumber(e.target.value)} />
        </Field>
        <Field
          id="sc-party"
            data-testid="sc-party"
          label={t("contracting.subcontractor.title")}
          hint={t("contracting.subcontract.partyHint")}
          required
        >
          <input
            id="sc-party"
            className="ctl mono"
            dir="ltr"
            value={subcontractorId}
            onChange={(e) => setSubcontractorId(e.target.value)}
          />
        </Field>
        <Field id="sc-signed" label={t("contracting.common.signedOn")} required>
          <input
            id="sc-signed"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={signedOn}
            onChange={(e) => setSignedOn(e.target.value)}
          />
        </Field>
        <Field
          id="sc-retention"
          label={t("contracting.common.retentionRate")}
          hint={retentionRate === "" || isRateText(retentionRate) ? t("contracting.common.rateHint") : t("contracting.common.rateBad")}
          required
        >
          <input
            id="sc-retention"
            className={"ctl amt-input" + (retentionRate !== "" && !isRateText(retentionRate) ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            aria-invalid={retentionRate !== "" && !isRateText(retentionRate)}
            value={retentionRate}
            onChange={(e) => setRetentionRate(e.target.value)}
            placeholder="0.10"
          />
        </Field>
        <Field id="sc-guarantee" label={t("contracting.common.guaranteeMonths")} required>
          <input
            id="sc-guarantee"
            className="ctl mono"
            inputMode="numeric"
            dir="ltr"
            value={guaranteeMonths}
            onChange={(e) => setGuaranteeMonths(e.target.value)}
            placeholder="12"
          />
        </Field>
      </div>

      <div className="con-lines" data-testid="subcontract-lines">
        {lines.map((line, index) => (
          <fieldset key={line.key} className="con-line">
            <legend className="k">
              <Num value={index + 1} />
            </legend>
            <Field id={"sl-code-" + line.key} label={t("contracting.boq.code")} required>
              <input
                id={"sl-code-" + line.key}
                className="ctl mono"
                dir="ltr"
                value={line.code}
                onChange={(e) => patch(line.key, { code: e.target.value })}
              />
            </Field>
            <Field id={"sl-desc-" + line.key} label={t("contracting.boq.description")} required>
              <input
                id={"sl-desc-" + line.key}
                className="ctl"
                lang="ar"
                value={line.descriptionAr}
                onChange={(e) => patch(line.key, { descriptionAr: e.target.value })}
              />
            </Field>
            <Field
              id={"sl-qty-" + line.key}
              label={t("contracting.boq.contractQuantity")}
              hint={line.magnitude === "" || isMagnitudeText(line.magnitude) ? t("contracting.common.quantityHint") : t("contracting.common.quantityBad")}
              required
            >
              <input
                id={"sl-qty-" + line.key}
                className={"ctl amt-input" + (line.magnitude !== "" && !isMagnitudeText(line.magnitude) ? " is-invalid" : "")}
                inputMode="decimal"
                dir="ltr"
                aria-invalid={line.magnitude !== "" && !isMagnitudeText(line.magnitude)}
                value={line.magnitude}
                onChange={(e) => patch(line.key, { magnitude: e.target.value })}
                placeholder="0.000000"
              />
            </Field>
            <Field id={"sl-unit-" + line.key} label={t("contracting.common.unit")} required>
              <input
                id={"sl-unit-" + line.key}
                className="ctl mono"
                dir="ltr"
                value={line.unit}
                onChange={(e) => patch(line.key, { unit: e.target.value })}
                placeholder="M3"
              />
            </Field>
            <Field
              id={"sl-rate-" + line.key}
              label={t("contracting.boq.unitRate")}
              hint={line.unitRate === "" || isMoneyText(line.unitRate) ? t("contracting.common.moneyHint") : t("contracting.common.moneyBad")}
              required
            >
              <input
                id={"sl-rate-" + line.key}
                className={"ctl amt-input" + (line.unitRate !== "" && !isMoneyText(line.unitRate) ? " is-invalid" : "")}
                inputMode="decimal"
                dir="ltr"
                aria-invalid={line.unitRate !== "" && !isMoneyText(line.unitRate)}
                value={line.unitRate}
                onChange={(e) => patch(line.key, { unitRate: e.target.value })}
                placeholder="0.0000"
              />
            </Field>
            <div className="con-line__wide inline-group">
              <Button
                label={t("contracting.common.removeLine")}
                kind="danger"
                size="sm"
                disabled={lines.length <= 1}
                onClick={() => setLines(lines.filter((x) => x.key !== line.key))}
              />
            </div>
          </fieldset>
        ))}
        <button
          type="button"
          className="addline"
          data-testid="subcontract-add-line"
          onClick={() => setLines([...lines, newSubLine()])}
        >
          {t("contracting.common.addLine")}
        </button>
      </div>

      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.subcontract.save")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="new-subcontract-save"
        />
      </div>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════ الدفعة المقدمة */

function AdvanceForm(props: { readonly subcontractId: string }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [paidOn, setPaidOn] = useState(todayIso);
  const [amount, setAmount] = useState("");
  const [settlementMethod, setSettlementMethod] = useState("");
  const [treasuryPartyId, setTreasuryPartyId] = useState("");
  const [guaranteeId, setGuaranteeId] = useState("");
  const [document, setDocument] = useState<ProjectsDocument | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const guarantee = useQuery({
    queryKey: ["contracting", "guarantee", config.baseUrl, config.token, config.companyId, guaranteeId],
    enabled: guaranteeId !== "",
    retry: false,
    queryFn: ({ signal }) => readGuarantee(transport, { companyId: config.companyId, guaranteeId }, signal),
  });

  const ready =
    number !== "" && paidOn !== "" && isMoneyText(amount) && settlementMethod !== "" && treasuryPartyId !== "";

  const draft = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await draftSubcontractorAdvance(transport, {
        companyId: config.companyId,
        body: {
          number,
          subcontractId: props.subcontractId,
          paidOn,
          amount: Money.wire(amount),
          settlementMethod,
          treasuryPartyId,
          guaranteeId: guaranteeId === "" ? null : guaranteeId,
        },
      });
      setDocument(created);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [amount, config.companyId, guaranteeId, number, paidOn, props.subcontractId, settlementMethod, transport, treasuryPartyId]);

  const post = useCallback(async () => {
    if (!document) return;
    setBusy(true);
    setError(null);
    try {
      const receipt = await postSubcontractorAdvance(transport, {
        companyId: config.companyId,
        advanceId: document.id,
      });
      setDocument(receipt);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, document, transport]);

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field id="ad-number" data-testid="ad-number" label={t("contracting.common.number")} required>
          <input id="ad-number" className="ctl mono" dir="ltr" value={number} onChange={(e) => setNumber(e.target.value)} />
        </Field>
        <Field id="ad-paid" label={t("contracting.advance.paidOn")} required>
          <input
            id="ad-paid"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={paidOn}
            onChange={(e) => setPaidOn(e.target.value)}
          />
        </Field>
        <Field
          id="ad-amount"
            data-testid="ad-amount"
          label={t("contracting.advance.amount")}
          hint={amount === "" || isMoneyText(amount) ? t("contracting.advance.amountHint") : t("contracting.common.moneyBad")}
          source="typed"
          required
        >
          <input
            id="ad-amount"
            className={"ctl amt-input" + (amount !== "" && !isMoneyText(amount) ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            aria-invalid={amount !== "" && !isMoneyText(amount)}
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.0000"
          />
        </Field>
        <Field
          id="ad-method"
            data-testid="ad-method"
          label={t("contracting.advance.settlementMethod")}
          hint={t("contracting.advance.settlementHint")}
          required
        >
          <input
            id="ad-method"
            className="ctl mono"
            dir="ltr"
            value={settlementMethod}
            onChange={(e) => setSettlementMethod(e.target.value)}
          />
        </Field>
        <Field
          id="ad-treasury"
            data-testid="ad-treasury"
          label={t("contracting.advance.treasury")}
          hint={t("contracting.advance.treasuryHint")}
          required
        >
          <input
            id="ad-treasury"
            className="ctl mono"
            dir="ltr"
            value={treasuryPartyId}
            onChange={(e) => setTreasuryPartyId(e.target.value)}
          />
        </Field>
        <Field
          id="ad-guarantee"
          label={t("contracting.guarantee.idLabel")}
          hint={t("contracting.guarantee.idHint")}
        >
          <input
            id="ad-guarantee"
            className="ctl mono"
            dir="ltr"
            value={guaranteeId}
            onChange={(e) => setGuaranteeId(e.target.value)}
          />
        </Field>
      </div>

      {guaranteeId !== "" ? (
        guarantee.isError ? (
          <ReadProblem error={guarantee.error} />
        ) : guarantee.data ? (
          <div className="kv" data-testid="guarantee-read">
            <div>
              <div className="k">{t("contracting.common.number")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.number}
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.kind")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.kind}
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.issuer")}</div>
              <div className="v">{guarantee.data.issuerNameAr}</div>
            </div>
            <div>
              <div className="k">{t("contracting.advance.amount")}</div>
              <div className="v">
                <Amount value={guarantee.data.amount} />
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.expires")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.expiresOn}
              </div>
            </div>
          </div>
        ) : (
          <LoadingPanel what={t("contracting.guarantee.title")} />
        )
      ) : null}

      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.advance.saveDraft")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void draft()}
          testId="advance-draft"
        />
        {document ? (
          <Button
            label={t("contracting.posting.post")}
            kind="primary"
            disabled={busy}
            onClick={() => void post()}
            testId="advance-post"
          />
        ) : null}
      </div>

      {document ? (
        <DocumentReceipt
          document={document}
          onRepeat={document.state === "POSTED" ? () => void post() : undefined}
          busy={busy}
          testId="advance-receipt"
        />
      ) : null}
      {error ? <ReadProblem error={error} /> : null}
      <p className="muted">{t("contracting.advance.whyItPosts")}</p>
    </div>
  );
}

/* ═══════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة المقاولين من الباطن وعقودهم ودفعتهم المقدمة. */
export function SubcontractingScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const feed = useProjects();
  const selection = useContractingSelection();
  const [subcontractorId, setSubcontractorId] = useState("");
  const [subcontractInput, setSubcontractInput] = useState("");

  const subcontractor = useQuery({
    queryKey: ["contracting", "subcontractor", config.baseUrl, config.token, config.companyId, subcontractorId],
    enabled: subcontractorId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontractor(transport, { companyId: config.companyId, subcontractorId }, signal),
  });

  const subcontract = useQuery({
    queryKey: ["contracting", "subcontract", config.baseUrl, config.token, config.companyId, selection.subcontractId],
    enabled: selection.subcontractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontract(transport, { companyId: config.companyId, subcontractId: selection.subcontractId }, signal),
  });

  const lines = useQuery({
    queryKey: ["contracting", "sublines", config.baseUrl, config.token, config.companyId, selection.subcontractId],
    enabled: selection.subcontractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontractLines(transport, { companyId: config.companyId, subcontractId: selection.subcontractId }, signal),
  });

  if (config.companyId === "") return <NeedsCompany />;

  const project = feed.projects.find((p) => p.id === selection.projectId) ?? null;

  return (
    <section className="stack" data-testid="contracting-subcontracting">
      <ContractingHead
        title={t("contracting.subcontract.title")}
        lede={t("contracting.subcontract.lede")}
      />

      <Panel
        title={t("contracting.subcontractor.title")}
        note={t("contracting.subcontractor.note")}
        testId="subcontractor-panel"
      >
        <div className="filterbar">
          <Field
            id="sub-id"
              data-testid="sub-id"
            label={t("contracting.subcontractor.idLabel")}
            hint={t("contracting.subcontractor.idHint")}
          >
            <input
              id="sub-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              value={subcontractorId}
              onChange={(e) => setSubcontractorId(e.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
        </div>
        {subcontractorId !== "" ? (
          subcontractor.isError ? (
            <ReadProblem error={subcontractor.error} />
          ) : subcontractor.data ? (
            <div className={"kv " + MOTION.arrive} data-testid="subcontractor-read">
              <div>
                <div className="k">{t("contracting.common.code")}</div>
                <div className="v mono" dir="ltr">
                  {subcontractor.data.code}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.register.nameAr")}</div>
                <div className="v">
                  <TranslatedName
                    nameAr={subcontractor.data.nameAr}
                    translations={subcontractor.data.nameTranslations}
                  />
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.subcontractor.vat")}</div>
                <div className="v mono" dir="ltr">
                  {subcontractor.data.vatNumber === ""
                    ? t("contracting.subcontractor.noVat")
                    : subcontractor.data.vatNumber}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.common.state")}</div>
                <div className="v">
                  <StatusBadge
                    state={subcontractor.data.isActive ? "posted" : "archived"}
                    label={
                      subcontractor.data.isActive
                        ? t("contracting.common.active")
                        : t("contracting.common.inactive")
                    }
                  />
                </div>
              </div>
            </div>
          ) : (
            <LoadingPanel what={t("contracting.subcontractor.title")} />
          )
        ) : (
          <ExplainedEmpty
            title={t("contracting.subcontractor.noListTitle")}
            body={t("contracting.subcontractor.noListBody")}
            testId="subcontractor-no-list"
          />
        )}
      </Panel>

      <Foldable
        title={t("contracting.subcontractor.newTitle")}
        note={t("contracting.subcontractor.newNote")}
        openLabel={t("contracting.common.open")}
        closeLabel={t("contracting.common.close")}
        testId="fold-new-subcontractor"
      >
        <NewSubcontractorForm onDone={setSubcontractorId} />
      </Foldable>

      <Panel title={t("contracting.subcontract.readTitle")} note={t("contracting.subcontract.readNote")}>
        <div className="filterbar">
          <Field
            id="subc-id"
              data-testid="subc-id"
            label={t("contracting.subcontract.idLabel")}
            hint={t("contracting.subcontract.idHint")}
          >
            <input
              id="subc-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              value={subcontractInput}
              onChange={(e) => setSubcontractInput(e.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="inline-group">
            <Button
              label={t("contracting.subcontract.read")}
              disabled={subcontractInput === ""}
              onClick={() => selectContracting({ subcontractId: subcontractInput, subcontractNumber: "" })}
              testId="subcontract-read"
            />
          </div>
        </div>

        {selection.subcontractId === "" ? (
          <ExplainedEmpty
            title={t("contracting.subcontract.noneTitle")}
            body={t("contracting.subcontract.noneBody")}
            testId="subcontract-none"
          />
        ) : subcontract.isError ? (
          <ReadProblem error={subcontract.error} />
        ) : subcontract.data ? (
          <>
            <div className={"kv " + MOTION.arrive} data-testid="subcontract-read-out">
              <div>
                <div className="k">{t("contracting.common.number")}</div>
                <div className="v mono" dir="ltr">
                  {subcontract.data.number}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.common.projectCode")}</div>
                <div className="v mono" dir="ltr">
                  {subcontract.data.projectCode}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.common.currency")}</div>
                <div className="v mono" dir="ltr">
                  {subcontract.data.currencyCode}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.common.retentionRate")}</div>
                <div className="v">
                  <RateValue rate={subcontract.data.retentionRate} />
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.common.guaranteeMonths")}</div>
                <div className="v">
                  <Num value={subcontract.data.guaranteeMonths} />
                </div>
              </div>
            </div>
            <p className="muted">{t("contracting.common.retentionRateHint")}</p>
          </>
        ) : (
          <LoadingPanel what={t("contracting.subcontract.title")} />
        )}
      </Panel>

      {subcontract.data && subcontract.data.pendingPolicy.length > 0 ? (
        <PendingPolicyPanel
          items={subcontract.data.pendingPolicy}
          subject={t("contracting.pending.subjectSubcontract", { number: subcontract.data.number })}
          testId="subcontract-pending"
        />
      ) : null}

      {selection.subcontractId !== "" ? (
        <Panel
          title={t("contracting.subcontract.linesTitle")}
          testId="subcontract-lines-panel"
          aside={
            lines.data ? <span className="muted">{tp("common.count.lines", lines.data.lines.length)}</span> : null
          }
        >
          {lines.isError ? (
            <ReadProblem error={lines.error} />
          ) : lines.data && lines.data.lines.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.subcontract.noLinesTitle")}
              body={t("contracting.subcontract.noLinesBody")}
              testId="subcontract-no-lines"
            />
          ) : lines.data ? (
            <div className="ledger" data-testid="subcontract-lines-table">
              <table>
                <caption className="visually-hidden">{t("contracting.subcontract.linesTitle")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("contracting.boq.lineNo")}</th>
                    <th scope="col">{t("contracting.boq.code")}</th>
                    <th scope="col">{t("contracting.boq.description")}</th>
                    <th scope="col" className="n">
                      {t("contracting.boq.contractQuantity")}
                    </th>
                    <th scope="col" className="n">
                      {t("contracting.boq.unitRate")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {lines.data.lines.map((line) => (
                    <tr key={line.id}>
                      <td className="code">
                        <Num value={line.lineNo} />
                      </td>
                      <td className="code">{line.code}</td>
                      <td>{line.descriptionAr}</td>
                      <td className="n">
                        <QuantityValue
                          magnitude={line.contractQuantity.magnitude}
                          unit={line.contractQuantity.unit}
                        />
                      </td>
                      <td className="n">
                        <Amount value={line.unitRate} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <LoadingPanel what={t("contracting.subcontract.linesTitle")} />
          )}
        </Panel>
      ) : null}

      <Panel title={t("contracting.subcontract.newTitle")} note={t("contracting.subcontract.newNote")}>
        <ProjectContractPicker feed={feed} selection={selection} contracts={false} testId="subcontract-picker" />
        {project ? (
          <NewSubcontractForm
            projectId={project.id}
            onDone={(id, number) => {
              selectContracting({ subcontractId: id, subcontractNumber: number });
              setSubcontractInput(id);
            }}
          />
        ) : (
          <ExplainedEmpty
            title={t("contracting.subcontract.pickProjectTitle")}
            body={t("contracting.subcontract.pickProjectBody")}
            testId="subcontract-pick-project"
          />
        )}
      </Panel>

      <Panel
        title={t("contracting.advance.title")}
        note={t("contracting.advance.note")}
        testId="advance-panel"
      >
        {selection.subcontractId === "" ? (
          <ExplainedEmpty
            title={t("contracting.advance.needSubcontractTitle")}
            body={t("contracting.advance.needSubcontractBody")}
            testId="advance-needs-subcontract"
          />
        ) : (
          <AdvanceForm subcontractId={selection.subcontractId} />
        )}
      </Panel>
    </section>
  );
}
