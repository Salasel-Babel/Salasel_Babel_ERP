/* ═══════════════════════════════════════════════════════════════════════════
   /ledger/chain — سلامة سلسلة الدفتر  ·  The ledger hash-chain verdict
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة لا تكتب شيئاً.** الباب `GET`، ووصفُه في العقد: «يعيد بناء كل
   مستند من الحقيقة المجالية المخزَّنة ويقارن بصمته، ويسمّي أول تسلسل منحرف
   إن وُجد». فهو حكمٌ على ما كُتب، لا كتابةٌ ثانية — ويُقال ذلك على الشاشة
   نصّاً كي لا يتردّد من يضغط.

   وثلاثةٌ تحكم عرضه:

   ١ · **الكسر أخطر ما يعرضه هذا النظام، فلا يُلطَّف.** لوحُ الكسر `role`
       `alert`، وعنوانُه يقول إن السلسلة **مكسورة** لا «تحتاج مراجعة»،
       ويسمّي **أول تسلسل منحرف** كما سمّاه الجواب، ويعرض التفصيل الفنّي —
       البصمة المتوقّعة والمخزَّنة — كما وصل بلا اختصار.

   ٢ · **والسلامة لا تُوسَّع.** الجواب حكمٌ على **دفترٍ واحد وسنةٍ واحدة**؛
       فالسلامة تُقال بنطاقها مكتوباً في اللوح نفسه، ولا تُقرأ ضماناً على
       دفاتر أخرى ولا على سنةٍ أخرى.

   ٣ · **ولا منطقٌ ثنائي وحده.** العقد يشرح لماذا: «المدقّق يسأل أين ومتى وما
       الذي بعده يجب أن يُراجَع؛ وإجابة منطقية واحدة لا تصلح تقريراً». فيُعرض
       العدد المفحوص ورمز الحكم والشرح العربي كاملةً في الحالين.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { verifyLedgerChain } from "../../api/generated/client";
import { PARAM_verifyLedgerChain_fiscalYear_RE } from "../../api/generated/formats";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
import {
  AccField,
  AccRow,
  ChooseCompanyFirst,
  DeclaredGap,
  StatePanel,
} from "../accounting/parts";
import { LedgerSectionNav } from "./parts";
import "../accounting/accounting.css";

/** السنة الميلادية الحالية بأربعة أرقام لاتينية — من الساعة لا من تنسيق ثقافة. */
function thisYear(): string {
  return String(new Date().getFullYear());
}

/** الشاشة كاملةً. */
export function LedgerChainScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const [book, setBook] = useState(config.book);
  const [year, setYear] = useState(thisYear);
  const [asked, setAsked] = useState<{ book: string; year: string } | null>(null);

  const yearValid = PARAM_verifyLedgerChain_fiscalYear_RE.test(year);
  const ready = book.trim() !== "" && yearValid;

  const check = useQuery({
    queryKey: [
      "ledger",
      "chain",
      config.baseUrl,
      config.token,
      config.companyId,
      asked?.book ?? "",
      asked?.year ?? "",
    ],
    enabled: config.companyId !== "" && asked !== null,
    retry: false,
    queryFn: ({ signal }) =>
      verifyLedgerChain(
        transport,
        { companyId: config.companyId, book: asked?.book ?? "", fiscalYear: asked?.year ?? "" },
        signal
      ),
  });

  const run = useCallback(() => {
    setAsked({ book: book.trim(), year });
  }, [book, year]);

  const verdict = check.data ?? null;

  /* المفردة تُشعَل **بعد** الرسم لا أثناءه: إشعالُ حالةٍ في جسم المكوّن
     يُعيد الرسم بلا نهاية. والكسر يُشعل `refuse` والسلامة `arrive`. */
  useEffect(() => {
    if (verdict === null) return;
    if (verdict.ok) fireArrive();
    else fireRefuse();
  }, [fireArrive, fireRefuse, verdict]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="ledger-chain-needs-company" />;

  return (
    <section className="stack" data-testid="ledger-chain-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.ledger.page.chainTitle")}</h1>
          <p className="sub">{t("accounting.ledger.page.chainLede")}</p>
        </div>
      </header>

      <LedgerSectionNav current="/ledger/chain" />

      {/* ═══════════════════════════════ ١ · نطاق الفحص ═══════════════ */}
      <StatePanel
        title={t("accounting.ledger.chain.scopeTitle")}
        note={t("accounting.ledger.chain.scopeNote")}
        testId="ledger-chain-scope"
      >
        <AccRow cols={2} testId="ledger-chain-scope-row">
          <AccField
            id="ledger-chain-book"
            label={t("accounting.ledger.field.book")}
            hint={t("accounting.ledger.field.bookHint")}
            source="typed"
            required
          >
            <input
              id="ledger-chain-book"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="ledger-chain-book"
              value={book}
              onChange={(e) => setBook(e.target.value)}
            />
          </AccField>
          <AccField
            id="ledger-chain-year"
            label={t("accounting.ledger.field.fiscalYear")}
            hint={t("accounting.ledger.field.fiscalYearHint")}
            error={yearValid ? undefined : t("accounting.ledger.field.fiscalYearBad")}
            source="typed"
            required
          >
            <input
              id="ledger-chain-year"
              className={"ctl mono" + (yearValid ? "" : " is-invalid")}
              inputMode="numeric"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              aria-invalid={!yearValid}
              data-testid="ledger-chain-year"
              value={year}
              onChange={(e) => setYear(e.target.value)}
            />
          </AccField>
        </AccRow>
        <div className="inline-group">
          <Button
            label={t("accounting.ledger.act.verify")}
            kind="primary"
            loading={check.isPending && check.fetchStatus === "fetching"}
            disabled={!ready}
            onClick={run}
            testId="ledger-chain-run"
          />
          <span className="hint">{t("accounting.ledger.chain.readsOnly")}</span>
        </div>
      </StatePanel>

      {/* ═══════════════════════════════ ٢ · الحكم ═══════════════════ */}
      <StatePanel
        title={t("accounting.ledger.chain.verdictTitle")}
        note={t("accounting.ledger.chain.verdictNote")}
        loading={check.isPending && check.fetchStatus === "fetching"}
        testId="ledger-chain-verdict"
      >
        {asked === null ? (
          <EmptyState
            title={t("accounting.ledger.chain.noneTitle")}
            body={t("accounting.ledger.chain.noneBody")}
            testId="ledger-chain-none"
          />
        ) : check.isError ? (
          <ProblemPanel error={check.error} onRetry={() => void check.refetch()} />
        ) : verdict ? (
          <div className={"stack " + arriveCls}>
            {verdict.ok ? (
              <div
                className="alert alert--success"
                data-ok="true"
                data-testid="ledger-chain-intact"
              >
                <div className="body">
                  <span className="title">{t("accounting.ledger.chain.intactTitle")}</span>
                  <p>
                    {t("accounting.ledger.chain.intactBody", {
                      book: asked.book,
                      year: asked.year,
                    })}
                  </p>
                  <p className="hint">{t("accounting.ledger.chain.intactLimit")}</p>
                </div>
              </div>
            ) : (
              <div
                className="alert alert--danger"
                role="alert"
                data-ok="false"
                data-testid="ledger-chain-broken"
              >
                <div className="body">
                  <span className="title">{t("accounting.ledger.chain.brokenTitle")}</span>
                  <p data-testid="ledger-chain-broken-body">
                    {t("accounting.ledger.chain.brokenBody", {
                      book: asked.book,
                      year: asked.year,
                    })}
                  </p>
                  {verdict.firstDivergentSequence !== null ? (
                    <p data-testid="ledger-chain-first-divergent">
                      {t("accounting.ledger.chain.firstDivergent", {
                        sequence: verdict.firstDivergentSequence,
                      })}
                    </p>
                  ) : (
                    <p data-testid="ledger-chain-no-sequence">
                      {t("accounting.ledger.chain.noSequence")}
                    </p>
                  )}
                  <p>{t("accounting.ledger.chain.afterMustBeReviewed")}</p>
                </div>
              </div>
            )}

            <div className="kv">
              <div>
                <div className="k">{t("accounting.ledger.chain.verdictCode")}</div>
                <div className="v mono acc-id" data-testid="ledger-chain-code">
                  {verdict.verdict}
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.chain.checked")}</div>
                <div className="v acc-id" data-testid="ledger-chain-checked">
                  <Num value={verdict.checked} />
                </div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.chain.scopeBook")}</div>
                <div className="v mono acc-id">{asked.book}</div>
              </div>
              <div>
                <div className="k">{t("accounting.ledger.chain.scopeYear")}</div>
                <div className="v mono acc-id">{asked.year}</div>
              </div>
            </div>

            {/* الشرح العربي كما كتبه الخادم — صالحٌ لتقرير تدقيق بنصّ العقد. */}
            <p className="muted" lang="ar" dir="rtl" data-testid="ledger-chain-reason">
              {verdict.reasonAr}
            </p>

            {/* التفصيل الفنّي: البصمات المتوقّعة والمخزَّنة — كما وصلت. */}
            {verdict.detail !== null ? (
              <div data-testid="ledger-chain-detail">
                <div className="k">{t("accounting.ledger.chain.detailTitle")}</div>
                <p className="mono" dir="ltr" data-testid="ledger-chain-detail-text">
                  {verdict.detail}
                </p>
              </div>
            ) : (
              <p className="hint" data-testid="ledger-chain-no-detail">
                {t("accounting.ledger.chain.noDetail")}
              </p>
            )}
          </div>
        ) : null}
      </StatePanel>

      {/* ═════════════════ ٣ · ما لا ينشره العقد — مُعلَناً ═══════════ */}
      <DeclaredGap
        title={t("accounting.ledger.gap.booksTitle")}
        body={t("accounting.ledger.gap.booksBody")}
        owed={t("accounting.ledger.gap.booksOwed")}
        testId="ledger-chain-gap"
      />
    </section>
  );
}
