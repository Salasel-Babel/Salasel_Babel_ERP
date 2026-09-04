/* ═══════════════════════════════════════════════════════════════════════════
   /ledger/credit-note — إشعار الدائن  ·  The credit note
   ───────────────────────────────────────────────────────────────────────────
   **هذا هو الطريق الوحيد إلى تصحيح فاتورة مُرحَّلة** — نصّ العقد حرفاً: «لا
   تعديل ولا حذف على هذا السطح ولا في هذا النظام (ADR-0002)». فالشاشة تقول
   ذلك في صدرها: من فتحها يبحث عن زرّ تعديلٍ لا وجود له.

   وأربعةٌ تحكمها:

   ١ · **سطرٌ بمعرّف سطر فاتورةٍ أصلي هو ردُّ بضاعة، وبلا معرّفٍ تخفيضُ
       قيمة.** نصّ العقد: «والفرق قرار تجاري لا يُخمَّن». فالنموذج **يسأل
       عنه صراحةً** ولا يستنتجه من فراغ حقل: خانةُ اختيارٍ بقيمتين، والمعرّف
       يُطلَب حين تكون القيمة ردَّ بضاعة.

   ٢ · **ولا عميل في الطلب.** عميلُ الإشعار عميلُ الفاتورة الأصلية، وإعادةُ
       ذكره «تفتح باباً لإشعارٍ على عميل غير عميل فاتورته». فلا حقلَ عميلٍ
       هنا، ويُقال لماذا.

   ٣ · **المسوّدة ثم الترحيل**، وإعادةُ الترحيل تُعيد المستند نفسه
       و`alreadyPosted = true` ورمز 200 — تُعرَض بلوحٍ يقول ذلك.

   ٤ · **ولا `readCreditNote` في العقد.** لا بابَ قراءةٍ لإشعارٍ مفرد في
       199 عملية — فإشعارٌ أُنشئ ثم أُعيد تحميل الصفحة **لا يُعاد فتحه**،
       ولا حالَ له تُقرأ قبل الترحيل. وهذا نقصٌ مُعلَنٌ على الشاشة نفسها لا
       مسكوتٌ عنه، والمعرّف يُحفظ في الحالة ويُقبل مكتوباً بيد.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { draftCreditNote, postCreditNote } from "../../api/generated/client";
import type { CommercialDocument, SalesLine } from "../../api/generated/types";
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
  todayIso,
} from "../accounting/parts";
import {
  SalesLineEditor,
  emptySalesLine,
  salesLineReady,
  toSalesLine,
  type DraftSalesLine,
} from "../accounting/lines";
import { LedgerSectionNav } from "./parts";
import "../accounting/accounting.css";

/** ردُّ بضاعةٍ يُقيَّم بتكلفة صرفه الأصلي — ويحمل معرّف سطر الفاتورة. */
const GOODS_RETURN = "goodsReturn";
/** تخفيضُ قيمةٍ لا يُحرّك مخزوناً — ولا معرّف سطرٍ معه. */
const VALUE_REDUCTION = "valueReduction";

/** سطرُ إشعارٍ دائن: سطرُ مبيعاتٍ ومعه القرار التجاري الذي لا يُخمَّن. */
interface DraftNoteLine {
  readonly sales: DraftSalesLine;
  readonly kind: string;
  readonly originalInvoiceLineId: string;
}

/** الشاشة كاملةً. */
export function CreditNoteScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const [postCls, firePost] = useMoment("post");
  const [, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── رأس الإشعار ──────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [invoiceId, setInvoiceId] = useState("");
  const [issuedOn, setIssuedOn] = useState(todayIso);

  /* ── السطور ───────────────────────────────────────────────────────── */
  const [sales, setSales] = useState<DraftSalesLine>(emptySalesLine);
  const [kind, setKind] = useState(GOODS_RETURN);
  const [originalLineId, setOriginalLineId] = useState("");
  const [lines, setLines] = useState<readonly DraftNoteLine[]>([]);

  /* ── المستند والترحيل ─────────────────────────────────────────────── */
  const [noteId, setNoteId] = useState("");
  const [drafted, setDrafted] = useState<CommercialDocument | null>(null);
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [posted, setPosted] = useState<CommercialDocument | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  const lineReady =
    salesLineReady(sales) && (kind === VALUE_REDUCTION || originalLineId !== "");

  const addLine = useCallback(() => {
    setLines((current) => [
      ...current,
      { sales, kind, originalInvoiceLineId: kind === GOODS_RETURN ? originalLineId : "" },
    ]);
    setSales(emptySalesLine());
    setOriginalLineId("");
  }, [kind, originalLineId, sales]);

  const dropLine = useCallback((index: number) => {
    setLines((current) => current.filter((_, i) => i !== index));
  }, []);

  const draftReady = number !== "" && invoiceId !== "" && issuedOn !== "" && lines.length > 0;

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setDraftError(null);
    setPosted(null);
    try {
      const wire: SalesLine[] = lines.map((line) => ({
        ...toSalesLine(line.sales),
        originalInvoiceLineId:
          line.kind === GOODS_RETURN && line.originalInvoiceLineId !== ""
            ? line.originalInvoiceLineId
            : null,
      }));
      const created = await draftCreditNote(transport, {
        companyId: config.companyId,
        body: { invoiceId, issuedOn, lines: wire, number },
      });
      setDrafted(created);
      setNoteId(created.id);
      setLines([]);
      fireArrive();
    } catch (failure) {
      setDraftError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, invoiceId, issuedOn, lines, number, transport]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const done = await postCreditNote(transport, {
        companyId: config.companyId,
        creditNoteId: noteId,
      });
      setPosted(done);
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, fireArrive, firePost, fireRefuse, noteId, transport]);

  /* ما يُعرض من حالِ المستند هو **آخرُ ما ردّه سطحٌ منشور** — لا قراءةٌ
     مستقلّة، فالعقد لا ينشر لها باباً. والفارق مكتوبٌ في اللوح المُعلَن. */
  const current: CommercialDocument | null = posted ?? drafted;

  if (config.companyId === "") return <ChooseCompanyFirst testId="ledger-note-needs-company" />;

  return (
    <section className="stack" data-testid="ledger-note-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.ledger.page.noteTitle")}</h1>
          <p className="sub">{t("accounting.ledger.page.noteLede")}</p>
        </div>
      </header>

      <LedgerSectionNav current="/ledger/credit-note" />

      {/* ══════════════════════════════ ١ · رأس الإشعار ═══════════════ */}
      <StatePanel
        title={t("accounting.ledger.note.headTitle")}
        note={t("accounting.ledger.note.headNote")}
        testId="ledger-note-head"
      >
        <AccRow cols={3} testId="ledger-note-head-row">
          <AccField
            id="ledger-note-number"
            label={t("accounting.ledger.field.noteNumber")}
            hint={t("accounting.ledger.field.noteNumberHint")}
            source="typed"
            required
          >
            <input
              id="ledger-note-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-note-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-note-invoice"
            label={t("accounting.ledger.field.invoiceId")}
            hint={t("accounting.ledger.field.invoiceIdHint")}
            source="typed"
            required
          >
            <input
              id="ledger-note-invoice"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-note-invoice"
              value={invoiceId}
              onChange={(e) => setInvoiceId(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-note-issued"
            label={t("accounting.ledger.field.issuedOn")}
            hint={t("accounting.ledger.field.noteIssuedOnHint")}
            source="typed"
            required
          >
            <input
              id="ledger-note-issued"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="ledger-note-issued"
              value={issuedOn}
              onChange={(e) => setIssuedOn(e.target.value)}
            />
          </AccField>
        </AccRow>
        <p className="hint" data-testid="ledger-note-no-customer">
          {t("accounting.ledger.note.noCustomer")}
        </p>
      </StatePanel>

      {/* ══════════════════════════════════ ٢ · السطور ════════════════ */}
      <StatePanel
        title={t("accounting.ledger.note.linesTitle")}
        note={t("accounting.ledger.note.linesNote")}
        aside={<span className="muted">{tp("accounting.count.lines", lines.length)}</span>}
        testId="ledger-note-lines"
      >
        <SalesLineEditor line={sales} onChange={setSales} idPrefix="ledger-note-line" />
        <AccRow cols={2} testId="ledger-note-kind-row">
          <AccField
            id="ledger-note-kind"
            label={t("accounting.ledger.field.lineKind")}
            hint={t("accounting.ledger.field.lineKindHint")}
            source="typed"
            required
          >
            <select
              id="ledger-note-kind"
              className="ctl"
              data-testid="ledger-note-kind"
              value={kind}
              onChange={(e) => setKind(e.target.value)}
            >
              <option value={GOODS_RETURN}>{t("accounting.ledger.note.kindGoods")}</option>
              <option value={VALUE_REDUCTION}>{t("accounting.ledger.note.kindValue")}</option>
            </select>
          </AccField>
          <AccField
            id="ledger-note-original-line"
            label={t("accounting.ledger.field.originalInvoiceLineId")}
            hint={
              kind === GOODS_RETURN
                ? t("accounting.ledger.field.originalLineHint")
                : t("accounting.ledger.field.originalLineOffHint")
            }
            source="typed"
            {...(kind === GOODS_RETURN ? { required: true } : {})}
          >
            <input
              id="ledger-note-original-line"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              disabled={kind !== GOODS_RETURN}
              data-testid="ledger-note-original-line"
              value={kind === GOODS_RETURN ? originalLineId : ""}
              onChange={(e) => setOriginalLineId(e.target.value)}
            />
          </AccField>
        </AccRow>
        <div className="inline-group">
          <Button
            label={t("accounting.ledger.act.addLine")}
            onClick={addLine}
            disabled={!lineReady}
            testId="ledger-note-add-line"
          />
        </div>

        {lines.length === 0 ? (
          <EmptyState
            title={t("accounting.ledger.note.linesEmptyTitle")}
            body={t("accounting.ledger.note.linesEmptyBody")}
            small
            testId="ledger-note-lines-empty"
          />
        ) : (
          <div className="acc-table" data-testid="ledger-note-lines-table">
            <table>
              <caption className="visually-hidden">
                {t("accounting.ledger.note.linesCaption")}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.ledger.field.narration")}</th>
                  <th scope="col">{t("accounting.ledger.field.itemGroup")}</th>
                  <th scope="col" className="n">{t("accounting.ledger.field.quantity")}</th>
                  <th scope="col" className="n">{t("accounting.ledger.field.unitPrice")}</th>
                  <th scope="col">{t("accounting.ledger.field.lineKind")}</th>
                  <th scope="col">{t("accounting.ledger.field.originalInvoiceLineId")}</th>
                  <th scope="col">{t("accounting.ledger.act.dropLine")}</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line, index) => (
                  <tr key={line.sales.descriptionAr + String(index)} data-testid={"ledger-note-line-" + index}>
                    <td>
                      <span lang="ar" dir="rtl">{line.sales.descriptionAr}</span>{" "}
                      <span className="alt" lang="en" dir="ltr">{line.sales.descriptionEn}</span>
                    </td>
                    <td><span className="mono acc-id">{line.sales.itemGroup}</span></td>
                    <td className="n"><span className="mono acc-id">{line.sales.quantity}</span></td>
                    <td className="n"><span className="mono acc-id">{line.sales.unitPrice}</span></td>
                    <td data-testid={"ledger-note-line-kind-" + index}>
                      {line.kind === GOODS_RETURN
                        ? t("accounting.ledger.note.kindGoods")
                        : t("accounting.ledger.note.kindValue")}
                    </td>
                    <td>
                      {line.originalInvoiceLineId === "" ? (
                        <span className="muted">{t("accounting.ledger.note.noOriginalLine")}</span>
                      ) : (
                        <span className="mono acc-id">{line.originalInvoiceLineId}</span>
                      )}
                    </td>
                    <td>
                      <Button
                        label={t("accounting.ledger.act.dropLine")}
                        kind="ghost"
                        size="sm"
                        onClick={() => dropLine(index)}
                        testId={"ledger-note-drop-" + index}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="hint">{t("accounting.ledger.note.noTotalsHere")}</p>
      </StatePanel>

      {/* ═══════════════════ ٣ · المسوّدة ثم الترحيل ══════════════════ */}
      <StatePanel
        title={t("accounting.ledger.note.docTitle")}
        note={t("accounting.ledger.note.docNote")}
        aside={current ? <AccState state={current.state} testId="ledger-note-state" /> : null}
        testId="ledger-note-doc"
      >
        <AccRow cols={2} testId="ledger-note-doc-row">
          <AccField
            id="ledger-note-id"
            label={t("accounting.ledger.field.noteId")}
            hint={t("accounting.ledger.field.noteIdHint")}
            source="typed"
          >
            <input
              id="ledger-note-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-note-id"
              value={noteId}
              onChange={(e) => setNoteId(e.target.value)}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.ledger.act.draft")}
              kind="primary"
              loading={draftBusy}
              disabled={!draftReady || draftBusy}
              onClick={() => void submitDraft()}
              testId="ledger-note-draft-submit"
            />
          </AccAction>
        </AccRow>

        {draftError ? <ProblemPanel error={draftError} /> : null}

        {current === null ? (
          <EmptyState
            title={t("accounting.ledger.note.noneTitle")}
            body={t("accounting.ledger.note.noneBody")}
            testId="ledger-note-none"
          />
        ) : (
          <div className={"stack " + arriveCls}>
            <DocumentTotals document={current} moment={arriveCls} testId="ledger-note-totals" />
            <div className="kv">
              <div>
                <div className="k">{t("accounting.ledger.field.noteNumber")}</div>
                <div className="v mono acc-id" data-testid="ledger-note-doc-number">
                  {current.number}
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.entryIdOfDoc")}</div>
                <div className="v">
                  <EntryRef entryId={current.entryId} testId="ledger-note-doc-entry" />
                </div>
              </div>
            </div>
          </div>
        )}

        <div className="inline-group">
          <Button
            label={t("accounting.ledger.act.post")}
            kind="primary"
            loading={postBusy}
            disabled={noteId === "" || postBusy}
            onClick={() => void submitPosting()}
            testId="ledger-note-post"
          />
          <span className="hint">{t("accounting.ledger.note.postHint")}</span>
        </div>

        <div className={postCls}>
          {posted ? <PostingReceipt document={posted} testId="ledger-note-receipt" /> : null}
        </div>
        {postError ? <ProblemPanel error={postError} /> : null}
      </StatePanel>

      {/* ═════════ ٤ · بابُ قراءةٍ غيرُ منشور — مُعلَناً لا مسكوتاً عنه ═ */}
      <DeclaredGap
        title={t("accounting.ledger.gap.noteReadTitle")}
        body={t("accounting.ledger.gap.noteReadBody")}
        owed={t("accounting.ledger.gap.noteReadOwed")}
        testId="ledger-note-gap"
      />
    </section>
  );
}
