/* ═══════════════════════════════════════════════════════════════════════════
   /purchasing/bill — فاتورة المورّد  ·  The supplier bill
   ───────────────────────────────────────────────────────────────────────────
   وهي **فاتورة مصروف**: `draftExpenseBill` — «بلا مخزون ولا مطابقة ثلاثية».
   والفاتورة المخزنية بابٌ آخر (`draftStockBill`) لمستندٍ آخر، **ولا تُخلَط
   به**: خلطُهما يجعل مصروفاً يمرّ على دفتر المخزون أو بضاعةً تُصرَف بلا أن
   تدخل. والباب الثاني مُعلَنٌ في اللوح أدناه لا مسكوتٌ عنه.

   وثلاثة تحكمها:

   ١ · **مركز التكلفة إلزامي على المصروف** — نصُّ العقد: «مصروفٌ بلا مركز
       رقمٌ لا يُبوَّب». فلا ارتدادَ صامت إلى مركزٍ افتراضي في هذه الشاشة.

   ٢ · **`expenseCategory` مؤهّل دور لا رمز حساب.** المصفوفة وحدها تحلّه.

   ٣ · **المسودّة ثم الترحيل**، والترحيل الثاني يُعيد الإيصال نفسه
       و`alreadyPosted = true` — فيُقال، ولا يُعدّ خطأً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { draftExpenseBill, postSupplierBill, readSupplierBill } from "../../api/generated/client";
import type { CommercialDocument } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
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
  DocumentTotals,
  EntryRef,
  PostingReceipt,
  StatePanel,
  todayIso,
} from "./parts";
import { POSTED } from "./contract";
import "./accounting.css";

/** الشاشة كاملةً. */
export function SupplierBillScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [focus, setFocus] = useAccountingFocus();

  const [postCls, firePost] = useMoment("post");
  const [, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.supplier_bill.capture") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return { supplier: of("supplier"), billNumber: of("billNumber"), issuedOn: of("issuedOn") };
  }, []);

  /* ── رأس الفاتورة ─────────────────────────────────────────────────── */
  const [number, setNumber] = useState(spoken?.billNumber ?? "");
  const [supplierId, setSupplierId] = useState(spoken?.supplier ?? "");
  const [costCenterId, setCostCenterId] = useState("");
  const [expenseCategory, setExpenseCategory] = useState("");
  const [issuedOn, setIssuedOn] = useState(() => spoken?.issuedOn || todayIso());

  /* ── السطور ───────────────────────────────────────────────────────── */
  const [line, setLine] = useState<DraftPurchaseLine>(emptyPurchaseLine);
  const [lines, setLines] = useState<readonly DraftPurchaseLine[]>([]);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [billId, setBillId] = useState(focus.billId);
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const bill = useQuery({
    queryKey: ["accounting", "supplier-bill", config.baseUrl, config.token, config.companyId, billId],
    enabled: config.companyId !== "" && billId !== "",
    retry: false,
    queryFn: ({ signal }) => readSupplierBill(transport, { companyId: config.companyId, billId }, signal),
  });

  const addLine = useCallback(() => {
    setLines((current) => [...current, line]);
    setLine(emptyPurchaseLine());
  }, [line]);

  const dropLine = useCallback((index: number) => {
    setLines((current) => current.filter((_, i) => i !== index));
  }, []);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const created = await draftExpenseBill(transport, {
        companyId: config.companyId,
        body: {
          costCenterId,
          expenseCategory,
          issuedOn,
          lines: lines.map(toPurchaseLine),
          number,
          supplierId,
        },
      });
      setBillId(created.id);
      setFocus({ billId: created.id });
      setLines([]);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [
    config.companyId,
    costCenterId,
    expenseCategory,
    fireArrive,
    fireRefuse,
    issuedOn,
    lines,
    number,
    setFocus,
    supplierId,
    transport,
  ]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postSupplierBill(transport, { companyId: config.companyId, billId });
      setPosted(done);
      await bill.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [bill, billId, config.companyId, fireArrive, firePost, fireRefuse, transport]);

  const current: CommercialDocument | null = bill.data ?? null;
  const draftReady =
    number !== "" &&
    supplierId !== "" &&
    costCenterId !== "" &&
    expenseCategory !== "" &&
    issuedOn !== "" &&
    lines.length > 0;
  const postReady = billId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-bill-needs-company" />;

  return (
    <section className="stack" data-testid="acc-supplier-bill-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.billTitle")}</h1>
          <p className="sub">{t("accounting.page.billLede")}</p>
        </div>
      </header>

      <AccSectionNav group="purchasing" current="/purchasing/bill" />

      {/* ══════════════════════════════════════ ١ · رأس الفاتورة ══════ */}
      <StatePanel
        title={t("accounting.bill.headTitle")}
        note={t("accounting.bill.headNote")}
        testId="acc-bill-head"
      >
        <AccRow cols={3} testId="acc-bill-head-row-1">
          <AccField
            id="acc-sb-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source={spoken?.billNumber ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sb-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-bill-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sb-supplier"
            label={t("accounting.field.supplierId")}
            hint={t("accounting.field.supplierIdHint")}
            source={spoken?.supplier ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sb-supplier"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-bill-supplier"
              value={supplierId}
              onChange={(e) => setSupplierId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sb-issued"
            label={t("accounting.field.issuedOn")}
            hint={t("accounting.field.issuedOnHint")}
            source={spoken?.issuedOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-sb-issued"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-bill-issued"
              value={issuedOn}
              onChange={(e) => setIssuedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <AccRow cols={2} testId="acc-bill-head-row-2">
          <AccField
            id="acc-sb-cost-center"
            label={t("accounting.field.costCenterId")}
            hint={t("accounting.field.costCenterOnExpenseHint")}
            source="typed"
            required
          >
            <input
              id="acc-sb-cost-center"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-bill-cost-center"
              value={costCenterId}
              onChange={(e) => setCostCenterId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-sb-category"
            label={t("accounting.field.expenseCategory")}
            hint={t("accounting.field.expenseCategoryHint")}
            source="typed"
            required
          >
            <input
              id="acc-sb-category"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-bill-category"
              value={expenseCategory}
              onChange={(e) => setExpenseCategory(e.target.value)}
            />
          </AccField>
        </AccRow>
      </StatePanel>

      {/* ═══════════════════════════════════════ ٢ · السطور ═══════════ */}
      <StatePanel
        title={t("accounting.lines.title")}
        note={t("accounting.lines.expenseNote")}
        aside={<span className="muted">{tp("accounting.count.lines", lines.length)}</span>}
        testId="acc-bill-lines"
      >
        <PurchaseLineEditor line={line} onChange={setLine} idPrefix="acc-sb-line" />
        <div className="inline-group">
          <Button
            label={t("accounting.act.addLine")}
            onClick={addLine}
            disabled={!purchaseLineReady(line)}
            testId="acc-bill-add-line"
          />
        </div>
        {lines.length === 0 ? (
          <EmptyState
            title={t("accounting.lines.emptyTitle")}
            body={t("accounting.lines.emptyBody")}
            small
            testId="acc-bill-lines-empty"
          />
        ) : (
          <PurchaseLineTable lines={lines} onDrop={dropLine} />
        )}
      </StatePanel>

      {/* ═════════════════════════ ٣ · المسوّدة ثم الترحيل ═══════════ */}
      <StatePanel
        title={t("accounting.bill.docTitle")}
        note={t("accounting.bill.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-bill-state" /> : null}
        loading={bill.isPending && bill.fetchStatus === "fetching"}
        testId="acc-bill-doc"
      >
        <AccRow cols={2} testId="acc-bill-doc-row">
          <AccField
            id="acc-sb-id"
            label={t("accounting.field.billId")}
            hint={t("accounting.field.billIdHint")}
          >
            <input
              id="acc-sb-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-bill-id"
              value={billId}
              onChange={(e) => {
                setBillId(e.target.value);
                setFocus({ billId: e.target.value });
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
              testId="acc-bill-draft-submit"
            />
          </AccAction>
        </AccRow>

        {draftError ? <ProblemPanel error={draftError} /> : null}

        {billId === "" ? (
          <EmptyState
            title={t("accounting.bill.noneTitle")}
            body={t("accounting.bill.noneBody")}
            testId="acc-bill-none"
          />
        ) : bill.isError ? (
          <ProblemPanel error={bill.error} onRetry={() => void bill.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <DocumentTotals document={current} moment={arriveCls} testId="acc-bill-totals" />
            <div className="kv">
              <div>
                <div className="k">{t("accounting.field.number")}</div>
                <div className="v mono acc-id" data-testid="acc-bill-doc-number">{current.number}</div>
              </div>
              <div>
                <div className="k">{t("accounting.field.entryId")}</div>
                <div className="v"><EntryRef entryId={current.entryId} testId="acc-bill-doc-entry" /></div>
              </div>
            </div>
            <div className={"inline-group " + postCls}>
              <Button
                label={t("accounting.act.post")}
                kind="primary"
                loading={postBusy}
                disabled={!postReady || postBusy}
                onClick={() => void submitPosting()}
                testId="acc-bill-post"
              />
              <span className="hint">{t("accounting.bill.postHint")}</span>
            </div>
          </div>
        ) : null}

        {posted ? <PostingReceipt document={posted} testId="acc-bill-receipt" /> : null}
        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>

      {/* ═══════════════ ٤ · بابٌ منشور لمستندٍ آخر — مُعلَناً لا مخلوطاً ═ */}
      <DeclaredGap
        title={t("accounting.gap.stockBillTitle")}
        body={t("accounting.gap.stockBillBody")}
        owed={t("accounting.gap.stockBillOwed")}
        testId="acc-bill-stock-gap"
      />
    </section>
  );
}
