/* ═══════════════════════════════════════════════════════════════════════════
   /sales/invoice — فاتورة المبيعات  ·  The sales invoice
   ───────────────────────────────────────────────────────────────────────────
   **أول شاشةٍ في دورة أمين الصندوق**، وثلاث جملٍ تحكمها:

   ١ · **المسودّة ثمّ الترحيل خطوتان لا واحدة.** `draftSalesInvoice` يُنشئ
       ولا يُرحّل، و`…/posting` يُرحّل. ولا زرَّ يجمعهما: نداءٌ واحد يُنشئ
       ويُرحِّل يجعل فشلَ الترحيل يترك مسوّدةً يتيمة برقمٍ **صار مستعملاً**،
       فيرتدّ عليها المستخدم برفض «رقم مستند مستعمل من قبل» ولا يفهم لماذا.
       والفعل الذي لا رجعة فيه لا يُخبَّأ خلف فعلٍ يُراجَع.

   ٢ · **الترحيل الثاني يقول الحقيقة.** هوية الإحكام تُعيد الإيصال نفسه
       و`alreadyPosted = true`. فيُعرَض ذلك نصّاً — «رُحِّل من قبل، وهذا
       إيصاله» — ولا يُعدّ خطأً ولا نجاحاً ثانياً.

   ٣ · **المجاميع تُقرأ ولا تُحسب.** العقد ينصّ أن الطلب «لا مجاميع فيه:
       المجاميع تُحسب في الوحدة على السطر ثم تُجمع». فالشاشة **لا تجمع سطراً
       واحداً** — تُرسل السطور، وتقرأ `net` و`tax` و`gross` كما عادت.

   ── ولا رمزَ حسابٍ في هذه الشاشة إطلاقاً ────────────────────────────────
   السطر يحمل `itemGroup` — مؤهّل دور — والمصفوفة في `data/posting-matrix/`
   وحدها تحوّله إلى حساب. وما يُعرَض من القيد هو **معرّفه في الإيصال العائد**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { draftSalesInvoice, postSalesInvoice, readSalesInvoice } from "../../api/generated/client";
import type { CommercialDocument } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, RefusalPanel, useMoment } from "../../ui";
import { peekVoiceDraft } from "../../voice";
import { useAccountingFocus } from "./focus";
import {
  emptySalesLine,
  salesLineReady,
  SalesLineEditor,
  SalesLineTable,
  toSalesLine,
  type DraftSalesLine,
} from "./lines";
import {
  AccAction,
  AccField,
  AccRow,
  AccSectionNav,
  AccState,
  ChooseCompanyFirst,
  DocumentTotals,
  EntryRef,
  PostingReceipt,
  StatePanel,
  todayIso,
} from "./parts";
import { POSTED } from "./contract";
import "./accounting.css";

/** الشاشة كاملةً. */
export function SalesInvoiceScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [focus, setFocus] = useAccountingFocus();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── المسوّدة المنطوقة تصل إلى الحقل، لا إلى لوحةٍ بجانبه ──────────────
     المستخدم قال «اكتب فاتورة للعميل …»، فهبط هنا. والقيمة تُملأ في حقلها،
     ويبقى الباقي عليه. ⚠ **ولا يُنشأ شيء ولا يُرحَّل**: الزرّان يُضغطان بيد. */
  const spoken = useMemo(() => {
    const draft = peekVoiceDraft();
    if (draft?.intentId !== "accounting.sales_invoice.draft") return null;
    const of = (name: string) => draft.fields.find((field) => field.name === name)?.text ?? "";
    return {
      customer: of("customer"),
      invoiceNumber: of("invoiceNumber"),
      issuedOn: of("issuedOn"),
    };
  }, []);

  /* ── رأس الفاتورة ─────────────────────────────────────────────────── */
  const [number, setNumber] = useState(spoken?.invoiceNumber ?? "");
  const [customerId, setCustomerId] = useState(spoken?.customer ?? "");
  const [branchId, setBranchId] = useState("");
  const [issuedOn, setIssuedOn] = useState(() => spoken?.issuedOn || todayIso());

  /* ── السطور ───────────────────────────────────────────────────────── */
  const [line, setLine] = useState<DraftSalesLine>(emptySalesLine);
  const [lines, setLines] = useState<readonly DraftSalesLine[]>([]);

  /* ── المستند ──────────────────────────────────────────────────────── */
  const [invoiceId, setInvoiceId] = useState(focus.invoiceId);
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);

  /* ── الترحيل ──────────────────────────────────────────────────────── */
  const [receipt, setReceipt] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const invoice = useQuery({
    queryKey: ["accounting", "sales-invoice", config.baseUrl, config.token, config.companyId, invoiceId],
    enabled: config.companyId !== "" && invoiceId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSalesInvoice(transport, { companyId: config.companyId, invoiceId }, signal),
  });

  const addLine = useCallback(() => {
    setLines((current) => [...current, line]);
    setLine(emptySalesLine());
  }, [line]);

  const dropLine = useCallback((index: number) => {
    setLines((current) => current.filter((_, i) => i !== index));
  }, []);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setReceipt(null);
    try {
      const created = await draftSalesInvoice(transport, {
        companyId: config.companyId,
        body: {
          branchId,
          customerId,
          issuedOn,
          /* التحويل عند الحدّ: `Money.wire` و`asQuantity` تتحقّقان من النحو
             المنشور، فلا يغادر ما يرفضه الخادم. */
          lines: lines.map(toSalesLine),
          number,
        },
      });
      setInvoiceId(created.id);
      setFocus({ invoiceId: created.id });
      setLines([]);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [branchId, config.companyId, customerId, fireArrive, fireRefuse, issuedOn, lines, number, setFocus, transport]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const posted = await postSalesInvoice(transport, {
        companyId: config.companyId,
        invoiceId,
      });
      setReceipt(posted);
      await invoice.refetch();
      /* **الحركة تتبع ما وقع فعلاً**: مفردة الترحيل لا تُصرَف على نداءٍ لم
         يُرحِّل شيئاً — وصرفُها هناك يُفقدها معناها في المرّة التي تعني. */
      if (posted.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, invoice, invoiceId, transport]);

  const current: CommercialDocument | null = invoice.data ?? null;
  const draftReady =
    number !== "" && customerId !== "" && branchId !== "" && issuedOn !== "" && lines.length > 0;
  const postReady = invoiceId !== "" && current !== null && current.state !== POSTED;

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-invoice-needs-company" />;

  return (
    <section className="stack" data-testid="acc-sales-invoice-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.invoiceTitle")}</h1>
          <p className="sub">{t("accounting.page.invoiceLede")}</p>
        </div>
      </header>

      <AccSectionNav group="sales" current="/sales/invoice" />

      {/* ═══════════════════════════════════ ١ · رأس الفاتورة ═══════════ */}
      <StatePanel
        title={t("accounting.invoice.headTitle")}
        note={t("accounting.invoice.headNote")}
        testId="acc-invoice-head"
      >
        <AccRow cols={4} testId="acc-invoice-head-row">
          <AccField
            id="acc-inv-number"
            label={t("accounting.field.number")}
            hint={t("accounting.field.numberHint")}
            source={spoken?.invoiceNumber ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-inv-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-invoice-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-inv-customer"
            label={t("accounting.field.customerId")}
            hint={t("accounting.field.customerIdHint")}
            source={spoken?.customer ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-inv-customer"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-invoice-customer"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-inv-branch"
            label={t("accounting.field.branchId")}
            hint={t("accounting.field.branchIdHint")}
            source="typed"
            required
          >
            <input
              id="acc-inv-branch"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-invoice-branch"
              value={branchId}
              onChange={(e) => setBranchId(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-inv-issued"
            label={t("accounting.field.issuedOn")}
            hint={t("accounting.field.issuedOnHint")}
            source={spoken?.issuedOn ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-inv-issued"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="acc-invoice-issued"
              value={issuedOn}
              onChange={(e) => setIssuedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
      </StatePanel>

      {/* ═══════════════════════════════════════ ٢ · السطور ════════════ */}
      <StatePanel
        title={t("accounting.lines.title")}
        note={t("accounting.lines.salesNote")}
        aside={<span className="muted">{tp("accounting.count.lines", lines.length)}</span>}
        testId="acc-invoice-lines"
      >
        <SalesLineEditor line={line} onChange={setLine} idPrefix="acc-inv-line" />
        <div className="inline-group">
          <Button
            label={t("accounting.act.addLine")}
            onClick={addLine}
            disabled={!salesLineReady(line)}
            testId="acc-invoice-add-line"
          />
        </div>
        {lines.length === 0 ? (
          <EmptyState
            title={t("accounting.lines.emptyTitle")}
            body={t("accounting.lines.emptyBody")}
            small
            testId="acc-invoice-lines-empty"
          />
        ) : (
          <SalesLineTable lines={lines} onDrop={dropLine} />
        )}
      </StatePanel>

      {/* ══════════════════════════ ٣ · إنشاء المسوّدة — ولا ترحيل ═════ */}
      <StatePanel
        title={t("accounting.invoice.draftTitle")}
        note={t("accounting.invoice.draftNote")}
        testId="acc-invoice-draft"
      >
        <AccRow cols={2} testId="acc-invoice-draft-row">
          <AccField
            id="acc-inv-id"
            label={t("accounting.field.invoiceId")}
            hint={t("accounting.field.invoiceIdHint")}
          >
            <input
              id="acc-inv-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="acc-invoice-id"
              value={invoiceId}
              onChange={(e) => {
                setInvoiceId(e.target.value);
                setFocus({ invoiceId: e.target.value });
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
              testId="acc-invoice-draft-submit"
            />
          </AccAction>
        </AccRow>
        {draftError ? <ProblemPanel error={draftError} /> : null}
      </StatePanel>

      {/* ═══════════════════════ ٤ · المستند كما هو، ثم ترحيله ════════ */}
      <StatePanel
        title={t("accounting.invoice.docTitle")}
        note={t("accounting.invoice.docNote")}
        aside={current ? <AccState state={current.state} testId="acc-invoice-state" /> : null}
        loading={invoice.isPending && invoice.fetchStatus === "fetching"}
        testId="acc-invoice-doc"
      >
        {invoiceId === "" ? (
          <EmptyState
            title={t("accounting.invoice.noneTitle")}
            body={t("accounting.invoice.noneBody")}
            testId="acc-invoice-none"
          />
        ) : invoice.isError ? (
          <ProblemPanel error={invoice.error} onRetry={() => void invoice.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <DocumentTotals document={current} moment={arriveCls} />
            <div className="kv">
              <div>
                <div className="k">{t("accounting.field.number")}</div>
                <div className="v mono acc-id" data-testid="acc-invoice-doc-number">{current.number}</div>
              </div>
              <div>
                <div className="k">{t("accounting.field.entryId")}</div>
                <div className="v">
                  <EntryRef entryId={current.entryId} testId="acc-invoice-doc-entry" />
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
                testId="acc-invoice-post"
              />
              <span className="hint">{t("accounting.invoice.postHint")}</span>
            </div>
          </div>
        ) : null}

        {receipt ? <PostingReceipt document={receipt} testId="acc-invoice-receipt" /> : null}

        {postError ? (
          <div className={"stack " + refuseCls}>
            <ProblemPanel error={postError} />
            {postError instanceof ProblemError ? (
              <RefusalPanel
                title={t("accounting.refusal.postTitle")}
                titleEn="Posting was refused; the draft is untouched"
                body={t("accounting.refusal.postBody")}
                code={postError.code}
                codeLabel={t("accounting.refusal.code")}
                next={t("accounting.refusal.postNext")}
                testId="acc-invoice-post-refusal"
              />
            ) : null}
          </div>
        ) : null}
      </StatePanel>
    </section>
  );
}
