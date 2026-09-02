/* ═══════════════════════════════════════════════════════════════════════════
   /purchasing/order — أمر الشراء  ·  The purchase order
   ───────────────────────────────────────────────────────────────────────────
   **ولا زرَّ ترحيلٍ في هذه الشاشة، ولا يجوز أن يوجد.** وهذا ليس نقصاً بل هو
   الفرق نفسه: أمر الشراء **التزامٌ تعاقدي لا حدثٌ محاسبي** — لا مورد
   `…/posting` له في العقد، ولا حدث له في مصفوفة الترحيل. ومخطّط جوابه
   `PurchaseOrder` **لا يحمل `entryId` ولا `alreadyPosted`**، وهو المخطّط
   الوحيد على هذا السطح بلا هذين الحقلين: حقلٌ فارغ لهما كان سيُقرأ «لم
   يُرحَّل بعد» بدل «لا يُرحَّل أبداً»، فيبني عليه العميل زرّاً لا باب له.
   (ADR-0047.)

   فالشاشة تقول ذلك **نصّاً** بدل أن تسكت: زرٌّ غائب بلا شرحٍ يُقرأ عطلاً.

   **ومعرّفات سطوره هي مدخل الاستلام**: بلا نشرها يصير باب الاستلام باباً لا
   يوصل إليه بابٌ آخر. ولذلك تُعرَض معرّفات السطور، ويُحمَل معرّف الأمر إلى
   شاشة الاستلام بلا أن يُكتب بيدٍ مرّتين.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { createPurchaseOrder, readPurchaseOrder } from "../../api/generated/client";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, StatCard, useMoment } from "../../ui";
import { peekVoiceDraft } from "../../voice";
import { useAccountingFocus } from "./focus";
import {
  emptyPurchaseLine,
  PurchaseLineEditor,
  PurchaseLineTable,
  purchaseLineReady,
  toPurchaseLine,
  type DraftPurchaseLine,
} from "./lines";
import {
  AccAction,
  AccField,
  AccRow,
  AccSectionNav,
  AccState,
  ChooseCompanyFirst,
  DeclaredGap,
  StatePanel,
  todayIso,
} from "./parts";
import "./accounting.css";

/** الشاشة كاملةً. */
export function PurchaseOrderScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const navigate = useNavigate();
  const [focus, setFocus] = useAccountingFocus();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.purchase_order.draft") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return { supplier: of("supplier"), warehouse: of("warehouse"), orderedOn: of("orderedOn") };
  }, []);

  /* ── رأس الأمر ────────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [supplierId, setSupplierId] = useState(spoken?.supplier ?? "");
  const [warehouseId, setWarehouseId] = useState(spoken?.warehouse ?? "");
  const [costCenterId, setCostCenterId] = useState("");
  const [orderedOn, setOrderedOn] = useState(() => spoken?.orderedOn || todayIso());

  /* ── السطور ───────────────────────────────────────────────────────── */
  const [line, setLine] = useState<DraftPurchaseLine>(emptyPurchaseLine);
  const [lines, setLines] = useState<readonly DraftPurchaseLine[]>([]);

  /* ── الأمر ────────────────────────────────────────────────────────── */
  const [orderId, setOrderId] = useState(focus.orderId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const order = useQuery({
    queryKey: ["accounting", "purchase-order", config.baseUrl, config.token, config.companyId, orderId],
    enabled: config.companyId !== "" && orderId !== "",
    retry: false,
    queryFn: ({ signal }) => readPurchaseOrder(transport, { companyId: config.companyId, orderId }, signal),
  });

  const addLine = useCallback(() => {
    setLines((current) => [...current, line]);
    setLine(emptyPurchaseLine());
  }, [line]);

  const dropLine = useCallback((index: number) => {
    setLines((current) => current.filter((_, i) => i !== index));
  }, []);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await createPurchaseOrder(transport, {
        companyId: config.companyId,
        body: {
          costCenterId,
          lines: lines.map(toPurchaseLine),
          number,
          orderedOn,
          supplierId,
          warehouseId,
        },
      });
      setOrderId(created.id);
      setFocus({ orderId: created.id });
      setLines([]);
      fireArrive();
    } catch (failure) {
      setError(failure);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [
    config.companyId,
    costCenterId,
    fireArrive,
    fireRefuse,
    lines,
    number,
    orderedOn,
    setFocus,
    supplierId,
    transport,
    warehouseId,
  ]);

  const openReceipt = useCallback(() => {
    setFocus({ orderId });
    void navigate({ to: "/purchasing/goods-receipt" });
  }, [navigate, orderId, setFocus]);

  const current = order.data ?? null;
  const ready =
    number !== "" &&
    supplierId !== "" &&
    warehouseId !== "" &&
    costCenterId !== "" &&
    orderedOn !== "" &&
    lines.length > 0;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-order-needs-company" />;

  return (
    <section className="stack" data-testid="acc-purchase-order-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.orderTitle")}</h1>
          <p className="sub">{t("accounting.page.orderLede")}</p>
        </div>
      </header>

      <AccSectionNav group="purchasing" current="/purchasing/order" />

      {/* ═══════════ ١ · لماذا لا زرَّ ترحيلٍ هنا — مُعلَناً لا مسكوتاً ═ */}
      <DeclaredGap
        title={t("accounting.order.noPostTitle")}
        body={t("accounting.order.noPostBody")}
        owed={t("accounting.order.noPostOwed")}
        testId="acc-order-no-posting"
      />

      {/* ═══════════════════════════════════════ ٢ · رأس الأمر ════════ */}
      <StatePanel
        title={t("accounting.order.headTitle")}
        note={t("accounting.order.headNote")}
        testId="acc-order-head"
      >
        <AccRow cols={3} testId="acc-order-head-row-1">
          <AccField
            id="acc-po-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source="typed"
            required
          >
            <input
              id="acc-po-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-order-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-po-supplier"
            label={t("accounting.field.supplierId")}
            hint={t("accounting.field.supplierIdHint")}
            source={spoken?.supplier ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-po-supplier"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-order-supplier"
              value={supplierId}
              onChange={(e) => setSupplierId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-po-ordered"
            label={t("accounting.field.orderedOn")}
            hint={t("accounting.field.orderedOnHint")}
            source={spoken?.orderedOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-po-ordered"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-order-ordered"
              value={orderedOn}
              onChange={(e) => setOrderedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <AccRow cols={2} testId="acc-order-head-row-2">
          <AccField
            id="acc-po-warehouse"
            label={t("accounting.field.warehouseId")}
            hint={t("accounting.field.warehouseIdHint")}
            source={spoken?.warehouse ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-po-warehouse"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-order-warehouse"
              value={warehouseId}
              onChange={(e) => setWarehouseId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-po-cost-center"
            label={t("accounting.field.costCenterId")}
            hint={t("accounting.field.costCenterIdHint")}
            source="typed"
            required
          >
            <input
              id="acc-po-cost-center"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-order-cost-center"
              value={costCenterId}
              onChange={(e) => setCostCenterId(e.target.value)}
            />
          </AccField>
        </AccRow>
      </StatePanel>

      {/* ═══════════════════════════════════════ ٣ · السطور ═══════════ */}
      <StatePanel
        title={t("accounting.lines.title")}
        note={t("accounting.lines.purchaseNote")}
        aside={<span className="muted">{tp("accounting.count.lines", lines.length)}</span>}
        testId="acc-order-lines"
      >
        <PurchaseLineEditor line={line} onChange={setLine} idPrefix="acc-po-line" />
        <div className="inline-group">
          <Button
            label={t("accounting.act.addLine")}
            onClick={addLine}
            disabled={!purchaseLineReady(line)}
            testId="acc-order-add-line"
          />
        </div>
        {lines.length === 0 ? (
          <EmptyState
            title={t("accounting.lines.emptyTitle")}
            body={t("accounting.lines.emptyBody")}
            small
            testId="acc-order-lines-empty"
          />
        ) : (
          <PurchaseLineTable lines={lines} onDrop={dropLine} />
        )}
      </StatePanel>

      {/* ══════════════════════════ ٤ · إنشاء الأمر ثم قراءته ════════ */}
      <StatePanel
        title={t("accounting.order.docTitle")}
        note={t("accounting.order.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-order-state" /> : null}
        loading={order.isPending && order.fetchStatus === "fetching"}
        testId="acc-order-doc"
      >
        <AccRow cols={2} testId="acc-order-doc-row">
          <AccField
            id="acc-po-id"
            label={t("accounting.field.orderId")}
            hint={t("accounting.field.orderIdHint")}
          >
            <input
              id="acc-po-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-order-id"
              value={orderId}
              onChange={(e) => {
                setOrderId(e.target.value);
                setFocus({ orderId: e.target.value });
              }}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.act.createOrder")}
              kind="primary"
              loading={busy}
              disabled={!ready || busy}
              onClick={() => void submit()}
              testId="acc-order-submit"
            />
          </AccAction>
        </AccRow>

        {error ? <ProblemPanel error={error} /> : null}

        {order.isError ? (
          <ProblemPanel error={order.error} onRetry={() => void order.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <div className="acc-stats acc-stats--3">
              <StatCard
                label={t("accounting.total.net")}
                amount={current.net}
                hint={t("accounting.total.netHint")}
                testId="acc-order-net"
              />
              <StatCard
                label={t("accounting.total.tax")}
                amount={current.tax}
                hint={t("accounting.total.taxHint")}
                testId="acc-order-tax"
              />
              <StatCard
                label={t("accounting.total.gross")}
                amount={current.gross}
                hint={t("accounting.total.grossHint")}
                tone="good"
                testId="acc-order-gross"
              />
            </div>

            <div className="acc-table" data-testid="acc-order-line-ids">
              <table>
                <caption className="visually-hidden">{t("accounting.order.linesTitle")}</caption>
                <thead>
                  <tr>
                    <th scope="col" className="n">{t("accounting.field.lineNo")}</th>
                    <th scope="col">{t("accounting.field.orderLineId")}</th>
                    <th scope="col">{t("accounting.field.itemId")}</th>
                    <th scope="col" className="n">{t("accounting.field.quantity")}</th>
                    <th scope="col" className="n">{t("accounting.field.unitPrice")}</th>
                  </tr>
                </thead>
                <tbody>
                  {current.lines.map((orderLine) => (
                    <tr key={orderLine.id} data-testid={"acc-order-line-" + orderLine.id}>
                      <td className="n"><Num value={orderLine.lineNo} /></td>
                      <td><span className="mono acc-id">{orderLine.id}</span></td>
                      <td><span className="mono acc-id">{orderLine.itemId}</span></td>
                      {/* ⚠ **بلا وحدة، ولا تُخترَع واحدة.** `PurchaseOrderLine`
                          يحمل `quantity` ولا يحمل وحدتها، فالكمّية تُعرض كما
                          وصلت نصّاً — و«حبة» مكتوبةً هنا كانت ستكون معلومةً
                          لا مصدر لها. والنقص مُعلَنٌ في اللوح أدناه. */}
                      <td className="n"><span className="mono">{orderLine.quantity}</span></td>
                      <td className="n"><Amount value={orderLine.unitPrice} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <DeclaredGap
              title={t("accounting.gap.unitTitle")}
              body={t("accounting.gap.unitBody")}
              owed={t("accounting.gap.unitOwed")}
              testId="acc-order-unit-gap"
            />

            <div className="inline-group">
              <Button
                label={t("accounting.act.goToReceipt")}
                onClick={openReceipt}
                testId="acc-order-go-receipt"
              />
              <span className="hint">{t("accounting.order.goToReceiptHint")}</span>
            </div>
          </div>
        ) : (
          <EmptyState
            title={t("accounting.order.noneTitle")}
            body={t("accounting.order.noneBody")}
            testId="acc-order-none"
          />
        )}
      </StatePanel>
    </section>
  );
}
