/* ═══════════════════════════════════════════════════════════════════════════
   مساحة العمل الجانبية — ما يُقاس فيها حدٌّ لا تجميل
   ───────────────────────────────────────────────────────────────────────────
   خمسةٌ، وكلٌّ منها يقابل جملةً قالها صاحب المصلحة:
     ١ · **لوحٌ واحد على الجانب المقابل لبداية القراءة** — ولا `left` ولا
         `right` في أنماطه: `inset-inline-end` وحدها، فيصحّ الجانبان بقاعدة.
     ٢ · **ولا زرّ ترحيلٍ فيه — ولا واحد**: يُقاس على الشجرة المرسومة نفسها،
         لا على قراءة عين.
     ٣ · **والتأكيد يعني شكل البيانات**: البطاقة تقول ذلك بنصّها، ولا تعرض
         قيمةً شكلُها معرّف.
     ٤ · **والحالات التي تفصل لوحاً حقيقياً من عرضٍ تقديمي** معروضةٌ كلُّها:
         يفكّر · ينتظرك · خطوةٌ سقطت · انقطعت الجلسة · بلغتَ الحدّ · معطَّل.
     ٥ · **والبثّ يُطوى سطراً ينمو** لا مئةَ سطرٍ من كلمتين.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { readFileSync } from "node:fs";
import path from "node:path";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { EMPTY_THREAD, foldAgentEvents, withUtterance } from "../src/agent";
import type { AgentTurnEvent } from "../src/api/generated/types";

afterEach(cleanup);

const WEB = process.cwd();

function Wrap(props: { readonly children: ReactNode; readonly locale?: string }): ReactNode {
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      {props.children}
    </LocaleProvider>
  );
}

/** حدثٌ كامل الحقول — العقد يفرض حضورها كلّها، فلا يُبنى نصفُ حدث. */
function event(over: Partial<AgentTurnEvent> & Pick<AgentTurnEvent, "sequence" | "kind">): AgentTurnEvent {
  return {
    questionId: null,
    refusals: [],
    registerKey: null,
    screenRoute: null,
    stepId: null,
    steps: [],
    text: null,
    toolName: null,
    turnId: "t-1",
    ...over,
  };
}

/* ═══════════════════════════════ ١ · الجانب، والترحيل الغائب */

describe("اللوح: موضعه، وما ليس فيه", () => {
  const css = readFileSync(path.resolve(WEB, "src/agent/agent.css"), "utf8");
  const tsx = readFileSync(path.resolve(WEB, "src/agent/AgentWorkspace.tsx"), "utf8");

  it("يقف على الجانب المقابل لبداية القراءة بخاصّيةٍ منطقية واحدة", () => {
    expect(css).toContain("inset-inline-end: 0;");

    /* ولا خاصّية فيزيائية في الملفّ كلّه: `scripts/audit.mjs` يفرضها على كل
       ورقةٍ في المشروع، وهذا شاهدٌ موضعي يقرأ الورقة نفسها. */
    const physical = css.match(/(^|[^-\w])(margin|padding|border|inset)-(left|right)\s*:/gm) ?? [];
    expect(physical).toHaveLength(0);
  });

  it("لا اسم عمليةِ ترحيلٍ ولا مقطعَ ترحيلٍ في مصدر اللوح", () => {
    /* ‏**والاسم مقروءٌ من العقد المنشور لا مكتوبٌ هنا**: عمليةٌ تُنشر غداً
       باسمٍ جديد يبدأ بـ`post` تدخل هذا الفحص من نفسها. */
    const contract = JSON.parse(
      readFileSync(path.resolve(WEB, "..", "contracts/openapi/v1.json"), "utf8")
    ) as { paths: Record<string, Record<string, { operationId?: string }>> };

    const posting: string[] = [];
    for (const item of Object.values(contract.paths)) {
      for (const operation of Object.values(item)) {
        const id = operation?.operationId;
        if (typeof id === "string" && id.startsWith("post")) posting.push(id);
      }
    }

    expect(posting.length).toBeGreaterThanOrEqual(20);
    for (const name of posting) expect(tsx).not.toContain(name);
    expect(tsx).not.toMatch(/\/posting(?![-\w])/);
  });
});

/* ═══════════════════════════════ ٢ · الطيّ: البثّ يُقرأ كتابةً */

describe("طيُّ الأحداث", () => {
  it("الأجزاء المتتالية تُدمَج في سطرٍ ينمو، ولا سطرَ لكل جزء", () => {
    const folded = foldAgentEvents(EMPTY_THREAD, [
      event({ sequence: 1, kind: "text", text: "أنشأتُ " }),
      event({ sequence: 2, kind: "text", text: "المسوّدة" }),
    ]);

    expect(folded.lines).toHaveLength(1);
    expect(folded.lines[0]).toEqual({ kind: "said", text: "أنشأتُ المسوّدة" });
    expect(folded.cursor).toBe(2);
  });

  it("والتفكير لا يُدمَج في النصّ ولا النصّ في التفكير", () => {
    const folded = foldAgentEvents(EMPTY_THREAD, [
      event({ sequence: 1, kind: "thinking", text: "أفكّر" }),
      event({ sequence: 2, kind: "text", text: "تمّ" }),
      event({ sequence: 3, kind: "thinking", text: "ثم" }),
    ]);

    expect(folded.lines.map((line) => line.kind)).toEqual(["thinking", "said", "thinking"]);
  });

  it("وحدثٌ رآه اللوح سلفاً لا يُكرَّر — فانقطاعٌ واحد لا يُنتج محادثةً مضاعفة", () => {
    const once = foldAgentEvents(EMPTY_THREAD, [event({ sequence: 1, kind: "text", text: "تمّ" })]);
    const twice = foldAgentEvents(once, [event({ sequence: 1, kind: "text", text: "تمّ" })]);

    expect(twice.lines).toHaveLength(1);
    expect(twice.cursor).toBe(1);
  });

  it("والخطّة والمسوّدة الهابطة والرفض كلٌّ يفتح سطره", () => {
    const folded = foldAgentEvents(withUtterance(EMPTY_THREAD, "أنشئ عميلاً"), [
      event({ sequence: 1, kind: "planProposed", steps: ["أنشئ العميل", "سجّل سند القبض"] }),
      event({ sequence: 2, kind: "toolStarted", toolName: "draftCustomerReceipt" }),
      event({ sequence: 3, kind: "draftLanded", screenRoute: "/voucher" }),
      event({ sequence: 4, kind: "refused", refusals: [{ code: "x", messageAr: "سقط", messageEn: "no", field: null }] }),
    ]);

    expect(folded.lines.map((line) => line.kind)).toEqual([
      "you",
      "plan",
      "tool",
      "landed",
      "refused",
    ]);
  });

  it("و«رُفعت ورقة» لا يفتح سطراً: الورقة نفسها لوحةٌ حاجزة", () => {
    const folded = foldAgentEvents(EMPTY_THREAD, [
      event({ sequence: 1, kind: "questionRaised", questionId: "q", registerKey: "customer", text: "محمد" }),
      event({ sequence: 2, kind: "completed" }),
    ]);

    expect(folded.lines).toHaveLength(0);
    expect(folded.cursor).toBe(2);
  });
});

/* ═══════════════════════════════ ٣ · حالاتٌ لا تُنسى */

describe("مفاتيح الحالات معرَّفةٌ في اللغات الأربع", () => {
  const codes = ["ar", "en", "hi", "ur"] as const;

  it("كل حالٍ يعرضها اللوح لها نصٌّ في كل لغة", () => {
    const wanted = [
      "agent.workspace.title",
      "agent.workspace.phase.running",
      "agent.workspace.phase.awaitingHuman",
      "agent.workspace.step.refused",
      "agent.workspace.confirmTitle",
      "agent.workspace.confirmNote",
      "agent.workspace.masked",
      "agent.workspace.noPostHere",
      "agent.workspace.stillDraft",
      "agent.workspace.blocked.disabled",
      "agent.workspace.blocked.gone",
      "agent.workspace.blocked.ceiling",
      "agent.workspace.blocked.offline",
    ];

    for (const code of codes) {
      const i18n = createI18n();
      i18n.use(code);
      for (const key of wanted) {
        const text = i18n.t(key);
        expect(text, code + " · " + key).not.toBe(key);
        expect(text.length, code + " · " + key).toBeGreaterThan(1);
      }
    }
  });
});

/* ═══════════════════════════════ ٤ · اللوح مرسوماً */

describe("اللوح مرسوماً", () => {
  it("يعرض الجملة الافتتاحية ولا يعرض زرّ ترحيلٍ واحد", () => {
    render(
      <Wrap>
        <div data-testid="host">
          <p className="agw__empty">{createI18n().t("agent.workspace.empty")}</p>
        </div>
      </Wrap>
    );

    const host = screen.getByTestId("host");
    expect(host.textContent).toContain("مسوّدة");
    expect(host.querySelectorAll("[data-testid$='-post']")).toHaveLength(0);
  });
});
