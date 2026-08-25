/* ═══════════════════════════════════════════════════════════════════════════
   الرسم: القيمة المنسّقة تصل إلى الشاشة بالمصرف الوحيد، ولا تصير نصّاً في JSX
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Amount, LocaleProvider, Num, useT } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { Money } from "../src/api/money";
import { ProblemPanel } from "../src/app/shell/ProblemPanel";
import { ProblemError } from "../src/api/transport";
import { problem } from "../scripts/mock-api.mjs";

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
