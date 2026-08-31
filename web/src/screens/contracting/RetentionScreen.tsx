/* ═══════════════════════════════════════════════════════════════════════════
   المحتجزات وكشف المقاولين — سجلّان مشتقّان من المُرحَّل وحده
   Retention and the subcontractor statement — two registers derived from posted entries alone
   ───────────────────────────────────────────────────────────────────────────
   أربعة قرارات تحكم هذه الشاشة:

   ١ · **الفراغ هنا ليس نقصاً بل أثرٌ مباشر لبندٍ معلَّق.** حركات المحتجز
       تُشتقّ من المستخلصات المُرحَّلة وحدها، وأول مستخلصٍ محجوب — فالسجلّ
       فارغٌ بحقّ، ويجب أن **يقول ذلك** لا أن يُقرأ عطلاً في القراءة.

   ٢ · **الإفراج والتحصيل يقعان على دفعةٍ مُسمّاة لا على رصيد مجمَّع.** ولذلك
       لا حقل «مبلغ المحتجز الكلي» في هذه الشاشة: الفعل ينطلق من **صفٍّ**
       في السجلّ ويحمل `movementId` الذي في ذلك الصفّ.

   ٣ · **والجانب يختار الفعل، ولا يُعرض فعلٌ لا يقع.** المحتجز الدائن
       (PAYABLE) يُفرَج عنه، والمدين (RECEIVABLE) يُحصَّل — والعرضُ ثم الرفض
       إهانةٌ لا خدمة.

   ٤ · **والمطابقة صفرٌ بالضبط لا «قريبٌ من الصفر».** فارقُ ريالٍ واحد بين
       الدفتر المساعد ونقطة الضبط فارقٌ يُسمّى ويُعرض بلوحٍ دائم، لا بشارةٍ
       خضراء متسامحة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftRetentionCollection,
  draftRetentionRelease,
  postRetentionCollection,
  postRetentionRelease,
  readRetentionRegister,
  readSubcontractorStatement,
} from "../../api/generated/client";
import { Money } from "../../api/money";
import type { ProjectsDocument, RetentionRegisterRow } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, Field, MOTION, Panel, StatCard, StatusBadge } from "../../ui";
import {
  ContractingHead,
  DocumentReceipt,
  ExplainedEmpty,
  isMoneyText,
  LoadingPanel,
  NeedsCompany,
  ReadProblem,
  todayIso,
  TranslatedName,
} from "./shared";

/** الجانبان كما ينشرهما العقد: مدينٌ لدى العميل · دائنٌ على المقاول. */
const RECEIVABLE = "RECEIVABLE";
const PAYABLE = "PAYABLE";

/* ═════════════════════════════════ الفعل على دفعةٍ مُسمّاة */

/**
 * إفراجٌ أو تحصيل على **حركةٍ بعينها**. والجانب يختار أيّهما، ولا يُعرض
 * الفعل الذي لا يقع على ذلك الجانب.
 * @param props الحركة.
 */
function MovementAction(props: { readonly row: RetentionRegisterRow; readonly onDone: () => void }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const { row } = props;
  const release = row.side === PAYABLE;

  const [number, setNumber] = useState("");
  const [on, setOn] = useState(todayIso);
  const [amount, setAmount] = useState("");
  const [approvedBy, setApprovedBy] = useState("");
  const [settlementMethod, setSettlementMethod] = useState("");
  const [treasuryPartyId, setTreasuryPartyId] = useState("");
  const [document, setDocument] = useState<ProjectsDocument | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const ready =
    number !== "" &&
    on !== "" &&
    isMoneyText(amount) &&
    (release ? approvedBy !== "" : settlementMethod !== "" && treasuryPartyId !== "");

  const draft = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = release
        ? await draftRetentionRelease(transport, {
            companyId: config.companyId,
            body: {
              number,
              retentionMovementId: row.movementId,
              releasedOn: on,
              amount: Money.wire(amount),
              approvedBy,
            },
          })
        : await draftRetentionCollection(transport, {
            companyId: config.companyId,
            body: {
              number,
              retentionMovementId: row.movementId,
              collectedOn: on,
              amount: Money.wire(amount),
              settlementMethod,
              treasuryPartyId,
            },
          });
      setDocument(created);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [amount, approvedBy, config.companyId, number, on, release, row.movementId, settlementMethod, transport, treasuryPartyId]);

  const post = useCallback(async () => {
    if (!document) return;
    setBusy(true);
    setError(null);
    try {
      const receipt = release
        ? await postRetentionRelease(transport, { companyId: config.companyId, releaseId: document.id })
        : await postRetentionCollection(transport, { companyId: config.companyId, collectionId: document.id });
      setDocument(receipt);
      props.onDone();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, document, props, release, transport]);

  return (
    <div className={"card card-pad " + MOTION.reveal} data-testid="movement-action">
      <div className="statline">
        <strong>{release ? t("contracting.retention.release") : t("contracting.retention.collect")}</strong>
        <span className="mono" dir="ltr">
          {row.movementId}
        </span>
      </div>
      <p className="muted">
        {release ? t("contracting.retention.releaseNote") : t("contracting.retention.collectNote")}
      </p>
      <div className="grid fields-3">
        <Field id="mv-number" label={t("contracting.common.number")} required>
          <input id="mv-number" className="ctl mono" dir="ltr" value={number} onChange={(e) => setNumber(e.target.value)} />
        </Field>
        <Field
          id="mv-date"
          label={release ? t("contracting.retention.releasedOn") : t("contracting.retention.collectedOn")}
          required
        >
          <input id="mv-date" className="ctl mono" type="date" dir="ltr" value={on} onChange={(e) => setOn(e.target.value)} />
        </Field>
        <Field
          id="mv-amount"
          label={t("contracting.advance.amount")}
          hint={amount === "" || isMoneyText(amount) ? t("contracting.common.moneyHint") : t("contracting.common.moneyBad")}
          required
        >
          <input
            id="mv-amount"
            className={"ctl amt-input" + (amount !== "" && !isMoneyText(amount) ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            aria-invalid={amount !== "" && !isMoneyText(amount)}
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.0000"
          />
        </Field>
        {release ? (
          <Field
            id="mv-approver"
            label={t("contracting.retention.approvedBy")}
            hint={t("contracting.retention.approvedByHint")}
            required
          >
            <input id="mv-approver" className="ctl" value={approvedBy} onChange={(e) => setApprovedBy(e.target.value)} />
          </Field>
        ) : (
          <>
            <Field
              id="mv-method"
              label={t("contracting.advance.settlementMethod")}
              hint={t("contracting.advance.settlementHint")}
              required
            >
              <input
                id="mv-method"
                className="ctl mono"
                dir="ltr"
                value={settlementMethod}
                onChange={(e) => setSettlementMethod(e.target.value)}
              />
            </Field>
            <Field id="mv-treasury" label={t("contracting.advance.treasury")} required>
              <input
                id="mv-treasury"
                className="ctl mono"
                dir="ltr"
                value={treasuryPartyId}
                onChange={(e) => setTreasuryPartyId(e.target.value)}
              />
            </Field>
          </>
        )}
      </div>
      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.retention.saveDraft")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void draft()}
          testId="movement-draft"
        />
        {document ? (
          <Button
            label={t("contracting.posting.post")}
            kind="primary"
            disabled={busy}
            onClick={() => void post()}
            testId="movement-post"
          />
        ) : null}
      </div>
      {document ? <DocumentReceipt document={document} busy={busy} testId="movement-receipt" /> : null}
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════════ الشاشة كاملةً */

/** سجلّ المحتجزات وكشف المقاولين. */
export function RetentionScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [asOf, setAsOf] = useState(todayIso);
  const [selected, setSelected] = useState<string>("");

  const register = useQuery({
    queryKey: ["contracting", "retention", config.baseUrl, config.token, config.companyId, asOf],
    enabled: config.companyId !== "" && asOf !== "",
    retry: false,
    queryFn: ({ signal }) => readRetentionRegister(transport, { companyId: config.companyId, asOf }, signal),
  });

  const statement = useQuery({
    queryKey: ["contracting", "statement", config.baseUrl, config.token, config.companyId, asOf],
    enabled: config.companyId !== "" && asOf !== "",
    retry: false,
    queryFn: ({ signal }) => readSubcontractorStatement(transport, { companyId: config.companyId, asOf }, signal),
  });

  const reload = useCallback(() => {
    void register.refetch();
    void statement.refetch();
  }, [register, statement]);

  if (config.companyId === "") return <NeedsCompany />;

  const rows = register.data?.rows ?? [];
  const chosen = rows.find((row) => row.movementId === selected) ?? null;

  return (
    <section className="stack" data-testid="contracting-retention">
      <ContractingHead
        title={t("contracting.retention.title")}
        lede={t("contracting.retention.lede")}
        aside={<Button label={t("contracting.common.refresh")} onClick={reload} testId="retention-reload" />}
      />

      <div className="filterbar" role="search">
        <Field id="ret-asof" label={t("contracting.retention.asOf")} hint={t("contracting.retention.asOfHint")}>
          <input
            id="ret-asof"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
            data-testid="retention-asof"
          />
        </Field>
      </div>

      <Panel title={t("contracting.retention.registerTitle")} note={t("contracting.retention.registerNote")} testId="retention-register">
        {register.isError ? (
          <ReadProblem error={register.error} onRetry={reload} />
        ) : register.data ? (
          <>
            <div className="stats-row">
              <StatCard
                label={t("contracting.retention.receivableTotal")}
                amount={register.data.receivableTotal}
                tone="debit"
                hint={t("contracting.retention.receivableHint")}
                moment={MOTION.arrive}
                testId="retention-receivable"
              />
              <StatCard
                label={t("contracting.retention.payableTotal")}
                amount={register.data.payableTotal}
                tone="credit"
                hint={t("contracting.retention.payableHint")}
                moment={MOTION.arrive}
                testId="retention-payable"
              />
              <StatCard
                label={t("contracting.retention.movements")}
                count={rows.length}
                hint={t("contracting.retention.movementsHint")}
                testId="retention-count"
              />
            </div>

            {rows.length === 0 ? (
              <ExplainedEmpty
                title={t("contracting.retention.emptyTitle")}
                body={t("contracting.retention.emptyBody")}
                testId="retention-empty"
              />
            ) : (
              <div className="ledger" data-testid="retention-table">
                <table>
                  <caption className="visually-hidden">{t("contracting.retention.registerTitle")}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("contracting.retention.sideColumn")}</th>
                      <th scope="col">{t("contracting.common.party")}</th>
                      <th scope="col">{t("contracting.common.projectCode")}</th>
                      <th scope="col">{t("contracting.retention.document")}</th>
                      <th scope="col" className="n">
                        {t("contracting.retention.amount")}
                      </th>
                      <th scope="col" className="n">
                        {t("contracting.retention.outstanding")}
                      </th>
                      <th scope="col">{t("contracting.retention.movedOn")}</th>
                      <th scope="col">{t("contracting.retention.dueOn")}</th>
                      <th scope="col">{t("contracting.retention.action")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.movementId} className={MOTION.arrive} data-testid="retention-row">
                        <td>
                          <StatusBadge
                            state={row.side === RECEIVABLE ? "debit" : "credit"}
                            label={t("contracting.retention.side." + row.side)}
                          />
                        </td>
                        <td>
                          <span className="code">{row.partyKind}</span>
                          <span className="alt mono">{row.partyId}</span>
                        </td>
                        <td className="code">{row.projectCode}</td>
                        <td>
                          <span className="code">{row.documentType}</span>
                          <span className="alt mono">{row.documentId}</span>
                        </td>
                        <td className="n">
                          <Amount value={row.amount} />
                        </td>
                        <td className="n">
                          <Amount value={row.outstanding} />
                        </td>
                        <td className="code">{row.movedOn}</td>
                        <td className="code">{row.dueOn}</td>
                        <td>
                          <button
                            type="button"
                            className={"btn btn-sm" + (selected === row.movementId ? " btn-primary" : "")}
                            aria-pressed={selected === row.movementId}
                            data-testid="retention-act"
                            onClick={() => setSelected(selected === row.movementId ? "" : row.movementId)}
                          >
                            {row.side === PAYABLE
                              ? t("contracting.retention.release")
                              : t("contracting.retention.collect")}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : (
          <LoadingPanel what={t("contracting.retention.registerTitle")} testId="retention-loading" />
        )}
      </Panel>

      {chosen ? <MovementAction row={chosen} onDone={reload} /> : null}

      <Panel
        title={t("contracting.statement.title")}
        note={t("contracting.statement.note")}
        testId="subcontractor-statement"
        aside={
          statement.data ? (
            <StatusBadge
              state={statement.data.isReconciled ? "posted" : "rejected"}
              label={
                statement.data.isReconciled
                  ? t("contracting.statement.reconciled")
                  : t("contracting.statement.diverged")
              }
              testId="statement-verdict"
            />
          ) : null
        }
      >
        {statement.isError ? (
          <ReadProblem error={statement.error} onRetry={reload} />
        ) : statement.data ? (
          <>
            <div className="stats-row">
              <StatCard
                label={t("contracting.statement.subledgerTotal")}
                amount={statement.data.subledgerTotal}
                hint={t("contracting.statement.subledgerHint")}
                testId="statement-subledger"
              />
              <StatCard
                label={t("contracting.statement.controlTotal")}
                amount={statement.data.controlTotal}
                hint={t("contracting.statement.controlHint")}
                testId="statement-control"
              />
              <StatCard
                label={t("contracting.statement.divergence")}
                amount={statement.data.divergence}
                tone={statement.data.isReconciled ? "good" : "bad"}
                hint={t("contracting.statement.divergenceHint")}
                testId="statement-divergence"
              />
            </div>

            {statement.data.isReconciled ? null : (
              <p className="problem" role="alert" data-testid="statement-refusal">
                {t("contracting.statement.divergedBody")}
              </p>
            )}

            {statement.data.rows.length === 0 ? (
              <ExplainedEmpty
                title={t("contracting.statement.emptyTitle")}
                body={t("contracting.statement.emptyBody")}
                testId="statement-empty"
              />
            ) : (
              <div className="ledger" data-testid="statement-table">
                <table>
                  <caption className="visually-hidden">{t("contracting.statement.title")}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("contracting.common.code")}</th>
                      <th scope="col">{t("contracting.register.nameAr")}</th>
                      <th scope="col" className="n">
                        {t("contracting.statement.effect")}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {statement.data.rows.map((row) => (
                      <tr key={row.subcontractorId} data-testid="statement-row">
                        <td className="code">{row.code}</td>
                        <td>
                          <TranslatedName nameAr={row.nameAr} translations={row.nameTranslations} />
                        </td>
                        <td className="n">
                          <Amount value={row.effect} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <p className="muted" data-testid="statement-count">
              {tp("contracting.count.parties", statement.data.rows.length)}
            </p>
            <p className="muted">
              {t("contracting.statement.asOf")}
              {": "}
              <span className="mono" dir="ltr">
                {statement.data.asOf}
              </span>
            </p>
          </>
        ) : (
          <LoadingPanel what={t("contracting.statement.title")} testId="statement-loading" />
        )}
      </Panel>

      <p className="muted">
        {t("contracting.retention.footnote")}
        {" · "}
        <Num value={rows.length} />
      </p>
    </section>
  );
}
