/* ═══════════════════════════════════════════════════════════════════════════
   /ledger/entry — القيد المُرحَّل وعكسه  ·  A posted entry and its reversal
   ───────────────────────────────────────────────────────────────────────────
   **العكس ليس حذفاً ولا تعديلاً.** نصّ العقد على هذا الباب حرفاً: «ينشئ قيد
   عكس مرتبطاً بالقيد الأصلي. القيد الأصلي لا يُمسّ ولا يُحذف ولا يُعدَّل —
   ولا يوجد على هذا السطح فعل حذف أصلاً». وهذه الشاشة تُري ذلك بعينه: القيد
   الأصلي معروضٌ بسطوره **قبل** الفعل و**بعده**، والزرّ يكتب مستنداً ثانياً
   يبقى الاثنان مقروءين. وشاشةٌ توحي بأن العكس يُزيل القيد كذبٌ على المحاسب.

   وأربعةٌ تحكمها:

   ١ · **الأثر يُقال قبل الضغط.** اللوح الثالث يعرض — قبل أن يُلمس زرّ —
       **أيّ أدوارٍ تتحرّك وفي أيّ جانب** في القيد المضادّ، وفي **أيّ فترة**
       يقع، ثم يطلب إقراراً نصُّه هو الأثر. ولا زرَّ عكسٍ بلا إقرار.

   ٢ · **ولا رقمَ حساب.** ما يُعرض من السطر هو `role` و`qualifier` **كما
       خُزِّنا** — ومصفوفة الترحيل وحدها تحلّهما إلى حساب. والجانب المعكوس
       نقلُ نصٍّ من عمودٍ إلى عمود، لا حسابٌ على المال.

   ٣ · **وهل الفترة مفتوحة؟ العقد لا ينشر باباً يقوله.** لا عمليةَ واحدة في
       199 تُخبر عن حال فترةٍ مالية، فلا تستطيع هذه الشاشة أن تَعِد به قبل
       الضغط — **وتقول ذلك صراحةً** بدل أن تسكت أو تخمّن. والطريق المنشور
       للفترة المقفلة `closedPeriodAuthorisation`: إذنٌ موثَّق يُملأ هنا
       اختياراً، أو 409 برمزه من الخادم.

   ٤ · **و501 له اسم.** سطحُ قراءة القيد المفرد قد لا يكون هبط بعد، ورمزُه
       الثابت `ledger.read.entry_surface_unavailable` منشورٌ في العقد —
       فيُقال باسمه لا برسالة عطلٍ عامّة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readJournalEntry, reverseJournalEntry } from "../../api/generated/client";
import type { JournalEntry, JournalLine, PostingReceipt } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, RefusalPanel, useMoment } from "../../ui";
import {
  AccAction,
  AccField,
  AccRow,
  AccState,
  ChooseCompanyFirst,
  DeclaredGap,
  StatePanel,
} from "../accounting/parts";
import { EntryReceipt, Irrevocable, LedgerSectionNav, isDateText, periodOf } from "./parts";
import "../accounting/accounting.css";

/** الرمز الثابت الذي ينشره العقد حين لا يكون سطح القراءة قد هبط. */
const READ_SURFACE_ABSENT = "ledger.read.entry_surface_unavailable";

/** هل السطر مدين؟ صفرُ المدين مع دائنٍ غير صفر يجعله سطرَ دائن. */
function isDebitSide(line: JournalLine): boolean {
  return !line.debit.isZero;
}

/** الشاشة كاملةً. */
export function JournalEntryScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const [postCls, firePost] = useMoment("post");
  const [, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── أيُّ قيد؟ ─────────────────────────────────────────────────────── */
  const [entryId, setEntryId] = useState("");
  const [asked, setAsked] = useState("");

  /* ── ما يُطلَب في العكس ────────────────────────────────────────────── */
  const [reasonAr, setReasonAr] = useState("");
  const [reasonEn, setReasonEn] = useState("");
  const [reversalDate, setReversalDate] = useState("");
  const [authOpen, setAuthOpen] = useState(false);
  const [authBy, setAuthBy] = useState("");
  const [authCode, setAuthCode] = useState("");
  const [authReasonAr, setAuthReasonAr] = useState("");
  const [authReasonEn, setAuthReasonEn] = useState("");

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [receipt, setReceipt] = useState<PostingReceipt | null>(null);

  const entry = useQuery({
    queryKey: ["ledger", "entry", config.baseUrl, config.token, config.companyId, asked],
    enabled: config.companyId !== "" && asked !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readJournalEntry(transport, { companyId: config.companyId, entryId: asked }, signal),
  });

  const current: JournalEntry | null = entry.data ?? null;

  /* الرفض المنشور باسمه: سطحُ القراءة لم يهبط. ويُفصَل عن كل رفضٍ آخر لأن
     علاجه غيرُ علاجها — لا شيء يفعله المستخدم، والباب نفسه غير موجود. */
  const surfaceAbsent =
    entry.error instanceof ProblemError && entry.error.code === READ_SURFACE_ABSENT;

  const ask = useCallback(() => {
    setAsked(entryId.trim());
    setReceipt(null);
    setFailure(null);
  }, [entryId]);

  /* الفترة التي يقع فيها تاريخُ القيد المضادّ — اقتطاعٌ من نصّ التاريخ، لا
     حساب. وغيابُ التاريخ يعني تاريخَ القيد الأصلي كما ينصّ العقد. */
  const effectiveDate = reversalDate !== "" ? reversalDate : (current?.entryDate ?? "");
  const effectivePeriod = periodOf(effectiveDate);
  const dateBad = reversalDate !== "" && !isDateText(reversalDate);

  const authComplete =
    authBy !== "" && authCode !== "" && authReasonAr !== "" && authReasonEn !== "";

  const blocked = useMemo(() => {
    if (reasonAr === "" || reasonEn === "") return t("accounting.ledger.rev.needReason");
    if (dateBad) return t("accounting.ledger.rev.needDate");
    if (authOpen && !authComplete) return t("accounting.ledger.rev.needAuth");
    return undefined;
  }, [authComplete, authOpen, dateBad, reasonAr, reasonEn, t]);

  const submit = useCallback(async () => {
    if (current === null) return;
    setBusy(true);
    setFailure(null);
    try {
      const done = await reverseJournalEntry(transport, {
        companyId: config.companyId,
        entryId: current.entryId,
        body: {
          reason: { ar: reasonAr, en: reasonEn },
          ...(reversalDate !== "" ? { reversalDate } : {}),
          ...(authOpen
            ? {
                closedPeriodAuthorisation: {
                  authorisedBy: authBy,
                  permissionCode: authCode,
                  reason: { ar: authReasonAr, en: authReasonEn },
                },
              }
            : {}),
        },
      });
      setReceipt(done);
      await entry.refetch();
      if (done.alreadyPosted) fireArrive();
      else firePost();
    } catch (problem) {
      setFailure(problem);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [
    authBy,
    authCode,
    authOpen,
    authReasonAr,
    authReasonEn,
    config.companyId,
    current,
    entry,
    fireArrive,
    firePost,
    fireRefuse,
    reasonAr,
    reasonEn,
    reversalDate,
    transport,
  ]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="ledger-entry-needs-company" />;

  return (
    <section className="stack" data-testid="ledger-entry-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.ledger.page.entryTitle")}</h1>
          <p className="sub">{t("accounting.ledger.page.entryLede")}</p>
        </div>
      </header>

      <LedgerSectionNav current="/ledger/entry" />

      {/* ══════════════════════════════════════ ١ · أيُّ قيد؟ ══════════ */}
      <StatePanel
        title={t("accounting.ledger.entry.askTitle")}
        note={t("accounting.ledger.entry.askNote")}
        testId="ledger-entry-ask"
      >
        <AccRow cols={2} testId="ledger-entry-ask-row">
          <AccField
            id="ledger-entry-id"
            label={t("accounting.ledger.field.entryId")}
            hint={t("accounting.ledger.field.entryIdHint")}
            source="typed"
            required
          >
            <input
              id="ledger-entry-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-entry-id"
              value={entryId}
              onChange={(e) => setEntryId(e.target.value)}
            />
          </AccField>
          <AccAction>
            <Button
              label={t("accounting.ledger.act.read")}
              kind="primary"
              loading={entry.isPending && entry.fetchStatus === "fetching"}
              disabled={entryId.trim() === ""}
              onClick={ask}
              testId="ledger-entry-read"
            />
          </AccAction>
        </AccRow>
      </StatePanel>

      {/* ═════════════════════════ ٢ · القيد كما هو في الدفتر ═════════ */}
      <StatePanel
        title={t("accounting.ledger.entry.docTitle")}
        note={t("accounting.ledger.entry.docNote")}
        aside={current ? <AccState state={current.status} testId="ledger-entry-state" /> : null}
        loading={entry.isPending && entry.fetchStatus === "fetching"}
        testId="ledger-entry-doc"
      >
        {asked === "" ? (
          <EmptyState
            title={t("accounting.ledger.entry.noneTitle")}
            body={t("accounting.ledger.entry.noneBody")}
            testId="ledger-entry-none"
          />
        ) : surfaceAbsent ? (
          <RefusalPanel
            title={t("accounting.ledger.entry.absentTitle")}
            body={t("accounting.ledger.entry.absentBody")}
            code={READ_SURFACE_ABSENT}
            codeLabel={t("accounting.ledger.field.stableCode")}
            next={t("accounting.ledger.entry.absentNext")}
            testId="ledger-entry-absent"
          />
        ) : entry.isError ? (
          <ProblemPanel error={entry.error} onRetry={() => void entry.refetch()} />
        ) : current ? (
          <div className={"stack " + arriveCls}>
            <div className="kv">
              <div>
                <div className="k">{t("accounting.ledger.field.entryNumber")}</div>
                <div className="v acc-id" data-testid="ledger-entry-number">
                  <Num value={current.entryNumber} />
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.entryDate")}</div>
                <div className="v mono acc-id">{current.entryDate}</div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.periodCode")}</div>
                <div className="v mono acc-id" data-testid="ledger-entry-period">
                  {current.periodCode}
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.book")}</div>
                <div className="v mono acc-id">{current.book}</div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.currency")}</div>
                <div className="v mono acc-id">{current.currency}</div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.chainSequence")}</div>
                <div className="v acc-id"><Num value={current.chainSequence} /></div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.entryHash")}</div>
                <div className="v mono acc-id" data-testid="ledger-entry-hash">
                  {current.entryHash}
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.field.memo")}</div>
                <div className="v">
                  <span lang="ar" dir="rtl">{current.memoAr}</span>{" "}
                  <span className="alt" lang="en" dir="ltr">{current.memoEn}</span>
                </div>
              </div>
            </div>

            {/* قيدٌ هو نفسه عكسُ قيدٍ آخر — يُقال، فالسلسلة تُقرأ من طرفيها. */}
            {current.reversesEntryId ? (
              <p className="hint" data-testid="ledger-entry-reverses">
                {t("accounting.ledger.entry.isReversalOf", { id: current.reversesEntryId })}
              </p>
            ) : null}

            <div className="acc-table" data-testid="ledger-entry-lines">
              <table>
                <caption className="visually-hidden">
                  {t("accounting.ledger.entry.linesCaption")}
                </caption>
                <thead>
                  <tr>
                    <th scope="col" className="n">{t("accounting.ledger.field.lineNo")}</th>
                    <th scope="col">{t("accounting.ledger.field.role")}</th>
                    <th scope="col">{t("accounting.ledger.field.qualifier")}</th>
                    <th scope="col">{t("accounting.ledger.field.narration")}</th>
                    <th scope="col" className="n">{t("accounting.ledger.field.debit")}</th>
                    <th scope="col" className="n">{t("accounting.ledger.field.credit")}</th>
                  </tr>
                </thead>
                <tbody>
                  {current.lines.map((line) => (
                    <tr key={line.lineNo} data-testid={"ledger-entry-line-" + line.lineNo}>
                      <td className="n"><Num value={line.lineNo} /></td>
                      <td><span className="mono acc-id">{line.role}</span></td>
                      <td><span className="mono acc-id">{line.qualifier}</span></td>
                      <td>
                        <span lang="ar" dir="rtl">{line.descriptionAr}</span>{" "}
                        <span className="alt" lang="en" dir="ltr">{line.descriptionEn}</span>
                      </td>
                      <td className="n"><Amount value={line.debit} /></td>
                      <td className="n"><Amount value={line.credit} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="muted">{tp("accounting.count.lines", current.lines.length)}</p>
          </div>
        ) : null}
      </StatePanel>

      {/* ═══════════════ ٣ · ما سيُكتب إن عكست — قبل الضغط لا بعده ════ */}
      {current ? (
        <StatePanel
          title={t("accounting.ledger.rev.previewTitle")}
          note={t("accounting.ledger.rev.previewNote")}
          testId="ledger-rev-preview"
        >
          <p data-testid="ledger-rev-keeps">{t("accounting.ledger.rev.originalKept")}</p>

          <div className="acc-table" data-testid="ledger-rev-effect">
            <table>
              <caption className="visually-hidden">
                {t("accounting.ledger.rev.effectCaption")}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.ledger.field.role")}</th>
                  <th scope="col">{t("accounting.ledger.field.qualifier")}</th>
                  <th scope="col">{t("accounting.ledger.rev.wasSide")}</th>
                  <th scope="col">{t("accounting.ledger.rev.becomesSide")}</th>
                  <th scope="col" className="n">{t("accounting.ledger.rev.sameAmount")}</th>
                </tr>
              </thead>
              <tbody>
                {current.lines.map((line) => (
                  <tr key={line.lineNo} data-testid={"ledger-rev-effect-" + line.lineNo}>
                    <td><span className="mono acc-id">{line.role}</span></td>
                    <td><span className="mono acc-id">{line.qualifier}</span></td>
                    <td>
                      {isDebitSide(line)
                        ? t("accounting.ledger.field.debit")
                        : t("accounting.ledger.field.credit")}
                    </td>
                    <td data-testid={"ledger-rev-side-" + line.lineNo}>
                      {isDebitSide(line)
                        ? t("accounting.ledger.field.credit")
                        : t("accounting.ledger.field.debit")}
                    </td>
                    <td className="n">
                      <Amount value={isDebitSide(line) ? line.debit : line.credit} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="hint">{t("accounting.ledger.rev.effectSource")}</p>

          <div className="kv">
            <div>
              <div className="k">{t("accounting.ledger.rev.fallsInPeriod")}</div>
              <div className="v mono acc-id" data-testid="ledger-rev-period">
                {effectivePeriod}
              </div>
            </div>
            <div>
              <div className="k">{t("accounting.ledger.rev.fallsOnDate")}</div>
              <div className="v mono acc-id" data-testid="ledger-rev-effective-date">
                {effectiveDate}
              </div>
            </div>
          </div>

          {/* هل الفترة مفتوحة؟ لا باب يقوله — فيُقال أنه لا يُقال. */}
          <p className="hint" data-testid="ledger-rev-period-unknown">
            {t("accounting.ledger.rev.periodUnknown")}
          </p>
        </StatePanel>
      ) : null}

      {/* ═══════════════════════════════ ٤ · العكس نفسه ═══════════════ */}
      {current ? (
        <StatePanel
          title={t("accounting.ledger.rev.formTitle")}
          note={t("accounting.ledger.rev.formNote")}
          testId="ledger-rev-form"
        >
          <AccRow cols={3} testId="ledger-rev-row-1">
            <AccField
              id="ledger-rev-reason-ar"
              label={t("accounting.ledger.field.reasonAr")}
              hint={t("accounting.ledger.field.reasonArHint")}
              source="typed"
              required
            >
              <input
                id="ledger-rev-reason-ar"
                className="ctl"
                lang="ar"
                dir="rtl"
                autoComplete="off"
                data-testid="ledger-rev-reason-ar"
                value={reasonAr}
                onChange={(e) => setReasonAr(e.target.value)}
              />
            </AccField>
            <AccField
              id="ledger-rev-reason-en"
              label={t("accounting.ledger.field.reasonEn")}
              hint={t("accounting.ledger.field.reasonEnHint")}
              source="typed"
              required
            >
              <input
                id="ledger-rev-reason-en"
                className="ctl"
                lang="en"
                dir="ltr"
                autoComplete="off"
                data-testid="ledger-rev-reason-en"
                value={reasonEn}
                onChange={(e) => setReasonEn(e.target.value)}
              />
            </AccField>
            <AccField
              id="ledger-rev-date"
              label={t("accounting.ledger.field.reversalDate")}
              hint={t("accounting.ledger.field.reversalDateHint")}
              error={dateBad ? t("accounting.ledger.field.reversalDateBad") : undefined}
              source="typed"
            >
              <input
                id="ledger-rev-date"
                className={"ctl mono" + (dateBad ? " is-invalid" : "")}
                type="date"
                dir="ltr"
                aria-invalid={dateBad}
                data-testid="ledger-rev-date"
                value={reversalDate}
                onChange={(e) => setReversalDate(e.target.value)}
              />
            </AccField>
          </AccRow>

          <label className="check" htmlFor="ledger-rev-auth-open">
            <input
              id="ledger-rev-auth-open"
              type="checkbox"
              checked={authOpen}
              data-testid="ledger-rev-auth-open"
              onChange={(e) => setAuthOpen(e.target.checked)}
            />
            <span>{t("accounting.ledger.rev.authOpen")}</span>
          </label>

          {authOpen ? (
            <>
              <p className="hint">{t("accounting.ledger.rev.authNote")}</p>
              <AccRow cols={4} testId="ledger-rev-auth-row">
                <AccField
                  id="ledger-rev-auth-by"
                  label={t("accounting.ledger.field.authorisedBy")}
                  hint={t("accounting.ledger.field.authorisedByHint")}
                  source="typed"
                  required
                >
                  <input
                    id="ledger-rev-auth-by"
                    className="ctl mono"
                    dir="ltr"
                    autoComplete="off"
                    spellCheck={false}
                    data-testid="ledger-rev-auth-by"
                    value={authBy}
                    onChange={(e) => setAuthBy(e.target.value)}
                  />
                </AccField>
                <AccField
                  id="ledger-rev-auth-code"
                  label={t("accounting.ledger.field.permissionCode")}
                  hint={t("accounting.ledger.field.permissionCodeHint")}
                  source="typed"
                  required
                >
                  <input
                    id="ledger-rev-auth-code"
                    className="ctl mono"
                    dir="ltr"
                    autoComplete="off"
                    spellCheck={false}
                    data-testid="ledger-rev-auth-code"
                    value={authCode}
                    onChange={(e) => setAuthCode(e.target.value)}
                  />
                </AccField>
                <AccField
                  id="ledger-rev-auth-ar"
                  label={t("accounting.ledger.field.authReasonAr")}
                  hint={t("accounting.ledger.field.authReasonArHint")}
                  source="typed"
                  required
                >
                  <input
                    id="ledger-rev-auth-ar"
                    className="ctl"
                    lang="ar"
                    dir="rtl"
                    autoComplete="off"
                    data-testid="ledger-rev-auth-ar"
                    value={authReasonAr}
                    onChange={(e) => setAuthReasonAr(e.target.value)}
                  />
                </AccField>
                <AccField
                  id="ledger-rev-auth-en"
                  label={t("accounting.ledger.field.authReasonEn")}
                  hint={t("accounting.ledger.field.authReasonEnHint")}
                  source="typed"
                  required
                >
                  <input
                    id="ledger-rev-auth-en"
                    className="ctl"
                    lang="en"
                    dir="ltr"
                    autoComplete="off"
                    data-testid="ledger-rev-auth-en"
                    value={authReasonEn}
                    onChange={(e) => setAuthReasonEn(e.target.value)}
                  />
                </AccField>
              </AccRow>
            </>
          ) : null}

          <Irrevocable
            title={t("accounting.ledger.rev.irrevocableTitle")}
            effect={t("accounting.ledger.rev.irrevocableEffect", {
              period: effectivePeriod,
              count: current.lines.length,
            })}
            acknowledge={t("accounting.ledger.rev.irrevocableAck")}
            label={t("accounting.ledger.act.reverse")}
            busy={busy}
            {...(blocked ? { blocked } : {})}
            onConfirm={() => void submit()}
            testId="ledger-rev-act"
          />

          {failure ? <ProblemPanel error={failure} /> : null}
          {receipt ? (
            <div className={postCls}>
              <EntryReceipt receipt={receipt} testId="ledger-rev-receipt" />
              <p className="hint" data-testid="ledger-rev-both-readable">
                {t("accounting.ledger.rev.bothReadable", { id: current.entryId })}
              </p>
              <div className="inline-group">
                <Button
                  label={t("accounting.ledger.act.openContra")}
                  onClick={() => {
                    setEntryId(receipt.entryId);
                    setAsked(receipt.entryId);
                  }}
                  testId="ledger-rev-open-contra"
                />
              </div>
            </div>
          ) : null}
        </StatePanel>
      ) : null}

      {/* ═════════════════ ٥ · ما لا ينشره العقد — مُعلَناً لا مسكوتاً ═ */}
      <DeclaredGap
        title={t("accounting.ledger.gap.entryListTitle")}
        body={t("accounting.ledger.gap.entryListBody")}
        owed={t("accounting.ledger.gap.entryListOwed")}
        testId="ledger-entry-gap"
      />
    </section>
  );
}
