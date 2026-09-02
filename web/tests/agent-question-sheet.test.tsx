/* ═══════════════════════════════════════════════════════════════════════════
   ورقة السؤال — الاسم يبقى في المتصفّح، ويعود إلى النموذج رمزٌ وحده
   ───────────────────────────────────────────────────────────────────────────
   ما يُقاس هنا أربعةٌ، وكلٌّ منها حدٌّ لا تجميل:
     ١ · **لا نصّ خيارٍ يعبر**: الجواب مفتاحان، وقيمتاهما من الورقة لا مشتقّتان.
     ٢ · **ولا العدد يُستدلّ**: ورقتان بخيارين وبخمسة تُخرجان جواباً واحد الشكل
         واحد الطول — فالطول نفسه لا يقول كم كانت.
     ٣ · **والمسار يكتمل بلوحة المفاتيح وحدها**، في الاتجاهين معاً: السهم الأيسر
         يتقدّم بالعربية ويتأخّر بالإنجليزية، والصفحة هي من يقول الاتجاه.
     ٤ · **و«جديد» تسأل عمّا يطلبه العقد المنشور بالضبط** — مقروءاً من
         `contracts/openapi/v1.json` نفسه وقت الاختبار، فانحرافُ العقد يُحمِّر
         بوّابةً لا نموذجاً على شاشة إنسان.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import type { I18n } from "../src/i18n/engine";
import { CONTRACT } from "../src/api/generated/contract";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import {
  AGENT_ANSWER_KEYS,
  AGENT_CREATE_OPERATIONS,
  AGENT_ENTITY_KINDS,
  AGENT_PERMITTED_VERBS,
  AGENT_TOKEN_GROUP_LENGTH,
  AGENT_TOKEN_GROUP_SEPARATOR,
  AGENT_TOKEN_LENGTH,
  AgentQuestionSheet,
  agentCreateFaults,
  agentSheetFaults,
  answerOf,
  planAgentCreateSheet,
  type AgentAnswer,
  type AgentCreateDraft,
  type AgentQuestionSheetData,
} from "../src/agent";

/* globals:false في vitest.config.ts، فلا تنظيف تلقائياً. */
afterEach(cleanup);

const REPO = path.resolve(process.cwd(), "..");

/* رموزٌ بطولٍ واحد وشكلٍ واحد — **كما يسكّها الخادم**: قاعدة 64 بلا حشو، مجموعاتٍ
   من ثمانية يفصلها `~`، وطولُها `AGENT_TOKEN_LENGTH` (وحارسٌ في الخادم يقرأ الملفّ
   ويطابق العدد بـ`SignedLookupHandles.TokenLength`). والطول الثابت جزءٌ من الحدّ:
   رمزٌ يطول بطول الاسم يقول شيئاً عن الاسم. */
const token = (seed: string): string => {
  const alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";
  const groups = (AGENT_TOKEN_LENGTH + 1) / (AGENT_TOKEN_GROUP_LENGTH + 1);
  const flat = (seed + "_").padEnd(groups * AGENT_TOKEN_GROUP_LENGTH, alphabet);
  const out: string[] = [];
  for (let at = 0; at < flat.length; at += AGENT_TOKEN_GROUP_LENGTH) {
    out.push(flat.slice(at, at + AGENT_TOKEN_GROUP_LENGTH));
  }
  return out.join(AGENT_TOKEN_GROUP_SEPARATOR);
};

const QUESTION = token("question");

const FOUR: AgentQuestionSheetData = {
  questionId: QUESTION,
  kind: "customer",
  subjectText: "محمد القحطاني",
  options: [
    { optionToken: token("one"), label: "محمد علي القحطاني", subtitle: "C-0001" },
    { optionToken: token("two"), label: "محمد أحمد القحطاني", subtitle: "C-0002" },
    { optionToken: token("three"), label: "محمد القحطاني", subtitle: "C-0003" },
    { optionToken: token("new"), label: "جديد" },
  ],
  allowsCreate: true,
};

const TWO: AgentQuestionSheetData = {
  ...FOUR,
  options: [FOUR.options[0]!, FOUR.options[3]!],
};

function Wrap(props: { readonly children: React.ReactNode; readonly locale?: string; readonly i18n?: I18n }) {
  return (
    <LocaleProvider i18n={props.i18n ?? createI18n()} initial={props.locale ?? "ar"}>
      {props.children}
    </LocaleProvider>
  );
}

interface Captured {
  readonly answers: AgentAnswer[];
  readonly drafts: AgentCreateDraft[];
  readonly dismissed: number[];
}

function open(sheet: AgentQuestionSheetData = FOUR, locale = "ar", i18n?: I18n): Captured {
  const answers: AgentAnswer[] = [];
  const drafts: AgentCreateDraft[] = [];
  const dismissed: number[] = [];
  render(
    <Wrap locale={locale} i18n={i18n}>
      <AgentQuestionSheet
        sheet={sheet}
        onAnswer={(a) => answers.push(a)}
        onCreate={(d) => drafts.push(d)}
        onDismiss={() => dismissed.push(1)}
      />
    </Wrap>
  );
  return { answers, drafts, dismissed };
}

/* ═════════════════════════════ ١ · ما يعبر إلى الوكيل ═══════════════════ */

describe("ورقة السؤال — لا اسم يعبر ولا عدد", () => {
  it("الجواب مفتاحان لا ثالث لهما، وقيمتاهما من الورقة لا مشتقّتان منها", () => {
    const answer = answerOf(FOUR, FOUR.options[1]!);
    expect(Object.keys(answer).sort()).toEqual([...AGENT_ANSWER_KEYS]);
    expect(answer.questionId).toBe(QUESTION);
    expect(answer.optionToken).toBe(FOUR.options[1]!.optionToken);

    /* كل قيمةٍ في الجواب قيمةٌ كانت على الورقة أصلاً: فلا موضعٌ ولا عددٌ ولا
       اشتقاقٌ من نصّ يستطيع أن يتسلّل بينهما. */
    for (const value of Object.values(answer)) {
      expect(value === FOUR.questionId || FOUR.options.some((o) => o.optionToken === value)).toBe(true);
    }
  });

  it("نصّ الخيار لا يظهر في الحمولة — ولا مقنَّعاً ولا مقطَّعاً", () => {
    const captured = open();
    fireEvent.click(screen.getByTestId("agent-option-1"));
    expect(captured.answers).toHaveLength(1);

    const wire = JSON.stringify(captured.answers[0]);
    for (const option of FOUR.options) {
      expect(wire).not.toContain(option.label);
      if (option.subtitle) expect(wire).not.toContain(option.subtitle);
    }
    expect(wire).not.toContain(FOUR.subjectText);
  });

  it("العدد لا يُستدلّ: ورقتان بخيارين وبأربعة تُخرجان حمولةً واحدةَ الشكل والطول", () => {
    const four = open(FOUR);
    fireEvent.click(screen.getByTestId("agent-option-0"));
    cleanup();
    const two = open(TWO);
    fireEvent.click(screen.getByTestId("agent-option-0"));

    const a = JSON.stringify(four.answers[0]);
    const b = JSON.stringify(two.answers[0]);
    expect(Object.keys(four.answers[0]!).sort()).toEqual(Object.keys(two.answers[0]!).sort());
    expect(a.length).toBe(b.length);
  });

  it("«جديد» لا تُخرج جواباً بذاتها — فلا يعلم النموذج حتى أنّ إنشاءً وقع", () => {
    const captured = open();
    fireEvent.click(screen.getByTestId("agent-option-3"));
    expect(captured.answers).toHaveLength(0);
    expect(screen.getByTestId("agent-create-sheet")).toBeTruthy();
  });
});

/* ═════════════════════════════ ٢ · لوحة المفاتيح والوصولية ══════════════ */

describe("ورقة السؤال — تكتمل بلوحة المفاتيح وحدها", () => {
  it("اللوحة قرارٌ حاجز مُعلَن، وخياراتها مجموعةُ اختيارٍ بمواضعها", () => {
    open();
    const dialog = screen.getByTestId("agent-question-sheet");
    expect(dialog.getAttribute("role")).toBe("dialog");
    expect(dialog.getAttribute("aria-modal")).toBe("true");
    expect(dialog.getAttribute("aria-labelledby")).toBe("aq-title");

    const group = screen.getByTestId("agent-question-options");
    expect(group.getAttribute("role")).toBe("radiogroup");
    const radios = [...group.querySelectorAll('[role="radio"]')];
    expect(radios).toHaveLength(4);
    /* العدد يُعلَن **للإنسان** — وهذا بعينه ما لا يعبر إلى النموذج. */
    expect(radios[2]!.getAttribute("aria-posinset")).toBe("3");
    expect(radios[2]!.getAttribute("aria-setsize")).toBe("4");
    expect(radios[0]!.getAttribute("aria-checked")).toBe("true");
  });

  it("التركيز يدخل اللوحة عند فتحها، والأسهم تنقله معها", () => {
    open();
    const group = screen.getByTestId("agent-question-options");
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-0"));

    fireEvent.keyDown(group, { key: "ArrowDown" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-1"));
    expect(screen.getByTestId("agent-option-1").getAttribute("aria-checked")).toBe("true");

    fireEvent.keyDown(group, { key: "End" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-3"));
    fireEvent.keyDown(group, { key: "Home" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-0"));
    fireEvent.keyDown(group, { key: "ArrowUp" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-3"));
  });

  it("المسار كاملاً بلا فأرة: سهمان ثم Enter يُخرجان الرمز الثالث", () => {
    const captured = open();
    const group = screen.getByTestId("agent-question-options");
    fireEvent.keyDown(group, { key: "ArrowDown" });
    fireEvent.keyDown(group, { key: "ArrowDown" });
    fireEvent.keyDown(group, { key: "Enter" });

    expect(captured.answers).toHaveLength(1);
    expect(captured.answers[0]!.optionToken).toBe(FOUR.options[2]!.optionToken);
  });

  it("Esc يُغلق بلا اختيار، وTab يبقى داخل اللوحة", () => {
    const captured = open();
    const dialog = screen.getByTestId("agent-question-sheet");
    const nodes = [...dialog.querySelectorAll<HTMLElement>("button:not([tabindex='-1'])")];
    const last = nodes[nodes.length - 1]!;

    last.focus();
    fireEvent.keyDown(dialog, { key: "Tab" });
    expect(document.activeElement).toBe(nodes[0]);

    fireEvent.keyDown(dialog, { key: "Escape" });
    expect(captured.dismissed).toHaveLength(1);
    expect(captured.answers).toHaveLength(0);
  });
});

describe("ورقة السؤال — الاتجاهان معاً", () => {
  it("بالعربية: اليسار يتقدّم واليمين يتأخّر", () => {
    open(FOUR, "ar");
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    const group = screen.getByTestId("agent-question-options");
    fireEvent.keyDown(group, { key: "ArrowLeft" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-1"));
    fireEvent.keyDown(group, { key: "ArrowRight" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-0"));
  });

  it("بالإنجليزية: اليمين يتقدّم واليسار يتأخّر — بالشيفرة نفسها", () => {
    open(FOUR, "en");
    expect(document.documentElement.getAttribute("dir")).toBe("ltr");
    const group = screen.getByTestId("agent-question-options");
    fireEvent.keyDown(group, { key: "ArrowRight" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-1"));
    fireEvent.keyDown(group, { key: "ArrowLeft" });
    expect(document.activeElement).toBe(screen.getByTestId("agent-option-0"));
  });
});

/* ═════════════════════════════ ٣ · ورقةٌ معتلّة تُرفض ولا تُعرض ═════════ */

describe("ورقة السؤال — الشاهد الموجب على كاشف العلل", () => {
  it("يكشف الرمزين المتطابقين، والورقة بلا خيار، والرمز الفارغ", () => {
    const twin = { ...FOUR, options: [FOUR.options[0]!, FOUR.options[0]!] };
    expect(agentSheetFaults(twin)).toContain("duplicateToken");
    expect(agentSheetFaults({ ...FOUR, options: [] })).toContain("noChoice");
    expect(
      agentSheetFaults({ ...FOUR, options: [{ optionToken: "  ", label: "س" }] })
    ).toContain("emptyToken");
    expect(agentSheetFaults({ ...FOUR, questionId: " " })).toContain("noQuestion");
    expect(agentSheetFaults(FOUR)).toEqual([]);
  });

  it("الورقة المعتلّة تُعرض رفضاً مُسمّى لا قائمةَ خيارات", () => {
    open({ ...FOUR, options: [] });
    expect(screen.getByTestId("agent-sheet-fault")).toBeTruthy();
    expect(screen.queryByTestId("agent-question-options")).toBeNull();
  });
});

/* ═════════════════════ ٤ · «جديد» تسأل عمّا ينشره العقد ═════════════════ */

interface OpenApi {
  readonly paths: Readonly<Record<string, Readonly<Record<string, { operationId?: string; requestBody?: unknown }>>>>;
  readonly components: { readonly schemas: Readonly<Record<string, { required?: string[]; properties?: Record<string, unknown> }>> };
}

const published = JSON.parse(
  readFileSync(path.resolve(REPO, "contracts/openapi/v1.json"), "utf8")
) as OpenApi;

/** يجد عمليةً منشورةً بمعرّفها، ومعها مسارُها ومخطّط جسمها. */
function publishedOperation(id: string): { path: string; schema: string } | null {
  for (const [route, methods] of Object.entries(published.paths)) {
    for (const operation of Object.values(methods)) {
      if (operation.operationId !== id) continue;
      const body = operation.requestBody as
        | { content?: { "application/json"?: { schema?: { $ref?: string } } } }
        | undefined;
      const ref = body?.content?.["application/json"]?.schema?.$ref ?? "";
      return { path: route, schema: ref.replace("#/components/schemas/", "") };
    }
  }
  return null;
}

describe("ورقة الإنشاء تُشتقّ من العقد المنشور", () => {
  it("العميل المُولَّد والمخطّطات وُلِّدا من بصمة عقدٍ واحدة", () => {
    expect(CONTRACT.sourceSha256).toMatch(/^[0-9a-f]{64}$/);
    const header = readFileSync(
      path.resolve(process.cwd(), "src/api/generated/runtime-schema.ts"),
      "utf8"
    );
    expect(header).toContain(CONTRACT.sourceSha256);
  });

  it("الجدول المكتوب بيدٍ يطابق العقد المنشور — معرّفاً ومخطّطاً", () => {
    let checked = 0;
    for (const kind of AGENT_ENTITY_KINDS) {
      const ref = AGENT_CREATE_OPERATIONS[kind];
      const actual = publishedOperation(ref.operationId);
      expect(actual, ref.operationId).not.toBeNull();
      expect(actual!.schema, ref.operationId).toBe(ref.requestSchema);
      checked++;
    }
    /* حارس اللافراغ: مسحٌ لا يقرأ شيئاً يمرّ دائماً. */
    expect(checked).toBe(6);
  });

  it("مرآةُ الأفعال المسموحة تطابق VoiceOperationGuard في الخادم", () => {
    const source = readFileSync(
      path.resolve(REPO, "src/Babel.Ai/Voice/VoiceOperationGuard.cs"),
      "utf8"
    );
    const block = /PermittedVerbs[\s\S]*?\{([\s\S]*?)\};/.exec(source)?.[1] ?? "";
    const verbs = [...block.matchAll(/"([a-z]+)"/g)].map((m) => m[1]!).sort();
    expect(verbs.length).toBeGreaterThan(4);
    expect(verbs).toEqual([...AGENT_PERMITTED_VERBS]);
  });

  it("العميل: خمسةُ حقولٍ بالضبط، وهي حقول CustomerRequest ولا vatNumber فيها", () => {
    const plan = planAgentCreateSheet("customer");
    expect(plan.ok).toBe(true);
    if (!plan.ok) return;
    expect(plan.operationId).toBe("addCustomer");
    expect(plan.fields.map((f) => f.path)).toEqual([
      "code",
      "creditLimit",
      "name.ar",
      "name.en",
      "paymentTermsDays",
    ]);
    expect(plan.fields.every((f) => f.required)).toBe(true);
    expect(plan.fields.some((f) => f.path === "vatNumber")).toBe(false);
    expect(plan.fields.find((f) => f.path === "creditLimit")!.pattern).toBe(
      "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$"
    );
  });

  it("المورّد: الحقول الأربعة نفسها ومعها vatNumber اختيارياً", () => {
    const plan = planAgentCreateSheet("supplier");
    expect(plan.ok).toBe(true);
    if (!plan.ok) return;
    expect(plan.operationId).toBe("addSupplier");
    expect(plan.fields.map((f) => f.path)).toEqual([
      "code",
      "creditLimit",
      "name.ar",
      "name.en",
      "paymentTermsDays",
      "vatNumber",
    ]);
    expect(plan.fields.find((f) => f.path === "vatNumber")!.required).toBe(false);
  });

  it("مجموعةُ حقول الورقة ومجموعةُ إلزامياتها هما ما ينشره العقد بالضبط", () => {
    let checked = 0;
    for (const kind of AGENT_ENTITY_KINDS) {
      const plan = planAgentCreateSheet(kind);
      if (!plan.ok) continue;
      const schema = published.components.schemas[plan.requestSchema]!;
      const properties = Object.keys(schema.properties ?? {}).sort();
      const required = [...(schema.required ?? [])].sort();

      /* المسار المركَّب `name.ar` جذرُه `name` — والمقارنة على الجذور. */
      const roots = [...new Set(plan.fields.map((f) => f.path.split(".")[0]!))].sort();
      const requiredRoots = [
        ...new Set(plan.fields.filter((f) => f.required).map((f) => f.path.split(".")[0]!)),
      ].sort();
      expect(roots, plan.requestSchema).toEqual(properties);
      expect(requiredRoots, plan.requestSchema).toEqual(required);

      /* والاسم ثنائي اللغة يُسأل عنه بشقّيه: العقد يوجب ar و en معاً. */
      if (properties.includes("name")) {
        expect(plan.fields.map((f) => f.path)).toContain("name.ar");
        expect(plan.fields.map((f) => f.path)).toContain("name.en");
      }
      checked++;
    }
    expect(checked).toBe(2);
  });

  it("ما لا يُملأ من العقد يُرفض باسم بنده — لا يُرسَم ناقصاً", () => {
    const employee = planAgentCreateSheet("employee");
    expect(employee.ok).toBe(false);
    if (!employee.ok) {
      expect(employee.reason).toBe("verbNotPermitted");
      expect(employee.subject).toBe("register");
    }

    const unit = planAgentCreateSheet("propertyUnit");
    expect(unit.ok).toBe(false);
    if (!unit.ok) {
      expect(unit.reason).toBe("parentRequired");
      expect(unit.subject).toBe("propertyId");
    }

    const item = planAgentCreateSheet("inventoryItem");
    expect(item.ok).toBe(false);
    if (!item.ok) {
      expect(item.reason).toBe("fieldIsAList");
      expect(item.subject).toBe("units");
    }

    const project = planAgentCreateSheet("project");
    expect(project.ok).toBe(false);
    if (!project.ok) {
      expect(project.reason).toBe("fieldIsAList");
      expect(project.subject).toBe("nameTranslations");
    }
  });

  it("الفحص يسمّي البند ولا يقصّ القيمة", () => {
    const plan = planAgentCreateSheet("customer");
    if (!plan.ok) throw new Error("خطّة العميل يجب أن تُرسَم");
    expect(agentCreateFaults(plan.fields, {}).map((f) => f.path)).toEqual([
      "code",
      "creditLimit",
      "name.ar",
      "name.en",
      "paymentTermsDays",
    ]);
    const filled = {
      code: "C-1",
      creditLimit: "1000.00",
      "name.ar": "المسار الأمثل",
      "name.en": "Optimal Path",
      paymentTermsDays: "30",
    };
    expect(agentCreateFaults(plan.fields, filled)).toEqual([]);
    expect(agentCreateFaults(plan.fields, { ...filled, creditLimit: "1000.00000" })).toEqual([
      { path: "creditLimit", reason: "pattern" },
    ]);
    expect(agentCreateFaults(plan.fields, { ...filled, creditLimit: "ألف" })).toEqual([
      { path: "creditLimit", reason: "pattern" },
    ]);
  });
});

describe("«جديد» على الشاشة", () => {
  it("تفتح ورقةً بحقول العقد، ولا تُرسِل ناقصةً، وتُخرج الرمز نفسه", () => {
    const captured = open();
    fireEvent.click(screen.getByTestId("agent-option-3"));

    const form = screen.getByTestId("agent-create-sheet");
    expect(form.getAttribute("data-operation")).toBe("addCustomer");
    for (const path of ["code", "creditLimit", "name.ar", "name.en", "paymentTermsDays"]) {
      expect(screen.getByTestId("agent-create-" + path)).toBeTruthy();
    }
    expect(screen.queryByTestId("agent-create-vatNumber")).toBeNull();

    /* ناقصةً: تُسمّى البنود ولا يُستدعى شيء. */
    fireEvent.click(screen.getByTestId("agent-create-submit"));
    expect(captured.drafts).toHaveLength(0);
    expect(screen.getByTestId("agent-create-faults")).toBeTruthy();

    const type = (path: string, value: string) =>
      fireEvent.change(screen.getByTestId("agent-create-" + path), { target: { value } });
    type("code", "C-1");
    type("creditLimit", "50000.00");
    type("name.ar", "شركة المسار الأمثل");
    type("name.en", "Optimal Path Co.");
    type("paymentTermsDays", "30");
    fireEvent.click(screen.getByTestId("agent-create-submit"));

    expect(captured.drafts).toHaveLength(1);
    const draft = captured.drafts[0]!;
    expect(draft.operationId).toBe("addCustomer");
    expect(draft.optionToken).toBe(FOUR.options[3]!.optionToken);
    expect(draft.questionId).toBe(QUESTION);
    expect(Object.keys(draft.values).sort()).toEqual([
      "code",
      "creditLimit",
      "name.ar",
      "name.en",
      "paymentTermsDays",
    ]);
    /* ولا شيء من نصّ الخيارات مرّ مع المسوّدة. */
    const wire = JSON.stringify(draft);
    for (const option of FOUR.options) {
      if (option.label !== "جديد") expect(wire).not.toContain(option.label);
    }
  });

  it("نوعٌ لا تُرسَم له ورقةٌ يُعرض رفضاً يسمّي بنده", () => {
    open({ ...FOUR, kind: "employee" });
    fireEvent.click(screen.getByTestId("agent-option-3"));
    const refusal = screen.getByTestId("agent-create-refusal");
    expect(refusal.textContent).toContain("register");
    expect(screen.queryByTestId("agent-create-submit")).toBeNull();
  });
});

/* ═════════════════════════════ ٥ · اللغات الأربع ═══════════════════════ */

describe("ورقة السؤال — أربع لغات بلا مفتاح ناقص", () => {
  it("كل نصّ معروضٍ في اللوحتين معرَّفٌ في اللغات الأربع", () => {
    for (const locale of ["ar", "en", "hi", "ur"]) {
      const i18n = createI18n();
      open(FOUR, locale, i18n);
      fireEvent.click(screen.getByTestId("agent-option-3"));
      fireEvent.click(screen.getByTestId("agent-create-submit"));
      expect(i18n.missing.map((m) => m.key), locale).toEqual([]);
      cleanup();
    }
  });

  it("كل نوعٍ من الستّة له عنوانُه واسمُه في كل لغة", () => {
    for (const locale of ["ar", "en", "hi", "ur"]) {
      const i18n = createI18n();
      i18n.use(locale);
      for (const kind of AGENT_ENTITY_KINDS) {
        expect(i18n.t("agent.sheet.ask." + kind, { name: "س" }), locale + "/" + kind).toContain("س");
        expect(i18n.t("agent.sheet.kind." + kind).length, locale + "/" + kind).toBeGreaterThan(1);
      }
      expect(i18n.missing.map((m) => m.key), locale).toEqual([]);
    }
  });

  it("كل حقلٍ تُرسمه ورقةُ إنشاءٍ له تسميةٌ في اللغات الأربع", () => {
    let checked = 0;
    for (const locale of ["ar", "en", "hi", "ur"]) {
      const i18n = createI18n();
      i18n.use(locale);
      for (const kind of AGENT_ENTITY_KINDS) {
        const plan = planAgentCreateSheet(kind);
        if (!plan.ok) continue;
        for (const field of plan.fields) {
          i18n.t("agent.field." + field.path);
          checked++;
        }
      }
      expect(i18n.missing.map((m) => m.key), locale).toEqual([]);
    }
    expect(checked).toBe(44);
  });
});

/* ═════════════ ٦ · المخطّطات المولَّدة هي المصدر، لا قائمةٌ في الشاشة ═══ */

describe("لا قائمة حقولٍ مكتوبةٌ بيدٍ في هذا المجلّد", () => {
  it("أسماء حقول العميل والمورّد لا تُذكر في مصدر المجلّد إلا مشتقّةً", () => {
    const dir = path.resolve(process.cwd(), "src/agent");
    const names = readdirSync(dir).filter((f) => f.endsWith(".ts") || f.endsWith(".tsx"));
    expect(names.length).toBeGreaterThan(2);
    for (const file of names) {
      const text = readFileSync(path.resolve(dir, file), "utf8");
      for (const field of ["creditLimit", "paymentTermsDays"]) {
        expect(text.includes('"' + field + '"'), file + " ← " + field).toBe(false);
      }
    }
    /* والمصدر الحقيقي موجودٌ ومقروء. */
    expect(Object.keys(SCHEMAS.CustomerRequest!.fields).sort()).toEqual([
      "code",
      "creditLimit",
      "name",
      "paymentTermsDays",
    ]);
  });
});

/* ═════ ٧ · الرفضُ يبقى قابلاً للتشغيل — وإلا صار حبساً لا حماية ═══════ */

describe("لوحةُ الرفض تُشغَّل بلوحة المفاتيح", () => {
  /* لوحةٌ بـ`role="dialog"` و`aria-modal="true"` بلا عنصرٍ يقبل التركيز تحبس
     مستخدم لوحة المفاتيح: التركيز لا يدخلها، وEscape ميّت لأن مُعالِجه على
     اللوحة والحدث لا يبلغها، فلا مخرج إلا الفأرة على العتمة. والقياس كان:
     focusables=0 · activeElement=BODY · escapeDismissed=0. */
  const MALFORMED = {
    questionId: "",
    kind: "customer",
    subjectText: "محمد القحطاني",
    options: [],
    allowsCreate: false,
  } as unknown as AgentQuestionSheetData;

  it("ورقةٌ معتلّة: الرفض يُعرض، والتركيز يدخل، وEscape يُغلق", () => {
    const captured = open(MALFORMED);

    expect(screen.getByTestId("agent-sheet-fault")).toBeTruthy();

    const dialog = screen.getByTestId("agent-question-sheet");
    const focusables = dialog.querySelectorAll(
      'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]), select:not([disabled])'
    );
    expect(focusables.length).toBeGreaterThan(0);
    expect(dialog.contains(document.activeElement)).toBe(true);

    fireEvent.keyDown(document.activeElement!, { key: "Escape" });
    expect(captured.dismissed.length).toBe(1);
  });

  it("«جديد» لنوعٍ لا تُرسَم ورقتُه: الرفض يُعرض ومعه العودة إلى الخيارات", () => {
    const refused: AgentQuestionSheetData = {
      questionId: token("q-project"),
      kind: "project",
      subjectText: "مشروع",
      options: [
        { optionToken: token("p-one"), label: "مشروع أ" },
        { optionToken: token("p-new"), label: "جديد" },
      ],
      allowsCreate: true,
    };

    open(refused);

    fireEvent.click(screen.getByTestId("agent-option-1"));

    expect(screen.getByTestId("agent-create-refusal")).toBeTruthy();

    const back = screen.getByTestId("agent-create-back");
    expect(back).toBeTruthy();

    fireEvent.click(back);
    expect(screen.getByTestId("agent-question-options")).toBeTruthy();
  });
});

/* ═════ ٨ · فحصُ الورقة يرفض ولا يرمي — والجهة السلكية لا كاتبَ لها ═══ */

describe("agentSheetFaults على حمولةٍ ينقصها مفتاح", () => {
  const cases: readonly [string, unknown][] = [
    ["بلا questionId", { kind: "customer", options: [], allowsCreate: false }],
    ["بلا options", { questionId: "x", kind: "customer", allowsCreate: false }],
    ["options ليست مصفوفة", { questionId: "x", kind: "customer", options: null, allowsCreate: false }],
    [
      "خيارٌ بلا optionToken",
      { questionId: "x", kind: "customer", options: [{ label: "أ" }], allowsCreate: false },
    ],
    ["بلا kind", { questionId: "x", options: [], allowsCreate: false }],
    ["حمولةٌ فارغة", {}],
  ];

  for (const [name, payload] of cases) {
    it(name + " ⇐ يُرفض ولا يُرمى", () => {
      const faults = agentSheetFaults(payload as AgentQuestionSheetData);
      expect(faults.length).toBeGreaterThan(0);
    });
  }

  it("والورقة السليمة لا عللَ فيها — فالفحص ليس رفضاً شاملاً", () => {
    expect(agentSheetFaults(FOUR)).toEqual([]);
  });
});
