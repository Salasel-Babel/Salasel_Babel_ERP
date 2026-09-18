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
import { useCallback, useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, Outlet, useNavigate, useRouterState } from "@tanstack/react-router";
import { health, renewSession, revokeSession } from "../api/generated/client";
import { useApi } from "./api-context";
import { useT } from "../i18n/react";
import { HealthBadge, LocaleSwitcher, ThemeSwitcher } from "./shell/Switchers";
import { CompanyBadge } from "./shell/CompanyBadge";
import { KeyboardHelp } from "./shell/KeyboardHelp";
import { CommandPalette } from "./shell/CommandPalette";
import { ScreenNav } from "./shell/ScreenNav";
import { AppLauncher } from "./shell/AppLauncher";
import { VoiceDock } from "./shell/VoiceDock";
import { AgentWorkspace } from "../agent";
import { VoiceDraftBanner } from "./VoiceDraftBanner";
import { sectionOf } from "./shell/sections";
import { SessionGate, isOpenScreen } from "./shell/SessionGate";
import { applySession, clearSession, hasSession, needsRenewal } from "./session";
import { fetchTransport } from "../api/transport";
import { MOTION } from "../ui";
import accessiblePaletteHref from "../styles/theme/theme-accessible.css?url";

/** الهيكل حول كل شاشة. */
export function AppShell(): ReactNode {
  const { t } = useT();
  const { transport, config, setConfig } = useApi();
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

  /* ── التجديدُ الصامت ────────────────────────────────────────────────────
     الاعتمادُ الفاعل يعيش خمس عشرة دقيقة، ومحاسبٌ يكتب قيداً لا يجوز أن يُطرَد
     في منتصفه. فيُجدَّد **وهو حيّ** قبل انقضائه بدقيقتين، ولا يرى المستخدم شيئاً.
     وبلا هذا يصير عمرُ الجلسة القصير — وهو ميزةٌ أمنية — عطلاً يوميّاً.

     **والفشلُ يُنهي الجلسة ولا يُعاد المحاولة:** اعتمادُ تجديدٍ مرفوض إمّا انقضى
     وإمّا أُبطل وإمّا قُدِّم مرّتين (وحينها أُسقطت العائلة كلّها عمداً). وإعادةُ
     المحاولة في الثلاثة تطرق باباً مغلقاً كلَّ دقيقة. */
  useEffect(() => {
    if (config.refreshToken === "") return;

    let live = true;
    const tick = async (): Promise<void> => {
      if (!live || !needsRenewal(config, Date.now())) return;
      try {
        const renewed = await renewSession(
          fetchTransport({ baseUrl: config.baseUrl }),
          { body: { refreshCredential: config.refreshToken } }
        );
        if (live) setConfig(applySession(config, renewed));
      } catch {
        if (live) setConfig(clearSession(config));
      }
    };

    void tick();
    const timer = setInterval(() => void tick(), 30_000);
    return () => {
      live = false;
      clearInterval(timer);
    };
  }, [config, setConfig]);

  const signOut = useCallback(() => {
    /* الإبطالُ على الخادم **يُطلب ولا يُنتظَر جوابه**: الخروج في المتصفّح يجب أن
       يقع ولو كانت الشبكة مقطوعة. والاعتمادُ المُهيَّأ من الإعداد لا عائلةَ له
       فيردّ الخادمُ رفضاً معلوماً — وهو رفضٌ لا يمنع المحوَ هنا. */
    void revokeSession(transport).catch(() => undefined);
    setConfig(clearSession(config));
    void navigate({ to: "/sign-in" });
  }, [config, navigate, setConfig, transport]);

  /* **ولا يُرسَم شيءٌ من النظام بلا جلسة.** والشرطُ بعد كل الخطّافات لا قبلها:
     خطّافٌ يُتخطّى في رسمةٍ ويُنفَّذ في التالية يكسر قواعد React. */
  if (!hasSession(config)) {
    return <SessionGate path={path} open={isOpenScreen(path)} />;
  }

  return (
    <div className="app-shell" data-section={section.id} style={tint}>
      {/* شريط الانتقال: يُعاد بناؤه بتغيّر المسار فتُعاد حركته. */}
      <span className="transit" key={path} aria-hidden="true" />

      <a className="visually-hidden" href="#main">
        {t("app.web.skipToTable")}
      </a>

      <nav className="app-side" aria-label={t("app.a11y.mainNav")}>
        {/* العلامةُ بابٌ إلى البداية لا زينة: هي الموضع الذي تعلّمه الناس
            للعودة إلى أوّل الطريق، وتركُها صمّاء يُهدر ما تعلّموه. */}
        <Link to="/home" className="brand" data-testid="brand-home">
          <span className="mark" aria-hidden="true" />
          <span>{t("app.name")}</span>
        </Link>

        {/* ── شجرةُ النظام المفتوح وحده ───────────────────────────────────
            وكانت هنا **ملاحةُ الأنظمة الخمسة فوق قائمةٍ مسطّحة بشاشاتها** —
            فيُقرأ النظامُ والشاشةُ مستوىً واحداً، ويقرأ الناظر ستّين رابطاً
            بلا تصنيف. فخرجت الأنظمة إلى مُشغّلها في الرأس (`AppLauncher`)،
            وصارت القائمة شجرةً من طبقتين لشاشات النظام المفتوح وحده. */}
        <ScreenNav section={section.id} path={path} />
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
          {/* الخروجُ في الرأس لا في شاشةٍ تُبحَث عنها: هو الفعلُ الذي يُطلب حين
              يقوم أحدٌ عن جهازه، فيجب أن يكون حيث تقع العين. */}
          <button
            type="button"
            className="btn btn-sm"
            data-testid="sign-out-shell"
            onClick={signOut}
          >
            {t("screen.signIn.signOut")}
          </button>
          {/* **آخرُ عنصرٍ في الرأس هو أقصى يساره في العربية** — وهو موضع
              مُشغّل الأنظمة الذي طلبه المالك، وموضعُ مُشغّلات التطبيقات
              الذي تعلّمه الناس. ولو وُضع أوّلاً لظهر في أقصى اليمين. */}
          <AppLauncher path={path} />
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
