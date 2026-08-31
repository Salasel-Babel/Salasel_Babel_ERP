/* ═══════════════════════════════════════════════════════════════════════════
   متأخّرات المستأجرين وسندات القبض
   Tenant arrears and the receipts against them
   ───────────────────────────────────────────────────────────────────────────
   **والمصالحة أوّل ما يُقرأ في هذه الشاشة لا آخره.** التقرير يصل ومعه
   `controlTotal` و`divergence` و`isReconciled`: مجموعُ دفتر المستأجرين مقابل
   حسابه الضابط في الدفتر العام. وفارقٌ غير صفري يعني أن **أحد الرقمين كاذب**،
   فيُعرَض في صدر الشاشة لا في حاشيتها — وتقريرٌ يعرض شرائح أعمارٍ جميلة فوق
   دفترٍ لا يصالح حسابَه الضابط يُقرأ ثقةً لا يستحقّها.

   **وأربعة أشياء لا تقع هنا:**
     · لا جمع ولا طرح على المال: الشرائح والمجاميع والفارق تصل محسوبةً من
       الاستعلام نفسه، والفرز بمقارنةٍ عشرية **نصّية** (`Money.compare`) بلا
       فاصلةٍ عائمة في أي خطوة.
     · ولا حكم مصالحةٍ في المتصفّح: `isReconciled` يصل من الخادم ولا يُشتقّ هنا.
     · ولا اسم حسابٍ ولا رمزه: القبض يبلغ الدفتر بمصفوفة الترحيل، والخزينة
       **طرفٌ في دفترٍ مساعد لا رقم حساب** — كما يقول العقد حرفياً.
     · ولا تخصيصَ بتخمين: سندٌ لا يُعرف صاحبه يُرحَّل على التحصيلات غير
       المخصَّصة، والتخصيص **قيدٌ مستقلّ يقع بعد الترحيل** وبمستأجرٍ مُسمّى.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import {
  allocateTenantReceipt,
  draftTenantReceipt,
  postTenantReceipt,
  readTenantArrearsAging,
} from "../../api/generated/client";
import type { ArrearsParty, TenantArrears, TenantReceipt } from "../../api/generated/types";
import { PARAM_readTenantArrearsAging_asOf_RE } from "../../api/generated/formats";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { Amount, useT } from "../../i18n/react";
import { EmptyState, Panel, StatCard, StatusBadge } from "../../ui";
import {
  NeedsCompany,
  Refusal,
  SETTLEMENT_METHODS,
  SectionHead,
  TranslatedName,
  isIsoDate,
  isMoneyText,
  todayIso,
  useWrite,
} from "./parts";

/** حالة المستند المُرحَّل، بالاسم الذي ينشره العقد. */
const POSTED = "POSTED";

/** شرائح الأعمار الخمس بترتيب قراءتها — والمجموع سادسٌ لا شريحة. */
const BANDS = ["notDue", "days1To30", "days31To60", "days61To90", "over90"] as const;

/** الشاشة كاملةً. */
export function ArrearsScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="realestate-arrears">
      <SectionHead
        here="arrears"
        title={t("realestate.arrears.title")}
        lede={t("realestate.arrears.lede")}
      />
      <ArrearsReport companyId={config.companyId} transport={transport} />
      <ReceiptFlow companyId={config.companyId} transport={transport} />
    </section>
  );
}

type Transport = ReturnType<typeof useApi>["transport"];

/* ══════════════════════════════════════════════════ تقرير الأعمار ═══ */

function ArrearsReport(props: { companyId: string; transport: Transport }): ReactNode {
  const { t, tp } = useT();
  const [asOf, setAsOf] = useState(todayIso);
  const read = useWrite<TenantArrears>("arrive");
  const dateValid = PARAM_readTenantArrearsAging_asOf_RE.test(asOf);

  const load = useCallback(() => {
    void read.run(() =>
      readTenantArrearsAging(props.transport, { companyId: props.companyId, asOf })
    );
  }, [asOf, props, read]);

  const report = read.value;

  /* الفرز بمقارنةٍ عشرية نصّية على المجموع — تنازلياً، فالأثقل أولاً.
     ولا `Number` ولا `parseFloat` في أي خطوة. */
  const parties: readonly ArrearsParty[] = useMemo(() => {
    if (!report) return [];
    return [...report.parties].sort((a, b) => -a.bands.total.compare(b.bands.total));
  }, [report]);

  return (
    <Panel
      title={t("realestate.arrears.report")}
      note={t("realestate.arrears.reportNote")}
      testId="re-arrears-report"
    >
      <div className="filterbar" role="search">
        <div className="field">
          <label htmlFor="re-arrears-asof">{t("realestate.arrears.asOf")}</label>
          <input
            id="re-arrears-asof"
            className={"ctl mono" + (dateValid ? "" : " is-invalid")}
            type="date"
            dir="ltr"
            aria-invalid={!dateValid}
            data-testid="re-arrears-asof"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
          />
          <span className={dateValid ? "hint" : "field-error"} role={dateValid ? undefined : "alert"}>
            {dateValid ? t("realestate.arrears.asOfHint") : t("realestate.common.dateBad")}
          </span>
        </div>
        <div className="inline-group">
          <button
            type="button"
            className="btn btn-primary"
            data-testid="re-arrears-load"
            disabled={!dateValid || read.busy}
            onClick={load}
          >
            {read.busy ? t("common.state.loading") : t("realestate.common.read")}
          </button>
        </div>
      </div>

      {read.busy ? (
        <div className="card card-pad" data-testid="re-arrears-loading">
          <strong>{t("common.state.loading")}</strong>
          <p className="muted">{t("common.state.loadingBody")}</p>
          <div className="skeleton-row cine-live" />
          <div className="skeleton-row cine-live" />
          <div className="skeleton-row cine-live" />
        </div>
      ) : null}

      {read.error ? <Refusal error={read.error} testId="re-arrears-refusal" /> : null}

      {report ? (
        <div className="stack">
          <section
            className={
              "alert " + (report.isReconciled ? "alert--success" : "alert--danger") + " " + read.moment
            }
            role="status"
            data-testid="re-arrears-reconciliation"
            data-reconciled={String(report.isReconciled)}
          >
            <div className="body">
              <p className="title">
                {report.isReconciled
                  ? t("realestate.arrears.reconciled")
                  : t("realestate.arrears.notReconciled")}
              </p>
              <p>
                {report.isReconciled
                  ? t("realestate.arrears.reconciledBody")
                  : t("realestate.arrears.notReconciledBody")}
              </p>
            </div>
          </section>

          <div className="stats-row">
            <StatCard
              label={t("realestate.arrears.controlTotal")}
              amount={report.controlTotal}
              testId="re-arrears-control"
            />
            <StatCard
              label={t("realestate.arrears.divergence")}
              amount={report.divergence}
              tone={report.isReconciled ? "good" : "bad"}
              testId="re-arrears-divergence"
            />
            {BANDS.map((band) => (
              <StatCard
                key={band}
                label={t("realestate.arrears.band." + band)}
                amount={report.totals[band]}
                tone={band === "over90" ? "bad" : "neutral"}
                testId={"re-arrears-total-" + band}
              />
            ))}
            <StatCard
              label={t("realestate.arrears.band.total")}
              amount={report.totals.total}
              testId="re-arrears-total"
            />
          </div>

          {parties.length === 0 ? (
            <EmptyState
              title={t("realestate.arrears.none")}
              body={t("realestate.arrears.noneBody")}
              testId="re-arrears-empty"
            />
          ) : (
            <>
              <p className="muted" data-testid="re-arrears-count">
                {tp("realestate.arrears.partyCount", parties.length)}
              </p>
              <div className="ledger" data-state="ready" data-testid="re-arrears-table">
                <table>
                  <caption className="visually-hidden">{t("realestate.arrears.report")}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("realestate.common.code")}</th>
                      <th scope="col">{t("realestate.kind.lessee")}</th>
                      {BANDS.map((band) => (
                        <th
                          scope="col"
                          key={band}
                          className={band === "over90" ? "n re-over" : "n"}
                        >
                          {t("realestate.arrears.band." + band)}
                        </th>
                      ))}
                      <th scope="col" className="n">
                        {t("realestate.arrears.band.total")}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {parties.map((party) => (
                      <tr key={party.partyId} data-testid="re-arrears-row">
                        <td className="code">{party.code}</td>
                        <td>
                          <TranslatedName
                            nameAr={party.nameAr}
                            translations={party.nameTranslations}
                          />
                          <span className="alt re-id">{party.partyId}</span>
                        </td>
                        {BANDS.map((band) => (
                          <td key={band} className={band === "over90" ? "n re-over" : "n"}>
                            <Amount value={party.bands[band]} />
                          </td>
                        ))}
                        <td className="n re-total">
                          <Amount value={party.bands.total} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr>
                      <td colSpan={2}>{t("acct.total")}</td>
                      {BANDS.map((band) => (
                        <td key={band} className="n">
                          <Amount value={report.totals[band]} className="amt--total" />
                        </td>
                      ))}
                      <td className="n">
                        <Amount value={report.totals.total} className="amt--total" />
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
              <p className="muted">{t("realestate.arrears.dueDateNote")}</p>
            </>
          )}
        </div>
      ) : null}

      {!report && !read.busy && !read.error ? (
        <EmptyState
          small
          title={t("realestate.arrears.idle")}
          body={t("realestate.arrears.idleBody")}
          testId="re-arrears-idle"
        />
      ) : null}
    </Panel>
  );
}

/* ═══════════════════════════════ سند القبض: مسوّدة ← ترحيل ← تخصيص ══ */

function ReceiptFlow(props: { companyId: string; transport: Transport }): ReactNode {
  const { t } = useT();
  const [number, setNumber] = useState("");
  const [receivedOn, setReceivedOn] = useState(todayIso);
  const [received, setReceived] = useState("");
  const [method, setMethod] = useState<string>(SETTLEMENT_METHODS[0]);
  const [treasury, setTreasury] = useState("");
  const [lesseeId, setLesseeId] = useState("");
  const [allocateTo, setAllocateTo] = useState("");

  const draft = useWrite<TenantReceipt>("arrive");
  const post = useWrite<TenantReceipt>("post");
  const allocate = useWrite<TenantReceipt>("post");

  const receipt = allocate.value ?? post.value ?? draft.value;
  const amountBad = received !== "" && !isMoneyText(received);
  const posted = receipt?.state === POSTED;
  const allocated = receipt?.isAllocated === true;

  const submitDraft = useCallback(() => {
    post.reset();
    allocate.reset();
    void draft.run(() =>
      draftTenantReceipt(props.transport, {
        companyId: props.companyId,
        body: {
          number,
          receivedOn,
          received: Money.wire(received),
          settlementMethod: method,
          treasuryPartyId: treasury,
          ...(lesseeId === "" ? {} : { lesseeId }),
        },
      })
    );
  }, [allocate, draft, lesseeId, method, number, post, props, received, receivedOn, treasury]);

  const submitPost = useCallback(() => {
    const current = draft.value;
    if (!current) return;
    void post.run(() =>
      postTenantReceipt(props.transport, { companyId: props.companyId, receiptId: current.id })
    );
  }, [draft.value, post, props]);

  const submitAllocate = useCallback(() => {
    const current = draft.value;
    if (!current) return;
    void allocate.run(() =>
      allocateTenantReceipt(props.transport, {
        companyId: props.companyId,
        receiptId: current.id,
        body: { lesseeId: allocateTo },
      })
    );
  }, [allocate, allocateTo, draft.value, props]);

  return (
    <Panel
      title={t("realestate.receipt.title")}
      note={t("realestate.receipt.note")}
      testId="re-receipt"
    >
      <div className="re-steps" data-testid="re-receipt-steps">
        <span className="re-step" data-state={receipt ? "done" : "active"}>
          <span className="re-step__dot" aria-hidden="true" />
          {t("realestate.receipt.stepDraft")}
        </span>
        <span className="re-step" data-state={posted ? "done" : receipt ? "active" : undefined}>
          <span className="re-step__dot" aria-hidden="true" />
          {t("realestate.receipt.stepPosted")}
        </span>
        <span className="re-step" data-state={allocated ? "done" : posted ? "active" : undefined}>
          <span className="re-step__dot" aria-hidden="true" />
          {t("realestate.receipt.stepAllocated")}
        </span>
      </div>

      <div className="grid fields-3">
        <div className="field">
          <label htmlFor="re-receipt-no">{t("realestate.receipt.number")}</label>
          <input
            id="re-receipt-no"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-receipt-no"
            value={number}
            onChange={(e) => setNumber(e.target.value)}
            placeholder="RCV-2026-0001"
          />
        </div>
        <div className="field">
          <label htmlFor="re-receipt-date">{t("realestate.receipt.receivedOn")}</label>
          <input
            id="re-receipt-date"
            className="ctl mono"
            type="date"
            dir="ltr"
            data-testid="re-receipt-date"
            value={receivedOn}
            onChange={(e) => setReceivedOn(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="re-receipt-amount">{t("realestate.receipt.received")}</label>
          <input
            id="re-receipt-amount"
            className={"ctl amt-input" + (amountBad ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            aria-invalid={amountBad}
            data-testid="re-receipt-amount"
            value={received}
            onChange={(e) => setReceived(e.target.value)}
            placeholder="0.0000"
          />
          <span className={amountBad ? "field-error" : "hint"} role={amountBad ? "alert" : undefined}>
            {amountBad ? t("realestate.common.moneyBad") : t("realestate.common.moneyHint")}
          </span>
        </div>
        <div className="field">
          <label htmlFor="re-receipt-method">{t("realestate.receipt.method")}</label>
          <input
            id="re-receipt-method"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            list="re-receipt-methods"
            data-testid="re-receipt-method"
            value={method}
            onChange={(e) => setMethod(e.target.value)}
          />
          <datalist id="re-receipt-methods">
            {SETTLEMENT_METHODS.map((one) => (
              <option key={one} value={one} />
            ))}
          </datalist>
          <span className="hint">{t("realestate.receipt.methodHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-receipt-treasury">{t("realestate.receipt.treasury")}</label>
          <input
            id="re-receipt-treasury"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-receipt-treasury"
            value={treasury}
            onChange={(e) => setTreasury(e.target.value)}
            placeholder="CASH-01"
          />
          <span className="hint">{t("realestate.receipt.treasuryHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-receipt-lessee">{t("realestate.receipt.lessee")}</label>
          <input
            id="re-receipt-lessee"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-receipt-lessee"
            value={lesseeId}
            onChange={(e) => setLesseeId(e.target.value)}
          />
          <span className="hint">{t("realestate.receipt.lesseeHint")}</span>
        </div>
      </div>

      <div className="inline-group">
        <button
          type="button"
          className="btn"
          data-testid="re-receipt-draft"
          disabled={
            number === "" ||
            !isIsoDate(receivedOn) ||
            !isMoneyText(received) ||
            method === "" ||
            treasury === "" ||
            draft.busy
          }
          onClick={submitDraft}
        >
          {draft.busy ? t("common.state.loading") : t("realestate.receipt.draftAction")}
        </button>
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-receipt-post"
          disabled={!draft.value || posted || post.busy}
          onClick={submitPost}
        >
          {post.busy ? t("common.state.loading") : t("common.action.post")}
        </button>
      </div>

      {draft.error ? <Refusal error={draft.error} testId="re-receipt-draft-refusal" /> : null}
      {post.error ? <Refusal error={post.error} testId="re-receipt-post-refusal" /> : null}

      {receipt ? (
        <section
          className={"alert " + (posted ? "alert--success " : "alert--info ") + (post.value ? post.moment : draft.moment)}
          role="status"
          data-testid="re-receipt-card"
          data-state={receipt.state}
          data-allocated={String(receipt.isAllocated)}
        >
          <div className="stats-row">
            <StatCard
              label={t("realestate.receipt.received")}
              amount={receipt.received}
              testId="re-receipt-amount-shown"
            />
          </div>
          <div className="kv">
            <div>
              <div className="k">{t("realestate.receipt.number")}</div>
              <div className="v code">{receipt.number}</div>
            </div>
            <div>
              <div className="k">{t("realestate.invoice.event")}</div>
              <div className="v code" data-testid="re-receipt-event">
                {receipt.eventCode}
              </div>
            </div>
            <div>
              <div className="k">{t("realestate.invoice.entry")}</div>
              <div className="v re-id">{receipt.entryId ?? t("common.label.dash")}</div>
            </div>
            <div>
              <div className="k">{t("realestate.receipt.allocationEntry")}</div>
              <div className="v re-id" data-testid="re-receipt-alloc-entry">
                {receipt.allocationEntryId ?? t("common.label.dash")}
              </div>
            </div>
          </div>
          <div className="row">
            <StatusBadge
              state={posted ? "posted" : "draft"}
              label={t("realestate.docState." + receipt.state)}
            />
            {receipt.alreadyPosted ? (
              <StatusBadge
                state="info"
                label={t("realestate.receipt.alreadyPosted")}
                testId="re-receipt-already"
              />
            ) : null}
          </div>
        </section>
      ) : null}

      {/* التخصيص قيدٌ مستقلّ يقع **بعد** الترحيل، وبمستأجرٍ مُسمّى لا بتخمين. */}
      <div className="card card-pad" data-testid="re-receipt-allocation">
        <h3 className="k">{t("realestate.receipt.allocation")}</h3>
        <p className="muted">{t("realestate.receipt.allocationNote")}</p>
        <div className="grid fields-half">
          <div className="field">
            <label htmlFor="re-alloc-lessee">{t("realestate.receipt.allocateTo")}</label>
            <input
              id="re-alloc-lessee"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="re-alloc-lessee"
              value={allocateTo}
              onChange={(e) => setAllocateTo(e.target.value)}
            />
          </div>
        </div>
        <div className="inline-group">
          <button
            type="button"
            className="btn btn-primary"
            data-testid="re-alloc-go"
            disabled={!draft.value || allocateTo === "" || allocate.busy}
            onClick={submitAllocate}
          >
            {allocate.busy ? t("common.state.loading") : t("realestate.receipt.allocateAction")}
          </button>
        </div>
        {allocate.error ? <Refusal error={allocate.error} testId="re-alloc-refusal" /> : null}
        {allocate.value ? (
          <p className={"alert alert--success " + allocate.moment} role="status" data-testid="re-alloc-done">
            {t("realestate.receipt.allocated")}
          </p>
        ) : null}
      </div>
    </Panel>
  );
}
