/* ═══════════════════════════════════════════════════════════════════════════
   لوحة الأوامر — Ctrl/⌘+K  ·  The command palette
   ───────────────────────────────────────────────────────────────────────────
   تفتح على **كل شاشةٍ وفعل** من أي موضع في التطبيق. وهي — لا الملاحة — أسرع
   طريقٍ إلى ما يعرف المستخدم اسمه ولا يعرف مكانه؛ والملاحة تبقى للاستكشاف.

   **والوصولية ليست اختيارية هنا:** اللوحة `role="dialog"` مُوسَمة، والقائمة
   `role="listbox"`، والعنصر النشط يُبلَّغ بـ`aria-activedescendant` فيقرؤه
   قارئ الشاشة وهو يتحرّك بالأسهم بلا نقل تركيز — وحقل البحث يبقى يستقبل
   الكتابة. وEsc يُغلق، والتركيز يعود إلى ما فتحها.

   **والقسم غير المبنيّ يظهر في اللوحة معطّلاً ومُعلَناً**، لا محذوفاً: من
   يبحث عن «الرواتب» يجب أن يعرف أنها في الطريق، لا أن يظنّها غير موجودة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { SCREENS, SECTIONS } from "./sections";

/** بندٌ في لوحة الأوامر. */
interface Entry {
  readonly id: string;
  readonly label: string;
  readonly group: string;
  readonly path: string | null;
  readonly enabled: boolean;
  readonly note?: string;
  /** لماذا لا يُفتَح — يُقرأ بالتحويم وبقارئ الشاشة. */
  readonly why?: string;
}

/**
 * لوحة الأوامر. الهيكل يركّبها عند الفتح ويفكّكها عند الإغلاق.
 * @param props كيف تُغلق.
 */
export function CommandPalette(props: { onClose: () => void }): ReactNode {
  const { t } = useT();
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const returnTo = useRef<Element | null>(null);

  const entries = useMemo<readonly Entry[]>(() => {
    const screens: Entry[] = SCREENS.map((s) => ({
      id: "screen:" + s.path,
      label: t(s.labelKey),
      group: t("app.command.screens"),
      path: s.path,
      enabled: true,
    }));
    const sections: Entry[] = SECTIONS.filter((s) => !s.built).map((s) => ({
      id: "section:" + s.id,
      label: t(s.labelKey),
      group: t("app.command.sections"),
      path: null,
      enabled: false,
      note: t("app.section.soon"),
      why: t("app.section.underConstruction"),
    }));
    return [...screens, ...sections];
  }, [t]);

  const shown = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return entries;
    return entries.filter((e) => e.label.toLocaleLowerCase().includes(needle));
  }, [entries, query]);

  /* اللوحة تُركَّب عند الفتح وتُفكَّك عند الإغلاق (App.tsx)، فحالتها تبدأ
     نظيفةً في كل مرّة بلا `setState` داخل أثر — وهو ما يمنعه حدّ React. */
  useEffect(() => {
    returnTo.current = document.activeElement;
    inputRef.current?.focus();
    return () => {
      const back = returnTo.current;
      if (back instanceof HTMLElement) back.focus();
    };
  }, []);

  const run = useCallback(
    (entry: Entry | undefined) => {
      if (!entry?.enabled || !entry.path) return;
      props.onClose();
      void navigate({ to: entry.path });
    },
    [navigate, props]
  );

  const onKey = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        props.onClose();
        return;
      }
      if (e.key === "ArrowDown") {
        e.preventDefault();
        setActive((i) => (shown.length ? (i + 1) % shown.length : 0));
        return;
      }
      if (e.key === "ArrowUp") {
        e.preventDefault();
        setActive((i) => (shown.length ? (i - 1 + shown.length) % shown.length : 0));
        return;
      }
      if (e.key === "Enter") {
        e.preventDefault();
        run(shown[active]);
      }
    },
    [active, props, run, shown]
  );

  let lastGroup = "";
  return (
    <div
      className="cmdk-scrim"
      data-testid="command-palette"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) props.onClose();
      }}
    >
      <div
        className="cmdk"
        role="dialog"
        aria-modal="true"
        aria-label={t("app.command.title")}
        onKeyDown={onKey}
      >
        <div className="cmdk__search">
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setActive(0);
            }}
            placeholder={t("app.command.placeholder")}
            aria-label={t("app.command.placeholder")}
            aria-controls="cmdk-list"
            aria-activedescendant={shown[active] ? "cmdk-" + shown[active].id : undefined}
            data-testid="command-input"
          />
        </div>
        <div className="cmdk__list" id="cmdk-list" role="listbox" aria-label={t("app.command.title")}>
          {shown.length === 0 ? (
            <p className="cmdk__group">{t("app.command.nothing")}</p>
          ) : (
            shown.map((entry, index) => {
              const head = entry.group !== lastGroup ? entry.group : null;
              lastGroup = entry.group;
              return (
                <div key={entry.id}>
                  {head ? <p className="cmdk__group">{head}</p> : null}
                  <button
                    type="button"
                    id={"cmdk-" + entry.id}
                    className="cmdk__item"
                    role="option"
                    aria-selected={index === active}
                    aria-disabled={!entry.enabled}
                    title={entry.why}
                    onMouseEnter={() => setActive(index)}
                    onClick={() => run(entry)}
                  >
                    <span>{entry.label}</span>
                    {entry.note ? <span className="spacer muted">{entry.note}</span> : null}
                  </button>
                </div>
              );
            })
          )}
        </div>
        <div className="cmdk__foot">
          <span>{t("app.command.hintMove")}</span>
          <span>{t("app.command.hintOpen")}</span>
          <span>{t("app.command.hintClose")}</span>
        </div>
      </div>
    </div>
  );
}
