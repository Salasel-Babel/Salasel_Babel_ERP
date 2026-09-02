/* ═══════════════════════════════════════════════════════════════════════════
   /purchasing/payment — سند الصرف للمورّد  ·  The supplier payment
   ───────────────────────────────────────────────────────────────────────────
   **ورسوم التحويل ليست ذمّة مورّد** — وهذه أهمّ جملةٍ في الشاشة، ونصُّ العقد
   عليها حرفاً: «يخرج من الخزينة `paid + bankFee` وينقص من ذمّة المورد `paid`
   وحده، ومجموع التخصيصات يُقاس على `paid` لا على مجموعهما». وخلطُهما «يجعل
   رصيد المورد أقلّ ممّا هو، فتظهر مطالبةٌ لا يعرف أحد مصدرها بعد أشهر».

   ولذلك **الحقلان منفصلان ومشروحان**، و**لا مجموعَ لهما على الشاشة**: مجموعٌ
   معروض كان سيُقرأ «هذا ما ينقص من ذمّة المورّد» وهو ليس كذلك. والجمع في
   الخادم لا في المتصفّح.

   و`settlementMethod` مؤهّل دور، و`treasuryPartyId` **طرفٌ لا رقم حساب**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftSupplierPayment,
  postSupplierPayment,
  readSupplierPayment,
} from "../../api/generated/client";
import type { CommercialDocument, PaymentAllocation } from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, RefusalPanel, useMoment } from "../../ui";
import { peekVoiceDraft } from "../../voice";
import { useAccountingFocus } from "./focus";
import {
  AccAction,
  AccField,
  AccRow,
  AccSectionNav,
  AccState,
  ChooseCompanyFirst,
  DropLineButton,
  EntryRef,
  PostingReceipt,
  StatePanel,
  isMoneyText,
  todayIso,
} from "./parts";
import { POSTED, PURCHASING_OVER_ALLOCATION, SETTLEMENT_METHODS } from "./contract";
import "./accounting.css";

/** معرّف قائمة اقتراح طرق التسوية. */
const METHOD_LIST = "acc-payment-methods";

/** تخصيصٌ كما يُكتب — المبلغ **نصّ** والفاتورة معرّف. */
interface DraftPaymentAllocation {
  billId: string;
  amount: string;
}

/** الشاشة كاملةً. */
export function SupplierPaymentScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [focus] = useAccountingFocus();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.supplier_payment.record") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return { supplier: of("supplier"), amount: of("amount"), method: of("method"), paidOn: of("paidOn") };
  }, []);

  /* ── رأس السند ────────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [supplierId, setSupplierId] = useState(spoken?.supplier ?? "");
  const [paidOn, setPaidOn] = useState(() => spoken?.paidOn || todayIso());
  const [paid, setPaid] = useState(spoken?.amount ?? "");
  const [bankFee, setBankFee] = useState("0");
  const [settlementMethod, setSettlementMethod] = useState(
    () => spoken?.method || SETTLEMENT_METHODS[0] || ""
  );
  const [treasuryPartyId, setTreasuryPartyId] = useState("");

  /* ── التخصيصات ────────────────────────────────────────────────────── */
  const [allocation, setAllocation] = useState<DraftPaymentAllocation>({
    billId: focus.billId,
    amount: "",
  });
  const [allocations, setAllocations] = useState<readonly DraftPaymentAllocation[]>([]);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [paymentId, setPaymentId] = useState("");
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const payment = useQuery({
    queryKey: ["accounting", "supplier-payment", config.baseUrl, config.token, config.companyId, paymentId],
    enabled: config.companyId !== "" && paymentId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSupplierPayment(transport, { companyId: config.companyId, paymentId }, signal),
  });

  const addAllocation = useCallback(() => {
    setAllocations((current) => [...current, allocation]);
    setAllocation({ billId: "", amount: "" });
  }, [allocation]);

  const dropAllocation = useCallback((index: number) => {
    setAllocations((current) => current.filter((_, i) => i !== index));
  }, []);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const wire: PaymentAllocation[] = allocations.map((one) => ({
        amount: Money.wire(one.amount),
        billId: one.billId,
      }));
      const created = await draftSupplierPayment(transport, {
        companyId: config.companyId,
        body: {
          allocations: wire,
          bankFee: Money.wire(bankFee),
          number,
          paid: Money.wire(paid),
          paidOn,
          settlementMethod,
          supplierId,
          treasuryPartyId,
        },
      });
      setPaymentId(created.id);
      setAllocations([]);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [
    allocations,
    bankFee,
    config.companyId,
    fireArrive,
    fireRefuse,
    number,
    paid,
    paidOn,
    settlementMethod,
    supplierId,
    transport,
    treasuryPartyId,
  ]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postSupplierPayment(transport, { companyId: config.companyId, paymentId });
      setPosted(done);
      await payment.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, payment, paymentId, transport]);

  const current: CommercialDocument | null = payment.data ?? null;
  const draftCode = draftError instanceof ProblemError ? draftError.code : null;
  const postCode = postError instanceof ProblemError ? postError.code : null;
  const overAllocated =
    draftCode === PURCHASING_OVER_ALLOCATION || postCode === PURCHASING_OVER_ALLOCATION;

  const draftReady =
    number !== "" &&
    supplierId !== "" &&
    paidOn !== "" &&
    isMoneyText(paid) &&
    isMoneyText(bankFee) &&
    settlementMethod !== "" &&
    treasuryPartyId !== "";
  const allocationReady = allocation.billId !== "" && isMoneyText(allocation.amount);
  const postReady = paymentId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-payment-needs-company" />;

  return (
    <section className="stack" data-testid="acc-supplier-payment-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.paymentTitle")}</h1>
          <p className="sub">{t("accounting.page.paymentLede")}</p>
        </div>
      </header>

      <AccSectionNav group="purchasing" current="/purchasing/payment" />

      {/* ══════════════════════════════════════ ١ · رأس السند ═════════ */}
      <StatePanel
        title={t("accounting.payment.headTitle")}
        note={t("accounting.payment.headNote")}
        testId="acc-payment-head"
      >
        <datalist id={METHOD_LIST}>
          {SETTLEMENT_METHODS.map((value) => (
            <option key={value} value={value} />
          ))}
        </datalist>
        <AccRow cols={3} testId="acc-payment-head-row-1">
          <AccField
            id="acc-sp-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source="typed"
            required
          >
            <input
              id="acc-sp-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sp-supplier"
            label={t("accounting.field.supplierId")}
            hint={t("accounting.field.supplierIdHint")}
            source={spoken?.supplier ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sp-supplier"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-supplier"
              value={supplierId}
              onChange={(e) => setSupplierId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sp-on"
            label={t("accounting.field.paidOn")}
            hint={t("accounting.field.paidOnHint")}
            source={spoken?.paidOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sp-on"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-payment-on"
              value={paidOn}
              onChange={(e) => setPaidOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <AccRow cols={4} testId="acc-payment-head-row-2">
          <AccField
            id="acc-sp-paid"
            label={t("accounting.field.paid")}
            hint={t("accounting.field.paidHint")}
            error={paid !== "" && !isMoneyText(paid) ? t("accounting.field.moneyBad") : undefined}
            source={spoken?.amount ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sp-paid"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={paid !== "" && !isMoneyText(paid)}
              data-testid="acc-payment-paid"
              value={paid}
              onChange={(e) => setPaid(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sp-fee"
            label={t("accounting.field.bankFee")}
            hint={t("accounting.field.bankFeeHint")}
            error={bankFee !== "" && !isMoneyText(bankFee) ? t("accounting.field.moneyBad") : undefined}
            source="typed"
            required
          >
            <input
              id="acc-sp-fee"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={bankFee !== "" && !isMoneyText(bankFee)}
              data-testid="acc-payment-fee"
              value={bankFee}
              onChange={(e) => setBankFee(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sp-method"
            label={t("accounting.field.settlementMethod")}
            hint={t("accounting.field.settlementMethodHint")}
            source={spoken?.method ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sp-method"
              className="ctl mono"
              dir="ltr"
              list={METHOD_LIST}
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-method"
              value={settlementMethod}
              onChange={(e) => setSettlementMethod(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sp-treasury"
            label={t("accounting.field.treasuryParty")}
            hint={t("accounting.field.treasuryPartyHint")}
            source="typed"
            required
          >
            <input
              id="acc-sp-treasury"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-treasury"
              value={treasuryPartyId}
              onChange={(e) => setTreasuryPartyId(e.target.value)}
            />
          </AccField>
        </AccRow>
        <p className="hint" data-testid="acc-payment-fee-rule">{t("accounting.payment.feeRule")}</p>
      </StatePanel>

      {/* ═════════════════════════════════════ ٢ · التخصيصات ══════════ */}
      <StatePanel
        title={t("accounting.alloc.paymentTitle")}
        note={t("accounting.alloc.paymentNote")}
        aside={<span className="muted">{tp("accounting.count.allocations", allocations.length)}</span>}
        testId="acc-payment-allocations"
      >
        <AccRow cols={3} testId="acc-payment-alloc-row">
          <AccField
            id="acc-sp-alloc-bill"
            label={t("accounting.field.postedBillId")}
            hint={t("accounting.field.postedBillIdHint")}
            source="typed"
          >
            <input
              id="acc-sp-alloc-bill"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-alloc-bill"
              value={allocation.billId}
              onChange={(e) => setAllocation({ ...allocation, billId: e.target.value })}
            />
          </AccField>
          <AccField
            id="acc-sp-alloc-amount"
            label={t("accounting.field.allocatedAmount")}
            hint={t("accounting.field.allocatedOnPaidHint")}
            error={
              allocation.amount !== "" && !isMoneyText(allocation.amount)
                ? t("accounting.field.moneyBad")
                : undefined
            }
            source="typed"
          >
            <input
              id="acc-sp-alloc-amount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={allocation.amount !== "" && !isMoneyText(allocation.amount)}
              data-testid="acc-payment-alloc-amount"
              value={allocation.amount}
              onChange={(e) => setAllocation({ ...allocation, amount: e.target.value })}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.addAllocation")}
              onClick={addAllocation}
              disabled={!allocationReady}
              testId="acc-payment-alloc-add"
            />
          </AccAction>
        </AccRow>

        {allocations.length === 0 ? (
          <EmptyState
            title={t("accounting.alloc.emptyTitle")}
            body={t("accounting.alloc.paymentEmptyBody")}
            small
            testId="acc-payment-alloc-empty"
          />
        ) : (
          <div className="acc-table" data-testid="acc-payment-alloc-table">
            <table>
              <caption className="visually-hidden">{t("accounting.alloc.paymentTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.field.postedBillId")}</th>
                  <th scope="col" className="n">{t("accounting.field.allocatedAmount")}</th>
                  <th scope="col">{t("accounting.field.action")}</th>
                </tr>
              </thead>
              <tbody>
                {allocations.map((one, index) => (
                  <tr key={index} data-testid={"acc-payment-alloc-" + String(index)}>
                    <td><span className="mono acc-id">{one.billId}</span></td>
                    <td className="n"><span className="mono">{one.amount}</span></td>
                    <td>
                      <DropLineButton
                        onClick={() => dropAllocation(index)}
                        testId={"acc-payment-alloc-drop-" + String(index)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═════════════════════════ ٣ · المسوّدة ثم الترحيل ═══════════ */}
      <StatePanel
        title={t("accounting.payment.docTitle")}
        note={t("accounting.payment.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-payment-state" /> : null}
        loading={payment.isPending && payment.fetchStatus === "fetching"}
        testId="acc-payment-doc"
      >
        <AccRow cols={2} testId="acc-payment-doc-row">
          <AccField
            id="acc-sp-id"
            label={t("accounting.field.paymentId")}
            hint={t("accounting.field.paymentIdHint")}
          >
            <input
              id="acc-sp-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-payment-id"
              value={paymentId}
              onChange={(e) => setPaymentId(e.target.value)}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.draft")}
              kind="primary"
              loading={draftBusy}
              disabled={!draftReady || draftBusy}
              onClick={() => void submitDraft()}
              testId="acc-payment-draft-submit"
            />
          </AccAction>
        </AccRow>

        {draftError ? <ProblemPanel error={draftError} /> : null}

        {overAllocated ? (
          <div className={refuseCls}>
            <RefusalPanel
              title={t("accounting.refusal.overPayTitle")}
              titleEn="The allocations exceed what was paid"
              body={t("accounting.refusal.overPayBody")}
              code={PURCHASING_OVER_ALLOCATION}
              codeLabel={t("accounting.refusal.code")}
              next={t("accounting.refusal.overPayNext")}
              testId="acc-payment-over-allocation"
            />
          </div>
        ) : null}

        {payment.isError ? (
          <ProblemPanel error={payment.error} onRetry={() => void payment.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <div className="kv">
              <div>
                <div className="k">{t("accounting.field.number")}</div>
                <div className="v mono acc-id" data-testid="acc-payment-doc-number">{current.number}</div>
              </div>
              <div>
                <div className="k">{t("accounting.field.entryId")}</div>
                <div className="v"><EntryRef entryId={current.entryId} testId="acc-payment-doc-entry" /></div>
              </div>
            </div>
            <div className={"inline-group " + postCls}>
              <Button
                label={t("accounting.act.post")}
                kind="primary"
                loading={postBusy}
                disabled={!postReady || postBusy}
                onClick={() => void submitPosting()}
                testId="acc-payment-post"
              />
              <span className="hint">{t("accounting.payment.postHint")}</span>
            </div>
          </div>
        ) : null}

        {posted ? <PostingReceipt document={posted} testId="acc-payment-receipt" /> : null}
        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>
    </section>
  );
}
