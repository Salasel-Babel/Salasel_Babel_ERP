/* شاشة ميزان المراجعة: المرشّحات، والقراءة من الخادم، والحالات الأربع. */
import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readTrialBalance } from "../../api/generated/client";
import { PARAM_readTrialBalance_period_RE } from "../../api/generated/formats";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { TrialBalanceTable, type ViewFilter } from "./TrialBalanceTable";

/** الشاشة كاملةً. */
export function TrialBalanceScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config, setConfig } = useApi();
  const [query, setQuery] = useState("");
  const [view, setView] = useState<ViewFilter>("all");
  const searchRef = useRef<HTMLInputElement | null>(null);

  const periodValid = config.period === "" || PARAM_readTrialBalance_period_RE.test(config.period);

  const result = useQuery({
    queryKey: ["trial-balance", config.baseUrl, config.token, config.companyId, config.book, config.period],
    enabled: config.companyId !== "" && config.book !== "" && periodValid,
    retry: false,
    queryFn: ({ signal }) =>
      readTrialBalance(
        transport,
        {
          companyId: config.companyId,
          book: config.book,
          ...(config.period ? { period: config.period } : {}),
        },
        signal
      ),
  });

  const reload = useCallback(() => {
    void result.refetch();
  }, [result]);

  const data = result.data ?? null;

  const balanced = useMemo(() => {
    if (!data) return null;
    /* حكم التوازن يصل محسوماً من الدفتر — لا يُحسب هنا ولا يُقرَّب. */
    return data.balanced;
  }, [data]);

  return (
    <section className="stack" data-testid="trial-balance-screen">
      <header className="statline">
        <h1 style={{ margin: 0, fontSize: "var(--font-size-h1)", fontFamily: "var(--font-display)" }}>
          {t("screen.trialBalance.title")}
        </h1>
        {data ? (
          <span
            className={"pill " + (balanced ? "pill--posted" : "pill--pending")}
            data-testid="balanced-pill"
            data-balanced={String(balanced)}
          >
            {balanced ? t("acct.balanced") : t("acct.unbalanced")}
          </span>
        ) : null}
        {data ? (
          <span className="muted" data-testid="row-count">
            {tp("common.count.accounts", data.rowCount)}
          </span>
        ) : null}
        {data ? (
          <span className="muted mono" data-testid="period-code">
            {data.periodCode ?? t("field.periodCode.all")} · {data.book}
          </span>
        ) : null}
      </header>

      <div className="filterbar" role="search">
        <div className="field">
          <label htmlFor="tb-company">{t("field.company.label")}</label>
          <input
            id="tb-company"
            className="ctl mono"
            data-testid="filter-company"
            value={config.companyId}
            onChange={(e) => setConfig({ ...config, companyId: e.target.value })}
            placeholder="00000000-0000-0000-0000-000000000000"
          />
        </div>
        <div className="field">
          <label htmlFor="tb-book">{t("field.book.label")}</label>
          <input
            id="tb-book"
            className="ctl mono"
            data-testid="filter-book"
            value={config.book}
            onChange={(e) => setConfig({ ...config, book: e.target.value })}
          />
        </div>
        <div className="field">
          <label htmlFor="tb-period">{t("field.periodCode.label")}</label>
          <input
            id="tb-period"
            className={"ctl mono" + (periodValid ? "" : " is-invalid")}
            data-testid="filter-period"
            aria-invalid={!periodValid}
            aria-describedby="tb-period-hint"
            value={config.period}
            onChange={(e) => setConfig({ ...config, period: e.target.value })}
            placeholder="2026-05"
          />
          <span className="hint" id="tb-period-hint">
            {periodValid ? t("field.periodCode.hint") : t("field.periodCode.bad")}
          </span>
        </div>
        <div className="field wide">
          <label htmlFor="tb-search">{t("field.searchAccounts.label")}</label>
          <input
            id="tb-search"
            ref={searchRef}
            className="ctl"
            type="search"
            data-testid="filter-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                setQuery("");
                e.currentTarget.blur();
              }
            }}
            placeholder={t("field.searchAccounts.ph")}
          />
          <span className="hint">{t("common.keys.hint")}</span>
        </div>
        <div className="rowctl">
          <div className="inline-group" role="group" aria-label={t("common.label.type")}>
            {(["all", "debit", "credit"] as const).map((v) => (
              <button
                key={v}
                type="button"
                className={"btn" + (view === v ? " btn-primary" : "")}
                data-view={v}
                data-testid={"view-" + v}
                aria-pressed={view === v}
                onClick={() => setView(v)}
              >
                {t(
                  v === "all"
                    ? "screen.trialBalance.viewAll"
                    : v === "debit"
                      ? "screen.trialBalance.viewDebit"
                      : "screen.trialBalance.viewCredit"
                )}
              </button>
            ))}
            <button type="button" className="btn" data-testid="reload" onClick={reload}>
              {t("common.action.refresh")}
            </button>
          </div>
        </div>
      </div>

      {!periodValid ? (
        <p className="field-error" role="alert" data-testid="period-error">
          {t("field.periodCode.bad")}
        </p>
      ) : null}

      {result.isPending && result.fetchStatus === "fetching" ? (
        <div className="card" data-testid="loading">
          <strong>{t("common.state.loading")}</strong>
          <p className="muted">{t("common.state.loadingBody")}</p>
          <div className="skel skel-text w-90" />
          <div className="skel skel-text w-75" />
          <div className="skel skel-text w-60" />
        </div>
      ) : null}

      {result.isError ? <ProblemPanel error={result.error} onRetry={reload} /> : null}

      {data ? (
        <TrialBalanceTable
          data={data}
          query={query}
          view={view}
          onView={setView}
          searchRef={searchRef}
        />
      ) : null}

      {data && data.rows.length === 0 ? (
        <div className="card" data-testid="empty">
          <strong>{t("common.state.noAccountMatch")}</strong>
          <p className="muted">{t("common.state.noAccountMatchBody")}</p>
        </div>
      ) : null}

      <p className="muted">{t("screen.trialBalance.footnote")}</p>
    </section>
  );
}
