/* ═══════════════════════════════════════════════════════════════════════════
   /admin/subscription — ما اشتريناه، وما الذي يعمل، وماذا يتوقّف
   The subscription — what was bought, what runs, and what stops
   ───────────────────────────────────────────────────────────────────────────
   **السؤال الذي تجيبه هذه الشاشة وحدها:** «ما حال اشتراكنا، وما الذي يقع
   بالضبط إن غيّرتُ الخطّة أو أوقفتُ الاشتراك؟»

   وأربعةُ أبوابٍ على حالةٍ واحدة: `readSubscription` تقرأها، و
   `changeSubscriptionPlan` و`lapseSubscription` و`resumeSubscription` تغيّرها.
   **والقراءةُ والكتابة على شاشةٍ واحدة لأن الجواب على «ماذا يتوقّف؟» هو
   القراءة نفسها**: جدول الوحدات وحالاتها. وشاشةُ تغييرٍ بلا هذا الجدول تطلب
   من إنسانٍ أن يوقّع على أثرٍ لا يراه.

   **ونموذجا كتابةٍ لا ثلاثة** (ADR-0080): نموذجُ الخطّة، ونموذجُ الانتقال —
   و«انقطاع» و«استئناف» جسمٌ واحد في العقد (`SubscriptionTransitionRequest`)
   وحالتان متنافيتان: الفعّال يُقطَع، والمنقطع يُستأنف.

   ── وثلاثة حدودٍ مقروءةٌ من العقد لا مخترَعة ──────────────────────────
   ١ · **الانقطاع لا يحجب قراءةً ولا ينتزع سجلّاً.** نصّ العقد: «من انقطع
       اشتراكه يدخل ويقرأ … ويُردّ عند أول كتابة بـ403 و`entitlement.read_only`».
       فما يتوقّف هو **الكتابة** وحدها — وهذا ما تقوله اللوحة قبل التنفيذ،
       ولا تقول أكثر منه.
   ٢ · **الأرضية قراءةٌ لا نزع — لِما يبلغ عملُه الدفتر.** والعقد يعلّق ذلك
       على `postsJournal` لكل وحدة. **ولا يسمّي العقد أرضيةَ ما لا يبلغ
       الدفتر**، فلا تقولها هذه الشاشة ولا تُخمّنها.
   ٣ · **الثلاثة أفعالُ مشغِّل** يُطلب كلٌّ منها باعتماد التزويد وحده؛ ورمزُ
       الرفض الثابت `subscription.operator_credential_required`. والشاشة لا
       تُخفي أزرارها لذلك: الإخفاء ليس منعاً، والمنع في الخادم — فتُظهر
       الرفض باسمه وتضيف فوقه الخطوة التالية.

   ── وما لا تعرفه هذه الشاشة، وتقوله ───────────────────────────────────
   **لا بابَ منشوراً يسرد الخطط ووحداتها.** فرمز الخطّة الجديدة يُكتب،
   ورمزٌ غير معروف يردّه الخادم بـ`subscription.plan_unknown` **ورسالةٍ
   تُسمّي المعروف** — وهي المصدر الوحيد الصادق لقائمة الخطط، وتُعرض كما وردت.
   والفرقُ بين قبل وبعد يُعرض من الجوابين نفسيهما لا من كتالوجٍ مكتوبٍ هنا.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  changeSubscriptionPlan,
  lapseSubscription,
  readSession,
  readSubscription,
  resumeSubscription,
} from "../../api/generated/client";
import type { Subscription, SubscriptionModule } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { RECORD_TAG } from "../../app/translated-name";
import { Num, useT } from "../../i18n/react";
import { EmptyState, StatCard, StatusBadge, useMoment, type DocState } from "../../ui";
import {
  AdminField,
  AdminSectionNav,
  DeclaredGap,
  Irreversible,
  StatePanel,
} from "./parts";

/** حالات الاشتراك كما ينشرها العقد — مجموعةٌ مغلقة تُقرأ ولا تُخترع. */
const ACTIVE = "Active";
const LAPSED = "Lapsed";

/** حالات الوحدة الثلاث — لا رابعة. */
const ENTITLED = "Entitled";
const READ_ONLY = "ReadOnly";

/** رمز رفض الخادم لفعلٍ لا يبلغه إلا اعتماد التزويد — كما ينشره العقد. */
const OPERATOR_CODE = "subscription.operator_credential_required";

/** الخطوة التالية التي تعرفها الشاشة لكل رمز رفض — مفتاح ترجمة، لا نصّ. */
const NEXT_STEP: Readonly<Record<string, string>> = {
  [OPERATOR_CODE]: "screen.subscription.next.operator",
  "subscription.plan_unknown": "screen.subscription.next.planUnknown",
  "subscription.not_active": "screen.subscription.next.notActive",
  "subscription.not_lapsed": "screen.subscription.next.notLapsed",
};

/** نغمةُ شارةِ حالةِ وحدة. */
function moduleTone(state: string): DocState {
  if (state === ENTITLED) return "posted";
  if (state === READ_ONLY) return "pending";
  return "archived";
}

/** لوحةُ خطوةٍ تالية تحت رفض. */
function NextStep(props: { readonly error: unknown; readonly testId: string }): ReactNode {
  const { t } = useT();
  const code = props.error instanceof ProblemError ? props.error.code : null;
  const key = code ? NEXT_STEP[code] : undefined;
  if (!key) return null;
  return (
    <p className="alert alert--info" role="status" data-testid={props.testId} data-code={code}>
      {t(key)}
    </p>
  );
}

/** الشاشة كاملةً. */
export function SubscriptionScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");

  const session = useQuery({
    queryKey: ["admin", "subscription", "session", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    queryFn: ({ signal }) => readSession(transport, signal),
  });

  /* المستأجر لا يُكتب بيد: العقد يطابقه باعتماد الطلب ويرفض إن اختلف، فمعرّفٌ
     يُلصَق هنا لا يفتح شيئاً ويجعل الرفض يُقرأ خطأً في الكتابة. */
  const tenantId = session.data?.tenantId ?? "";

  const current = useQuery({
    queryKey: ["admin", "subscription", "read", config.baseUrl, config.token, tenantId],
    enabled: tenantId !== "",
    retry: false,
    queryFn: ({ signal }) => readSubscription(transport, { tenantId }, signal),
  });

  /* ── لوح الخطّة ────────────────────────────────────────────────────── */
  const [planCode, setPlanCode] = useState("");
  const [planAuthority, setPlanAuthority] = useState("");
  const [planReason, setPlanReason] = useState("");
  const [planBusy, setPlanBusy] = useState(false);
  const [planFailure, setPlanFailure] = useState<unknown>(null);

  /* ── لوح الانتقال ─────────────────────────────────────────────────── */
  const [moveAuthority, setMoveAuthority] = useState("");
  const [moveReason, setMoveReason] = useState("");
  const [moveBusy, setMoveBusy] = useState(false);
  const [moveFailure, setMoveFailure] = useState<unknown>(null);

  /* ── ما وقع فعلاً: قبل وبعد ───────────────────────────────────────── */
  const [before, setBefore] = useState<Subscription | null>(null);
  const [after, setAfter] = useState<Subscription | null>(null);

  const live = after ?? current.data ?? null;

  const applied = useCallback(
    (was: Subscription | null, now: Subscription) => {
      setBefore(was);
      setAfter(now);
      fireArrive();
      void current.refetch();
    },
    [current, fireArrive]
  );

  const doChangePlan = useCallback(async () => {
    setPlanBusy(true);
    setPlanFailure(null);
    const was = live;
    try {
      const now = await changeSubscriptionPlan(transport, {
        tenantId,
        body: {
          planCode: planCode.trim(),
          authority: planAuthority.trim(),
          reasonAr: planReason.trim(),
        },
      });
      applied(was, now);
    } catch (problem) {
      setPlanFailure(problem);
    } finally {
      setPlanBusy(false);
    }
  }, [applied, live, planAuthority, planCode, planReason, tenantId, transport]);

  const doMove = useCallback(async () => {
    setMoveBusy(true);
    setMoveFailure(null);
    const was = live;
    const body = { authority: moveAuthority.trim(), reasonAr: moveReason.trim() };
    try {
      const now =
        was?.state === LAPSED
          ? await resumeSubscription(transport, { tenantId, body })
          : await lapseSubscription(transport, { tenantId, body });
      applied(was, now);
    } catch (problem) {
      setMoveFailure(problem);
    } finally {
      setMoveBusy(false);
    }
  }, [applied, live, moveAuthority, moveReason, tenantId, transport]);

  const modules = useMemo(() => live?.modules ?? [], [live]);
  const writing = useMemo(() => modules.filter((m) => m.state === ENTITLED), [modules]);
  const posting = useMemo(() => writing.filter((m) => m.postsJournal), [writing]);
  const isLapsed = live?.state === LAPSED;

  const planOk =
    planCode.trim() !== "" && planAuthority.trim() !== "" && planReason.trim() !== "";
  const moveOk = moveAuthority.trim() !== "" && moveReason.trim() !== "";

  return (
    <section className="stack" data-testid="admin-subscription-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.subscription.title")}</h1>
          <p className="sub">{t("screen.subscription.lede")}</p>
        </div>
      </header>

      <AdminSectionNav current="/admin/subscription" />

      <p className="alert alert--info" role="note" data-testid="admin-subscription-operator">
        {t("screen.subscription.operatorNotice")}{" "}
        <span className="mono" dir="ltr">{OPERATOR_CODE}</span>
      </p>

      {/* ═══════════════════════════ ١ · الحال الآن ═══════════════════ */}
      <StatePanel
        title={t("screen.subscription.stateTitle")}
        note={t("screen.subscription.stateNote")}
        loading={current.isPending && current.fetchStatus === "fetching"}
        testId="admin-subscription-state"
      >
        {config.token === "" ? (
          <EmptyState
            title={t("screen.subscription.noCredentialTitle")}
            body={t("screen.subscription.noCredentialBody")}
            small
            testId="admin-subscription-no-credential"
          />
        ) : session.isError ? (
          <ProblemPanel error={session.error} onRetry={() => void session.refetch()} />
        ) : current.isError ? (
          <>
            <ProblemPanel error={current.error} onRetry={() => void current.refetch()} />
            <NextStep error={current.error} testId="admin-subscription-read-next" />
          </>
        ) : live ? (
          <div className="stack">
            <div className={"kv " + arriveCls}>
              <div>
                <div className="k">{t("screen.subscription.tenantName")}</div>
                <div className="v" lang={RECORD_TAG} dir="rtl" data-testid="admin-subscription-tenant-name">
                  {live.nameAr}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.subscription.plan")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-subscription-plan-code">
                  {live.planCode}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.subscription.state")}</div>
                <div className="v">
                  <StatusBadge
                    state={live.state === ACTIVE ? "posted" : live.state === LAPSED ? "pending" : "archived"}
                    label={t("screen.subscription.stateName." + live.state)}
                    testId="admin-subscription-state-badge"
                  />
                </div>
              </div>
              <div>
                <div className="k">{t("screen.subscription.renewsOn")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-subscription-renews">
                  {live.renewsOn ?? t("screen.subscription.noRenewal")}
                </div>
              </div>
            </div>

            <div className="stats-row" data-testid="admin-subscription-price">
              <StatCard
                label={t("screen.subscription.monthlyPrice")}
                amount={live.monthlyPrice}
                hint={t("screen.subscription.monthlyPriceHint", { currency: live.currency })}
                testId="admin-subscription-monthly"
              />
              <StatCard
                label={t("screen.subscription.perUserPrice")}
                amount={live.perUserPrice}
                hint={t("screen.subscription.perUserPriceHint", { currency: live.currency })}
                testId="admin-subscription-per-user"
              />
              <StatCard
                label={t("screen.subscription.includedUsers")}
                count={live.includedUsers}
                hint={t("screen.subscription.includedUsersHint")}
                testId="admin-subscription-included"
              />
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ═══════════════════════════ ٢ · ما الذي يعمل ═════════════════ */}
      <StatePanel
        title={t("screen.subscription.modulesTitle")}
        note={t("screen.subscription.modulesNote")}
        aside={
          live ? (
            <span className="muted" data-testid="admin-subscription-module-count">
              {tp("screen.subscription.moduleCount", modules.length)}
            </span>
          ) : null
        }
        testId="admin-subscription-modules"
      >
        {live === null ? (
          <EmptyState
            title={t("screen.subscription.noModulesTitle")}
            body={t("screen.subscription.noModulesBody")}
            small
            testId="admin-subscription-no-modules"
          />
        ) : (
          <div className="tablewrap" data-testid="admin-subscription-modules-table">
            <table className="data">
              <caption className="visually-hidden">{t("screen.subscription.modulesTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("screen.subscription.moduleCode")}</th>
                  <th scope="col">{t("screen.subscription.moduleName")}</th>
                  <th scope="col">{t("screen.subscription.moduleState")}</th>
                  <th scope="col">{t("screen.subscription.postsJournal")}</th>
                  <th scope="col">{t("screen.subscription.onLapse")}</th>
                </tr>
              </thead>
              <tbody>
                {modules.map((unit) => (
                  <ModuleRow key={unit.code} unit={unit} was={before} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═══════════════════════════ ٣ · تغيير الخطّة ═════════════════ */}
      <StatePanel
        title={t("screen.subscription.planTitle")}
        note={t("screen.subscription.planNote")}
        testId="admin-subscription-plan"
      >
        <div className="grid fields-3">
          <AdminField
            id="adm-sb-plan"
            label={t("screen.subscription.newPlan")}
            hint={t("screen.subscription.newPlanHint")}
            source="typed"
            required
          >
            <input
              id="adm-sb-plan"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="admin-subscription-plan-input"
              value={planCode}
              onChange={(e) => setPlanCode(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-sb-plan-authority"
            label={t("screen.subscription.authority")}
            hint={t("screen.subscription.authorityHint")}
            source="typed"
            required
          >
            <input
              id="adm-sb-plan-authority"
              className="ctl"
              autoComplete="off"
              data-testid="admin-subscription-plan-authority"
              value={planAuthority}
              onChange={(e) => setPlanAuthority(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-sb-plan-reason"
            label={t("screen.subscription.reason")}
            hint={t("screen.subscription.reasonHint")}
            source="typed"
            required
          >
            <input
              id="adm-sb-plan-reason"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-subscription-plan-reason"
              value={planReason}
              onChange={(e) => setPlanReason(e.target.value)}
            />
          </AdminField>
        </div>

        <Irreversible
          title={t("screen.subscription.planAskTitle")}
          effect={t("screen.subscription.planEffect")}
          acknowledge={t("screen.subscription.planAck")}
          action={t("screen.subscription.planAction")}
          busy={planBusy}
          {...(tenantId === ""
            ? { blocked: t("screen.subscription.blockedNoTenant") }
            : !planOk
              ? { blocked: t("screen.subscription.blockedNoAuthority") }
              : {})}
          onConfirm={() => void doChangePlan()}
          testId="admin-subscription-plan-confirm"
        >
          <ul className="adm-effects" data-testid="admin-subscription-plan-effects">
            <li>{t("screen.subscription.planEffectFloor")}</li>
            <li>{t("screen.subscription.planEffectRow")}</li>
            <li>
              {t("screen.subscription.planEffectWriting")} <Num value={writing.length} />
            </li>
            <li>
              {t("screen.subscription.planEffectPosting")} <Num value={posting.length} />
            </li>
            <li>{t("screen.subscription.planEffectUnknown")}</li>
          </ul>
        </Irreversible>

        {planFailure ? (
          <>
            <ProblemPanel error={planFailure} />
            <NextStep error={planFailure} testId="admin-subscription-plan-next" />
          </>
        ) : null}
      </StatePanel>

      {/* ══════════════════════ ٤ · الانقطاع أو الاستئناف ═════════════ */}
      <StatePanel
        title={isLapsed ? t("screen.subscription.resumeTitle") : t("screen.subscription.lapseTitle")}
        note={isLapsed ? t("screen.subscription.resumeNote") : t("screen.subscription.lapseNote")}
        testId="admin-subscription-move"
      >
        <div className="grid fields-half">
          <AdminField
            id="adm-sb-move-authority"
            label={t("screen.subscription.authority")}
            hint={t("screen.subscription.authorityHint")}
            source="typed"
            required
          >
            <input
              id="adm-sb-move-authority"
              className="ctl"
              autoComplete="off"
              data-testid="admin-subscription-move-authority"
              value={moveAuthority}
              onChange={(e) => setMoveAuthority(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-sb-move-reason"
            label={t("screen.subscription.reason")}
            hint={t("screen.subscription.reasonHint")}
            source="typed"
            required
          >
            <input
              id="adm-sb-move-reason"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-subscription-move-reason"
              value={moveReason}
              onChange={(e) => setMoveReason(e.target.value)}
            />
          </AdminField>
        </div>

        <Irreversible
          title={
            isLapsed ? t("screen.subscription.resumeAskTitle") : t("screen.subscription.lapseAskTitle")
          }
          effect={isLapsed ? t("screen.subscription.resumeEffect") : t("screen.subscription.lapseEffect")}
          acknowledge={isLapsed ? t("screen.subscription.resumeAck") : t("screen.subscription.lapseAck")}
          action={isLapsed ? t("screen.subscription.resumeAction") : t("screen.subscription.lapseAction")}
          busy={moveBusy}
          {...(tenantId === ""
            ? { blocked: t("screen.subscription.blockedNoTenant") }
            : !moveOk
              ? { blocked: t("screen.subscription.blockedNoAuthority") }
              : {})}
          onConfirm={() => void doMove()}
          testId="admin-subscription-move-confirm"
        >
          <ul className="adm-effects" data-testid="admin-subscription-move-effects">
            {isLapsed ? (
              <>
                <li>{t("screen.subscription.resumeEffectWrite")}</li>
                <li>{t("screen.subscription.resumeEffectRow")}</li>
                <li>{t("screen.subscription.resumeEffectSamePlan")}</li>
              </>
            ) : (
              <>
                <li data-testid="admin-subscription-lapse-effect-read">
                  {t("screen.subscription.lapseEffectRead")}
                </li>
                <li>
                  {t("screen.subscription.lapseEffectWriting")} <Num value={writing.length} />
                </li>
                <li>
                  {t("screen.subscription.lapseEffectPosting")} <Num value={posting.length} />
                </li>
                <li>{t("screen.subscription.lapseEffectRenewal")}</li>
                <li>{t("screen.subscription.lapseEffectUnknownFloor")}</li>
              </>
            )}
          </ul>
        </Irreversible>

        {moveFailure ? (
          <>
            <ProblemPanel error={moveFailure} />
            <NextStep error={moveFailure} testId="admin-subscription-move-next" />
          </>
        ) : null}

        {after ? (
          <p className="alert alert--info" role="status" data-testid="admin-subscription-applied">
            {t("screen.subscription.applied")}
          </p>
        ) : null}
      </StatePanel>

      {/* ═════════════════════════ ٥ · قرارٌ على المالك — مُعلَناً ════ */}
      <DeclaredGap
        title={t("screen.subscription.gapTitle")}
        body={t("screen.subscription.gapBody")}
        owed={t("screen.subscription.gapOwed")}
        testId="admin-subscription-gap"
      />
    </section>
  );
}

/** صفُّ وحدةٍ واحدة — ومعه ما كانت عليه قبل آخر تغيير، إن تغيّرت. */
function ModuleRow(props: {
  readonly unit: SubscriptionModule;
  readonly was: Subscription | null;
}): ReactNode {
  const { t } = useT();
  const { unit } = props;
  const previous = props.was?.modules.find((m) => m.code === unit.code)?.state;
  const changed = previous !== undefined && previous !== unit.state;
  return (
    <tr data-testid="admin-subscription-module" data-module={unit.code} data-state={unit.state}>
      <td>
        <span className="mono" dir="ltr">{unit.code}</span>
      </td>
      <td>
        <span lang={RECORD_TAG} dir="rtl">{unit.nameAr}</span>
        {changed ? (
          <>
            {" "}
            <span className="pill pill--pending" data-testid="admin-subscription-module-changed">
              {t("screen.subscription.wasState", {
                state: t("screen.subscription.moduleStateName." + previous),
              })}
            </span>
          </>
        ) : null}
      </td>
      <td>
        <StatusBadge
          state={moduleTone(unit.state)}
          label={t("screen.subscription.moduleStateName." + unit.state)}
        />
      </td>
      <td>{unit.postsJournal ? t("screen.subscription.reachesLedger") : t("screen.subscription.noLedger")}</td>
      <td data-testid="admin-subscription-module-floor">
        {unit.postsJournal
          ? t("screen.subscription.floorReadOnly")
          : t("screen.subscription.floorUnstated")}
      </td>
    </tr>
  );
}
