/* ═══════════════════════════════════════════════════════════════════════════
   /sales/receipt — سند القبض من عميل  ·  The customer receipt
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة التي وصفها صاحب المصلحة**: فاتورةٌ ثم سند قبض. وأربعة أشياء
   تحكمها، وكلّها منشورةٌ في العقد لا مُخترَعة هنا:

   ١ · **لا مجاميع في الطلب.** «المجموع هو received + settlementDiscount
       وتحسبه الوحدة». فالشاشة **لا تجمع** ولا تعرض مجموعاً مؤلَّفاً.

   ٢ · **التخصيص على فواتير مُرحَّلة وحدها**، وقائمةٌ فارغة **مقبولة شكلاً** —
       «سندٌ يُقبض على الحساب». فالفراغ هنا حالةٌ صحيحة تُشرَح، لا نقصٌ يُمنع.

   ٣ · **التخصيص الزائد مرفوض ويُسمّي الرقمين** برمز `sales.over_allocation`،
       ولا يصير الزائد دفعةً مقدّمة: «الدفعة المقدّمة مستندٌ آخر وحدثٌ آخر في
       مصفوفة الترحيل». فالرفض يُعرَض ويُشرَح ولا يُلتَفّ عليه.

   ٤ · **ولا رقم حساب.** `settlementMethod` **مؤهّل دور** تحلّه المصفوفة إلى
       حساب خزينة أو بنك، و`treasuryPartyId` **طرفٌ في دفتره المساعد لا رقم
       حساب**. والشاشة تقول ذلك بالكلمات كي لا يُكتب رقمٌ في حقلٍ ليس له.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftCustomerReceipt,
  postCustomerReceipt,
  readCustomerReceipt,
} from "../../api/generated/client";
import type { CommercialDocument, ReceiptAllocation } from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, RefusalPanel, useMoment } from "../../ui";
import { peekVoiceDraft } from "../../voice";
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
import { POSTED, SALES_OVER_ALLOCATION, SETTLEMENT_METHODS } from "./contract";
import "./accounting.css";

/** معرّف قائمة اقتراح طرق التسوية. */
const METHOD_LIST = "acc-settlement-methods";

/** تخصيصٌ كما يُكتب — المبلغ **نصّ** والفاتورة معرّف. */
interface DraftAllocation {
  invoiceId: string;
  amount: string;
}

/** الشاشة كاملةً. */
export function CustomerReceiptScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.customer_receipt.record") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return { customer: of("customer"), amount: of("amount"), method: of("method"), receivedOn: of("receivedOn") };
  }, []);

  /* ── رأس السند ────────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [customerId, setCustomerId] = useState(spoken?.customer ?? "");
  const [receivedOn, setReceivedOn] = useState(() => spoken?.receivedOn || todayIso());
  const [received, setReceived] = useState(spoken?.amount ?? "");
  const [settlementDiscount, setSettlementDiscount] = useState("0");
  const [settlementMethod, setSettlementMethod] = useState(
    () => spoken?.method || SETTLEMENT_METHODS[0] || ""
  );
  const [treasuryPartyId, setTreasuryPartyId] = useState("");

  /* ── التخصيصات ────────────────────────────────────────────────────── */
  const [allocation, setAllocation] = useState<DraftAllocation>({ invoiceId: "", amount: "" });
  const [allocations, setAllocations] = useState<readonly DraftAllocation[]>([]);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [receiptId, setReceiptId] = useState("");
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const receipt = useQuery({
    queryKey: ["accounting", "customer-receipt", config.baseUrl, config.token, config.companyId, receiptId],
    enabled: config.companyId !== "" && receiptId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readCustomerReceipt(transport, { companyId: config.companyId, receiptId }, signal),
  });

  const addAllocation = useCallback(() => {
    setAllocations((current) => [...current, allocation]);
    setAllocation({ invoiceId: "", amount: "" });
  }, [allocation]);

  const dropAllocation = useCallback((index: number) => {
    setAllocations((current) => current.filter((_, i) => i !== index));
  }, []);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const wire: ReceiptAllocation[] = allocations.map((one) => ({
        amount: Money.wire(one.amount),
        invoiceId: one.invoiceId,
      }));
      const created = await draftCustomerReceipt(transport, {
        companyId: config.companyId,
        body: {
          allocations: wire,
          customerId,
          number,
          received: Money.wire(received),
          receivedOn,
          settlementDiscount: Money.wire(settlementDiscount),
          settlementMethod,
          treasuryPartyId,
        },
      });
      setReceiptId(created.id);
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
    config.companyId,
    customerId,
    fireArrive,
    fireRefuse,
    number,
    received,
    receivedOn,
    settlementDiscount,
    settlementMethod,
    transport,
    treasuryPartyId,
  ]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postCustomerReceipt(transport, {
        companyId: config.companyId,
        receiptId,
      });
      setPosted(done);
      await receipt.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, receipt, receiptId, transport]);

  const current: CommercialDocument | null = receipt.data ?? null;
  const postCode = postError instanceof ProblemError ? postError.code : null;
  const draftCode = draftError instanceof ProblemError ? draftError.code : null;
  const overAllocated = draftCode === SALES_OVER_ALLOCATION || postCode === SALES_OVER_ALLOCATION;

  const draftReady =
    number !== "" &&
    customerId !== "" &&
    receivedOn !== "" &&
    isMoneyText(received) &&
    isMoneyText(settlementDiscount) &&
    settlementMethod !== "" &&
    treasuryPartyId !== "";
  const allocationReady = allocation.invoiceId !== "" && isMoneyText(allocation.amount);
  const postReady = receiptId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-receipt-needs-company" />;

  return (
    <section className="stack" data-testid="acc-customer-receipt-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.receiptTitle")}</h1>
          <p className="sub">{t("accounting.page.receiptLede")}</p>
        </div>
      </header>

      <AccSectionNav group="sales" current="/sales/receipt" />

      {/* ══════════════════════════════════════ ١ · رأس السند ═════════ */}
      <StatePanel
        title={t("accounting.receipt.headTitle")}
        note={t("accounting.receipt.headNote")}
        testId="acc-receipt-head"
      >
        <datalist id={METHOD_LIST}>
          {SETTLEMENT_METHODS.map((value) => (
            <option key={value} value={value} />
          ))}
        </datalist>
        <AccRow cols={3} testId="acc-receipt-head-row-1">
          <AccField
            id="acc-rc-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source="typed"
            required
          >
            <input
              id="acc-rc-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-rc-customer"
            label={t("accounting.field.customerId")}
            hint={t("accounting.field.customerIdHint")}
            source={spoken?.customer ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-rc-customer"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-customer"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-rc-on"
            label={t("accounting.field.receivedOn")}
            hint={t("accounting.field.receivedOnHint")}
            source={spoken?.receivedOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-rc-on"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-receipt-on"
              value={receivedOn}
              onChange={(e) => setReceivedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <AccRow cols={4} testId="acc-receipt-head-row-2">
          <AccField
            id="acc-rc-received"
            label={t("accounting.field.received")}
            hint={t("accounting.field.receivedHint")}
            error={received !== "" && !isMoneyText(received) ? t("accounting.field.moneyBad") : undefined}
            source={spoken?.amount ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-rc-received"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={received !== "" && !isMoneyText(received)}
              data-testid="acc-receipt-received"
              value={received}
              onChange={(e) => setReceived(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-rc-discount"
            label={t("accounting.field.settlementDiscount")}
            hint={t("accounting.field.settlementDiscountHint")}
            error={
              settlementDiscount !== "" && !isMoneyText(settlementDiscount)
                ? t("accounting.field.moneyBad")
                : undefined
            }
            source="typed"
            required
          >
            <input
              id="acc-rc-discount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={settlementDiscount !== "" && !isMoneyText(settlementDiscount)}
              data-testid="acc-receipt-discount"
              value={settlementDiscount}
              onChange={(e) => setSettlementDiscount(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-rc-method"
            label={t("accounting.field.settlementMethod")}
            hint={t("accounting.field.settlementMethodHint")}
            source={spoken?.method ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-rc-method"
              className="ctl mono"
              dir="ltr"
              list={METHOD_LIST}
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-method"
              value={settlementMethod}
              onChange={(e) => setSettlementMethod(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-rc-treasury"
            label={t("accounting.field.treasuryParty")}
            hint={t("accounting.field.treasuryPartyHint")}
            source="typed"
            required
          >
            <input
              id="acc-rc-treasury"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-treasury"
              value={treasuryPartyId}
              onChange={(e) => setTreasuryPartyId(e.target.value)}
            />
          </AccField>
        </AccRow>
      </StatePanel>

      {/* ════════════════════════════ ٢ · التخصيصات — والفراغ مقبول ═══ */}
      <StatePanel
        title={t("accounting.alloc.title")}
        note={t("accounting.alloc.note")}
        aside={<span className="muted">{tp("accounting.count.allocations", allocations.length)}</span>}
        testId="acc-receipt-allocations"
      >
        <AccRow cols={3} testId="acc-receipt-alloc-row">
          <AccField
            id="acc-rc-alloc-invoice"
            label={t("accounting.field.postedInvoiceId")}
            hint={t("accounting.field.postedInvoiceIdHint")}
            source="typed"
          >
            <input
              id="acc-rc-alloc-invoice"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-alloc-invoice"
              value={allocation.invoiceId}
              onChange={(e) => setAllocation({ ...allocation, invoiceId: e.target.value })}
            />
          </AccField>
          <AccField
            id="acc-rc-alloc-amount"
            label={t("accounting.field.allocatedAmount")}
            hint={t("accounting.field.allocatedAmountHint")}
            error={
              allocation.amount !== "" && !isMoneyText(allocation.amount)
                ? t("accounting.field.moneyBad")
                : undefined
            }
            source="typed"
          >
            <input
              id="acc-rc-alloc-amount"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={allocation.amount !== "" && !isMoneyText(allocation.amount)}
              data-testid="acc-receipt-alloc-amount"
              value={allocation.amount}
              onChange={(e) => setAllocation({ ...allocation, amount: e.target.value })}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.addAllocation")}
              onClick={addAllocation}
              disabled={!allocationReady}
              testId="acc-receipt-alloc-add"
            />
          </AccAction>
        </AccRow>

        {allocations.length === 0 ? (
          <EmptyState
            title={t("accounting.alloc.emptyTitle")}
            body={t("accounting.alloc.emptyBody")}
            small
            testId="acc-receipt-alloc-empty"
          />
        ) : (
          <div className="acc-table" data-testid="acc-receipt-alloc-table">
            <table>
              <caption className="visually-hidden">{t("accounting.alloc.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.field.postedInvoiceId")}</th>
                  <th scope="col" className="n">{t("accounting.field.allocatedAmount")}</th>
                  <th scope="col">{t("accounting.field.action")}</th>
                </tr>
              </thead>
              <tbody>
                {allocations.map((one, index) => (
                  <tr key={index} data-testid={"acc-receipt-alloc-" + String(index)}>
                    <td><span className="mono acc-id">{one.invoiceId}</span></td>
                    <td className="n"><span className="mono">{one.amount}</span></td>
                    <td>
                      <DropLineButton
                        onClick={() => dropAllocation(index)}
                        testId={"acc-receipt-alloc-drop-" + String(index)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═════════════════════ ٣ · إنشاء المسوّدة، ثم ترحيلها ═════════ */}
      <StatePanel
        title={t("accounting.receipt.docTitle")}
        note={t("accounting.receipt.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-receipt-state" /> : null}
        loading={receipt.isPending && receipt.fetchStatus === "fetching"}
        testId="acc-receipt-doc"
      >
        <AccRow cols={2} testId="acc-receipt-doc-row">
          <AccField
            id="acc-rc-id"
            label={t("accounting.field.receiptId")}
            hint={t("accounting.field.receiptIdHint")}
          >
            <input
              id="acc-rc-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-receipt-id"
              value={receiptId}
              onChange={(e) => setReceiptId(e.target.value)}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.draft")}
              kind="primary"
              loading={draftBusy}
              disabled={!draftReady || draftBusy}
              onClick={() => void submitDraft()}
              testId="acc-receipt-draft-submit"
            />
          </AccAction>
        </AccRow>

        {draftError ? <ProblemPanel error={draftError} /> : null}

        {current ? (
          <div className={"stack " + arriveCls}>
            <div className="kv">
              <div>
                <div className="k">{t("accounting.field.number")}</div>
                <div className="v mono acc-id" data-testid="acc-receipt-doc-number">{current.number}</div>
              </div>
              <div>
                <div className="k">{t("accounting.field.entryId")}</div>
                <div className="v">
                  <EntryRef entryId={current.entryId} testId="acc-receipt-doc-entry" />
                </div>
              </div>
            </div>
            <div className={"inline-group " + postCls}>
              <Button
                label={t("accounting.act.post")}
                kind="primary"
                loading={postBusy}
                disabled={!postReady || postBusy}
                onClick={() => void submitPosting()}
                testId="acc-receipt-post"
              />
              <span className="hint">{t("accounting.receipt.postHint")}</span>
            </div>
          </div>
        ) : receipt.isError ? (
          <ProblemPanel error={receipt.error} onRetry={() => void receipt.refetch()} />
        ) : null}

        {posted ? <PostingReceipt document={posted} testId="acc-receipt-receipt" /> : null}

        {overAllocated ? (
          <div className={refuseCls}>
            <RefusalPanel
              title={t("accounting.refusal.overAllocTitle")}
              titleEn="The allocations exceed what was collected"
              body={t("accounting.refusal.overAllocBody")}
              code={SALES_OVER_ALLOCATION}
              codeLabel={t("accounting.refusal.code")}
              next={t("accounting.refusal.overAllocNext")}
              testId="acc-receipt-over-allocation"
            />
          </div>
        ) : null}

        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>
    </section>
  );
}
