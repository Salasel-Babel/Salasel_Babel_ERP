/* ═══════════════════════════════════════════════════════════════════════════
   الهيكل السينمائي — ما حول كل شاشة
   ───────────────────────────────────────────────────────────────────────────
   ملاحةٌ بين **الأقسام الخمسة** (والقسم غير المبنيّ مُعلَنٌ لا مخفيّ)، ورأسٌ
   فيه المنشأة واللغة والمظهر وحالة الخدمة، و**لوحةُ أوامر** بـCtrl/⌘+K تفتح
   على كل شاشةٍ وفعل، و**زرّ صوتٍ حاضرٌ دائماً**، وانتقالٌ بين المسارات يُقرأ
   كـ«مسار عرض»: شريطٌ بلون القسم الذي دخلتَه، وصفحةٌ تدخل بمنحنى `enter`.

   **والانتقال يقول أين ذهبتَ لا أنه حدث فقط**: لون الشريط هو لون القسم، وهو
   نفسه لون شارته في الملاحة. مؤثّرٌ يحمل معلومة، لا وميضٌ يُبطئ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Outlet, useNavigate, useRouterState } from "@tanstack/react-router";
import { health } from "../api/generated/client";
import { useApi } from "./api-context";
import { useT } from "../i18n/react";
import { HealthBadge, LocaleSwitcher, ThemeSwitcher } from "./shell/Switchers";
import { CompanyBadge } from "./shell/CompanyBadge";
import { KeyboardHelp } from "./shell/KeyboardHelp";
import { CommandPalette } from "./shell/CommandPalette";
import { ScreenNav } from "./shell/ScreenNav";
import { SectionNav } from "./shell/SectionNav";
import { VoiceDock } from "./shell/VoiceDock";
import { AgentWorkspace } from "../agent";
import { VoiceDraftBanner } from "./VoiceDraftBanner";
import { sectionOf } from "./shell/sections";
import { MOTION } from "../ui";
import accessiblePaletteHref from "../styles/theme/theme-accessible.css?url";

/** الهيكل حول كل شاشة. */
export function AppShell(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [helpOpen, setHelpOpen] = useState(false);
  const [cmdOpen, setCmdOpen] = useState(false);
  const [agentOpen, setAgentOpen] = useState(false);
  const navigate = useNavigate();
  const path = useRouterState({ select: (s) => s.location.pathname });
  const section = sectionOf(path);
  const tint = { "--section-tint": section.tint } as CSSProperties;

  const healthQuery = useQuery({
    queryKey: ["health"],
    retry: false,
    refetchInterval: 60_000,
    queryFn: ({ signal }) => health(transport, signal),
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      /* Ctrl/⌘+K يفتح لوحة الأوامر **من داخل الحقول أيضاً**: هي المخرج من
         شاشةٍ لا يعرف المستخدم أين يمضي منها، فحجبُها أثناء الكتابة يُفقدها
         أنفع مواضعها. */
      if ((e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K")) {
        e.preventDefault();
        setCmdOpen((v) => !v);
        return;
      }
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
    <div className="app-shell" data-section={section.id} style={tint}>
      {/* شريط الانتقال: يُعاد بناؤه بتغيّر المسار فتُعاد حركته. */}
      <span className="transit" key={path} aria-hidden="true" />

      <a className="visually-hidden" href="#main">
        {t("app.web.skipToTable")}
      </a>

      <nav className="app-side" aria-label={t("app.a11y.mainNav")}>
        <div className="brand">
          <span className="mark" aria-hidden="true" />
          <span>{t("app.name")}</span>
        </div>

        <SectionNav path={path} />

        {/* ── شاشاتُ القسم المفتوح وحدها ──────────────────────────────────
            وكانت هنا **نسخةٌ ثانية من `SCREENS` مكتوبةٌ بيد** تسرد الشاشات
            التسع والخمسين كلَّها بلا ترشيح، فيرى من يفتح «الموارد البشرية»
            ميزانَ المراجعة وشجرةَ التسكين تحته. وقد كان تعليقٌ هنا يوصي بأن
            تُقاد القائمة من `SCREENS` — وهذا ما صار: الترشيحُ والعناوين في
            `shell/ScreenNav.tsx`، ولا موضعَ ثانٍ ينحرف عن الأوّل. */}
        <ScreenNav section={section.id} />
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
            className="btn btn-sm"
            data-testid="open-agent"
            aria-expanded={agentOpen}
            title={t("agent.workspace.openTitle")}
            onClick={() => setAgentOpen((v) => !v)}
          >
            {t("agent.workspace.open")}
          </button>
          <button
            type="button"
            className="btn btn-sm"
            data-testid="open-command"
            aria-keyshortcuts="Control+K Meta+K"
            onClick={() => setCmdOpen(true)}
          >
            {t("app.command.open")}
          </button>
          <button
            type="button"
            className="btn btn-sm"
            data-testid="open-help"
            onClick={() => setHelpOpen(true)}
          >
            {t("common.action.keyboardHelp")}
          </button>
        </header>

        <main className="app-page" id="main">
          {/* المسوّدة المنطوقة تظهر **فوق الشاشة التي هبطت عليها**، لا في اللوحة
              التي غادرها المستخدم. وهي في الهيكل لأن الهبوط عابرٌ للشاشات. */}
          <VoiceDraftBanner />
          <div className={MOTION.transit} key={path}>
            <Outlet />
          </div>
        </main>
      </div>

      {/* مساحةُ عمل الوكيل: **لوحٌ واحد ينفتح فوق أي شاشة** — لا ميزةٌ مبعثرة
          على كل شاشة. وموضعُه في الهيكل لا في شاشةٍ بعينها للسبب نفسه الذي
          وضع لوحةَ المسوّدة المنطوقة هنا: ما يعبر الشاشات لا يُنسَخ فيها. */}
      {/* **اللوح يُرسَم كلّما فُتح، ولو بلا شركة.** كان مشروطاً بـ
          `config.companyId !== ""`، فيضغط من يفتح الموقع أوّل مرّة زرَّ «الوكيل»
          ولا يقع شيء — بلا رسالة ولا تعطيل. والسبب الآن يُقال داخل اللوح ومعه
          طريقُ الخروج، لا يُبتلع. */}
      {agentOpen ? (
        <AgentWorkspace
          transport={transport}
          companyId={config.companyId}
          onClose={() => setAgentOpen(false)}
          onOpenScreen={(route) => {
            setAgentOpen(false);
            void navigate({ to: route });
          }}
        />
      ) : null}

      <VoiceDock />
      {cmdOpen ? <CommandPalette onClose={() => setCmdOpen(false)} /> : null}
      <KeyboardHelp open={helpOpen} onClose={() => setHelpOpen(false)} />
    </div>
  );
}
