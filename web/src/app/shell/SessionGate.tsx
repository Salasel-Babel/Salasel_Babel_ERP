/* ═══════════════════════════════════════════════════════════════════════════
   البوّابةُ الأمامية  ·  The front door
   ───────────────────────────────────────────────────────────────────────────
   **العطل الذي يُصلحه هذا الملفّ كان يجعل المنتَج غيرَ قابلٍ للبيع، ووصفَه
   المالكُ بنفسه:** لم يكن في الواجهة **حارسُ مسارٍ واحد**. كلُّ شاشةٍ تُفتح بلا
   جلسة، وكلٌّ منها تقول «اختر المنشأة أوّلاً» — فيبدو نظاماً مفتوحاً معطوباً لا
   نظاماً محمياً. وذلك أسوأ من منعٍ صريح.

   **والحجبُ هنا لا في الموجّه بقصد.** التحويلُ إلى `/sign-in` يُبدّل العنوان،
   فمن فتح رابطاً عميقاً إلى قيدٍ بعينه يفقده ويعود إلى الجذر بعد دخوله.
   والبوّابةُ تُبقي العنوان وتُبدّل ما يُرسَم فوقه: يدخل، فيجد **الصفحة التي
   طلبها** لا صفحةً أخرى.

   **ولا تُرسَم القشرةُ خلفها:** لا قائمةً جانبية ولا مبدّلَ أنظمةٍ ولا لوحةَ
   أوامر. وصفحةُ دخولٍ لها قائمةُ تنقّلٍ تقول للعميل «أنت داخل النظام أصلاً».

   **وبابان يُستثنيان — وهما اللذان يُستثنيان في العقد المنشور نفسه:** شاشةُ
   الدخول، وشاشةُ الانتساب التي تُسجّل منشأةً جديدة أو تُفعّل دعوة. ومن يطلب
   اعتماداً لا يملك اعتماداً، وبابٌ يُصدر جلسةً ويشترط جلسةً بابٌ لا يُفتح أبداً.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Outlet } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { SignInScreen } from "../../screens/session/SignInScreen";
import { LocaleSwitcher, ThemeSwitcher } from "./Switchers";
import accessiblePaletteHref from "../../styles/theme/theme-accessible.css?url";

/**
 * المساراتُ التي تُخدَم بلا جلسة — <b>وهي صورةُ `security: []` في العقد</b>.
 * <p>
 * قائمةٌ مقفلة لا بادئةٌ مفتوحة: <code>startsWith</code> على <code>/admin</code>
 * كان سيفتح الأعضاءَ والاشتراكَ والخطط معها.
 * </p>
 */
export const OPEN_SCREENS: readonly string[] = ["/sign-in", "/admin/enrolment"];

/** هل يُخدَم هذا المسار بلا جلسة؟ — مطابقةٌ كاملة لا بادئة. */
export function isOpenScreen(path: string): boolean {
  return OPEN_SCREENS.includes(path);
}

/**
 * الصفحةُ التي تحجب النظام: علامةُ المنتج وشاشةٌ واحدة، ولا قشرةَ خلفهما.
 * <p>
 * <b>والبابان المفتوحان يُرسمان داخلها لا خارجها</b>: شاشةُ الانتساب تُفتح بلا
 * جلسة — وهذا حقٌّ لها — ولكنّها لا تستحقّ قائمةً جانبية ومبدّلَ أنظمةٍ حولها
 * أكثر ممّا تستحقّه شاشةُ الدخول. فالبوّابةُ تُرسم في الحالين، ويتبدّل ما بداخلها.
 * </p>
 * @param props المسارُ الذي طُلب، وهل هو بابٌ مفتوح يُرسَم بنفسه.
 */
export function SessionGate(props: { path: string; open: boolean }): ReactNode {
  const { t } = useT();
  return (
    <div className="gate" data-testid="session-gate">
      <main className="gate__card" id="main">
        <div className="gate__head">
          <div className="gate__brand">
            <span className="mark" aria-hidden="true" />
            <span>{t("app.name")}</span>
          </div>
          {/* ── واللغةُ والمظهر يُبدَّلان **قبل** الدخول لا بعده ──────────────
              وهما في القشرة، والقشرةُ لا تُرسَم هنا. فكان المُبدِّلان يغيبان مع
              ما يفعلانه: `data-theme` لا يُكتب على الجذر أصلاً، فيرى من اختار
              الداكن بابَ دخولٍ فاتحاً، وتسقط لوحةُ الوصولية معه — وهي لمن
              يحتاجها شرطُ قراءة لا تفضيل. ومن لا يقرأ العربية يحتاج الإنجليزية
              **ليدخل**، لا بعد أن يدخل. */}
          <div className="gate__switchers">
            <LocaleSwitcher />
            <ThemeSwitcher accessiblePaletteHref={accessiblePaletteHref} />
          </div>
        </div>

        {props.open ? <Outlet /> : <SignInScreen />}

        {/* **ما طلبتَه محفوظ** — والقول به يمنع أن يُقرأ الحجبُ ضياعاً. */}
        {props.path !== "/" && !props.open ? (
          <p className="gate__return" data-testid="gate-return">
            {t("app.gate.returnTo")} <code className="mono" dir="ltr">{props.path}</code>
          </p>
        ) : null}
      </main>
    </div>
  );
}
