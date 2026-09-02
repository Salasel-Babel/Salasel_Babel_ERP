/* ═══════════════════════════════════════════════════════════════════════════
   أوامر التغيير — ما دخل نطاق العقد بعد توقيعه، ومن اعتمده
   Change orders — what entered the contract's scope after signature, and who approved it
   ───────────────────────────────────────────────────────────────────────────
   **لماذا شاشةٌ ولم تكن.** أمر التغيير كان نموذجاً ثالثاً مطويّاً في ذيل
   `/contracting`، وشاشةُ السجلّ تجيب سؤالاً آخر تماماً: «ما المشاريع والعقود
   المسجَّلة؟». وبابُ قراءة الأمر المفرد — `readChangeOrder` — **لم يكن يبلغه
   شيء في الواجهة كلّها**: أمرٌ سُجِّل ثم أُعيد تحميل الصفحة لا يُعاد فتحه
   بمعرّفه، ولا تُقرأ بنوده إلا بالمرور على العقد كلّه. وقاعدةُ ADR-0077 تقول
   القسمة بوحدة العمل: من يجلس أمام أمر تغييرٍ ورقيّ معتمَد يسأل سؤالاً واحداً
   — «ماذا أُضيف، وبكم، ومن وقّع؟» — لا «أي مشروعٍ أفتح».

   وأربعة قرارات تحكم هذا الملفّ:

   ١ · **الأمر التغييري لا يُرحَّل أبداً**، والعقد المنشور يقول ذلك ببنيته لا
       بتعليق: `ChangeOrder` **لا يحمل `entryId` ولا `alreadyPosted`**. فلا
       زرَّ ترحيلٍ هنا ولا شارة حالة — وشارةٌ فارغة تُقرأ «لم يُرحَّل بعد»
       وهو وعدٌ بدورةٍ لا وجود لها. والأثر المالي يقع على **المستخلص** الذي
       يقيس البند المُضاف، لا على الأمر.

   ٢ · **البنود المُضافة تُعرض بكمّياتها كما وصلت** (`scale="wire"`): عمود
       الكمّية هنا يُقارَن بعمود جدول الكمّيات في `/contracting`، والمقياس
       الموحَّد هو ما يجعل المقارنة بالعين ممكنة.

   ٣ · **لا رمز حساب**: البند وحدة تسعيرٍ داخل المشروع، ومصفوفة الترحيل وحدها
       تقرّر الحساب الذي يبلغه.

   ٤ · **ولا قائمةَ أوامرَ على مستوى المنشأة في العقد**: `readContractChangeOrders`
       تسرد أوامر **عقدٍ بعينه** وحدها. فالشاشة تقول ذلك ولا ترسم قائمةً لا
       تملكها، وتضع إلى جانبها بابَ القراءة بمعرّف.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { addChangeOrder, readChangeOrder, readContractChangeOrders } from "../../api/generated/client";
import type { BoqItem, ChangeOrder } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, Field, MOTION, Panel, QuantityValue, StatusBadge } from "../../ui";
import {
  BoqEditor,
  ContractingHead,
  ExplainedEmpty,
  Foldable,
  itemReady,
  LoadingPanel,
  NeedsCompany,
  newItem,
  ProjectContractPicker,
  ReadProblem,
  todayIso,
  toBoqRequest,
  useProjects,
  type DraftItem,
} from "./shared";
import { useContractingSelection } from "./selection";

/* ═════════════════════════════════════════ جدول البنود التي يُدخلها الأمر */

/**
 * بنود أمرٍ تغييري في جدول — وهي ما يجعل الأمر مقروءاً: رقمٌ وتاريخٌ بلا
 * بنودٍ يقول أن شيئاً وقع ولا يقول ماذا.
 * @param props البنود كما وصلت.
 */
function AddedItems(props: { readonly items: readonly BoqItem[]; readonly testId?: string }): ReactNode {
  const { t } = useT();
  if (props.items.length === 0) {
    return (
      <ExplainedEmpty
        title={t("contracting.changeOrder.noItemsTitle")}
        body={t("contracting.changeOrder.noItemsBody")}
        testId="change-order-no-items"
      />
    );
  }
  return (
    <div className="ledger" data-testid={props.testId ?? "change-order-items"}>
      <table>
        <caption className="visually-hidden">{t("contracting.changeOrder.itemsCaption")}</caption>
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
          {props.items.map((item) => (
            <tr key={item.id} className={MOTION.arrive} data-testid="change-order-item">
              <td className="code">
                <Num value={item.lineNo} />
              </td>
              <td className="code">{item.code}</td>
              <td>{item.descriptionAr}</td>
              <td className="n">
                {/* المقياس كما وصل لا مقصوصاً — ليُقارَن هذا العمود بعمود جدول
                    الكمّيات في شاشة السجلّ صفّاً بصفّ. */}
                <QuantityValue
                  magnitude={item.contractQuantity.magnitude}
                  unit={item.contractQuantity.unit}
                  scale="wire"
                />
              </td>
              <td className="n">
                <Amount value={item.unitRate} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** رأسُ أمرٍ تغييري: رقمه وتاريخه ومعتمِدُه وسببه. */
function OrderHead(props: { readonly order: ChangeOrder }): ReactNode {
  const { t, tp } = useT();
  const { order } = props;
  return (
    <>
      <div className="kv">
        <div>
          <div className="k">{t("contracting.common.number")}</div>
          <div className="v mono" dir="ltr" data-testid="change-order-number">
            {order.number}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.changeOrder.issuedOn")}</div>
          <div className="v mono" dir="ltr">
            {order.issuedOn}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.changeOrder.approvedBy")}</div>
          <div className="v" data-testid="change-order-approver">
            {order.approvedBy}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.changeOrder.reason")}</div>
          <div className="v">{order.reasonAr}</div>
        </div>
        <div>
          <div className="k">{t("contracting.changeOrder.itemCount")}</div>
          <div className="v">{tp("common.count.lines", order.addedItems.length)}</div>
        </div>
        <div>
          <div className="k">{t("contracting.common.state")}</div>
          <div className="v">
            <StatusBadge
              state="info"
              label={t("contracting.changeOrder.neverPostsBadge")}
              title={t("contracting.changeOrder.neverPosts")}
              testId="change-order-no-posting"
            />
          </div>
        </div>
      </div>
      <p className="muted">{t("contracting.changeOrder.neverPosts")}</p>
    </>
  );
}

/* ═══════════════════════════════ قراءة أمرٍ بمعرّفه — البابُ اليتيم يجد بيته */

function ReadOneOrder(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [typed, setTyped] = useState("");
  const [changeOrderId, setChangeOrderId] = useState("");

  const order = useQuery({
    queryKey: ["contracting", "change-order", config.baseUrl, config.token, config.companyId, changeOrderId],
    enabled: changeOrderId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readChangeOrder(transport, { companyId: config.companyId, changeOrderId }, signal),
  });

  return (
    <Panel
      title={t("contracting.changeOrder.readTitle")}
      note={t("contracting.changeOrder.readNote")}
      testId="change-order-read-panel"
    >
      <div className="filterbar">
        <Field
          id="co-read-id"
          label={t("contracting.changeOrder.idLabel")}
          hint={t("contracting.changeOrder.idHint")}
        >
          <input
            id="co-read-id"
            data-testid="co-read-id"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("contracting.changeOrder.read")}
              disabled={typed === ""}
              onClick={() => setChangeOrderId(typed)}
              testId="co-read-go"
            />
          </div>
        </div>
      </div>

      {changeOrderId === "" ? (
        <ExplainedEmpty
          title={t("contracting.changeOrder.noneReadTitle")}
          body={t("contracting.changeOrder.noneReadBody")}
          testId="co-read-none"
        />
      ) : order.isError ? (
        <ReadProblem error={order.error} onRetry={() => void order.refetch()} />
      ) : order.data ? (
        <div className={"stack " + MOTION.arrive} data-testid="co-read-out">
          <OrderHead order={order.data} />
          <AddedItems items={order.data.addedItems} testId="co-read-items" />
        </div>
      ) : (
        <LoadingPanel what={t("contracting.changeOrder.title")} testId="co-read-loading" />
      )}
    </Panel>
  );
}

/* ═════════════════════════════════════ أوامر عقدٍ بعينه — القائمة الوحيدة */

function ContractOrders(props: { readonly contractId: string }): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const orders = useQuery({
    queryKey: [
      "contracting",
      "changes",
      config.baseUrl,
      config.token,
      config.companyId,
      props.contractId,
    ],
    retry: false,
    queryFn: ({ signal }) =>
      readContractChangeOrders(
        transport,
        { companyId: config.companyId, contractId: props.contractId },
        signal
      ),
  });

  return (
    <Panel
      title={t("contracting.changeOrder.listTitle")}
      note={t("contracting.changeOrder.listNote")}
      testId="change-order-list-panel"
      aside={
        orders.data ? (
          <span className="muted" data-testid="change-order-count">
            {tp("contracting.count.changeOrders", orders.data.changeOrderCount)}
          </span>
        ) : null
      }
    >
      {orders.isError ? (
        <ReadProblem error={orders.error} onRetry={() => void orders.refetch()} />
      ) : orders.data && orders.data.changeOrders.length === 0 ? (
        <ExplainedEmpty
          title={t("contracting.changeOrder.emptyTitle")}
          body={t("contracting.changeOrder.emptyBody")}
          testId="change-order-empty"
        />
      ) : orders.data ? (
        <div className="stack" data-testid="change-order-list">
          {orders.data.changeOrders.map((order) => (
            <div key={order.id} className="card card-pad" data-testid="change-order-card">
              <OrderHead order={order} />
              <AddedItems items={order.addedItems} />
            </div>
          ))}
        </div>
      ) : (
        <LoadingPanel what={t("contracting.changeOrder.listTitle")} testId="change-order-loading" />
      )}
    </Panel>
  );
}

/* ══════════════════════════════════════════════ نموذج تسجيل أمرٍ تغييري */

function NewChangeOrderForm(props: {
  readonly contractId: string;
  readonly onDone: () => void;
}): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [issuedOn, setIssuedOn] = useState(todayIso);
  const [reasonAr, setReasonAr] = useState("");
  const [approvedBy, setApprovedBy] = useState("");
  const [items, setItems] = useState<DraftItem[]>(() => [newItem()]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [done, setDone] = useState<string | null>(null);

  const ready =
    number !== "" && issuedOn !== "" && reasonAr !== "" && approvedBy !== "" && items.every(itemReady);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await addChangeOrder(transport, {
        companyId: config.companyId,
        body: {
          number,
          contractId: props.contractId,
          issuedOn,
          reasonAr,
          approvedBy,
          addedItems: items.map(toBoqRequest),
        },
      });
      setDone(created.number);
      props.onDone();
      setNumber("");
      setReasonAr("");
      setItems([newItem()]);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [approvedBy, config.companyId, issuedOn, items, number, props, reasonAr, transport]);

  return (
    <div className="stack">
      <p className="muted">{t("contracting.changeOrder.lede")}</p>
      <div className="grid fields-4">
        <Field
          id="co-number"
          label={t("contracting.common.number")}
          hint={t("contracting.changeOrder.numberHint")}
          required
        >
          <input
            id="co-number"
            data-testid="co-number"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={number}
            onChange={(e) => setNumber(e.target.value)}
          />
        </Field>
        <Field
          id="co-issued"
          label={t("contracting.changeOrder.issuedOn")}
          hint={t("contracting.changeOrder.issuedOnHint")}
          required
        >
          <input
            id="co-issued"
            data-testid="co-issued"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={issuedOn}
            onChange={(e) => setIssuedOn(e.target.value)}
          />
        </Field>
        <Field
          id="co-approver"
          label={t("contracting.changeOrder.approvedBy")}
          hint={t("contracting.changeOrder.approvedByHint")}
          required
        >
          <input
            id="co-approver"
            data-testid="co-approver"
            className="ctl"
            value={approvedBy}
            onChange={(e) => setApprovedBy(e.target.value)}
          />
        </Field>
        <Field
          id="co-reason"
          label={t("contracting.changeOrder.reason")}
          hint={t("contracting.changeOrder.reasonHint")}
          required
        >
          <input
            id="co-reason"
            data-testid="co-reason"
            className="ctl"
            lang="ar"
            value={reasonAr}
            onChange={(e) => setReasonAr(e.target.value)}
          />
        </Field>
      </div>
      <h3 className="subhead">{t("contracting.boq.title")}</h3>
      <p className="muted">{t("contracting.boq.noAccountNote")}</p>
      <BoqEditor items={items} onChange={setItems} idPrefix="co" />
      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.changeOrder.save")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="new-change-order-save"
        />
        {done ? (
          <span className="pill pill--posted" data-testid="new-change-order-done">
            {done}
          </span>
        ) : null}
      </div>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** أوامر التغيير على عقد مقاولة. */
export function ChangeOrdersScreen(): ReactNode {
  const { t } = useT();
  const { config } = useApi();
  const feed = useProjects();
  const selection = useContractingSelection();
  const [reloadKey, setReloadKey] = useState(0);

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="contracting-change-orders">
      <ContractingHead
        title={t("contracting.changeOrder.screenTitle")}
        lede={t("contracting.changeOrder.screenLede")}
        testId="change-orders-head"
      />

      <ProjectContractPicker feed={feed} selection={selection} testId="change-order-picker" />

      {selection.contractId === "" ? (
        <ExplainedEmpty
          title={t("contracting.changeOrder.pickContractTitle")}
          body={t("contracting.changeOrder.pickContractBody")}
          testId="change-order-pick-contract"
        />
      ) : (
        <>
          <ContractOrders key={reloadKey} contractId={selection.contractId} />
          <Foldable
            title={t("contracting.changeOrder.title")}
            note={t("contracting.changeOrder.note")}
            openLabel={t("contracting.common.open")}
            closeLabel={t("contracting.common.close")}
            testId="fold-change-order"
          >
            <NewChangeOrderForm
              contractId={selection.contractId}
              onDone={() => setReloadKey((n) => n + 1)}
            />
          </Foldable>
        </>
      )}

      <ReadOneOrder />
    </section>
  );
}
