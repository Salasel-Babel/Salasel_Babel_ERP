/* ═══════════════════════════════════════════════════════════════════════════
   /purchasing/goods-receipt — إشعار استلام البضاعة  ·  The goods receipt
   ───────────────────────────────────────────────────────────────────────────
   **الضلع الأول من المطابقة الثلاثية**، وأربع جملٍ تحكم الشاشة:

   ١ · **الاستلام يشير إلى سطر الأمر بمعرّفه**، ولا يحمل مورداً ولا مستودعاً:
       «مورده مورد الأمر، ومستودعه مستودع الأمر — وإعادة ذكرهما تفتح باب
       انحرافٍ عن الأمر الذي يُطابَق به لاحقاً». فالشاشة تقرأ الأمر أوّلاً
       وتُري سطوره، ثم يُستلَم **عليها**.

   ٢ · **ولا سعر على سطر الاستلام.** «التكلفة تُحسب في الوحدة بسعر أمر الشراء
       للكمية المستلمة، وسعرٌ يرسله العميل كان سيصير مصدر حقيقة ثانياً ينحرف
       عن الأمر». فلا حقلَ سعرٍ هنا — ولا يجوز أن يوجد.

   ٣ · **الزيادة تُرفض هنا لا عند الفاتورة**، برمز
       `purchasing.receipt_exceeds_order` الذي «يُسمّي الصنف والرقمين».
       والشاشة تعرض الرفض وتشرحه ولا تُخفيه.

   ٤ · **المسوّدة ثم الترحيل**: «لا مخزون ولا قيد قبل الترحيل — المسوّدة
       تحجز الكمية على سطر الأمر ولا تُدخل بضاعةً». والفرق مُظهَرٌ بالحالة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftGoodsReceipt,
  postGoodsReceipt,
  readGoodsReceipt,
  readGoodsReceiptLines,
  readPurchaseOrder,
} from "../../api/generated/client";
import { asQuantity } from "../../api/generated/brands";
import type { CommercialDocument, GoodsReceiptLine } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, Num, useT } from "../../i18n/react";
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
  isQuantityText,
  todayIso,
} from "./parts";
import { POSTED, RECEIPT_EXCEEDS_ORDER } from "./contract";
import "./accounting.css";

/** سطر استلامٍ كما يُكتب — سطرُ أمرٍ وكمّية، **ولا سعر**. */
interface DraftReceiptLine {
  orderLineId: string;
  quantity: string;
}

/** الشاشة كاملةً. */
export function GoodsReceiptScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [focus, setFocus] = useAccountingFocus();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.goods_receipt.draft") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return { orderNumber: of("orderNumber"), quantity: of("quantity"), receivedOn: of("receivedOn") };
  }, []);

  /* ── الأمر المستلَم عليه ──────────────────────────────────────────── */
  const [orderId, setOrderId] = useState(focus.orderId);

  /* ── رأس الاستلام ─────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [receivedOn, setReceivedOn] = useState(() => spoken?.receivedOn || todayIso());

  /* ── السطور ───────────────────────────────────────────────────────── */
  const [line, setLine] = useState<DraftReceiptLine>({
    orderLineId: "",
    quantity: spoken?.quantity ?? "",
  });
  const [lines, setLines] = useState<readonly DraftReceiptLine[]>([]);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [receiptId, setReceiptId] = useState(focus.goodsReceiptId);
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const order = useQuery({
    queryKey: ["accounting", "purchase-order", config.baseUrl, config.token, config.companyId, orderId],
    enabled: config.companyId !== "" && orderId !== "",
    retry: false,
    queryFn: ({ signal }) => readPurchaseOrder(transport, { companyId: config.companyId, orderId }, signal),
  });

  const receipt = useQuery({
    queryKey: ["accounting", "goods-receipt", config.baseUrl, config.token, config.companyId, receiptId],
    enabled: config.companyId !== "" && receiptId !== "",
    retry: false,
    queryFn: ({ signal }) => readGoodsReceipt(transport, { companyId: config.companyId, receiptId }, signal),
  });

  const receiptLines = useQuery({
    queryKey: ["accounting", "goods-receipt-lines", config.baseUrl, config.token, config.companyId, receiptId],
    enabled: config.companyId !== "" && receiptId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readGoodsReceiptLines(transport, { companyId: config.companyId, receiptId }, signal),
  });

  const addLine = useCallback(() => {
    setLines((current) => [...current, line]);
    setLine({ orderLineId: "", quantity: "" });
  }, [line]);

  const dropLine = useCallback((index: number) => {
    setLines((current) => current.filter((_, i) => i !== index));
  }, []);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const wire: GoodsReceiptLine[] = lines.map((one) => ({
        orderLineId: one.orderLineId,
        quantity: asQuantity(one.quantity),
      }));
      const created = await draftGoodsReceipt(transport, {
        companyId: config.companyId,
        body: { lines: wire, number, orderId, receivedOn },
      });
      setReceiptId(created.id);
      setFocus({ goodsReceiptId: created.id });
      setLines([]);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, lines, number, orderId, receivedOn, setFocus, transport]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postGoodsReceipt(transport, { companyId: config.companyId, receiptId });
      setPosted(done);
      await receipt.refetch();
      await receiptLines.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, receipt, receiptId, receiptLines, transport]);

  const current: CommercialDocument | null = receipt.data ?? null;
  const draftCode = draftError instanceof ProblemError ? draftError.code : null;
  const exceeds = draftCode === RECEIPT_EXCEEDS_ORDER;
  const lineReady = line.orderLineId !== "" && isQuantityText(line.quantity);
  const draftReady = number !== "" && orderId !== "" && receivedOn !== "" && lines.length > 0;
  const postReady = receiptId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-gr-needs-company" />;

  return (
    <section className="stack" data-testid="acc-goods-receipt-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.goodsReceiptTitle")}</h1>
          <p className="sub">{t("accounting.page.goodsReceiptLede")}</p>
        </div>
      </header>

      <AccSectionNav group="purchasing" current="/purchasing/goods-receipt" />

      {/* ═════════════════════ ١ · الأمر المستلَم عليه، وسطوره ════════ */}
      <StatePanel
        title={t("accounting.gr.orderTitle")}
        note={t("accounting.gr.orderNote")}
        loading={order.isPending && order.fetchStatus === "fetching"}
        testId="acc-gr-order"
      >
        <AccRow cols={3} testId="acc-gr-order-row">
          <AccField
            id="acc-gr-order-id"
            label={t("accounting.field.orderId")}
            hint={t("accounting.field.orderIdOnReceiptHint")}
            source="typed"
            required
          >
            <input
              id="acc-gr-order-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-gr-order-id"
              value={orderId}
              onChange={(e) => {
                setOrderId(e.target.value);
                setFocus({ orderId: e.target.value });
              }}
            />
          </AccField>
          <AccField
            id="acc-gr-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source={spoken?.orderNumber ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-gr-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-gr-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-gr-on"
            label={t("accounting.field.receivedOnGoods")}
            hint={t("accounting.field.receivedOnGoodsHint")}
            source={spoken?.receivedOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-gr-on"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-gr-on"
              value={receivedOn}
              onChange={(e) => setReceivedOn(e.target.value)}
            />
          </AccField>
        </AccRow>

        {order.isError ? (
          <ProblemPanel error={order.error} onRetry={() => void order.refetch()} />
        ) : order.data ? (
          <div className="acc-table" data-testid="acc-gr-order-lines">
            <table>
              <caption className="visually-hidden">{t("accounting.gr.orderLinesTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col" className="n">{t("accounting.field.lineNo")}</th>
                  <th scope="col">{t("accounting.field.orderLineId")}</th>
                  <th scope="col">{t("accounting.field.itemId")}</th>
                  <th scope="col" className="n">{t("accounting.field.orderedQuantity")}</th>
                  <th scope="col" className="n">{t("accounting.field.unitPrice")}</th>
                  <th scope="col">{t("accounting.field.action")}</th>
                </tr>
              </thead>
              <tbody>
                {order.data.lines.map((orderLine) => (
                  <tr key={orderLine.id}>
                    <td className="n"><Num value={orderLine.lineNo} /></td>
                    <td><span className="mono acc-id">{orderLine.id}</span></td>
                    <td><span className="mono acc-id">{orderLine.itemId}</span></td>
                    <td className="n"><span className="mono">{orderLine.quantity}</span></td>
                    <td className="n"><Amount value={orderLine.unitPrice} /></td>
                    <td>
                      <Button
                        label={t("accounting.act.pickLine")}
                        kind="ghost"
                        size="sm"
                        onClick={() => setLine({ orderLineId: orderLine.id, quantity: line.quantity })}
                        testId={"acc-gr-pick-" + orderLine.id}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            title={t("accounting.gr.noOrderTitle")}
            body={t("accounting.gr.noOrderBody")}
            small
            testId="acc-gr-no-order"
          />
        )}
      </StatePanel>

      {/* ════════════════════ ٢ · سطور الاستلام — ولا سعر فيها ═══════ */}
      <StatePanel
        title={t("accounting.gr.linesTitle")}
        note={t("accounting.gr.linesNote")}
        aside={<span className="muted">{tp("accounting.count.lines", lines.length)}</span>}
        testId="acc-gr-lines"
      >
        <AccRow cols={3} testId="acc-gr-line-row">
          <AccField
            id="acc-gr-line-order-line"
            label={t("accounting.field.orderLineId")}
            hint={t("accounting.field.orderLineIdHint")}
            source="typed"
            required
          >
            <input
              id="acc-gr-line-order-line"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-gr-line-order-line"
              value={line.orderLineId}
              onChange={(e) => setLine({ ...line, orderLineId: e.target.value })}
            />
          </AccField>
          <AccField
            id="acc-gr-line-qty"
            label={t("accounting.field.receivedQuantity")}
            hint={t("accounting.field.receivedQuantityHint")}
            error={
              line.quantity !== "" && !isQuantityText(line.quantity)
                ? t("accounting.field.quantityBad")
                : undefined
            }
            source={spoken?.quantity ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-gr-line-qty"
              className="ctl amt-input"
              inputMode="decimal"
              dir="ltr"
              autoComplete="off"
              aria-invalid={line.quantity !== "" && !isQuantityText(line.quantity)}
              data-testid="acc-gr-line-qty"
              value={line.quantity}
              onChange={(e) => setLine({ ...line, quantity: e.target.value })}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.addLine")}
              onClick={addLine}
              disabled={!lineReady}
              testId="acc-gr-add-line"
            />
          </AccAction>
        </AccRow>

        {lines.length === 0 ? (
          <EmptyState
            title={t("accounting.gr.emptyLinesTitle")}
            body={t("accounting.gr.emptyLinesBody")}
            small
            testId="acc-gr-lines-empty"
          />
        ) : (
          <div className="acc-table" data-testid="acc-gr-draft-lines">
            <table>
              <caption className="visually-hidden">{t("accounting.gr.linesTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.field.orderLineId")}</th>
                  <th scope="col" className="n">{t("accounting.field.receivedQuantity")}</th>
                  <th scope="col">{t("accounting.field.action")}</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((one, index) => (
                  <tr key={index} data-testid={"acc-gr-draft-line-" + String(index)}>
                    <td><span className="mono acc-id">{one.orderLineId}</span></td>
                    <td className="n"><span className="mono">{one.quantity}</span></td>
                    <td>
                      <DropLineButton
                        onClick={() => dropLine(index)}
                        testId={"acc-gr-drop-" + String(index)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═════════════ ٣ · المسوّدة، ثم سطورها المُسعَّرة، ثم الترحيل ═ */}
      <StatePanel
        title={t("accounting.gr.docTitle")}
        note={t("accounting.gr.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-gr-state" /> : null}
        loading={receipt.isPending && receipt.fetchStatus === "fetching"}
        testId="acc-gr-doc"
      >
        <AccRow cols={2} testId="acc-gr-doc-row">
          <AccField
            id="acc-gr-id"
            label={t("accounting.field.goodsReceiptId")}
            hint={t("accounting.field.goodsReceiptIdHint")}
          >
            <input
              id="acc-gr-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-gr-id"
              value={receiptId}
              onChange={(e) => {
                setReceiptId(e.target.value);
                setFocus({ goodsReceiptId: e.target.value });
              }}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.draft")}
              kind="primary"
              loading={draftBusy}
              disabled={!draftReady || draftBusy}
              onClick={() => void submitDraft()}
              testId="acc-gr-draft-submit"
            />
          </AccAction>
        </AccRow>

        {draftError ? <ProblemPanel error={draftError} /> : null}

        {exceeds ? (
          <div className={refuseCls}>
            <RefusalPanel
              title={t("accounting.refusal.exceedsTitle")}
              titleEn="The received quantity exceeds what the order line asked for"
              body={t("accounting.refusal.exceedsBody")}
              code={RECEIPT_EXCEEDS_ORDER}
              codeLabel={t("accounting.refusal.code")}
              next={t("accounting.refusal.exceedsNext")}
              testId="acc-gr-exceeds"
            />
          </div>
        ) : null}

        {receipt.isError ? (
          <ProblemPanel error={receipt.error} onRetry={() => void receipt.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <div className="kv">
              <div>
                <div className="k">{t("accounting.field.number")}</div>
                <div className="v mono acc-id" data-testid="acc-gr-doc-number">{current.number}</div>
              </div>
              <div>
                <div className="k">{t("accounting.field.entryId")}</div>
                <div className="v"><EntryRef entryId={current.entryId} testId="acc-gr-doc-entry" /></div>
              </div>
            </div>

            {receiptLines.data ? (
              <div className="acc-table" data-testid="acc-gr-priced-lines">
                <table>
                  <caption className="visually-hidden">{t("accounting.gr.pricedTitle")}</caption>
                  <thead>
                    <tr>
                      <th scope="col" className="n">{t("accounting.field.lineNo")}</th>
                      <th scope="col">{t("accounting.field.itemId")}</th>
                      <th scope="col" className="n">{t("accounting.field.receivedQuantity")}</th>
                      <th scope="col">{t("accounting.field.unit")}</th>
                      <th scope="col" className="n">{t("accounting.field.unitPrice")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {receiptLines.data.lines.map((one) => (
                      <tr key={one.id} data-testid={"acc-gr-priced-" + one.id}>
                        <td className="n"><Num value={one.lineNo} /></td>
                        <td><span className="mono acc-id">{one.itemId}</span></td>
                        <td className="n"><span className="mono">{one.quantity}</span></td>
                        <td><span className="mono acc-id">{one.unit}</span></td>
                        <td className="n"><Amount value={one.unitPrice} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}

            <div className={"inline-group " + postCls}>
              <Button
                label={t("accounting.act.post")}
                kind="primary"
                loading={postBusy}
                disabled={!postReady || postBusy}
                onClick={() => void submitPosting()}
                testId="acc-gr-post"
              />
              <span className="hint">{t("accounting.gr.postHint")}</span>
            </div>
          </div>
        ) : null}

        {posted ? <PostingReceipt document={posted} testId="acc-gr-receipt" /> : null}
        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>
    </section>
  );
}
