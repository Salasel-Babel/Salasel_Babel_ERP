/* ═══════════════════════════════════════════════════════════════════════════
   ملاحة الأقسام الخمسة  ·  The five-section navigation
   ───────────────────────────────────────────────────────────────────────────
   القسم المبنيّ رابطٌ يعمل، وغير المبنيّ **بندٌ مُعلَن معطَّل** يقول «قيد
   البناء» — لا رابطٌ ميت ولا غياب. والفرق مقيس في الأثر لا في الذوق: رابطٌ
   يقود إلى لا شيء يُعلّم المستخدم ألّا يثق بالملاحة كلّها؛ وغيابُ القسم
   يجعل النظام يبدو أصغر مما بيع له.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { CSSProperties, ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { SECTIONS } from "./sections";

/**
 * قائمة الأقسام الخمسة.
 * @param props المسار الحالي، لتوسيم القسم القائم.
 */
export function SectionNav(props: { path: string }): ReactNode {
  const { t } = useT();
  return (
    <div className="sections" data-testid="section-nav">
      <p className="sections__label">{t("app.section.label")}</p>
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
              <span className="section__mark" aria-hidden="true" />
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
            aria-current={props.path === section.path ? "page" : undefined}
          >
            <span className="section__mark" aria-hidden="true" />
            <span className="section__name">{t(section.labelKey)}</span>
          </Link>
        );
      })}
    </div>
  );
}
