/* ═══════════════════════════════════════════════════════════════════════════
   مساحة العمل الجانبية  ·  The side workspace
   ───────────────────────────────────────────────────────────────────────────
   **لوحٌ واحد ينفتح فوق أي شاشة** — لا ميزةٌ مبعثرة على كل شاشة. فيه الدردشة،
   والخطّة بخطواتها وحال كلٍّ منها، والتنفيذ خطوةً خطوة، وطلبُ التأكيد، وورقةُ
   السؤال حين يلتبس اسم، والمسوّدةُ الناتجة مع الزرّ الذي يفتحها في شاشتها.

   **ولا زرّ ترحيلٍ في هذا اللوح — ولا واحد.** التأكيد هنا يعني «أقبل شكل هذه
   البيانات»، ولا يعني «رحّلها»: الناتج بعده مسوّدةٌ كما كان قبله، والترحيل فعلٌ
   بصريّ يدويّ على شاشة المستند. وأربع طبقاتٍ في الخادم تجعل ذلك بنيوياً، وهذا
   اللوح لا يملك ما ينقضها ولو أراد: لا باب ترحيلٍ في العقد المنشور للوكيل.

   **وموضعه الجانبُ المقابل لبداية القراءة**: يسارُ الشاشة بالعربية ويمينُها
   بالإنجليزية. و`inset-inline-end` تقول ذلك بقاعدةٍ واحدة — ولا `left` ولا
   `right` في هذا المسار، يفرضه `scripts/audit.mjs`.

   **والحالات التي تفصل لوحاً حقيقياً من عرضٍ تقديمي، وكلُّها معروضة هنا:**
   النموذج يفكّر · ينتظر تأكيدك · خطوةٌ سقطت وما التالي · الجلسة انقطعت ·
   تجاوزتَ حدّ الإنفاق · الوكيل معطَّل لهذا المستأجر.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  answerAgentQuestion,
  confirmAgentStep,
  openAgentSession,
  readAgentSession,
  readAgentSpend,
  readAgentTurnEvents,
  sendAgentMessage,
} from "../api/generated/client";
import type { AgentSession, AgentSpend, ApiError } from "../api/generated/types";
import { ProblemError, type Transport } from "../api/transport";
import { Num, useT } from "../i18n/react";
import { Button, Field, StatusBadge } from "../ui";
import { AgentQuestionSheet } from "./QuestionSheet";
import type { AgentAnswer, AgentQuestionSheet as SheetData } from "./sheet";
import { isAgentEntityKind } from "./sheet";
import {
  AGENT_DISABLED_CODE,
  AGENT_SESSION_GONE_CODE,
  AGENT_SPEND_CEILING_CODE,
  EMPTY_THREAD,
  foldAgentEvents,
  hasCode,
  withUtterance,
  type AgentThread,
} from "./workspace";
import "./agent.css";

/* ═══════════════════════════════════════════════════ ١ · حالُ اللوح */

/** ما يمنع اللوح من العمل، إن وُجد — وكلٌّ منها يُعرَض بجملته لا بـ«تعذّر». */
type Blocked =
  | { readonly kind: "none" }
  /** الوكيل غير مركَّب على هذا الخادم — إعدادُ نشرٍ لا عطل. */
  | { readonly kind: "disabled" }
  /** الجلسة انقطعت أو انقضت — يُعاد فتحها بضغطة. */
  | { readonly kind: "gone" }
  /** بلغت المنشأة سقف إنفاقها — لا يُرفع بإعادة المحاولة. */
  | { readonly kind: "ceiling"; readonly refusals: readonly ApiError[] }
  /** انقطاعُ شبكةٍ أو عطلُ خادم — يُعاد الوصل بضغطة. */
  | { readonly kind: "offline"; readonly detail: string };

/** خصائص اللوح. */
export interface AgentWorkspaceProps {
  /** النقل — نسخةٌ واحدة من الاعتماد والعنوان. */
  readonly transport: Transport;
  /** الشركة المفتوحة. */
  readonly companyId: string;
  /** يُستدعى عند إغلاق اللوح. */
  readonly onClose: () => void;
  /** يُستدعى بمسار شاشة المسوّدة حين يطلب المستخدم فتحها. */
  readonly onOpenScreen?: (route: string) => void;
}

/* ═══════════════════════════════════════════ ٢ · اللوح */

/**
 * مساحة العمل الجانبية. تُركَّب عند الفتح وتُفكَّك عند الإغلاق.
 * @param props النقل والشركة وما يقع عند الإغلاق.
 */
export function AgentWorkspace(props: AgentWorkspaceProps): ReactNode {
  const { t } = useT();
  const { transport, companyId, onClose } = props;

  const [session, setSession] = useState<AgentSession | null>(null);
  const [thread, setThread] = useState<AgentThread>(EMPTY_THREAD);
  const [spend, setSpend] = useState<AgentSpend | null>(null);
  const [blocked, setBlocked] = useState<Blocked>({ kind: "none" });
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);

  const cursor = useRef(0);
  const log = useRef<HTMLDivElement | null>(null);

  /* ── فتحُ الجلسة، وقراءةُ الإنفاق معها ─────────────────────────────────
     والإنفاق يُقرأ **قبل** أول رسالة لا بعدها: من بلغ سقفه يجب أن يعرف قبل
     أن يكتب فقرةً كاملة ثم تُرفض. */
  useEffect(() => {
    if (companyId === "") return;

    const abort = new AbortController();

    void (async () => {
      try {
        const opened = await openAgentSession(transport, { companyId }, abort.signal);
        if (abort.signal.aborted) return;
        cursor.current = opened.lastSequence;
        setSession(opened);
        setBlocked({ kind: "none" });

        const measured = await readAgentSpend(transport, { companyId }, abort.signal);
        if (!abort.signal.aborted) setSpend(measured);
      } catch (fault) {
        if (!abort.signal.aborted) setBlocked(blockedBy(fault));
      }
    })();

    return () => abort.abort();
  }, [transport, companyId, attempt]);

  /* ── حلقةُ القراءة: «ما بعد ن؟» ───────────────────────────────────────
     **وهي البثّ**: كل جزءِ تفكيرٍ ونصٍّ يصل حدثاً، فتُرى الكتابة تنمو. والطلب
     ينتظر عند الخادم حتى يجدّ شيء، فلا استطلاعٌ مشغولٌ يُتعب الطرفين.

     **وانقطاعُ الشبكة يُعرض ولا يُبتلع**: اللوحة تعلن «انقطعت»، وتُستأنف من
     المؤشّر نفسه بضغطة — بلا تكرار سطرٍ وبلا فجوة. */
  useEffect(() => {
    if (session === null || blocked.kind !== "none") return;

    const abort = new AbortController();
    let alive = true;

    void (async () => {
      while (alive && !abort.signal.aborted) {
        try {
          const page = await readAgentTurnEvents(
            transport,
            {
              companyId,
              agentSessionId: session.agentSessionId,
              after: String(cursor.current),
            },
            abort.signal
          );

          if (!alive || abort.signal.aborted) return;

          if (page.events.length > 0) {
            setThread((current) => {
              const folded = foldAgentEvents(current, page.events);
              cursor.current = folded.cursor;
              return folded;
            });

            const ceiling = page.events.find(
              (event) => event.kind === "refused" && hasCode(event.refusals, AGENT_SPEND_CEILING_CODE)
            );

            if (ceiling !== undefined) {
              setBlocked({ kind: "ceiling", refusals: ceiling.refusals });
            }
          }

          /* الحالُ يُقرأ بعد كل صفحة: هو ما يحمل ما ينتظر تأكيداً وورقةَ
             السؤال والخطّة بحالها. والأحداث تقول ما جرى، والحالُ يقول أين نقف. */
          const fresh = await readAgentSession(
            transport,
            { companyId, agentSessionId: session.agentSessionId },
            abort.signal
          );

          if (!alive || abort.signal.aborted) return;
          setSession(fresh);
        } catch (fault) {
          if (!alive || abort.signal.aborted) return;
          setBlocked(blockedBy(fault));
          return;
        }
      }
    })();

    return () => {
      alive = false;
      abort.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [transport, companyId, session?.agentSessionId, blocked.kind]);

  /* آخرُ ما وصل يبقى مرئياً — ولوحةٌ تبثّ ولا تتبع ما تبثّه تجعل القارئ يطارد. */
  useEffect(() => {
    log.current?.scrollTo({ top: log.current.scrollHeight, behavior: "smooth" });
  }, [thread, session?.pendingConfirmation]);

  const send = useCallback(async () => {
    const text = draft.trim();
    if (text === "" || session === null || busy) return;

    setBusy(true);
    setThread((current) => withUtterance(current, text));
    setDraft("");

    try {
      await sendAgentMessage(transport, {
        companyId,
        agentSessionId: session.agentSessionId,
        body: { text },
      });
    } catch (fault) {
      setBlocked(blockedBy(fault));
    } finally {
      setBusy(false);
    }
  }, [draft, session, busy, transport, companyId]);

  const confirm = useCallback(
    async (stepId: string, accepted: boolean) => {
      if (session === null) return;
      setBusy(true);
      try {
        const next = await confirmAgentStep(transport, {
          companyId,
          agentSessionId: session.agentSessionId,
          stepId,
          body: { accepted },
        });
        setSession(next);
      } catch (fault) {
        setBlocked(blockedBy(fault));
      } finally {
        setBusy(false);
      }
    },
    [session, transport, companyId]
  );

  const answer = useCallback(
    async (given: AgentAnswer) => {
      if (session === null) return;
      setBusy(true);
      try {
        const next = await answerAgentQuestion(transport, {
          companyId,
          agentSessionId: session.agentSessionId,
          body: { questionId: given.questionId, optionToken: given.optionToken },
        });
        setSession(next);
      } catch (fault) {
        setBlocked(blockedBy(fault));
      } finally {
        setBusy(false);
      }
    },
    [session, transport, companyId]
  );

  const sheet: SheetData | null = useMemo(() => {
    const pending = session?.pendingQuestion ?? null;
    if (pending === null || !isAgentEntityKind(pending.kind)) return null;
    return {
      questionId: pending.questionId,
      kind: pending.kind,
      subjectText: pending.subjectText,
      options: pending.options.map((option) => ({
        optionToken: option.optionToken,
        label: option.label,
        ...(option.subtitle === null ? {} : { subtitle: option.subtitle }),
      })),
      allowsCreate: pending.allowsCreate,
    };
  }, [session?.pendingQuestion]);

  const phase = session?.phase ?? "completed";
  const waiting = session?.pendingConfirmation ?? null;

  return (
    <aside
      className="agw"
      data-testid="agent-workspace"
      data-phase={phase}
      data-blocked={blocked.kind}
      aria-label={t("agent.workspace.title")}
    >
      <header className="agw__hd">
        <strong>{t("agent.workspace.title")}</strong>
        <StatusBadge
          state={phaseTone(phase)}
          label={t("agent.workspace.phase." + phase)}
          testId="agent-phase"
        />
        <span className="spacer" />
        {spend === null ? null : (
          /* **الرقم يمرّ بطبقة التدويل لا بنصٍّ مترجَم**: الأرقام العربية-الهندية
             تُختار باللغة، ورقمٌ يُحقن في جملةٍ مترجَمة يخرج لاتينياً في كل لغة. */
          <span className="agw__spend" data-testid="agent-spend" title={t("agent.workspace.spendTitle")}>
            <span>{t("agent.workspace.spendLabel")}</span>{" "}
            <Num value={spend.billable} />
            {spend.ceiling === null ? (
              <span>{" · " + t("agent.workspace.ownKey")}</span>
            ) : (
              <>
                <span aria-hidden="true">{" / "}</span>
                <Num value={spend.ceiling} />
              </>
            )}
          </span>
        )}
        <Button
          label={t("agent.workspace.close")}
          kind="ghost"
          size="sm"
          onClick={onClose}
          testId="agent-close"
        />
      </header>

      {blocked.kind === "none" ? null : (
        <BlockedNotice
          blocked={blocked}
          onRetry={() => {
            cursor.current = 0;
            setThread(EMPTY_THREAD);
            setSession(null);
            setBlocked({ kind: "none" });
            setAttempt((n) => n + 1);
          }}
        />
      )}

      {session !== null && session.plan.length > 0 ? (
        <section className="agw__plan" data-testid="agent-plan" aria-label={t("agent.workspace.planLabel")}>
          <p className="agw__planhd">{t("agent.workspace.planLabel")}</p>
          <ol className="agw__steps">
            {session.plan.map((step) => (
              <li
                key={step.stepId}
                className="agw__step"
                data-state={step.state}
                data-testid={"agent-step-" + String(step.order)}
              >
                <span className="agw__stepmark" aria-hidden="true" />
                <span className="agw__steptitle">{step.titleAr}</span>
                <StatusBadge state={stepTone(step.state)} label={t("agent.workspace.step." + step.state)} />
                {step.refusals.length > 0 ? (
                  <span className="agw__stepwhy">{step.refusals[0]?.messageAr ?? ""}</span>
                ) : null}
              </li>
            ))}
          </ol>
        </section>
      ) : null}

      <div className="agw__log" ref={log} data-testid="agent-log" role="log" aria-live="polite">
        {thread.lines.length === 0 && blocked.kind === "none" ? (
          <p className="agw__empty" data-testid="agent-empty">
            {t("agent.workspace.empty")}
          </p>
        ) : null}

        {thread.lines.map((line, index) => (
          <Line key={index} line={line} index={index} onOpenScreen={props.onOpenScreen} />
        ))}

        {phase === "running" ? (
          <p className="agw__pulse" data-testid="agent-thinking">
            <span className="agw__dot" aria-hidden="true" />
            {t("agent.workspace.working")}
          </p>
        ) : null}
      </div>

      {waiting === null ? null : (
        <section className="agw__confirm" data-testid="agent-confirmation" aria-live="polite">
          <p className="agw__confirmhd">{t("agent.workspace.confirmTitle")}</p>
          <p className="agw__confirmnote">{t("agent.workspace.confirmNote")}</p>

          <dl className="agw__fields">
            {waiting.fields.map((field) => (
              <div className="agw__field" key={field.path} data-testid={"agent-field-" + field.path}>
                <dt>{field.path}</dt>
                <dd>
                  {field.masked ? (
                    <span className="agw__masked">{t("agent.workspace.masked")}</span>
                  ) : (
                    field.value
                  )}
                </dd>
              </div>
            ))}
          </dl>

          <div className="agw__confirmact">
            <Button
              label={t("agent.workspace.acceptShape")}
              kind="primary"
              disabled={busy}
              onClick={() => void confirm(waiting.stepId, true)}
              testId="agent-accept-shape"
            />
            <Button
              label={t("agent.workspace.refuseShape")}
              disabled={busy}
              onClick={() => void confirm(waiting.stepId, false)}
              testId="agent-refuse-shape"
            />
            <span className="agw__nopost">{t("agent.workspace.noPostHere")}</span>
          </div>
        </section>
      )}

      <form
        className="agw__composer"
        onSubmit={(event) => {
          event.preventDefault();
          void send();
        }}
      >
        {/* والمؤلِّف حقلٌ من النظام لا حقلٌ يقيس نفسه: التسمية والتحكّم والوصف
            ثلاث خانات يملكها الصفّ، فلا يُزيح طولُ التلميح موضعَ الحقل — وهو
            العطل الذي سمّاه صاحب المصلحة بعينه. */}
        <Field
          id="agent-draft"
          label={t("agent.workspace.composerLabel")}
          hint={t("agent.workspace.composerHint")}
        >
          <textarea
            id="agent-draft"
            className="agw__input"
            data-testid="agent-input"
            rows={2}
            value={draft}
            disabled={session === null || blocked.kind !== "none"}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void send();
              }
            }}
          />
        </Field>
        <Button
          label={t("agent.workspace.send")}
          kind="primary"
          loading={busy}
          disabled={session === null || busy || blocked.kind !== "none" || draft.trim() === ""}
          onClick={() => void send()}
          testId="agent-send"
        />
      </form>

      {sheet === null ? null : (
        <AgentQuestionSheet
          sheet={sheet}
          busy={busy}
          onAnswer={(given) => void answer(given)}
          onDismiss={onClose}
        />
      )}
    </aside>
  );
}

/* ═══════════════════════════════════════════ ٣ · قطعُ اللوح */

/** سطرٌ واحد في الخيط. */
function Line(props: {
  readonly line: AgentThread["lines"][number];
  readonly index: number;
  readonly onOpenScreen?: (route: string) => void;
}): ReactNode {
  const { t } = useT();
  const { line } = props;

  switch (line.kind) {
    case "you":
      return (
        <p className="agw__you" data-testid={"agent-line-" + String(props.index)}>
          {line.text}
        </p>
      );

    case "said":
      return (
        <p className="agw__said" data-testid={"agent-line-" + String(props.index)}>
          {line.text}
        </p>
      );

    case "thinking":
      /* التفكير مُلخَّصٌ يُعرَض تقدّماً لا سلسلةَ استدلالٍ يُبنى عليها قرار،
         فيُعرض خافتاً ومطوياً — حاضراً لمن أراد، لا مزاحماً لما يهمّ. */
      return (
        <details className="agw__think" data-testid={"agent-line-" + String(props.index)}>
          <summary>{t("agent.workspace.thinking")}</summary>
          <p>{line.text}</p>
        </details>
      );

    case "plan":
      return (
        <p className="agw__declared" data-testid={"agent-line-" + String(props.index)}>
          {t("agent.workspace.planDeclared")}
        </p>
      );

    case "tool":
      return (
        <p
          className={line.refused ? "agw__tool agw__tool--bad" : "agw__tool"}
          data-testid={"agent-line-" + String(props.index)}
        >
          <span className="agw__toolname">{line.toolName}</span>
          {line.refused ? (
            <span className="agw__toolwhy">
              {line.refusals[0]?.messageAr ?? t("agent.workspace.stepFailed")}
            </span>
          ) : null}
        </p>
      );

    case "landed":
      /* ⚠ زرٌّ يفتح الشاشة — **ولا زرّ ترحيلٍ هنا ولا في هذا الملفّ كلّه**.
         الترحيل زرُّ الشاشة نفسها: فعلٌ بصريّ يدويّ على مستندٍ قُرئ.

         **والزرُّ لا يُعرض بلا مسار.** حدثُ الهبوط يحمل `screenRoute` نصّاً، وفراغُه
         حالٌ ممكنة على السلك (خادمٌ أقدم، أو عمليةٌ بلا شاشةٍ مُعلَنة). وزرٌّ يقود
         إلى `""` يفتح لا شيء — وهو أسوأ من غيابه: يُعلّم المستخدم ألّا يثق باللوح.
         والجملة تبقى: المسوّدة هبطت، وهي مسوّدةٌ بعد. */
      return (
        <p className="agw__landed" data-testid={"agent-line-" + String(props.index)}>
          <span>{t("agent.workspace.landed")}</span>
          {line.screenRoute === "" ? null : (
            <Button
              label={t("agent.workspace.openScreen")}
              size="sm"
              onClick={() => props.onOpenScreen?.(line.screenRoute)}
              testId={"agent-open-" + String(props.index)}
            />
          )}
          <span className="agw__stilldraft">{t("agent.workspace.stillDraft")}</span>
        </p>
      );

    case "refused":
      return (
        <p className="agw__refused" role="alert" data-testid={"agent-line-" + String(props.index)}>
          {line.refusals.map((refusal) => refusal.messageAr).join(" · ")}
        </p>
      );

    default:
      return null;
  }
}

/** ما يمنع اللوح، بجملته وبخطوته التالية. */
function BlockedNotice(props: {
  readonly blocked: Exclude<Blocked, { kind: "none" }>;
  readonly onRetry: () => void;
}): ReactNode {
  const { t } = useT();
  const { blocked } = props;

  const body =
    blocked.kind === "ceiling"
      ? (blocked.refusals[0]?.messageAr ?? t("agent.workspace.blocked.ceiling"))
      : blocked.kind === "offline"
        ? blocked.detail
        : t("agent.workspace.blocked." + blocked.kind);

  return (
    <div className="agw__blocked" role="alert" data-testid={"agent-blocked-" + blocked.kind}>
      <p className="agw__blockedhd">{t("agent.workspace.blockedTitle." + blocked.kind)}</p>
      <p>{body}</p>
      {blocked.kind === "disabled" || blocked.kind === "ceiling" ? (
        <p className="muted">{t("agent.workspace.blockedNoRetry")}</p>
      ) : (
        <Button
          label={t("agent.workspace.reconnect")}
          kind="primary"
          size="sm"
          onClick={props.onRetry}
          testId="agent-reconnect"
        />
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════ ٤ · ترجمةُ الأعطال */

/**
 * يترجم عطلاً إلى حالٍ **مسمّى**. و«تعذّر» ليست حالاً: من بلغ سقفه يفعل شيئاً
 * غير ما يفعله من انقطعت شبكته، ومن لم يُركَّب عنده الوكيل لا يفعل شيئاً أصلاً.
 */
function blockedBy(fault: unknown): Blocked {
  if (fault instanceof ProblemError) {
    /* **والرمز يُقرأ ولو لم يكن الجسم على الصيغة المنشورة**: `ProblemError.from`
       تُبقي الرمز والحالة وتجعل `problem` معدوماً حين ينطق الخادم غير العقد —
       فقراءةُ `problem.errors` وحدها كانت ستُسقط الحالَ المسمّى في اللحظة التي
       يكون فيها أنفع: حين يردّ وسيطٌ في الطريق صفحةَ خطأٍ من عنده. */
    const errors: readonly ApiError[] = fault.problem?.errors ?? [];
    const codes = [fault.code, ...errors.map((error) => error.code)];

    if (codes.includes(AGENT_DISABLED_CODE)) return { kind: "disabled" };
    if (codes.includes(AGENT_SESSION_GONE_CODE)) return { kind: "gone" };
    if (codes.includes(AGENT_SPEND_CEILING_CODE)) {
      return { kind: "ceiling", refusals: errors };
    }

    return { kind: "offline", detail: fault.problem?.detailAr ?? fault.message };
  }

  return { kind: "offline", detail: fault instanceof Error ? fault.message : String(fault) };
}

/** نغمةُ الطور — لونٌ من الرمز الدلالي لا اختيارٌ في الشاشة. */
function phaseTone(phase: string): "pending" | "draft" | "rejected" | "info" {
  if (phase === "running") return "pending";
  if (phase === "awaitingHuman") return "draft";
  if (phase === "refused") return "rejected";
  return "info";
}

/** نغمةُ حال الخطوة. */
function stepTone(state: string): "pending" | "draft" | "rejected" | "info" {
  if (state === "running") return "pending";
  if (state === "awaitingConfirmation" || state === "awaitingAnswer") return "draft";
  if (state === "refused") return "rejected";
  return "info";
}
