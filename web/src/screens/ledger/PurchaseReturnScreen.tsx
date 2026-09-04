/* ═══════════════════════════════════════════════════════════════════════════
   /ledger/purchase-return — مرتجع المشتريات  ·  The purchase return
   ───────────────────────────────────────────────────────────────────────────
   إشعارٌ مدين على المورّد، **على فاتورةٍ مخزنيةٍ مُرحَّلة**. وأربعةٌ تحكمه،
   وكلُّها منقولةٌ عن نصّ العقد لا مُستنتَجة:

   ١ · **لا صافي في هذا الطلب — ولا يُخترَع.** نصّ العقد: صافي المرتجع
       «بتكلفة الاستلام الأصلي لا بتكلفة اليوم»، وتلك التكلفة يملكها دفتر
       المخزون وحده. فالطلب يحمل **الكمّية وسطر الاستلام والضريبة**، ويُملأ
       الصافي لحظة الترحيل. ولذلك **تعود المسوّدة بصافٍ صفر** — والشاشة تقول
       ذلك نصّاً بدل أن يُقرأ عطلاً.

   ٢ · **والضريبة تُسلَّم**: هي بتصنيف الفاتورة الأصلية، وواقعةٌ تجارية لا
       يملكها المخزون. فهي الحقل المالي الوحيد في هذا النموذج.

   ٣ · **المسوّدة ثم الترحيل فعلان لا فعل**، وإعادةُ الترحيل **آمنة**: نصّ
       العقد «201 أوّلاً و200 ثانياً ومعرّف القيد نفسه»، و`alreadyPosted`
       تُعرَض بلوحٍ يقول «رُحِّل من قبل» — لا خطأً ولا نجاحاً ثانياً.

   ٤ · **ولا رقمَ حساب.** ما يُعرض من الترحيل معرّفُ قيدٍ ومجاميعُ مستند،
       وحساباتُه تحلّها مصفوفة الترحيل وحدها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { draftPurchaseReturn, postPurchaseReturn, readPurchaseReturn } from "../../api/generated/client";
import { asQuantity } from "../../api/generated/brands";
import type { CommercialDocument } from "../../api/generated/types";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
import {
  AccAction,
  AccField,
  AccRow,
  AccState,
  ChooseCompanyFirst,
  DeclaredGap,
  DocumentTotals,
  EntryRef,
  PostingReceipt,
  StatePanel,
  isMoneyText,
  isQuantityText,
  todayIso,
} from "../accounting/parts";
import { POSTED } from "../accounting/contract";
import { LedgerSectionNav } from "./parts";
import "../accounting/accounting.css";

/** الشاشة كاملةً. */
export function PurchaseReturnScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const [postCls, firePost] = useMoment("post");
  const [, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── المسوّدة ─────────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [billId, setBillId] = useState("");
  const [issuedOn, setIssuedOn] = useState(todayIso);
  const [receiptLineId, setReceiptLineId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [tax, setTax] = useState("0");

  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [returnId, setReturnId] = useState("");
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const doc = useQuery({
    queryKey: ["ledger", "purchase-return", config.baseUrl, config.token, config.companyId, returnId],
    enabled: config.companyId !== "" && returnId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readPurchaseReturn(transport, { companyId: config.companyId, returnId }, signal),
  });

  const quantityBad = quantity !== "" && !isQuantityText(quantity);
  const taxBad = tax !== "" && !isMoneyText(tax);
  const draftReady =
    number !== "" &&
    billId !== "" &&
    issuedOn !== "" &&
    receiptLineId !== "" &&
    isQuantityText(quantity) &&
    isMoneyText(tax);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const created = await draftPurchaseReturn(transport, {
        companyId: config.companyId,
        body: {
          billId,
          issuedOn,
          number,
          quantity: asQuantity(quantity),
          receiptLineId,
          tax: Money.wire(tax),
        },
      });
      setReturnId(created.id);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [
    billId,
    config.companyId,
    fireArrive,
    fireRefuse,
    issuedOn,
    number,
    quantity,
    receiptLineId,
    tax,
    transport,
  ]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postPurchaseReturn(transport, { companyId: config.companyId, returnId });
      setPosted(done);
      await doc.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, doc, fireArrive, firePost, fireRefuse, returnId, transport]);

  const current: CommercialDocument | null = doc.data ?? null;
  const postReady = returnId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="ledger-return-needs-company" />;

  return (
    <section className="stack" data-testid="ledger-return-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.ledger.page.returnTitle")}</h1>
          <p className="sub">{t("accounting.ledger.page.returnLede")}</p>
        </div>
      </header>

      <LedgerSectionNav current="/ledger/purchase-return" />

      {/* ══════════════════════════════════ ١ · المسوّدة ══════════════ */}
      <StatePanel
        title={t("accounting.ledger.ret.draftTitle")}
        note={t("accounting.ledger.ret.draftNote")}
        testId="ledger-return-draft"
      >
        <AccRow cols={3} testId="ledger-return-row-1">
          <AccField
            id="ledger-ret-number"
            label={t("accounting.ledger.field.returnNumber")}
            hint={t("accounting.ledger.field.returnNumberHint")}
            source="typed"
            required
          >
            <input
              id="ledger-ret-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-ret-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-ret-bill"
            label={t("accounting.ledger.field.billId")}
            hint={t("accounting.ledger.field.billIdHint")}
            source="typed"
            required
          >
            <input
              id="ledger-ret-bill"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-ret-bill"
              value={billId}
              onChange={(e) => setBillId(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-ret-issued"
            label={t("accounting.ledger.field.issuedOn")}
            hint={t("accounting.ledger.field.issuedOnHint")}
            source="typed"
            required
          >
            <input
              id="ledger-ret-issued"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="ledger-ret-issued"
              value={issuedOn}
              onChange={(e) => setIssuedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <AccRow cols={3} testId="ledger-return-row-2">
          <AccField
            id="ledger-ret-line"
            label={t("accounting.ledger.field.receiptLineId")}
            hint={t("accounting.ledger.field.receiptLineIdHint")}
            source="typed"
            required
          >
            <input
              id="ledger-ret-line"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-ret-line"
              value={receiptLineId}
              onChange={(e) => setReceiptLineId(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-ret-qty"
            label={t("accounting.ledger.field.returnQuantity")}
            hint={t("accounting.ledger.field.returnQuantityHint")}
            error={quantityBad ? t("accounting.ledger.field.quantityBad") : undefined}
            source="typed"
            required
          >
            <input
              id="ledger-ret-qty"
              className={"ctl amt-input" + (quantityBad ? " is-invalid" : "")}
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={quantityBad}
              data-testid="ledger-ret-qty"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-ret-tax"
            label={t("accounting.ledger.field.returnTax")}
            hint={t("accounting.ledger.field.returnTaxHint")}
            error={taxBad ? t("accounting.ledger.field.moneyBad") : undefined}
            source="typed"
            required
          >
            <input
              id="ledger-ret-tax"
              className={"ctl amt-input" + (taxBad ? " is-invalid" : "")}
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={taxBad}
              data-testid="ledger-ret-tax"
              value={tax}
              onChange={(e) => setTax(e.target.value)}
            />
          </AccField>
        </AccRow>

        <p className="hint" data-testid="ledger-return-no-net">
          {t("accounting.ledger.ret.noNet")}
        </p>

        <div className="inline-group">
          <Button
            label={t("accounting.ledger.act.draft")}
            kind="primary"
            loading={draftBusy}
            disabled={!draftReady || draftBusy}
            onClick={() => void submitDraft()}
            testId="ledger-ret-draft-submit"
          />
        </div>
        {draftError ? <ProblemPanel error={draftError} /> : null}
      </StatePanel>

      {/* ══════════════════════ ٢ · المرتجع وحاله وترحيله ═════════════ */}
      <StatePanel
        title={t("accounting.ledger.ret.docTitle")}
        note={t("accounting.ledger.ret.docNote")}
        aside={current ? <AccState state={current.state} testId="ledger-ret-state" /> : null}
        loading={doc.isPending && doc.fetchStatus === "fetching"}
        testId="ledger-return-doc"
      >
        <AccRow cols={2} testId="ledger-return-doc-row">
          <AccField
            id="ledger-ret-id"
            label={t("accounting.ledger.field.returnId")}
            hint={t("accounting.ledger.field.returnIdHint")}
            source="typed"
          >
            <input
              id="ledger-ret-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-ret-id"
              value={returnId}
              onChange={(e) => setReturnId(e.target.value)}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.ledger.act.post")}
              kind="primary"
              loading={postBusy}
              disabled={!postReady || postBusy}
              onClick={() => void submitPosting()}
              testId="ledger-ret-post"
            />
          </AccAction>
        </AccRow>

        {returnId === "" ? (
          <EmptyState
            title={t("accounting.ledger.ret.noneTitle")}
            body={t("accounting.ledger.ret.noneBody")}
            testId="ledger-return-none"
          />
        ) : doc.isError ? (
          <ProblemPanel error={doc.error} onRetry={() => void doc.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <DocumentTotals document={current} moment={arriveCls} testId="ledger-ret-totals" />
            <div className="kv">
              <div>
                <div className="k">{t("accounting.ledger.field.returnNumber")}</div>
                <div className="v mono acc-id" data-testid="ledger-ret-doc-number">
                  {current.number}
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.entryIdOfDoc")}</div>
                <div className="v">
                  <EntryRef entryId={current.entryId} testId="ledger-ret-doc-entry" />
                </div>
              </div>
            </div>
            <p className="hint">{t("accounting.ledger.ret.postHint")}</p>
          </div>
        ) : null}

        <div className={postCls}>
          {posted ? <PostingReceipt document={posted} testId="ledger-ret-receipt" /> : null}
        </div>
        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>

      {/* ═════════════════ ٣ · ما لا ينشره العقد — مُعلَناً ═══════════ */}
      <DeclaredGap
        title={t("accounting.ledger.gap.returnListTitle")}
        body={t("accounting.ledger.gap.returnListBody")}
        owed={t("accounting.ledger.gap.returnListOwed")}
        testId="ledger-return-gap"
      />
    </section>
  );
}
