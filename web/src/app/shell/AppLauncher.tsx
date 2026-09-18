/* ═══════════════════════════════════════════════════════════════════════════
   مُشغّلُ الأنظمة  ·  The system launcher
   ───────────────────────────────────────────────────────────────────────────
   **لماذا خرجت الأنظمةُ الخمسة من القائمة الجانبية إلى زرٍّ في الرأس:**
   كانت الأنظمةُ والشاشاتُ في عمودٍ واحد، فيقرأهما الناظر مستوىً واحداً —
   «المخزني» و«ميزان المراجعة» بنداً إلى بند. وهما ليسا كذلك: الأولُ نظامٌ
   يُشترى ويُرخَّص، والثاني شاشةٌ تُفتح داخله. والخلطُ بينهما هو ما جعل
   المالك يقول إن ما يراه لا يُباع نظاماً محاسبياً.

   **فصار للأنظمة مكانُها الخاصّ**: زرُّ مصفوفةِ نقاطٍ في أقصى يسار الرأس —
   وهو الموضع الذي تعلّمه الناسُ من مُشغّلات التطبيقات — يفتح لوحاً تُعرَض
   فيه الأنظمةُ **مربّعاتٍ برموزها**، فتُقرأ في نظرةٍ واحدة لا في مسحٍ رأسي.
   والقائمةُ الجانبية صارت لشاشات النظام المفتوح وحدها.

   **واللوح يحمل ما تحمله الملاحة القديمة بلا نقص:** النظامُ غير المبنيّ
   يظهر فيه «قيد البناء» معطَّلاً لا مخفيّاً — وهو `SectionNav` نفسه بهيئة
   مربّعات، لا نسخةٌ ثانية منه تنحرف عنه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useT } from "../../i18n/react";
import { Icon } from "./icons";
import { SectionNav } from "./SectionNav";

/**
 * زرُّ الأنظمة ولوحُه.
 * @param props المسار القائم، لتوسيم النظام المفتوح داخل اللوح.
 */
export function AppLauncher(props: { path: string }): ReactNode {
  const { t } = useT();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    /* **الهروبُ يُغلق، والنقرُ خارج اللوح يُغلق** — ولوحٌ لا يُغلق إلا بزرّه
       يحبس من فتحه بالخطأ، وهو أشيعُ سببٍ لفتحه. */
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    const onDown = (e: MouseEvent) => {
      if (box.current && !box.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onDown);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onDown);
    };
  }, [open]);

  return (
    <div className="applauncher" ref={box}>
      <button
        type="button"
        className="iconbtn applauncher__btn"
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={t("app.launcher.open")}
        title={t("app.launcher.open")}
        data-testid="open-launcher"
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name="grid" size={20} />
      </button>

      {open ? (
        <div className="launchpanel" data-testid="launch-panel">
          <p className="launchpanel__label">{t("app.launcher.title")}</p>
          <SectionNav path={props.path} onPick={() => setOpen(false)} />
          <p className="launchpanel__note">{t("app.launcher.note")}</p>
        </div>
      ) : null}
    </div>
  );
}
