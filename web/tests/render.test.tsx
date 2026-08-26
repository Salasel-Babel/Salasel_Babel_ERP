/* ═══════════════════════════════════════════════════════════════════════════
   الرسم: القيمة المنسّقة تصل إلى الشاشة بالمصرف الوحيد، ولا تصير نصّاً في JSX
   ═══════════════════════════════════════════════════════════════════════════ */
import { createRef } from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { TrialBalanceTable } from "../src/screens/trial-balance/TrialBalanceTable";
import { decodeSchema } from "../src/api/transport";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import type { TrialBalance } from "../src/api/generated/types";
import { Amount, LocaleProvider, Num, useT } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { Money } from "../src/api/money";
import { ProblemPanel } from "../src/app/shell/ProblemPanel";
import { ProblemError } from "../src/api/transport";
import { buildTrialBalance, problem } from "../scripts/mock-api.mjs";

function Wrap(props: { children: React.ReactNode; locale?: string }) {
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      {props.children}
    </LocaleProvider>
  );
}

describe("<Amount>", () => {
  it("يعرض المبلغ بلغة الواجهة ويُبقي نصّ السلك في السمة", () => {
    const money = Money.wire("1000000000000.4013");
    const { container } = render(
      <Wrap>
        <Amount value={money} />
      </Wrap>
    );
    const span = container.querySelector("span.amt");
    expect(span).not.toBeNull();
    expect(span?.textContent).toBe("1,000,000,000,000.40");
    /* الأصل باقٍ ولم يُستبدَل: التقريب عرضٌ مُعلَن. */
    expect(span?.getAttribute("title")).toBe("1000000000000.4013");
    expect(span?.getAttribute("dir")).toBe("ltr");
  });

  it("يميّز الصفر والسالب بلا حساب", () => {
    const { container } = render(
      <Wrap>
        <Amount value={Money.wire("0.0000")} />
        <Amount value={Money.wire("-12.5000")} />
      </Wrap>
    );
    const spans = container.querySelectorAll("span.amt");
    expect(spans[0]?.className).toContain("amt-zero");
    expect(spans[1]?.className).toContain("amt-neg");
  });

  it("لا يمكن وضع قيمة معروضة في JSX كنصّ — ترمي قبل أن تصل", () => {
    const i18n = createI18n();
    i18n.use("ar");
    const display = i18n.amount("10");
    /* هذا هو الاستعمال الخاطئ الذي يمنعه النوع بالسلوك لا بالتعليق. */
    expect(() => render(<Wrap>{"" + (display as unknown as string)}</Wrap>)).toThrow(TypeError);
  });

  it("الأرقام تتبع اللغة والقيمة الآلية لا تتبعها", () => {
    const money = Money.wire("1234567.8900");
    const ar = render(
      <Wrap locale="ar">
        <Amount value={money} />
      </Wrap>
    );
    const arText = ar.container.querySelector("span.amt")?.textContent;
    ar.unmount();
    const hi = render(
      <Wrap locale="hi">
        <Amount value={money} />
      </Wrap>
    );
    const hiTitle = hi.container.querySelector("span.amt")?.getAttribute("title");
    expect(arText).toBe("1,234,567.89");
    expect(hiTitle).toBe("1234567.8900");
  });
});

describe("<Num>", () => {
  it("يعرض العدد بأرقام اللغة", () => {
    const { container } = render(
      <Wrap>
        <Num value={12345} />
      </Wrap>
    );
    expect(container.querySelector("span")?.textContent).toBe("12,345");
  });
});

describe("سطح الخطأ", () => {
  it("يعرض الرمز والرسالتين وكل الأخطاء ومعرّف التتبّع", () => {
    const error = ProblemError.from({
      ok: false,
      status: 403,
      json: problem(403, "auth.company_out_of_scope", "/api/v1/companies/x/trial-balance"),
      url: "/x",
    });
    render(
      <Wrap>
        <ProblemPanel error={error} />
      </Wrap>
    );
    expect(screen.getByTestId("problem-code").textContent).toBe("auth.company_out_of_scope");
    expect(screen.getByTestId("problem-trace").textContent).not.toBe("");
    expect(screen.getByTestId("problem-panel").textContent).toContain("الاعتماد لا يبلغ هذه الشركة");
    expect(screen.getByTestId("problem-panel").textContent).toContain(
      "The credential does not reach this company"
    );
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
  });

  it("لا يبتلع استجابة لا تنطق العقد", () => {
    const error = ProblemError.from({ ok: false, status: 502, json: "<html>", url: "/x" });
    const { container } = render(
      <Wrap>
        <ProblemPanel error={error} />
      </Wrap>
    );
    expect(container.querySelector('[data-testid="problem-code"]')?.textContent).toBe("http.502");
    /* ويقول صراحةً إن الخادم لم ينطق العقد بدل أن يعرض فراغاً. */
    expect(container.textContent).toContain("لم يستجب الخادم بصيغة المشكلة المنشورة");
  });
});

describe("عدد الترجمات المتاحة", () => {
  it("كل نصّ في السطح يمرّ بمفتاح — لا نصّ مطبوع", () => {
    function Probe() {
      const { t } = useT();
      return <span data-testid="probe">{t("screen.trialBalance.title")}</span>;
    }
    render(
      <Wrap locale="ur">
        <Probe />
      </Wrap>
    );
    const text = screen.getByTestId("probe").textContent ?? "";
    expect(text.length).toBeGreaterThan(0);
    expect(text).not.toBe("screen.trialBalance.title");
  });
});

/* ═══════════════════════════════════════════════════════════════════════════
   الاسم في ميزان المراجعة: سجلٌّ عربي، وترجمة **لغة الواجهة** لا الإنجليزية
   ───────────────────────────────────────────────────────────────────────────
   ADR-0021 بند 2: «قابلية الترجمة إلى أيّ عدد من اللغات». وكان العقد يحمل
   nameAr و nameEn فحسب، فالمحاسب الأردي يرى ترجمةً إنجليزية لا ترجمةً بلغته.
   ═══════════════════════════════════════════════════════════════════════════ */
describe("<TrialBalanceTable> — الاسم والترجمة", () => {
  const decode = (rowCount: number) =>
    decodeSchema(SCHEMAS, "TrialBalance", buildTrialBalance(rowCount, "MAIN", "2026-05")) as TrialBalance;

  function Table(props: { locale: string; rowCount?: number }) {
    const ref = createRef<HTMLInputElement>();
    return (
      <Wrap locale={props.locale}>
        <TrialBalanceTable
          data={decode(props.rowCount ?? 12)}
          query=""
          view="all"
          onView={() => {}}
          searchRef={ref}
        />
      </Wrap>
    );
  }

  it("السجلّ العربي يُعرَض في كل لغة — فهو السجلّ لا اللغة المفضَّلة", () => {
    for (const locale of ["ar", "en", "ur", "hi"]) {
      const { container, unmount } = render(<Table locale={locale} />);
      const record = container.querySelector('td span[lang="ar"][dir="rtl"]');
      expect(record?.textContent, "لغة الواجهة " + locale).toBe("الصندوق الرئيسي 1");
      unmount();
    }
  });

  it("الترجمة المعروضة هي ترجمة لغة الواجهة نفسها، لا الإنجليزية دائماً", () => {
    const expected: Record<string, string> = {
      en: "Main cash box 1",
      ur: "مرکزی نقدی صندوق 1",
      hi: "मुख्य नकद पेटी 1",
    };

    for (const [locale, text] of Object.entries(expected)) {
      const { container, unmount } = render(<Table locale={locale} />);
      const alt = container.querySelector("td span.alt");
      expect(alt?.getAttribute("lang"), "لغة الواجهة " + locale).toBe(locale);
      expect(alt?.textContent).toBe(text);
      unmount();
    }
  });

  it("اتجاه الترجمة من فهرس اللغات: الأردية rtl والهندية ltr بلا سطر لكل لغة", () => {
    const ur = render(<Table locale="ur" />);
    expect(ur.container.querySelector("td span.alt")?.getAttribute("dir")).toBe("rtl");
    ur.unmount();

    const hi = render(<Table locale="hi" />);
    expect(hi.container.querySelector("td span.alt")?.getAttribute("dir")).toBe("ltr");
    hi.unmount();
  });

  it("صفٌّ بلا ترجمة بلغة الواجهة يُظهر سجلّه وحده، لا سجلّه مكرَّراً تحت نفسه", () => {
    /* الصفّ ٥ («الأصول الثابتة — التكلفة») له en وحدها، فتحت ur لا ترجمة له.
       والارتداد الصامت المكرَّر هو العطل: عنوانٌ يبدو ترجمةً وهو السجلّ نفسه. */
    const { container } = render(<Table locale="ur" />);
    const cells = [...container.querySelectorAll("tbody tr")];
    const fixedAssets = cells.find((row) =>
      row.querySelector('span[lang="ar"]')?.textContent?.startsWith("الأصول الثابتة")
    );

    expect(fixedAssets, "صفّ الأصول الثابتة موجود").toBeDefined();
    expect(fixedAssets?.querySelector("span.alt")).toBeNull();
  });

  it("المحاسب الهندي على صفٍّ ترجمتُه الوحيدة إنجليزية يرى العربية — لا الإنجليزية", () => {
    /* هذا هو الشرط الذي حُذف nameEn لأجله. الصفّ ٥ («الأصول الثابتة — التكلفة»)
       يحمل en وحدها ولا يحمل hi. والارتداد الصحيح هو **السجلّ العربي**، لا
       «اللغة الأخرى» — فالإنجليزية واحدة من N لا افتراضٌ عند غياب الهندية.
       والفحص على نصّ الخليّة كلّه: لو تسرّبت الإنجليزية من أي طريق لظهرت هنا. */
    const { container } = render(<Table locale="hi" />);
    const row = [...container.querySelectorAll("tbody tr")].find((r) =>
      r.querySelector('span[lang="ar"]')?.textContent?.startsWith("الأصول الثابتة")
    );

    expect(row, "صفّ الأصول الثابتة موجود").toBeDefined();
    const cell = row?.querySelector("td.name");
    expect(cell?.querySelector("span.alt")).toBeNull();
    expect(cell?.textContent).toContain("الأصول الثابتة");
    expect(cell?.textContent).not.toContain("Fixed assets");
    expect(cell?.textContent?.toLowerCase()).not.toMatch(/[a-z]/);
  });

  it("الصفّ الذي لا ترجمة له إطلاقاً يبقى مقروءاً — الارتداد إلى السجلّ لا إلى الفراغ", () => {
    const { container } = render(<Table locale="hi" />);
    const rows = [...container.querySelectorAll("tbody tr")];
    const vat = rows.find((row) =>
      row.querySelector('span[lang="ar"]')?.textContent?.startsWith("ضريبة القيمة المضافة")
    );

    expect(vat, "صفّ الضريبة موجود").toBeDefined();
    expect(vat?.querySelector('span[lang="ar"]')?.textContent).toContain("ضريبة القيمة المضافة");
    expect(vat?.querySelector("span.alt")).toBeNull();
  });

  it("البحث يجد الاسم بأي لغة من لغاته، لا بالعربية والإنجليزية وحدهما", () => {
    const ref = createRef<HTMLInputElement>();
    const { container } = render(
      <Wrap locale="ar">
        <TrialBalanceTable
          data={decode(12)}
          query="مرکزی نقدی"
          view="all"
          onView={() => {}}
          searchRef={ref}
        />
      </Wrap>
    );

    const rows = container.querySelectorAll("tbody tr");
    expect(rows).toHaveLength(1);
    expect(rows[0]?.querySelector('span[lang="ar"]')?.textContent).toBe("الصندوق الرئيسي 1");
  });
});
