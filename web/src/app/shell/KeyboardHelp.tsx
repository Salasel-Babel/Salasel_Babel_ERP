/* قائمة الاختصارات. المحاسب يكتب ولا ينقر — فالاختصارات معروضة لا مخفيّة. */
import { Fragment, useEffect, type ReactNode } from "react";
import { useT } from "../../i18n/react";

/** صفّ اختصار: المفتاح كما يُضغَط، ووصفه من ملفّ اللغة. */
const SHORTCUTS: readonly { keys: string[]; key: string }[] = [
  { keys: ["Ctrl", "K"], key: "common.keys.command" },
  { keys: ["/"], key: "common.keys.search" },
  { keys: ["↓", "j"], key: "common.keys.rowNext" },
  { keys: ["↑", "k"], key: "common.keys.rowPrev" },
  { keys: ["Home"], key: "common.keys.rowFirst" },
  { keys: ["End"], key: "common.keys.rowLast" },
  { keys: ["PgDn"], key: "common.keys.pageNext" },
  { keys: ["PgUp"], key: "common.keys.pagePrev" },
  { keys: ["v"], key: "common.keys.viewCycle" },
  { keys: ["r"], key: "common.keys.reload" },
  { keys: ["?"], key: "common.keys.help" },
  { keys: ["Esc"], key: "common.keys.dismiss" },
];

/**
 * نافذة الاختصارات.
 * @param props مفتوحة أم لا، وكيف تُغلق.
 */
export function KeyboardHelp(props: { open: boolean; onClose: () => void }): ReactNode {
  const { t } = useT();
  const { open, onClose } = props;

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="overlay" data-open="true" onClick={onClose}>
      <div
        className="sheet sheet--sm"
        role="dialog"
        aria-modal="true"
        aria-label={t("common.keys.title")}
        data-testid="keyboard-help"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="sheet-hd">
          <h2>{t("common.keys.title")}</h2>
          <button type="button" className="iconbtn" aria-label={t("app.a11y.close")} onClick={onClose}>
            ✕
          </button>
        </header>
        <div className="sheet-bd">
          <div className="keys-grid">
            {SHORTCUTS.map((s) => (
              <Fragment key={s.key}>
                <div className="inline-group">
                  {s.keys.map((k) => (
                    <kbd key={k}>{k}</kbd>
                  ))}
                </div>
                <div>{t(s.key)}</div>
              </Fragment>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
