/* ═══════════════════════════════════════════════════════════════════════════
   ملاحة الأقسام الخمسة  ·  The five-section navigation
   ───────────────────────────────────────────────────────────────────────────
   القسم المبنيّ رابطٌ يعمل، وغير المبنيّ **بندٌ مُعلَن معطَّل** يقول «قيد
   البناء» — لا رابطٌ ميت ولا غياب. والفرق مقيس في الأثر لا في الذوق: رابطٌ
   يقود إلى لا شيء يُعلّم المستخدم ألّا يثق بالملاحة كلّها؛ وغيابُ القسم
   يجعل النظام يبدو أصغر مما بيع له.

   **وموضعُها اليوم لوحُ المُشغّل، لا عمودٌ في القائمة الجانبية** (ADR-0093):
   النظامُ يُشترى ويُرخَّص، والشاشةُ تُفتح داخله — وجوارُهما في عمودٍ واحد
   كان يجعلهما يُقرآن مستوىً واحداً. والعنوانُ فوقها عنوانُ اللوح، فلا
   يُكرَّر عنوانان فوق شيءٍ واحد.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { CSSProperties, ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { Icon } from "./icons";
import { SECTIONS, sectionOf } from "./sections";

/**
 * الأنظمة الخمسة مربّعاتٍ في لوح المُشغّل.
 * @param props المسار الحالي لتوسيم النظام القائم، وما يُفعَل عند الاختيار.
 */
export function SectionNav(props: { path: string; onPick?: () => void }): ReactNode {
  const { t } = useT();
  /* **النظامُ القائم يُعرَف من المسار لا من مطابقة نصّية.** كانت المقارنة
     `props.path === section.path`، فلا يُوسَم المحاسبيُّ قائماً وأنت في
     `/sales/invoice` — وأنت فيه. والموجّه يُطابق بالبادئة فكان يوسمه على
     **كل** مسار لأن مساره `/`. و`sectionOf` تعرف الجواب الصحيح وحدها.
     و`activeOptions` أدناه ليست تكراراً لها: هي التي **تُسكِت** توسيمَ
     الموجّه بالبادئة، وبلا إسكاته يبقى المحاسبيُّ موسوماً في المخزني. */
  const here = sectionOf(props.path).id;
  return (
    <div className="sections" data-testid="section-nav">
      {SECTIONS.map((section) => {
        const tint = { "--section-tint": section.tint } as CSSProperties;
        if (!section.built || !section.path) {
          return (
            <span
              key={section.id}
              className="section"
              data-built="false"
              data-section={section.id}
              style={tint}
              aria-disabled="true"
              title={t("app.section.underConstruction")}
            >
              <span className="section__mark">
                <Icon name={section.icon} size={20} />
              </span>
              <span className="section__name">{t(section.labelKey)}</span>
              <span className="section__soon">{t("app.section.soon")}</span>
            </span>
          );
        }
        /* المسار نصٌّ في العقد أعلاه، والموجّه يطلب حرفيّةً — والتحويل هنا
           هو الموضع الوحيد الذي يعرف الاثنين، فلا يتسرّب إلى الشاشات. */
        const to = section.path as "/";
        return (
          <Link
            key={section.id}
            to={to}
            className="section"
            data-built="true"
            data-section={section.id}
            data-testid={"section-" + section.id}
            style={tint}
            activeOptions={{ exact: true, includeSearch: false }}
            aria-current={here === section.id ? "page" : undefined}
            onClick={props.onPick}
          >
            <span className="section__mark">
              <Icon name={section.icon} size={20} />
            </span>
            <span className="section__name">{t(section.labelKey)}</span>
          </Link>
        );
      })}
    </div>
  );
}
