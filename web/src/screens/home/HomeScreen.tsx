/* ═══════════════════════════════════════════════════════════════════════════
   /home — صفحةُ البداية  ·  The start page
   ───────────────────────────────────────────────────────────────────────────
   **ما تجيبه هذه الصفحة سؤالٌ واحد: «ما الذي اشتريتُه، وأين أبدأ؟»**
   ولا تجيبه قائمةٌ رأسيةٌ من ستّين رابطاً: الناظرُ إليها يرى شاشاتٍ ولا يرى
   أنظمة. فالأنظمةُ هنا **مربّعاتٌ برموزها** — خمسةٌ في نظرةٍ واحدة، كلٌّ
   منها بلونه ورمزه واسمه وسطرٍ يقول ما يفعله. وهو الشكل الذي طلبه المالك.

   **ولا رقمَ مخترَع على هذه الصفحة.** لا «١٢٤٨ صنفاً» ولا «٢٣ تنبيهاً» ولا
   منحنى إيرادات: كلُّ رقمٍ يُعرَض هنا كان سيكون رقماً لم يُقرأ من الدفتر —
   ولوحةٌ تعرض أرقاماً لا مصدر لها تُعلّم المستخدم ألّا يصدّق أرقام النظام
   كلَّها. فحين يصير لكلّ نظامٍ مؤشّرٌ **من بابٍ منشور** يُكتب في مربّعه،
   وقبل ذلك المربّعُ بابٌ لا لوحةُ قيادة.

   **والتسمياتُ من `SECTIONS` لا من نسخةٍ ثانية**: مربّعٌ يسمّي نظاماً باسمٍ
   غير اسمه في المُشغّل والملاحة عطلٌ صامت — والمصدرُ واحد فلا يقع.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { CSSProperties, ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { Icon } from "../../app/shell/icons";
import { SCREENS, SECTIONS } from "../../app/shell/sections";
import { useMoment } from "../../ui";
import "./home.css";

/** صفحةُ البداية كاملةً. */
export function HomeScreen(): ReactNode {
  const { t } = useT();
  const [arriveCls] = useMoment("arrive");

  return (
    <section className="stack" data-testid="home-screen">
      <header className="pagehead">
        <div>
          <h1>{t("app.home.title")}</h1>
          <p className="sub">{t("app.home.lede")}</p>
        </div>
      </header>

      <div className={"systiles " + arriveCls} data-testid="home-tiles">
        {SECTIONS.map((system) => {
          const tint = { "--section-tint": system.tint } as CSSProperties;
          const body = (
            <>
              <span className="systile__mark">
                <Icon name={system.icon} size={26} />
              </span>
              <span className="systile__name">{t(system.labelKey)}</span>
              <span className="systile__note">{t("app.home.desc." + system.id)}</span>
            </>
          );
          /* نظامٌ لم تُبنَ شاشاته: مربّعٌ **مُعلَنٌ معطَّل** لا مربّعٌ غائب —
             وهو حكم `SectionNav` نفسه، وغيابُه يجعل النظام يبدو أصغر مما بيع. */
          if (!system.built || !system.path) {
            return (
              <span
                key={system.id}
                className="systile"
                data-built="false"
                data-section={system.id}
                style={tint}
                aria-disabled="true"
                title={t("app.section.underConstruction")}
              >
                {body}
                <span className="systile__soon">{t("app.section.soon")}</span>
              </span>
            );
          }
          const to = system.path as "/";
          return (
            <Link
              key={system.id}
              to={to}
              className="systile"
              data-built="true"
              data-section={system.id}
              data-testid={"home-tile-" + system.id}
              style={tint}
            >
              {body}
            </Link>
          );
        })}
      </div>

      {/* ── مدخلان يوميّان ─────────────────────────────────────────────────
          **ولماذا هذان بالذات:** الشاشتان اللتان تُفتحان كلَّ يوم في كل
          منشأة — ما يُكتب (قيدُ اليومية) وما يُقرأ (ميزانُ المراجعة). وهما
          مُعلَنتان في `SCREENS` كغيرهما، فلا مسارَ مكتوبٌ بيدٍ هنا. */}
      <div className="homequick" data-testid="home-quick">
        <p className="homequick__label">{t("app.home.quick")}</p>
        {SCREENS.filter((screen) => screen.path === "/" || screen.path === "/voucher").map(
          (screen) => (
            <Link
              key={screen.path}
              to={screen.path as "/"}
              className="navitem"
              data-testid={"home-quick-" + (screen.path === "/" ? "trial-balance" : "voucher")}
            >
              <Icon name={screen.icon} />
              <span className="navitem__name">{t(screen.labelKey)}</span>
            </Link>
          )
        )}
      </div>
    </section>
  );
}
