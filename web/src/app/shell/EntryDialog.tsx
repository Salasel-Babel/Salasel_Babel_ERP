/* ═══════════════════════════════════════════════════════════════════════════
   نافذةُ الإدخال المنبثقة  ·  The entry dialog
   ───────────────────────────────────────────────────────────────────────────
   (ADR-0097) شاشاتُ المستندات والسندات تُعرَض **نافذةً فوق قسمها** لا صفحةً:
   المحاسب يفتح سندَ الصرف، يكتبه، يرحّله، ويغلقه فيعود إلى حيث كان. والمسارُ
   باقٍ كما هو — الرابطُ العميق ولوحةُ الأوامر والشجرة تفتحه — وإنما يتغيّر
   **كيف يُرسَم**، فلا شاشةٌ تُعاد كتابتها ولا اختبارٌ يُعاد.

   · القائمةُ الجانبية والرأسُ يبقيان حيّين خلفها: النافذة تغطّي ساحةَ الصفحة
     وحدها، فمن يريد شاشةً أخرى ينقرها من الشجرة مباشرةً.
   · الإغلاقُ بزرّه أو بـEscape **من خارج الحقول** — فـEscape داخل حقلٍ يُغلق
     قائمتَه المنسدلة لا النافذة، وما كُتب لا يُرمى بضغطةٍ خاطئة.
   · والنقرُ على الظلّ لا يُغلق: مستندٌ نصفُ مكتوبٍ أغلى من راحة النقر.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useEffect, useRef, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { SCREENS, sectionOf } from "./sections";

const FIELD = new Set(["INPUT", "SELECT", "TEXTAREA"]);

/**
 * يُغلّف شاشةَ إدخالٍ في نافذةٍ منبثقة فوق ساحة الصفحة.
 * @param props مسارُ الشاشة وما يُرسَم داخلها.
 */
export function EntryDialog(props: { readonly path: string; readonly children: ReactNode }): ReactNode {
  const { t } = useT();
  const navigate = useNavigate();
  const box = useRef<HTMLDivElement>(null);
  const screen = SCREENS.find((entry) => entry.path === props.path);
  const home = sectionOf(props.path).path ?? "/home";

  const close = (): void => {
    void navigate({ to: home as "/" });
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent): void => {
      if (e.key !== "Escape") return;
      const target = e.target as HTMLElement | null;
      if (target && FIELD.has(target.tagName)) return;
      e.preventDefault();
      void navigate({ to: home as "/" });
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [home, navigate]);

  /* الشاشةُ التي تُركّز حقلَها الأوّل تسبق؛ وإلا وقع التركيزُ على النافذة
     نفسها كي يبدأ القارئُ من عنوانها لا من القائمة خلفها. */
  useEffect(() => {
    const node = box.current;
    if (node && !node.contains(document.activeElement)) node.focus({ preventScroll: true });
  }, [props.path]);

  return (
    <div className="entry-scrim" data-testid="entry-scrim">
      <div
        ref={box}
        className="entry-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={screen ? t(screen.labelKey) : undefined}
        data-testid="entry-dialog"
        tabIndex={-1}
      >
        <button
          type="button"
          className="iconbtn entry-dialog__close"
          aria-label={t("app.a11y.close")}
          title={t("app.a11y.close")}
          data-testid="entry-dialog-close"
          onClick={close}
        >
          ✕
        </button>
        {props.children}
      </div>
    </div>
  );
}
