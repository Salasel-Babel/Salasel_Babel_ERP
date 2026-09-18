/* ═══════════════════════════════════════════════════════════════════════════
   شجرةُ النظام المفتوح  ·  The open system's tree
   ───────────────────────────────────────────────────────────────────────────
   **العطلان اللذان يُصلحهما هذا الملفّ كانا يجعلان النظام يبدو جرداً لا
   منتَجاً — وكلاهما ملاحظةُ المالك على الشاشة الحيّة:**

   ١ · كانت القائمة تسرد **الشاشات التسع والخمسين كلَّها دفعةً واحدة**،
       مكتوبةً بيد في `App.tsx`. فأُغلق ذلك بترشيح القسم، وهذا الملفّ هو
       الذي يُرشّح.

   ٢ · ثم بقيت — بعد الترشيح — **قائمةً مسطّحة من ثلاثين رابطاً** في
       المحاسبي. وقائمةٌ مسطّحة بهذا الطول تُقرأ كوماً لا كنظام: لا تقول
       أين تبدأ دورةُ المبيعات ولا أين تنتهي، ولا تفصل ما يُؤسَّس مرّةً
       عمّا يُكتب كلَّ يوم. **فصارت شجرةً من طبقتين**: عقدةٌ تحمل اسم
       المجموعة ورمزَها، وتحتها شاشاتُها — وهو ما طلبه المالك حرفاً.

   **والطبقتان طبقتان بالضبط، لا ثلاث.** شجرةٌ أعمق تُخفي الشاشةَ خلف
   نقرتين وتجعل «أين هي؟» سؤالاً — وهو العطل الذي جاءت الشجرةُ تُصلحه.

   **والمفتوحُ افتراضياً مجموعةُ الشاشة القائمة وحدها**، فمن يفتح «الرواتب»
   يرى شاشاتِها مفتوحةً وبقيّةَ الشجرة مطويّة. ويبقى ما طواه المستخدم
   مطويّاً وما فتحه مفتوحاً — اختيارُه يعلو على الافتراض.

   **ولا شاشةَ تسقط من العرض:** اتّحادُ ما تعرضه الأنظمة الخمسة يساوي
   `SCREENS` بالضبط، ويحرسه `tests/shell-nav.test.tsx` — وهو يعدّ المطويَّ
   أيضاً، لأن المطويَّ مُعلَنٌ خلف نقرةٍ والغائبَ غير موجود.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useId, useState, type CSSProperties, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { Icon } from "./icons";
import {
  SCREENS,
  SCREEN_GROUPS,
  SECTIONS,
  type GroupId,
  type ScreenEntry,
  type Section,
} from "./sections";

/**
 * رمزُ اختبارٍ من المسار: `/admin/plans` ← `nav-admin-plans`.
 * <p>
 * والجذرُ `/` يصير `nav-root` لا `nav-home`: صفحةُ البداية على `/home`،
 * وقاعدةُ الاشتقاق كانت تعطيها الرمزَ نفسه — <b>رمزان متطابقان لشاشتين</b>،
 * فيمسك الاختبارُ أوّلَهما ويظنّ أنه يقيس الثانية. وهو عطلٌ لا يُحمِّر شيئاً
 * حتى يقيس أحدُهم الخطأ وهو مطمئنّ.
 * </p>
 */
function testId(path: string): string {
  return "nav" + (path === "/" ? "-root" : path.replace(/\//g, "-"));
}

/** ورقةٌ في الشجرة — شاشةٌ تُفتح. */
function Leaf(props: { entry: ScreenEntry; depth: 1 | 2 }): ReactNode {
  const { t } = useT();
  /* `to` مُضيَّق إلى نوع المسار: الموجّه يعرف المسارات المسجَّلة، وهذه منها
     بحكم الحارس الذي يقارن `SCREENS` بالموجّه. */
  const to = props.entry.path as "/";
  return (
    <Link
      to={to}
      className={props.depth === 1 ? "navitem" : "subitem"}
      data-testid={testId(props.entry.path)}
      /* **«أين أنت» ضُبط على الشاشة لا على الورق، وكلا طرفيه كان يكسر:**
         · الافتراضُ يُطابق **بالبادئة**، فيلمع `/hr` وأنت في `/hr/payroll`
           فيلمع بندان ولا يعرف الناظر أيّهما هو.
         · و`exact` وحدها تُطابق **معاملات البحث** أيضاً، فـ`?lang=ar` في
           العنوان تُسقط التوسيم كلَّه ولا يلمع شيء.
         فالاثنان معاً: مسارٌ بالضبط، وبحثٌ لا يُحسَب. (وكلاهما قِيس بقراءة
         `aria-current` من المستند المبنيّ، لا بقراءة التوثيق.) */
      activeOptions={{ exact: true, includeSearch: false }}
      title={t(props.entry.labelKey)}
    >
      <Icon name={props.entry.icon} size={props.depth === 1 ? 18 : 16} />
      <span className="navitem__name">{t(props.entry.labelKey)}</span>
    </Link>
  );
}

/** عقدةٌ من الطبقة الأولى: اسمٌ ورمزٌ، وتحتها شاشاتُها. */
function Branch(props: {
  labelKey: string;
  icon: ScreenEntry["icon"];
  entries: readonly ScreenEntry[];
  current: string;
  open: boolean;
  onToggle: () => void;
}): ReactNode {
  const { t } = useT();
  const listId = useId();
  /* **العقدةُ التي تحوي الشاشة القائمة موسومةٌ ولو كانت مطويّة** — فيعرف من
     طواها أين هو، ولا يبدو أنه خارج الشجرة كلّها. */
  const holdsCurrent = props.entries.some((entry) => entry.path === props.current);
  return (
    <>
      <button
        type="button"
        className="navitem navitem--branch"
        aria-expanded={props.open}
        aria-controls={listId}
        data-holds-current={holdsCurrent ? "true" : undefined}
        data-testid={"navgroup-" + props.labelKey}
        /* الاسمُ كاملاً عند الوقوف: العمودُ ٢١٦ بكسلاً، وأسماءُ المجموعات
           بالإنجليزية والهندية تبلغ ضعفَ العربية فتُبتَر. والبترُ مقبولٌ
           والاسمُ الكامل على بُعد وقفة — وحذفُ المجموعة ليس مقبولاً. */
        title={t(props.labelKey)}
        onClick={props.onToggle}
      >
        <Icon name={props.icon} />
        <span className="navitem__name">{t(props.labelKey)}</span>
        <span className="caret" aria-hidden="true">
          <Icon name="chevron" size={15} />
        </span>
      </button>
      <div className="subnav" id={listId} role="group" hidden={!props.open}>
        {props.entries.map((entry) => (
          <Leaf key={entry.path} entry={entry} depth={2} />
        ))}
      </div>
    </>
  );
}

/** شاراتُ النظام المفتوح: رمزُه واسمُه بلونه — «أين أنا الآن». */
function SystemBadge(props: { section: Section["id"] }): ReactNode {
  const { t } = useT();
  const system = SECTIONS.find((s) => s.id === props.section);
  if (!system) return null;
  const tint = { "--section-tint": system.tint } as CSSProperties;
  return (
    <p className="sysbadge" style={tint} data-testid="system-badge">
      <Icon name={system.icon} size={20} />
      <span>{t(system.labelKey)}</span>
    </p>
  );
}

/**
 * شجرةُ النظام المفتوح، بترتيب `SCREENS` — وهو ترتيبُ العمل.
 * @param props معرّفُ النظام المفتوح، والمسارُ القائم لتوسيم عقدته.
 */
export function ScreenNav(props: { section: Section["id"]; path: string }): ReactNode {
  /* `{}` تعني «لم يلمس المستخدم شيئاً بعد»، فيقرّر المسارُ وحده ما ينفتح. */
  const [touched, setTouched] = useState<Partial<Record<GroupId, boolean>>>({});

  const mine = SCREENS.filter((screen) => screen.section === props.section && !screen.universal);
  const universal = SCREENS.filter((screen) => screen.universal === true);
  const groups = SCREEN_GROUPS.filter((group) => group.section === props.section);
  const leaves = mine.filter((screen) => screen.group === undefined);
  const currentGroup = SCREENS.find((screen) => screen.path === props.path)?.group;

  return (
    <div className="screentree" data-testid="screen-tree">
      <SystemBadge section={props.section} />

      {universal.map((entry) => (
        <Leaf key={entry.path} entry={entry} depth={1} />
      ))}

      {leaves.map((entry) => (
        <Leaf key={entry.path} entry={entry} depth={1} />
      ))}

      {groups.map((group) => (
        <Branch
          key={group.id}
          labelKey={group.labelKey}
          icon={group.icon}
          entries={mine.filter((screen) => screen.group === group.id)}
          current={props.path}
          open={touched[group.id] ?? currentGroup === group.id}
          onToggle={() =>
            setTouched((was) => ({
              ...was,
              [group.id]: !(was[group.id] ?? currentGroup === group.id),
            }))
          }
        />
      ))}
    </div>
  );
}
