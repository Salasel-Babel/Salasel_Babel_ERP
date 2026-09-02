/* مبدّلا اللغة والمظهر، وشارة حالة الخدمة. */
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useLocale, useT } from "../../i18n/react";
import type { HealthResponse } from "../../api/generated/types";

/* ── اللغة ───────────────────────────────────────────────────────────── */

/** مبدّل اللغة: كل لغة في الفهرس تظهر باسمها الأصلي. */
export function LocaleSwitcher(): ReactNode {
  const { i18n, locale, setLocale } = useLocale();
  const { t } = useT();
  return (
    <div className="field" style={{ minWidth: "9rem" }}>
      <label htmlFor="sb-locale-select">{t("app.locale.label")}</label>
      <select
        id="sb-locale-select"
        className="ctl"
        aria-label={t("app.locale.aria")}
        data-testid="locale-switcher"
        value={locale}
        onChange={(e) => setLocale(e.target.value)}
      >
        {i18n.catalogue.map((entry) => (
          <option key={entry.code} value={entry.code} lang={entry.code} dir={entry.dir}>
            {entry.native}
          </option>
        ))}
      </select>
    </div>
  );
}

/* ── المظهر ──────────────────────────────────────────────────────────── */

/** الفاتح · الداكن · مظهر النظام. */
export type ThemeChoice = "system" | "light" | "dark";
/** اللوحة المعتمدة أو المقترحة (وصولية). */
export type PaletteChoice = "default" | "accessible";

/* **الداكن السينمائي هو المظهر الافتراضي للتطبيق كلّه** — قرارُ مالك صريح
   (ADR-0055 «الداكن السينمائي مظهرٌ افتراضي والفاتح بديلٌ قائم»). والفاتح
   يبقى **بديلاً صحيحاً لا مهجوراً**: هو المظهر المعتمد في `theme-default.css`
   على `:root` المجرّد، وتبقى فوقه لوحة الوصولية تعمل. */
const DEFAULT_THEME: ThemeChoice = "dark";

const THEME_KEY = "sb-theme";
const PALETTE_KEY = "sb-palette";

function read(key: string, fallback: string): string {
  try {
    return globalThis.localStorage?.getItem(key) ?? fallback;
  } catch {
    return fallback;
  }
}

/** يطبّق المظهر واللوحة على الجذر ويحفظهما. */
export function ThemeSwitcher(props: { accessiblePaletteHref: string }): ReactNode {
  const { t } = useT();
  const [theme, setTheme] = useState<ThemeChoice>(() => read(THEME_KEY, DEFAULT_THEME) as ThemeChoice);
  const [palette, setPalette] = useState<PaletteChoice>(
    () => read(PALETTE_KEY, "default") as PaletteChoice
  );
  const linkRef = useRef<HTMLLinkElement | null>(null);

  useEffect(() => {
    const root = document.documentElement;
    if (theme === "system") root.removeAttribute("data-theme");
    else root.setAttribute("data-theme", theme);
    try {
      globalThis.localStorage?.setItem(THEME_KEY, theme);
    } catch {
      /* التصفّح الخاص. */
    }
  }, [theme]);

  useEffect(() => {
    /* «سمة عميل = ملفّ واحد يُربَط بعد ملفّ الرموز فيفوز بترتيب المصدر»
       — وهي القصّة نفسها التي يرويها design/theme/theme-accessible.css.
       ولذلك تُربَط هنا كملفّ لا كصنف على الجذر. */
    if (!linkRef.current) {
      const link = document.createElement("link");
      link.rel = "stylesheet";
      link.href = props.accessiblePaletteHref;
      link.dataset.palette = "accessible";
      /* ⚠ **الإطفاء بـ`media` لا بـ`disabled` وحدها.** كُتب هذا أولاً بـ
         `link.disabled = …` وحدها، فكان يعمل أحياناً: `disabled` تُضبَط قبل أن
         تصل الورقة، وإن وصلت **بعدها** فُقد الإطفاء ودخلت اللوحة الثانية فوق
         الأولى بلا أن يتغيّر شيء في الواجهة. ومقيسٌ أنه تكرّر: تشغيلان لنفس
         الأمر على نفس البناء أعطيا لوحتين مختلفتين، أوّلهما بعد بناءٍ بارد.
         و`media` تُقيَّم في المطابقة لا عند التحميل، فلا تُفقد.
         (traps.md#fakh-a-stylesheet-disabled-before-it-loads-comes-back-enabled) */
      link.media = "not all";
      document.head.appendChild(link);
      linkRef.current = link;
    }
    linkRef.current.media = palette === "accessible" ? "all" : "not all";
    linkRef.current.disabled = palette !== "accessible";
    document.documentElement.setAttribute("data-palette", palette);
    try {
      globalThis.localStorage?.setItem(PALETTE_KEY, palette);
    } catch {
      /* التصفّح الخاص. */
    }
  }, [palette, props.accessiblePaletteHref]);

  return (
    <div className="inline-group">
      <div className="field" style={{ minWidth: "8rem" }}>
        <label htmlFor="sb-theme-select">{t("app.theme.toggle")}</label>
        <select
          id="sb-theme-select"
          className="ctl"
          data-testid="theme-switcher"
          value={theme}
          onChange={(e) => setTheme(e.target.value as ThemeChoice)}
        >
          <option value="system">{t("app.theme.system")}</option>
          <option value="light">{t("app.theme.light")}</option>
          <option value="dark">{t("app.theme.dark")}</option>
        </select>
      </div>
      <div className="field" style={{ minWidth: "9rem" }}>
        <label htmlFor="sb-palette-select">{t("app.theme.palette")}</label>
        <select
          id="sb-palette-select"
          className="ctl"
          data-testid="palette-switcher"
          value={palette}
          onChange={(e) => setPalette(e.target.value as PaletteChoice)}
        >
          <option value="default">{t("app.theme.paletteDefault")}</option>
          <option value="accessible">{t("app.theme.paletteAccessible")}</option>
        </select>
      </div>
    </div>
  );
}

/* ── حالة الخدمة ─────────────────────────────────────────────────────── */

/** شارة تقول: هل الخادم يردّ، وبأي ثقافة وتقويم يعمل. */
export function HealthBadge(props: {
  health: HealthResponse | null;
  failed: boolean;
  loading: boolean;
}): ReactNode {
  const { t } = useT();
  const hijri = props.health?.calendar === "UmAlQuraCalendar";
  const state = props.loading ? "checking" : props.failed ? "down" : "ok";
  return (
    <span
      className={"pill " + (state === "ok" ? "pill--posted" : state === "down" ? "pill--rejected" : "pill--pending")}
      data-testid="health-badge"
      data-state={state}
      title={
        props.health
          ? t("app.health.culture") +
            ": " +
            props.health.culture +
            " · " +
            t("app.health.calendar") +
            ": " +
            props.health.calendar +
            (hijri ? " — " + t("app.health.hijriWarning") : "")
          : t("app.health.label")
      }
    >
      {t("app.health." + state)}
      {props.health ? <span className="mono"> · {props.health.apiVersion}</span> : null}
    </span>
  );
}
