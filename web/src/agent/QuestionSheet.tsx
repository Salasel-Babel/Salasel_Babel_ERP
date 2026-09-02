/* ═══════════════════════════════════════════════════════════════════════════
   لوحة السؤال — قرارٌ حاجز، يعمل بلا فأرة ويُقرأ بلا بصر
   The question sheet — a modal decision point, mouse-free and sight-free
   ───────────────────────────────────────────────────────────────────────────
   **الأسماء هنا محلّية**: رُسمت من سجلّ المستخدم على هذا الجهاز، ولم يعبر منها
   إلى النموذج حرفٌ واحد — ولا عددُها. وما يعود إليه بعد الاختيار رمزٌ معتم
   واحد، شكلُه واحدٌ سواءٌ اختار الإنسان قائماً أو أنشأ جديداً.

   **ولا لغةَ بصريةٍ ثانية**: اللوحة تبني على `web/src/ui/` — `RefusalPanel`
   للرفض، و`Field` و`Button` للحقول، و`ProvenanceMark` لمصدر كل سطر. ووسمُ
   المصدر **شكلٌ لا لونٌ وحده**: «من سجلّك» حدٌّ متقطّع، و«ستكتبه الآن» حدٌّ
   متّصل باهت — يُقرآن في الطباعة بالأبيض والأسود وعلى شاشةٍ لا تفرّق الألوان.

   **والاتجاه منطقيٌّ كلّه**: لا خاصّية فيزيائية في `agent.css`، والأسهم
   الأفقية تُقرأ بحسب اتجاه الصفحة — اليمين يتقدّم في الإنجليزية ويتأخّر في
   العربية — فتعمل اللوحة يميناً-ليساراً ويساراً-يميناً بلا سطرٍ يتغيّر.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { Button, Field, ProvenanceMark, RefusalPanel } from "../ui";
import { useT } from "../i18n/react";
import {
  agentCreateFaults,
  planAgentCreateSheet,
  type AgentCreateField,
  type AgentCreatePlan,
} from "./create-fields";
import {
  agentSheetFaults,
  answerOf,
  isCreateOption,
  type AgentAnswer,
  type AgentCreateDraft,
  type AgentQuestionSheet as Sheet,
  type AgentSheetFault,
} from "./sheet";
import "./agent.css";

/** خصائص لوحة السؤال. */
export interface AgentQuestionSheetProps {
  /** الورقة كما رسمها الخادم من بياناتٍ محلّية. */
  readonly sheet: Sheet;
  /** يُستدعى بالجواب — **مفتاحان لا ثالث لهما**. */
  readonly onAnswer: (answer: AgentAnswer) => void;
  /** يُستدعى بمسوّدة الإنشاء حين يختار الإنسان «جديد» ويملأ حقولها. */
  readonly onCreate?: (draft: AgentCreateDraft) => void;
  /** يُستدعى عند الإغلاق بلا اختيار. */
  readonly onDismiss: () => void;
  /** الطلب جارٍ — الأزرار معطَّلة ولا يُرسَل الجواب مرّتين. */
  readonly busy?: boolean;
  readonly testId?: string;
}

/** العناصر التي يبلغها Tab داخل اللوحة. */
const FOCUSABLE =
  'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]), select:not([disabled])';

/** اتجاه العنصر كما تراه الصفحة فعلاً — لا كما تفترضه الشيفرة. */
function directionOf(node: Element | null): "rtl" | "ltr" {
  const owner = node?.closest("[dir]") ?? node?.ownerDocument?.documentElement ?? null;
  return owner?.getAttribute("dir") === "rtl" ? "rtl" : "ltr";
}

/**
 * لوحة السؤال. تُركَّب عند الفتح وتُفكَّك عند الإغلاق، فحالتها نظيفةٌ في كل مرّة.
 * @param props الورقة وما يُستدعى عند الاختيار.
 */
export function AgentQuestionSheet(props: AgentQuestionSheetProps): ReactNode {
  const { t } = useT();
  const { sheet, onAnswer, onCreate, onDismiss } = props;
  const [step, setStep] = useState<"choose" | "create">("choose");
  const [active, setActive] = useState(0);
  const [values, setValues] = useState<Readonly<Record<string, string>>>({});
  const [tried, setTried] = useState(false);
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const optionsRef = useRef<HTMLDivElement | null>(null);
  const returnTo = useRef<Element | null>(null);

  const faults = useMemo(() => agentSheetFaults(sheet), [sheet]);
  const fault: AgentSheetFault | undefined = faults[0];
  const plan: AgentCreatePlan = useMemo(() => planAgentCreateSheet(sheet.kind), [sheet.kind]);

  /* التركيز يدخل اللوحة عند فتحها ويعود إلى ما فتحها عند إغلاقها. ومن يفتح
     قراراً حاجزاً ولا ينقل التركيز إليه يترك قارئ الشاشة في الصفحة تحته. */
  useEffect(() => {
    returnTo.current = document.activeElement;
    const first = dialogRef.current?.querySelector<HTMLElement>(FOCUSABLE);
    first?.focus();
    return () => {
      const back = returnTo.current;
      if (back instanceof HTMLElement) back.focus();
    };
  }, []);

  /* الأسهم تنقل **التركيز** لا الحالة وحدها: مجموعةُ اختيارٍ يتحرّك فيها
     `aria-checked` والتركيز ثابت تجعل قارئ الشاشة يقرأ العنصر القديم. ولا
     يُخطف التركيز من خارج المجموعة — الأثر يعمل حين يكون داخلها أصلاً. */
  useEffect(() => {
    const group = optionsRef.current;
    if (!group || !group.contains(document.activeElement)) return;
    const radios = group.querySelectorAll<HTMLElement>('[role="radio"]');
    const node = radios[active];
    if (node && node !== document.activeElement) node.focus();
  }, [active, step]);

  const choose = useCallback(
    (index: number) => {
      const option = sheet.options[index];
      if (!option || props.busy) return;
      if (isCreateOption(sheet, index)) {
        setStep("create");
        return;
      }
      onAnswer(answerOf(sheet, option));
    },
    [onAnswer, props.busy, sheet]
  );

  const move = useCallback(
    (delta: number) => {
      const count = sheet.options.length;
      if (count === 0) return;
      setActive((index) => (index + delta + count) % count);
    },
    [sheet.options.length]
  );

  /* حبسُ التركيز: قرارٌ حاجز لا يُترك Tab يخرج منه إلى صفحةٍ لا تُقرأ تحته. */
  const onDialogKey = useCallback(
    (event: React.KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onDismiss();
        return;
      }
      if (event.key !== "Tab") return;
      const nodes = [...(dialogRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [])];
      if (nodes.length === 0) return;
      const first = nodes[0];
      const last = nodes[nodes.length - 1];
      if (!first || !last) return;
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    },
    [onDismiss]
  );

  const onOptionsKey = useCallback(
    (event: React.KeyboardEvent) => {
      const rtl = directionOf(event.currentTarget) === "rtl";
      if (event.key === "ArrowDown") {
        event.preventDefault();
        move(1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        move(-1);
      } else if (event.key === "ArrowRight") {
        event.preventDefault();
        move(rtl ? -1 : 1);
      } else if (event.key === "ArrowLeft") {
        event.preventDefault();
        move(rtl ? 1 : -1);
      } else if (event.key === "Home") {
        event.preventDefault();
        setActive(0);
      } else if (event.key === "End") {
        event.preventDefault();
        setActive(Math.max(0, sheet.options.length - 1));
      } else if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        choose(active);
      }
    },
    [active, choose, move, sheet.options.length]
  );

  const submitCreate = useCallback(() => {
    if (!plan.ok || props.busy) return;
    setTried(true);
    if (agentCreateFaults(plan.fields, values).length > 0) return;
    const option = sheet.options[sheet.options.length - 1];
    if (!option || !onCreate) return;
    onCreate({
      questionId: sheet.questionId,
      optionToken: option.optionToken,
      operationId: plan.operationId,
      values,
    });
  }, [onCreate, plan, props.busy, sheet, values]);

  const title = t("agent.sheet.ask." + sheet.kind, { name: sheet.subjectText });
  const kindLabel = t("agent.sheet.kind." + sheet.kind);

  return (
    <div
      className="aq-scrim"
      data-testid={props.testId ?? "agent-question-scrim"}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onDismiss();
      }}
    >
      <div
        className="aq"
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="aq-title"
        aria-describedby="aq-note"
        onKeyDown={onDialogKey}
        data-step={step}
        data-testid="agent-question-sheet"
      >
        <div className="aq__hd">
          <strong id="aq-title" data-testid="agent-question-title">
            {step === "choose" ? title : t("agent.create.title", { kind: kindLabel })}
          </strong>
          <span className="spacer">
            <ProvenanceMark
              source="read"
              label={t("agent.sheet.fromRegister")}
              testId="agent-question-provenance"
            />
          </span>
        </div>

        <p className="aq__note" id="aq-note" data-testid="agent-question-note">
          {step === "choose" ? t("agent.sheet.note") : t("agent.create.note")}
        </p>

        {fault ? (
          /* **والرفض يحمل زرّه.** لوحةٌ بـ`role="dialog"` و`aria-modal` بلا عنصرٍ
             واحد يقبل التركيز تحبس مستخدم لوحة المفاتيح: التركيز لا يدخلها (لا شيء
             يُركَّز عليه)، وEscape ميّت لأن مُعالِجه على اللوحة والحدث لا يبلغها،
             فلا مخرج إلا الفأرة على العتمة. وهذا يقع بنقرةٍ عادية لا بحمولةٍ
             مشوَّهة: أربعةٌ من أنواع الكيانات الستّة تُرفض ورقةُ إنشائها اليوم. */
          <>
            <RefusalPanel
              title={t("agent.sheet.faultTitle")}
              body={t("agent.sheet.fault." + fault)}
              code={"agent.sheet." + fault}
              codeLabel={t("agent.refuse.codeLabel")}
              next={t("agent.sheet.faultNext")}
              testId="agent-sheet-fault"
            />
            <div className="aq__foot">
              <span>{t("agent.sheet.hintClose")}</span>
              <span className="spacer">
                <Button
                  label={t("agent.sheet.cancel")}
                  kind="primary"
                  size="sm"
                  onClick={onDismiss}
                  testId="agent-sheet-fault-close"
                />
              </span>
            </div>
          </>
        ) : step === "choose" ? (
          <>
            <div
              className="aq__list"
              ref={optionsRef}
              role="radiogroup"
              aria-label={t("agent.sheet.optionsLabel")}
              onKeyDown={onOptionsKey}
              data-testid="agent-question-options"
            >
              {sheet.options.map((option, index) => {
                const create = isCreateOption(sheet, index);
                return (
                  <button
                    key={option.optionToken}
                    type="button"
                    className="aq__opt"
                    role="radio"
                    aria-checked={index === active}
                    aria-posinset={index + 1}
                    aria-setsize={sheet.options.length}
                    tabIndex={index === active ? 0 : -1}
                    data-create={create ? "true" : "false"}
                    data-testid={"agent-option-" + index}
                    disabled={props.busy}
                    onFocus={() => setActive(index)}
                    onClick={() => choose(index)}
                  >
                    <ProvenanceMark
                      source={create ? "typed" : "read"}
                      label={create ? t("agent.sheet.willBeTyped") : t("agent.sheet.fromRegister")}
                    />
                    <span className="aq__label">{create ? t("agent.sheet.create") : option.label}</span>
                    {option.subtitle && !create ? (
                      <span className="aq__sub">{option.subtitle}</span>
                    ) : null}
                    {create ? <span className="aq__sub">{t("agent.sheet.createHint")}</span> : null}
                  </button>
                );
              })}
            </div>
            <div className="aq__foot">
              <span>{t("agent.sheet.hintMove")}</span>
              <span>{t("agent.sheet.hintChoose")}</span>
              <span>{t("agent.sheet.hintClose")}</span>
              <span className="spacer">
                <Button
                  label={t("agent.sheet.cancel")}
                  kind="ghost"
                  size="sm"
                  onClick={onDismiss}
                  testId="agent-sheet-cancel"
                />
              </span>
            </div>
          </>
        ) : (
          <AgentCreateForm
            plan={plan}
            kindLabel={kindLabel}
            values={values}
            tried={tried}
            busy={props.busy === true}
            onChange={(path, value) => setValues((current) => ({ ...current, [path]: value }))}
            onBack={() => setStep("choose")}
            onSubmit={submitCreate}
          />
        )}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════ ورقةُ الإنشاء المعروضة */

/** خصائص ورقة الإنشاء. */
interface CreateFormProps {
  readonly plan: AgentCreatePlan;
  readonly kindLabel: string;
  readonly values: Readonly<Record<string, string>>;
  readonly tried: boolean;
  readonly busy: boolean;
  readonly onChange: (path: string, value: string) => void;
  readonly onBack: () => void;
  readonly onSubmit: () => void;
}

/** حقلٌ واحد من العقد، معروضاً بأوّليّة `Field` نفسها التي تبني بها الشاشات. */
function CreateField(props: {
  readonly field: AgentCreateField;
  readonly value: string;
  readonly bad: boolean;
  readonly busy: boolean;
  readonly onChange: (value: string) => void;
}): ReactNode {
  const { t } = useT();
  const { field } = props;
  const id = "aq-f-" + field.path.split(".").join("-");
  const numeric = field.kind === "money" || field.kind === "decimal";
  return (
    <Field
      id={id}
      label={t("agent.field." + field.path)}
      hint={field.pattern ? t("agent.create.patternHint") : undefined}
      error={props.bad ? t("agent.create.fieldBad") : undefined}
      required={field.required}
      source="typed"
    >
      {field.choices ? (
        <select
          id={id}
          className="ctl"
          value={props.value}
          disabled={props.busy}
          aria-invalid={props.bad}
          data-testid={"agent-create-" + field.path}
          onChange={(event) => props.onChange(event.target.value)}
        >
          <option value="">{t("agent.create.choose")}</option>
          {field.choices.map((choice) => (
            <option key={choice} value={choice}>
              {choice}
            </option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          className={"ctl" + (numeric ? " mono amt-input" : "")}
          type="text"
          dir={numeric ? "ltr" : undefined}
          inputMode={numeric ? "decimal" : undefined}
          autoComplete="off"
          spellCheck={false}
          disabled={props.busy}
          aria-invalid={props.bad}
          data-testid={"agent-create-" + field.path}
          value={props.value}
          onChange={(event) => props.onChange(event.target.value)}
        />
      )}
    </Field>
  );
}

/** ورقة الإنشاء: حقول العقد، أو رفضٌ يسمّي بنده. */
function AgentCreateForm(props: CreateFormProps): ReactNode {
  const { t } = useT();
  const { plan } = props;

  if (!plan.ok) {
    /* **الرفض يُعرض ولا يُخرَج من قبل التذييل.** كان `return` يقع قبل الأزرار، فكان
       من يختار «جديد» لنوعٍ لا تُرسَم ورقتُه — وهي أربعةٌ من ستّة اليوم — يجد لوحةً
       حاجزة بلا عنصرٍ واحد يقبل التركيز: لا عودةَ ولا إغلاق إلا بالفأرة. */
    return (
      <div className="aq__create" data-testid="agent-create-refused">
        <RefusalPanel
          title={t("agent.refuse.title", { kind: props.kindLabel })}
          body={t("agent.refuse." + plan.reason)}
          code={"agent.create." + plan.reason}
          codeLabel={t("agent.refuse.codeLabel")}
          subject={plan.subject}
          subjectLabel={t("agent.refuse.subjectLabel")}
          next={t("agent.refuse.next")}
          testId="agent-create-refusal"
        />
        <div className="aq__foot">
          <Button
            label={t("agent.create.back")}
            kind="ghost"
            size="sm"
            onClick={props.onBack}
            testId="agent-create-back"
          />
        </div>
      </div>
    );
  }

  const faults = props.tried ? agentCreateFaults(plan.fields, props.values) : [];
  const badPaths = new Set(faults.map((fault) => fault.path));

  return (
    <div className="aq__create" data-testid="agent-create-sheet" data-operation={plan.operationId}>
      <div className="grid fields-4">
        {plan.fields.map((field) => (
          <CreateField
            key={field.path}
            field={field}
            value={props.values[field.path] ?? ""}
            bad={badPaths.has(field.path)}
            busy={props.busy}
            onChange={(value) => props.onChange(field.path, value)}
          />
        ))}
      </div>
      {plan.omitted.length > 0 ? (
        <p className="aq__note" data-testid="agent-create-omitted">
          {t("agent.create.omitted", { fields: plan.omitted.join(" · ") })}
        </p>
      ) : null}
      {faults.length > 0 ? (
        <p className="aq__bad" role="alert" data-testid="agent-create-faults">
          {t("agent.create.faults", { fields: faults.map((fault) => fault.path).join(" · ") })}
        </p>
      ) : null}
      <div className="aq__foot">
        <Button
          label={t("agent.create.back")}
          kind="ghost"
          size="sm"
          onClick={props.onBack}
          testId="agent-create-back"
        />
        <span className="spacer">
          <Button
            label={t("agent.create.submit")}
            kind="primary"
            disabled={props.busy}
            onClick={props.onSubmit}
            testId="agent-create-submit"
          />
        </span>
      </div>
    </div>
  );
}
