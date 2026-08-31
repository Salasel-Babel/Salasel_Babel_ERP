/* ═══════════════════════════════════════════════════════════════════════════
   هيكل التطبيق: التنقّل، ومبدّلا اللغة والمظهر، وشارة حالة الخدمة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useEffect, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, Outlet, useRouterState } from "@tanstack/react-router";
import { health } from "../api/generated/client";
import { useApi } from "./api-context";
import { useT } from "../i18n/react";
import { HealthBadge, LocaleSwitcher, ThemeSwitcher } from "./shell/Switchers";
import { CompanyBadge } from "./shell/CompanyBadge";
import { KeyboardHelp } from "./shell/KeyboardHelp";
import accessiblePaletteHref from "../styles/theme/theme-accessible.css?url";

/** الهيكل حول كل شاشة. */
export function AppShell(): ReactNode {
  const { t } = useT();
  const { transport } = useApi();
  const [helpOpen, setHelpOpen] = useState(false);
  const path = useRouterState({ select: (s) => s.location.pathname });

  const healthQuery = useQuery({
    queryKey: ["health"],
    retry: false,
    refetchInterval: 60_000,
    queryFn: ({ signal }) => health(transport, signal),
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const typing =
        !!target &&
        (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
      if (typing || e.ctrlKey || e.metaKey || e.altKey) return;
      if (e.key === "?") {
        e.preventDefault();
        setHelpOpen((v) => !v);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  useEffect(() => {
    document.title = t("app.web.docTitle");
  }, [t, path]);

  return (
    <div className="app-shell">
      <a className="visually-hidden" href="#main">
        {t("app.web.skipToTable")}
      </a>
      <nav className="app-side" aria-label={t("app.a11y.mainNav")}>
        <div className="brand">
          <span className="mark" aria-hidden="true" />
          <span>{t("app.name")}</span>
        </div>
        <Link to="/sign-in" className="navitem" data-testid="nav-sign-in">
          {t("app.nav.signIn")}
        </Link>
        <Link to="/" className="navitem" data-testid="nav-trial-balance">
          {t("app.nav.trialBalance")}
        </Link>
        <Link to="/voucher" className="navitem" data-testid="nav-voucher">
          {t("app.nav.voucher")}
        </Link>
        <Link to="/contract" className="navitem" data-testid="nav-contract">
          {t("app.nav.contract")}
        </Link>
        <Link to="/voice" className="navitem" data-testid="nav-voice">
          {t("app.nav.voice")}
        </Link>
      </nav>

      <div className="app-main">
        <header className="app-topbar">
          <CompanyBadge />
          <LocaleSwitcher />
          <ThemeSwitcher accessiblePaletteHref={accessiblePaletteHref} />
          <span className="spacer" />
          <HealthBadge
            health={healthQuery.data ?? null}
            failed={healthQuery.isError}
            loading={healthQuery.isPending}
          />
          <button
            type="button"
            className="btn"
            data-testid="open-help"
            onClick={() => setHelpOpen(true)}
          >
            {t("common.action.keyboardHelp")}
          </button>
        </header>

        <main className="app-page" id="main">
          <Outlet />
        </main>
      </div>

      <KeyboardHelp open={helpOpen} onClose={() => setHelpOpen(false)} />
    </div>
  );
}
