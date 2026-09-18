/* ═══════════════════════════════════════════════════════════════════════════
   شاشاتُ القسم المفتوح وحدها  ·  The open section's screens, and only those
   ───────────────────────────────────────────────────────────────────────────
   **العطل الذي يُصلحه هذا الملفّ كان يجعل النظام يبدو جرداً لا منتَجاً:**
   كانت القائمة الجانبية تسرد **الشاشات التسع والخمسين كلَّها دفعةً واحدة**،
   مكتوبةً بيد في `App.tsx`، بينما مبدّلُ الأقسام فوقها يلوّن القشرة ولا
   يُرشِّح شيئاً. فمن يفتح «الموارد البشرية» يرى تحتها ميزانَ المراجعة وأمرَ
   الشراء وشجرةَ التسكين — ويقرأ ذلك على أنه نظامٌ واحد مكدَّس لا خمسةُ أنظمة.
   وهو انطباعٌ صحيح عمّا كان يُعرَض، لا سوءُ ظنّ.

   والآن: **القسمُ يفتح شاشاتِه**. والقائمة تُبنى من `SCREENS` نفسها — لا من
   نسخةٍ ثانية بيد — فلا تنحرف عن لوحة الأوامر ولا عن مصفوفة الحراسة، وهو ما
   كان تعليقٌ في `App.tsx` يوصي به منذ القسم المخزني.

   **وثلاثُ مجموعاتٍ داخل المحاسبي تبقى معنونة** لأنها تُقرأ ولا تُخلط:
   دورةُ المبيعات، ودورةُ المشتريات، والإدارةُ والتأسيس. وهذا فصلُ قراءةٍ لا
   قسمٌ سادس — عقدُ الملاحة خماسيٌّ مقفل (ADR-0069).

   **ولا شاشةَ تسقط من العرض:** اتّحادُ ما تعرضه الأقسامُ الخمسة يساوي
   `SCREENS` بالضبط، ويحرسه `tests/shell-nav.test.tsx`. فترشيحٌ يُخفي شاشةً
   عن كلّ الأقسام يُحمّر اختباراً، لا يمرّ صامتاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { SCREENS, SCREEN_GROUPS, type ScreenEntry, type Section } from "./sections";

/** رمزُ اختبارٍ من المسار: `/admin/plans` ← `nav-admin-plans`. */
function testId(path: string): string {
  return "nav" + (path === "/" ? "-home" : path.replace(/\//g, "-"));
}

/**
 * عناوينُ القراءة داخل القسم المحاسبي — <b>وهي فصلُ قراءةٍ لا أقسام</b>.
 * <p>
 * من يكتب سندَ قبضٍ في يومه لا يفتح «سحب عضوية» ولا «تأسيس المنشأة»، فجوارُهما
 * في قائمةٍ واحدة يُبطئ القراءة ولا يُسرّعها. والمسارُ هو ما يقرّر، لا تخمين.
 * </p>
 */
const ADMIN_PREFIX = "/admin/";
const SETUP_PREFIX = "/setup";

function isAdmin(entry: ScreenEntry): boolean {
  return entry.path.startsWith(ADMIN_PREFIX);
}

function isSetup(entry: ScreenEntry): boolean {
  return entry.path === SETUP_PREFIX || entry.path.startsWith(SETUP_PREFIX + "/");
}

function Item(props: { entry: ScreenEntry }): ReactNode {
  const { t } = useT();
  /* ‏`to` مُضيَّق إلى نوع المسار: الموجّه يعرف المسارات المسجَّلة، وهذه منها
     بحكم الحارس الذي يقارن `SCREENS` بالموجّه. */
  const to = props.entry.path as "/";
  return (
    <Link to={to} className="navitem" data-testid={testId(props.entry.path)}>
      {t(props.entry.labelKey)}
    </Link>
  );
}

function Block(props: { labelKey: string; entries: readonly ScreenEntry[] }): ReactNode {
  const { t } = useT();
  if (props.entries.length === 0) return null;
  return (
    <>
      <p className="sections__label">{t(props.labelKey)}</p>
      {props.entries.map((entry) => (
        <Item key={entry.path} entry={entry} />
      ))}
    </>
  );
}

/** شاشاتُ القسم المفتوح، مرتّبةً بترتيب `SCREENS` — وهو ترتيبُ العمل. */
export function ScreenNav(props: { section: Section["id"] }): ReactNode {
  const mine = SCREENS.filter((screen) => screen.section === props.section);

  const groups = SCREEN_GROUPS.filter((group) => group.section === props.section);
  const grouped = new Set(groups.map((group) => group.id));

  const main = mine.filter(
    (screen) =>
      !isAdmin(screen) &&
      !isSetup(screen) &&
      !(screen.group !== undefined && grouped.has(screen.group))
  );

  return (
    <>
      <Block labelKey="app.nav.screens" entries={main} />

      {groups.map((group) => (
        <Block
          key={group.id}
          labelKey={group.labelKey}
          entries={mine.filter((screen) => screen.group === group.id)}
        />
      ))}

      <Block labelKey="app.nav.administration" entries={mine.filter(isAdmin)} />
      <Block labelKey="app.nav.setupGroup" entries={mine.filter(isSetup)} />
    </>
  );
}
