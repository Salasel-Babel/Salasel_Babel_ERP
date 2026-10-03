/* ═══════════════════════════════════════════════════════════════════════════
   ملحقاتُ القسم في الواجهة المبسّطة  ·  Section extras in the simple UI
   ───────────────────────────────────────────────────────────────────────────
   (ADR-0095 · التوصيتان الرابعة والخامسة)
   · على صفحةِ بداية كل نظام: **أكثرُ عملياته تكراراً** أزرارٌ كبيرة — ثلاثة أو
     أربعة لا أكثر — فيبدأ المستخدم من حيث يعمل لا من حيث يقرأ.
   · وعلى كل شاشة: زرُّ «إظهار كل الحقول» يُظهر ما طوته الثوابت للحالة
     الاستثنائية، ويعود بزرّه. وهو مفتاحُ الواجهة المبسّطة نفسه، لا مفتاحٌ ثانٍ.
   والمساراتُ من `SCREENS` لا من نسخةٍ ثانية، والمتقدّمُ منها يختفي معها.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { Icon } from "./icons";
import { SCREENS, SECTIONS, type Section } from "./sections";
import { setShowAdvanced, shownInMenus, useShowAdvanced } from "./simple-mode";

/** أكثر عمليات كل نظام تكراراً، بترتيب دورة العمل. */
const QUICK: Readonly<Record<Section["id"], readonly string[]>> = {
  accounting: ["/sales/invoice", "/sales/receipt", "/purchasing/bill", "/purchasing/payment"],
  inventory: ["/inventory/movements", "/inventory/items", "/inventory/warehouses", "/inventory/transfers"],
  hr: ["/hr/payroll", "/hr/advances-deductions", "/hr/payslip"],
  contracting: ["/contracting/certificate", "/contracting/change-orders", "/contracting/subcontracting", "/contracting/guarantees"],
  realestate: ["/realestate/lease", "/realestate/parties", "/realestate/arrears"],
};

/**
 * ملحقات القسم: أزرار البداية على صفحة النظام الأولى، ومفتاح الحقول على كل شاشة.
 * @param props النظام والمسار القائم.
 */
export function SectionExtras(props: { readonly section: Section["id"]; readonly current: string }): ReactNode {
  const { t } = useT();
  const showAdvanced = useShowAdvanced();
  const home = SECTIONS.find((system) => system.id === props.section)?.path;
  const quick = (QUICK[props.section] ?? [])
    .filter((path) => path !== props.current && shownInMenus(path, showAdvanced, props.current))
    .flatMap((path) => SCREENS.filter((screen) => screen.path === path));

  return (
    <div className="sec-extras" data-testid={"sec-extras-" + props.section}>
      {!showAdvanced && home === props.current && quick.length > 0 ? (
        <div className="quickacts" data-testid="sec-quick">
          <span className="quickacts__label">{t("app.quick.title")}</span>
          {quick.map((screen) => (
            <Link key={screen.path} to={screen.path as "/"} className="navitem quickact" data-testid={"sec-quick-" + screen.path.slice(1).replace(/\//g, "-")}>
              <Icon name={screen.icon} />
              <span className="navitem__name">{t(screen.labelKey)}</span>
            </Link>
          ))}
        </div>
      ) : null}
      <button
        type="button"
        className="btn btn-sm btn-ghost"
        data-testid="sec-more-fields"
        aria-pressed={showAdvanced}
        onClick={() => setShowAdvanced(!showAdvanced)}
      >
        {t(showAdvanced ? "app.quick.fewerFields" : "app.quick.moreFields")}
      </button>
    </div>
  );
}
