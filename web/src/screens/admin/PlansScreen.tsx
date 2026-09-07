/* ═══════════════════════════════════════════════════════════════════════════
   /admin/plans — كتالوج الخطط: ما يُباع، وبكم، ومن قرّر ذلك
   The plan catalogue — what is sold, at what price, and who decided
   ───────────────────────────────────────────────────────────────────────────
   **السؤال الذي تجيبه هذه الشاشة وحدها:** «ما الخطط التي تبيعها المنصّة الآن،
   وبأيّ سعر، وكيف أُنشئ خطّةً أو أُسعّرها أو أنشرها — بسندٍ يُحتجّ به؟»

   وبابان على كتالوجٍ واحد (ADR-0092): `readPlans` يقرأ **المنشور** — وهو ما
   يبلغه كلُّ مصادَق، وشاشةُ الاشتراك تختار منه — و`readPlatformPlans` يقرأ
   الكتالوج **كلّه** لمشغّل المنصّة وحده، و`putPlatformPlan` يُنشئ أو يعدّل
   أو ينشر. **والكتالوج بياناتُ المنصّة لا قائمةٌ في شيفرة**: كلُّ سعرٍ هنا
   صفٌّ في `control.plan`، وكلُّ تغييرٍ سطرٌ في `control.plan_change` بفاعله
   وسنده وسببه وما قبله وما بعده.

   ── ومشغّلُ المنصّة اعتمادٌ يُعلَن، لا دورٌ يُستنتج ─────────────────────
   **الإخفاء ليس منعاً.** المنعُ في الخادم: اعتمادُ منشأةٍ — مهما اتّسع —
   يُردّ على الكتالوج كلّه وعلى الكتابة بـ403 و`platform.operator_required`.
   فالشاشة لا تُخفي نموذجها لذلك: تُظهر الرفض باسمه وتضيف فوقه الخطوة
   التالية، وتُبقي **المنشورَ** مقروءاً لمن لا يبلغ الكتالوج كلّه.

   ── ولا حذف ────────────────────────────────────────────────────────────
   لا بابَ حذفٍ في العقد، فلا زرَّ حذفٍ هنا: الخطّةُ التي تخرج من البيع
   **يُلغى نشرُها** وتبقى صفّاً لأن اشتراكاتٍ ماضية تشير إليها.

   ── والوحداتُ المعروفة تُقرأ من الكتالوج نفسه ────────────────────────────
   لا بابَ منشوراً يسرد كتالوج الوحدات. فما تعرضه الشاشة خاناتٍ هو **اتّحادُ
   رموز الوحدات في الخطط المقروءة** — مصدرٌ صادق لا مخترَع — ومعه حقلٌ لرموزٍ
   أخرى تُكتب بيد، والخادمُ يردّ المجهولَ منها **باسمه** (`platform.plan_refused`).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { putPlatformPlan, readPlans, readPlatformPlans } from "../../api/generated/client";
import type { Plan } from "../../api/generated/types";
import { Money } from "../../api/money";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { RECORD_TAG } from "../../app/translated-name";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, StatusBadge, useMoment } from "../../ui";
import { AdminField, AdminSectionNav, DeclaredGap, Irreversible, StatePanel } from "./parts";

/** رمز رفض الخادم لسطحٍ لا يبلغه إلا اعتماد مشغّل المنصّة — كما ينشره العقد. */
const OPERATOR_CODE = "platform.operator_required";

/** الخطوة التالية التي تعرفها الشاشة لكل رمز رفض — مفتاح ترجمة، لا نصّ. */
const NEXT_STEP: Readonly<Record<string, string>> = {
  [OPERATOR_CODE]: "screen.plans.next.operator",
  "platform.plan_refused": "screen.plans.next.refused",
  "platform.plan_authority_missing": "screen.plans.next.authority",
  "fleet.unavailable": "screen.plans.next.unavailable",
};

/** شكلُ رمز الخطّة كما ينشره العقد على المسار: A-Z0-9_ حتى 32 محرفاً. */
const PLAN_CODE_RE = /^[A-Z0-9_]{1,32}$/;

/** شكلُ عددٍ صحيح غير سالب كما يُكتب بيد — تسعُ خاناتٍ تكفي عدداً من المستخدمين. */
const COUNT_RE = /^\d{1,9}$/;

/**
 * عددٌ صحيح من نصٍّ سبق فحصُه بـ`COUNT_RE` — رقماً رقماً، بلا `parseInt` ولا عائم:
 * قسمُ الإدارة لا يحوّل نصّاً إلى عددٍ بدالّةٍ تقبل ما لا يقبله الفحص.
 * @param text النصّ المفحوص.
 */
function countOf(text: string): number {
  let n = 0;
  for (const ch of text) n = n * 10 + (ch.charCodeAt(0) - 48);
  return n;
}

/** نصُّ مبلغٍ كما يقبله نحو المال المنشور — يُفحص بـ`Money.wire` لا بتعبيرٍ ثانٍ. */
function isWireMoney(text: string): boolean {
  try {
    Money.wire(text);
    return true;
  } catch {
    return false;
  }
}

/** لوحةُ خطوةٍ تالية تحت رفض. */
function NextStep(props: { readonly error: unknown; readonly testId: string }): ReactNode {
  const { t } = useT();
  const code = props.error instanceof ProblemError ? props.error.code : null;
  const key = code ? NEXT_STEP[code] : undefined;
  if (!key) return null;
  return (
    <div className="alert alert--info" role="status" data-testid={props.testId} data-code={code}>
      <div className="body">
        <p>{t(key)}</p>
      </div>
    </div>
  );
}

/** الشاشة كاملةً. */
export function PlansScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* الكتالوج كلّه — لمشغّل المنصّة؛ والمنشورُ — لكلّ مصادَق. */
  const catalogue = useQuery({
    queryKey: ["admin", "plans", "catalogue", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    queryFn: ({ signal }) => readPlatformPlans(transport, signal),
  });
  const published = useQuery({
    queryKey: ["admin", "plans", "published", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    queryFn: ({ signal }) => readPlans(transport, signal),
  });

  const operatorRefused =
    catalogue.error instanceof ProblemError && catalogue.error.code === OPERATOR_CODE;
  const catalogueRows = catalogue.data?.plans;
  const publishedRows = published.data?.plans;
  const rows = useMemo<readonly Plan[]>(
    () => catalogueRows ?? publishedRows ?? [],
    [catalogueRows, publishedRows],
  );
  const isOperator = catalogue.data !== undefined;

  /* ── النموذج ──────────────────────────────────────────────────────── */
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [monthlyPrice, setMonthlyPrice] = useState("");
  const [perUserPrice, setPerUserPrice] = useState("");
  const [includedUsers, setIncludedUsers] = useState("");
  const [picked, setPicked] = useState<readonly string[]>([]);
  const [otherModules, setOtherModules] = useState("");
  const [publish, setPublish] = useState(false);
  const [authority, setAuthority] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [written, setWritten] = useState<Plan | null>(null);

  /** الوحداتُ المعروفة: اتّحادُ رموز الخطط المقروءة — لا كتالوجٌ مكتوبٌ هنا. */
  const knownModules = useMemo(() => {
    const all = new Set<string>();
    for (const plan of rows) for (const module of plan.modules) all.add(module);
    return [...all].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  }, [rows]);

  const modules = useMemo(() => {
    const typed = otherModules
      .split(/[,\s،]+/)
      .map((m) => m.trim().toUpperCase())
      .filter((m) => m !== "");
    return [...new Set([...picked, ...typed])].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  }, [otherModules, picked]);

  const togglePicked = useCallback((module: string, on: boolean) => {
    setPicked((was) => (on ? [...new Set([...was, module])] : was.filter((m) => m !== module)));
  }, []);

  /** يملأ النموذج من صفٍّ — للتسعير أو النشر أو إلغائه. */
  const edit = useCallback((plan: Plan) => {
    setCode(plan.code);
    setNameAr(plan.nameAr);
    setNameEn(plan.nameTranslations.find((n) => n.name === "en")?.value ?? "");
    setMonthlyPrice(plan.monthlyPrice.text);
    setPerUserPrice(plan.perUserPrice.text);
    setIncludedUsers(String(plan.includedUsers));
    setPicked(plan.modules);
    setOtherModules("");
    setPublish(plan.published);
    setWritten(null);
    setFailure(null);
  }, []);

  const usersOk = COUNT_RE.test(includedUsers.trim());
  const users = usersOk ? countOf(includedUsers.trim()) : 0;
  const codeOk = PLAN_CODE_RE.test(code.trim());
  const moneyOk = isWireMoney(monthlyPrice.trim()) && isWireMoney(perUserPrice.trim());
  const namesOk = nameAr.trim() !== "";
  const authorityOk = authority.trim() !== "" && reason.trim() !== "";
  const blocked = !codeOk
    ? t("screen.plans.blockedCode")
    : !namesOk
      ? t("screen.plans.blockedNames")
      : !moneyOk
        ? t("screen.plans.blockedMoney")
        : !usersOk
          ? t("screen.plans.blockedUsers")
          : modules.length === 0
            ? t("screen.plans.blockedModules")
            : !authorityOk
              ? t("screen.plans.blockedAuthority")
              : undefined;

  const existing = rows.find((p) => p.code === code.trim());

  const doPut = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    try {
      const now = await putPlatformPlan(transport, {
        planCode: code.trim(),
        body: {
          nameAr: nameAr.trim(),
          /* الإنجليزية ترجمةٌ موسومة لا حقلٌ ثابت (ADR-0021). */
          nameTranslations: nameEn.trim() === "" ? [] : [{ name: "en", value: nameEn.trim() }],
          monthlyPrice: Money.wire(monthlyPrice.trim()),
          perUserPrice: Money.wire(perUserPrice.trim()),
          includedUsers: users,
          modules,
          published: publish,
          authority: authority.trim(),
          reasonAr: reason.trim(),
        },
      });
      setWritten(now);
      fireArrive();
      void catalogue.refetch();
      void published.refetch();
    } catch (problem) {
      setFailure(problem);
    } finally {
      setBusy(false);
    }
  }, [
    authority,
    catalogue,
    code,
    fireArrive,
    modules,
    monthlyPrice,
    nameAr,
    nameEn,
    perUserPrice,
    publish,
    published,
    reason,
    transport,
    users,
  ]);

  return (
    <section className="stack" data-testid="admin-plans-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.plans.title")}</h1>
          <p className="sub">{t("screen.plans.lede")}</p>
        </div>
      </header>

      <AdminSectionNav current="/admin/plans" />

      <div className="alert alert--info" role="note" data-testid="admin-plans-operator">
        <div className="body">
          <p>
            {t("screen.plans.operatorNotice")} <code className="mono">{OPERATOR_CODE}</code>
          </p>
        </div>
      </div>

      {/* ═══════════════════════════ ١ · الكتالوج ═══════════════════════ */}
      <StatePanel
        title={isOperator ? t("screen.plans.catalogueTitle") : t("screen.plans.publishedTitle")}
        note={isOperator ? t("screen.plans.catalogueNote") : t("screen.plans.publishedNote")}
        loading={catalogue.isPending && published.isPending && config.token !== ""}
        aside={
          rows.length > 0 ? (
            <span className="muted" data-testid="admin-plans-count">
              {tp("screen.plans.count", rows.length)}
            </span>
          ) : undefined
        }
        testId="admin-plans-catalogue"
      >
        {config.token === "" ? (
          <EmptyState
            title={t("screen.plans.noCredentialTitle")}
            body={t("screen.plans.noCredentialBody")}
            testId="admin-plans-no-credential"
          />
        ) : operatorRefused ? (
          <>
            <ProblemPanel error={catalogue.error} />
            <NextStep error={catalogue.error} testId="admin-plans-list-next" />
          </>
        ) : catalogue.error && !published.data ? (
          <ProblemPanel error={catalogue.error} onRetry={() => void catalogue.refetch()} />
        ) : null}

        {rows.length > 0 ? (
          <div className={"tablewrap " + arriveCls} data-testid="admin-plans-table">
            <table className="ledger">
              <thead>
                <tr>
                  <th>{t("screen.plans.code")}</th>
                  <th>{t("screen.plans.name")}</th>
                  <th className="num">{t("screen.plans.monthly")}</th>
                  <th className="num">{t("screen.plans.perUser")}</th>
                  <th className="num">{t("screen.plans.included")}</th>
                  <th>{t("screen.plans.modules")}</th>
                  <th>{t("screen.plans.state")}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((plan) => (
                  <tr
                    key={plan.code}
                    data-testid="admin-plans-row"
                    data-plan={plan.code}
                    data-published={plan.published ? "true" : "false"}
                  >
                    <td className="mono" dir="ltr">
                      {plan.code}
                    </td>
                    <td lang={RECORD_TAG} dir="rtl">
                      {plan.nameAr}
                    </td>
                    <Amount as="td" value={plan.monthlyPrice} />
                    <Amount as="td" value={plan.perUserPrice} />
                    <td className="num">
                      <Num value={plan.includedUsers} />
                    </td>
                    <td className="mono" dir="ltr">
                      {plan.modules.join(" · ")}
                    </td>
                    <td>
                      <StatusBadge
                        state={plan.published ? "posted" : "pending"}
                        label={
                          plan.published ? t("screen.plans.published") : t("screen.plans.draft")
                        }
                      />
                    </td>
                    <td>
                      <Button
                        label={t("screen.plans.edit")}
                        size="sm"
                        kind="ghost"
                        onClick={() => edit(plan)}
                        testId={"admin-plans-edit-" + plan.code}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : config.token !== "" &&
          !catalogue.isPending &&
          !published.isPending &&
          !operatorRefused ? (
          <EmptyState
            title={t("screen.plans.emptyTitle")}
            body={t("screen.plans.emptyBody")}
            small
            testId="admin-plans-empty"
          />
        ) : null}
      </StatePanel>

      {/* ═══════════════════════ ٢ · إنشاءٌ أو تسعيرٌ أو نشر ═══════════════ */}
      <StatePanel
        title={t("screen.plans.formTitle")}
        note={t("screen.plans.formNote")}
        testId="admin-plans-form"
      >
        <div className="grid fields-3">
          <AdminField
            id="adm-pl-code"
            label={t("screen.plans.code")}
            hint={t("screen.plans.codeHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-code"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="admin-plans-code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
            />
          </AdminField>
          <AdminField
            id="adm-pl-name-ar"
            label={t("screen.plans.nameAr")}
            hint={t("screen.plans.nameArHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-name-ar"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-plans-name-ar"
              value={nameAr}
              onChange={(e) => setNameAr(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-pl-name-en"
            label={t("screen.plans.nameEn")}
            hint={t("screen.plans.nameEnHint")}
            source="typed"
          >
            <input
              id="adm-pl-name-en"
              className="ctl"
              dir="ltr"
              autoComplete="off"
              data-testid="admin-plans-name-en"
              value={nameEn}
              onChange={(e) => setNameEn(e.target.value)}
            />
          </AdminField>
        </div>

        <div className="grid fields-3">
          <AdminField
            id="adm-pl-monthly"
            label={t("screen.plans.monthly")}
            hint={t("screen.plans.monthlyHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-monthly"
              className="ctl mono"
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              data-testid="admin-plans-monthly"
              value={monthlyPrice}
              onChange={(e) => setMonthlyPrice(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-pl-per-user"
            label={t("screen.plans.perUser")}
            hint={t("screen.plans.perUserHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-per-user"
              className="ctl mono"
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              data-testid="admin-plans-per-user"
              value={perUserPrice}
              onChange={(e) => setPerUserPrice(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-pl-included"
            label={t("screen.plans.included")}
            hint={t("screen.plans.includedHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-included"
              className="ctl mono"
              dir="ltr"
              inputMode="numeric"
              autoComplete="off"
              data-testid="admin-plans-included"
              value={includedUsers}
              onChange={(e) => setIncludedUsers(e.target.value)}
            />
          </AdminField>
        </div>

        <fieldset className="stack" data-testid="admin-plans-modules">
          <legend>{t("screen.plans.modules")}</legend>
          <p className="hint">{t("screen.plans.modulesHint")}</p>
          {knownModules.length === 0 ? (
            <p className="muted" data-testid="admin-plans-modules-none">
              {t("screen.plans.modulesNone")}
            </p>
          ) : (
            <div className="grid fields-3">
              {knownModules.map((module) => (
                <label key={module} className="check" htmlFor={"adm-pl-module-" + module}>
                  <input
                    id={"adm-pl-module-" + module}
                    type="checkbox"
                    checked={picked.includes(module)}
                    data-testid={"admin-plans-module-" + module}
                    onChange={(e) => togglePicked(module, e.target.checked)}
                  />
                  <span className="mono" dir="ltr">
                    {module}
                  </span>
                </label>
              ))}
            </div>
          )}
        </fieldset>

        {/* خانةُ النشر خارج صفّ الحقول عمداً: عضوُ الصفّ يحمل مساراته الثلاثة —
            تسمية · تحكّم · وصف (ADR-0067) — وخانةٌ بلا وصفٍ تكسر إيقاعه. */}
        <label className="check" htmlFor="adm-pl-publish" data-testid="admin-plans-publish-row">
          <input
            id="adm-pl-publish"
            type="checkbox"
            checked={publish}
            data-testid="admin-plans-publish"
            onChange={(e) => setPublish(e.target.checked)}
          />
          <span>{t("screen.plans.publishLabel")}</span>
        </label>

        <div className="grid fields-3">
          <AdminField
            id="adm-pl-other-modules"
            label={t("screen.plans.otherModules")}
            hint={t("screen.plans.otherModulesHint")}
            source="typed"
          >
            <input
              id="adm-pl-other-modules"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="admin-plans-other-modules"
              value={otherModules}
              onChange={(e) => setOtherModules(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-pl-authority"
            label={t("screen.plans.authority")}
            hint={t("screen.plans.authorityHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-authority"
              className="ctl"
              autoComplete="off"
              data-testid="admin-plans-authority"
              value={authority}
              onChange={(e) => setAuthority(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-pl-reason"
            label={t("screen.plans.reason")}
            hint={t("screen.plans.reasonHint")}
            source="typed"
            required
          >
            <input
              id="adm-pl-reason"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-plans-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </AdminField>
        </div>

        <Irreversible
          title={existing ? t("screen.plans.askChangeTitle") : t("screen.plans.askCreateTitle")}
          effect={
            publish
              ? existing?.published
                ? t("screen.plans.effectRepriced")
                : t("screen.plans.effectPublished")
              : existing?.published
                ? t("screen.plans.effectWithdrawn")
                : t("screen.plans.effectDraft")
          }
          acknowledge={t("screen.plans.ack")}
          action={t("screen.plans.action")}
          busy={busy}
          {...(blocked ? { blocked } : {})}
          onConfirm={() => void doPut()}
          testId="admin-plans-confirm"
        >
          <ul className="adm-effects" data-testid="admin-plans-effects">
            <li>{t("screen.plans.effectLogged")}</li>
            <li>{t("screen.plans.effectNoDelete")}</li>
            <li>
              {t("screen.plans.effectModules")}{" "}
              <span className="mono" dir="ltr" data-testid="admin-plans-effect-modules">
                {modules.join(" · ")}
              </span>
            </li>
          </ul>
        </Irreversible>

        {written ? (
          <div className="alert alert--info" role="status" data-testid="admin-plans-written">
            <div className="body">
              <p>
                {t("screen.plans.written")}{" "}
                <span className="mono" dir="ltr" data-testid="admin-plans-written-code">
                  {written.code}
                </span>{" "}
                —{" "}
                <span data-testid="admin-plans-written-state">
                  {written.published ? t("screen.plans.published") : t("screen.plans.draft")}
                </span>
              </p>
            </div>
          </div>
        ) : null}

        {failure ? (
          <>
            <ProblemPanel error={failure} />
            <NextStep error={failure} testId="admin-plans-next" />
          </>
        ) : null}
      </StatePanel>

      <DeclaredGap
        title={t("screen.plans.gapTitle")}
        body={t("screen.plans.gapBody")}
        owed={t("screen.plans.gapOwed")}
        testId="admin-plans-gap"
      />
    </section>
  );
}
