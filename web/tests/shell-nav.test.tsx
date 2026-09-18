/* ═══════════════════════════════════════════════════════════════════════════
   القائمة الجانبية تحمل كل شاشة — ولا شاشةَ تُفتح بلوحة الأوامر وحدها
   ───────────────────────────────────────────────────────────────────────────
   **العطل الذي يمنعه هذا الملفّ وقع فعلاً أكثر من مرّة، وهو عطلٌ صامت:**
   قائمةُ الملاحة في `App.tsx` مكتوبةٌ بيد — سلسلةُ `<Link>` مرتَّبةٌ بترتيب
   العمل — بينما لوحةُ الأوامر ومصفوفةُ الحراسة تُبنيان من `SCREENS` في
   `shell/sections.ts`. فمن يضيف شاشةً في أحد الموضعين وينسى الآخر لا يكسر
   بناءً ولا يُحمّر اختباراً: الشاشةُ تعمل، ومسارُها مسجَّل، وCtrl+K يفتحها —
   **ولا يراها من يقرأ الملاحة**. وذلك أسوأ من رابطٍ مكسور لأن أحداً لا يشتكي
   منه: من لا يعلم بوجود الشاشة لا يبلّغ عن غيابها.

   وقد وقع آخرُ مرّة عند إضافة `/purchasing/payables`: دخلت `SCREENS` والموجّه،
   وغابت عن القائمة. فصار هذا الحارس يقارن **ما يُعرَض في `.app-side` فعلاً بعد
   الرسم** بما تعلنه `SCREENS` — لا نصَّ الملفّ بنصّه، فقارئُ النصّ يمرّ على
   رابطٍ معلَّقٍ خلف شرط.

   **ومنذ صارت القائمة شجرةً: المطويُّ محسوبٌ حاضراً.** عقدةٌ مطويّة تُبقي
   روابطَها في المستند وتُخفيها بـ`hidden`، فيراها `querySelectorAll` —
   وذلك مقصود: المطويُّ **مُعلَنٌ خلف نقرة**، والغائبُ غير موجود. والفرق
   بينهما هو بالضبط العطلُ الذي يُقاس هنا. ويحرس الطيَّ نفسَه اختبارٌ آخر
   أسفل: العقدةُ تنفتح بالنقر، وما تحتها يظهر.

   **ولماذا الطرفان معاً لا طرفٌ واحد:** غيابُ شاشةٍ من القائمة يُخفيها،
   ورابطٌ في القائمة بلا صفٍّ في `SCREENS` يصنع شاشةً لا تعرف قسمَها ولا لونَه
   ولا يجدها بحثُ لوحة الأوامر. فكلاهما عطل، وكلاهما يُقاس هنا.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS, SCREEN_GROUPS, SECTIONS } from "../src/app/shell/sections";
import type { RawResponse, Transport } from "../src/api/transport";

/** ناقلٌ صامت: هذا الحارس يقيس الملاحة لا البيانات، فيردّ 503 على كل باب. */
const silent: Transport = ({ url }) =>
  Promise.resolve<RawResponse>({ ok: false, status: 503, json: null, url });

/** يُمهل الدورةَ التالية: نقرةٌ تُبدّل حالةً أو تُبحر لا تكتمل في اللحظة نفسها. */
function settled(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

async function navHrefs(initialPath = "/"): Promise<string[]> {
  const router = createAppRouter({ memory: true, initialPath });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={silent}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
  }
  await act(async () => {
    render(<Tree />);
    await router.load();
  });
  const nav = document.querySelector(".app-side");
  expect(nav, "لا قائمةَ جانبية في القشرة").not.toBeNull();
  return [...(nav?.querySelectorAll("a[href]") ?? [])]
    .map((a) => a.getAttribute("href") ?? "")
    .filter((href) => href.startsWith("/"));
}

describe("القائمة الجانبية و SCREENS لا تنحرف إحداهما عن الأخرى", () => {
  it("شاهدٌ إيجابي: الحارس يرى روابطَ الملاحة أصلاً", async () => {
    const hrefs = await navHrefs();
    /* بلا هذا السطر يمرّ الحارسُ على قائمةٍ فارغة فيصير أخضرَ وهو أعمى. */
    expect(hrefs.length).toBeGreaterThan(5);
  });

  /* ‏**القائمة تُرشَّح بالقسم المفتوح** (ScreenNav)، فلا تُقاس على مسارٍ واحد:
     الشاشةُ الغائبة عن قسمها لا تُرى في أي مكان، والقياسُ الصحيح هو اتّحادُ
     ما تعرضه الأقسام الخمسة. */
  async function unionAcrossSections(): Promise<Set<string>> {
    const seen = new Set<string>();
    for (const section of SECTIONS) {
      if (!section.built || !section.path) continue;
      for (const href of await navHrefs(section.path)) seen.add(href);
      cleanup();
    }
    return seen;
  }

  it("كلُّ شاشةٍ في SCREENS تُرى في ملاحة قسمها", async () => {
    const shown = await unionAcrossSections();
    const hidden = SCREENS.map((s) => s.path).filter((p) => !shown.has(p));
    expect(
      hidden,
      "شاشاتٌ تُفتح بـCtrl+K ولا يراها من يقرأ الملاحة: " + hidden.join(" · ")
    ).toEqual([]);
  });

  it("وكلُّ رابطٍ معروضٍ له صفٌّ في SCREENS — فيعرف قسمَه ولونَه", async () => {
    const declared = new Set<string>([
      ...SCREENS.map((s) => s.path),
      ...SECTIONS.flatMap((s) => (s.path ? [s.path] : [])),
    ]);
    const stray = [...(await unionAcrossSections())].filter((h) => !declared.has(h));
    expect(
      stray,
      "روابطُ ملاحةٍ بلا صفٍّ في SCREENS — لا قسمَ لها ولا يجدها البحث: " + stray.join(" · ")
    ).toEqual([]);
  });

  /* ‏**وهذا هو الحارسُ الجديد**: القسمُ يفتح شاشاتِه لا شاشاتِ غيره. وبلا هذا
     يمرّ ترشيحٌ معطوب يعرض كلَّ شيء في كل قسم — وهو العطل الذي كان قائماً. */
  it("ولا يعرض القسمُ شاشةَ قسمٍ آخر", async () => {
    for (const section of SECTIONS) {
      if (!section.built || !section.path) continue;
      const hrefs = await navHrefs(section.path);
      const byPath = new Map(SCREENS.map((s) => [s.path, s.section]));
      /* ‏**ومداخلُ مبدّل الأقسام مستثناة**: كلُّ قسمٍ مبنيّ يُعرَض رابطاً في
         المبدّل دائماً، ومساره شاشةٌ من شاشاته — فهو ظاهرٌ بحقّ لا تسرّب.
         والمقيسُ هنا قائمةُ الشاشات وحدها. */
      const switcher = new Set(SECTIONS.flatMap((s) => (s.path ? [s.path] : [])));
      /* **وصفحةُ البداية كذلك**: هي `universal` في `SCREENS` — مدخلٌ إلى
         الأنظمة لا شاشةُ عملٍ في أحدها، فتُعرَض في شجرة كلٍّ منها بحقّ.
         والاستثناء **مقروءٌ من الراية لا مكتوبٌ بمسارٍ هنا**، فلو زالت
         الرايةُ يوماً عاد الحارسُ يمسك المسار. */
      for (const universal of SCREENS.filter((entry) => entry.universal === true)) {
        switcher.add(universal.path);
      }
      const alien = hrefs.filter(
        (h) => !switcher.has(h) && byPath.has(h) && byPath.get(h) !== section.id
      );
      expect(alien, "قسم «" + section.id + "» يعرض: " + alien.join(" · ")).toEqual([]);
      cleanup();
    }
  });

  /* **ولا رمزَ اختبارٍ مكرَّر في الشجرة.** وقع مقيساً: صفحةُ البداية على
     `/home`، وقاعدةُ اشتقاق الرمز كانت تعطي الجذرَ `/` الرمزَ `nav-home`
     نفسه — فيمسك `getByTestId` أوّلَهما ويظنّ من يقرأ الاختبار أنه يقيس
     الثانية. وهو عطلٌ **يُخضِّر** اختباراً كاذباً بدل أن يُحمّر. */
  it("ولا رمزَ اختبارٍ مكرَّر في الشجرة — فلا يقيس اختبارٌ غيرَ ما يظنّ", async () => {
    for (const section of SECTIONS) {
      if (!section.built || !section.path) continue;
      await navHrefs(section.path);
      const ids = [...document.querySelectorAll(".app-side [data-testid]")].map((el) =>
        el.getAttribute("data-testid")
      );
      const twice = ids.filter((id, i) => ids.indexOf(id) !== i);
      expect(twice, "رموزٌ مكرَّرة في «" + section.id + "»: " + twice.join(" · ")).toEqual([]);
      cleanup();
    }
  });

  /* **ولا مجموعةَ من شاشةٍ واحدة.** عقدةٌ تنفتح على ورقةٍ يتيمة تُكلّف نقرةً
     ولا تُعطي تصنيفاً — وهي أشيعُ ما يقع حين تُنقل شاشةٌ من مجموعةٍ إلى
     أخرى فتُترك جارتُها وحدها. والحدُّ اثنتان لأن المجموعة **تجميع**. */
  it("كلُّ مجموعةٍ تحمل شاشتين فأكثر، وكلُّ شاشةٍ في مجموعةٍ معلَنة", () => {
    const declared = new Set(SCREEN_GROUPS.map((g) => g.id));
    const thin = SCREEN_GROUPS.filter(
      (group) => SCREENS.filter((s) => s.group === group.id).length < 2
    ).map((g) => g.id);
    expect(thin, "مجموعاتٌ من شاشةٍ واحدة: " + thin.join(" · ")).toEqual([]);

    /* ومجموعةٌ تكتبها شاشةٌ ولا صفَّ لها تُسقِط الشاشةَ من الشجرة صامتةً. */
    const stray = SCREENS.filter(
      (screen) => screen.group !== undefined && !declared.has(screen.group)
    ).map((s) => s.path);
    expect(stray, "شاشاتٌ في مجموعةٍ بلا صفّ: " + stray.join(" · ")).toEqual([]);

    /* ومجموعةٌ وشاشتُها في قسمين مختلفين تجعل الشجرةَ تعرض غريباً. */
    const crossed = SCREENS.filter((screen) => {
      const group = SCREEN_GROUPS.find((g) => g.id === screen.group);
      return group !== undefined && group.section !== screen.section;
    }).map((s) => s.path);
    expect(crossed, "شاشاتٌ في مجموعةِ قسمٍ آخر: " + crossed.join(" · ")).toEqual([]);
  });

  /* **والطيُّ يُطوى ويُفتح فعلاً.** بلا هذا يمرّ عطلٌ يُبقي كلَّ عقدةٍ
     مطويّةً أبداً — وحينها تكون كلُّ الحراسات أعلاه خضراء والقائمةُ في
     الشاشة فارغةٌ إلا من العناوين. */
  it("عقدةُ الشجرة تنفتح بالنقر وتُظهر شاشاتِها", async () => {
    const router = createAppRouter({ memory: true, initialPath: "/inventory/stock" });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    await act(async () => {
      render(
        <LocaleProvider i18n={createI18n()} initial="ar">
          <QueryClientProvider client={client}>
            <ApiProvider transport={silent}>
              <RouterProvider router={router} />
            </ApiProvider>
          </QueryClientProvider>
        </LocaleProvider>
      );
      await router.load();
    });
    const branches = [...document.querySelectorAll(".app-side .navitem--branch")];
    expect(branches.length, "لا عقدَ في شجرة المخزني").toBeGreaterThan(1);

    /* المخزنيُّ مفتوحٌ على «الأرصدة» وهي ورقةٌ في الطبقة الأولى، فكلُّ عقده
       مطويّةٌ — وهو الشاهد الإيجابي لهذا الاختبار. */
    const first = branches[0] as HTMLButtonElement;
    expect(first.getAttribute("aria-expanded")).toBe("false");
    const listId = first.getAttribute("aria-controls") ?? "";
    const list = document.getElementById(listId);
    expect(list, "عقدةٌ لا تشير إلى قائمتها").not.toBeNull();
    expect(list?.hasAttribute("hidden")).toBe(true);

    await act(async () => {
      first.click();
      await settled();
    });
    expect(first.getAttribute("aria-expanded")).toBe("true");
    expect(document.getElementById(listId)?.hasAttribute("hidden")).toBe(false);
    expect(
      document.getElementById(listId)?.querySelectorAll("a[href]").length
    ).toBeGreaterThan(1);
  });
});
