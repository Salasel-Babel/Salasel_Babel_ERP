/* ═══════════════════════════════════════════════════════════════════════════
   /setup/parameters — معامِلاتُ المنشأة وقائمةُ المراجعة  ·  Parameters
   ───────────────────────────────────────────────────────────────────────────
   وخمسةٌ تحكمها:

   ١ · **حالةُ الاعتماد تُعرَض دائماً، وغيرُ المعتمَدة تُرى موسومةً لا مخفيّة.**
       افتراضُ المنصّة رقمٌ يعمل به النظام **ولم يعتمده إنسان**؛ وإخفاؤه يجعل
       المنشأة تُرحّل به وهي لا تعرف أنه مفترَض. فالشارة على كل صفّ، وعدّادُ
       «غير المعتمَد» في صدر الشاشة.

   ٢ · **ولا رقمَ ولا رمزَ مجموعةٍ مكتوبٌ في هذا الملفّ.** المجموعاتُ ومفاتيحُها
       تُشتقّ من الإصدارات التي ردّها الخادم — ومنها افتراضاتُ المنصّة المشحونة،
       وهي موجودةٌ دائماً. وشاشةٌ تكتب رمزاً أو قيمةً تصير مصدرَ حقيقةٍ ثانياً
       ينحرف عن الأول ولا يُظهره شيء.

   ٣ · **ولا تعديلَ ولا حذف — لأن العقد لا يحملهما أصلاً.** الإصدار يُضاف
       ولا يُعدَّل، والتغييرُ إصدارٌ جديد بتاريخ سريانه. فلا زرَّ حذفٍ هنا ولا
       يُخترَع، والثابتة مفروضةٌ بغياب العملية لا بامتناع الشاشة.

   ٤ · **والنسبةُ كسرٌ عشري لا مئوية**، والشاشة تقول ذلك **قبل الضغط**: حقلُ
       القيمة يحمل وصفه، ويُعرض رفضُ الخادم `core.parameter_rate_looks_like_a_percentage`
       باسمه إن وصل. ولا ضربَ في مئة هنا ولا علامة `%`: الضربُ حسابٌ على قيمةٍ
       مالية الأثر، وعلامةٌ على كسرٍ تجعل «0.15» تُقرأ خمس عشرة بالمئة من واحد.

   ٥ · **وقائمةُ المراجعة تُقرأ من بابها لا تُحسَب هنا.** الشاشة لا تجمع
       الإصدارات بالمستندات: `readParameterReview` باب قراءةٍ منشور يفعل ذلك
       باستعلامٍ واحد — وتقريرٌ تحسبه شاشةٌ يعني أن كلّ شاشةٍ تحسبه بطريقتها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { depositParameterVersion, listParameterVersions, readParameterReview } from "../../api/generated/client";
import { asParameterAmount } from "../../api/generated/brands";
import type {
  ParameterVersion,
  ParameterVersionList,
  ParameterVersionRequest,
  ParameterReviewList,
} from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Decimal } from "../../i18n/react";
import { Button, EmptyState, StatCard, useMoment } from "../../ui";
import {
  ChooseCompanyFirst,
  SetupBadge,
  SetupField,
  SetupSectionNav,
  StatePanel,
} from "./parts";
import "./setup.css";

/** حالةُ الاعتماد كما ينشرها العقد — مجموعةٌ مغلقة تُقرأ ولا تُخترع. */
type Approval = ParameterVersion["approval"];

/**
 * الحالاتُ التي يقبلها بابُ الإيداع — <b>وهي نوعٌ أضيق يعلنه العقد نفسه</b>.
 * <p>
 * فلا تستطيع هذه الشاشة أن ترسل «افتراضَ منصّة» ولو أرادت: المترجِم يرفضه قبل
 * أن يرفضه الخادم. وهذا هو الفرق بين قيدٍ مفروضٍ وقيدٍ موصوف.
 * </p>
 */
type DepositableApproval = ParameterVersionRequest["approval"];

/** وهي معدودةٌ هنا مرّةً — والترتيب هو ترتيب القائمة على الشاشة. */
const DEPOSITABLE: readonly DepositableApproval[] = ["tenant_approved", "auditor_signed"];

/**
 * مفتاحُ المورد لحالةٍ وصلت من السلك.
 * <p>
 * ورمزُ السلك <code>snake_case</code> ومفاتيحُ الموارد <code>camelCase</code>،
 * والوصلُ بينهما <b>جدولٌ صريح</b> لا تحويلٌ نصّي: تحويلٌ يبني مفتاحاً من قيمةٍ
 * يجعل قيمةً جديدة على السلك تُنتج مفتاحاً غير موجود فتُعرض على الشاشة كما هي.
 * </p>
 * @param approval حالة الاعتماد كما وصلت.
 */
function approvalKey(approval: Approval | DepositableApproval): string {
  if (approval === "auditor_signed") return "auditorSigned";
  if (approval === "tenant_approved") return "tenantApproved";
  return "platformDefault";
}

/** لونُ الشارة لكلّ حالة — والافتراضُ غيرُ المعتمَد يُرى، ولا يُخفى. */
function toneOf(approval: Approval): "pending" | "posted" | "info" {
  if (approval === "auditor_signed") return "posted";
  if (approval === "tenant_approved") return "info";
  return "pending";
}

/** الشاشة كاملةً. */
export function ParametersScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const [setCode, setSetCode] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState("");
  const [approval, setApproval] = useState<DepositableApproval>("tenant_approved");
  const [approvedBy, setApprovedBy] = useState("");
  const [approvedOn, setApprovedOn] = useState("");
  const [sourceRef, setSourceRef] = useState("");
  const [values, setValues] = useState<Readonly<Record<string, string>>>({});
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);

  const versions = useQuery({
    queryKey: ["setup", "parameters", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      listParameterVersions(transport, { companyId: config.companyId }, signal),
  });

  const review = useQuery({
    queryKey: ["setup", "parameter-review", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readParameterReview(transport, { companyId: config.companyId }, signal),
  });

  const read: ParameterVersionList | null = versions.data ?? null;
  const audit: ParameterReviewList | null = review.data ?? null;

  /* ــ المجموعاتُ كما يقولها الخادم — ولا واحدةٌ مكتوبة هنا ــــــــــــــ */
  const sets = useMemo(() => {
    const seen = new Set<string>();
    for (const version of read?.items ?? []) seen.add(version.setCode);
    return [...seen].sort();
  }, [read]);

  /* ــ ومفاتيحُ المجموعة المختارة من أيّ إصدارٍ لها: الإيداعُ يطلبها كلَّها ــ */
  const keys = useMemo(() => {
    const found = (read?.items ?? []).find((version) => version.setCode === setCode);
    if (!found) return [] as readonly { key: string; kind: string }[];
    return found.values.map((value) => ({ key: value.key, kind: value.kind }));
  }, [read, setCode]);

  const unapproved = (read?.items ?? []).filter((v) => v.approval === "platform_default").length;
  const signed = (read?.items ?? []).filter((v) => v.approval === "auditor_signed").length;

  const ready =
    setCode !== "" &&
    effectiveFrom !== "" &&
    approvedBy.trim() !== "" &&
    approvedOn !== "" &&
    sourceRef.trim() !== "" &&
    keys.length > 0 &&
    keys.every((entry) => (values[entry.key] ?? "").trim() !== "");

  const runDeposit = useCallback(async () => {
    if (!ready) return;
    setBusy(true);
    setFailure(null);
    try {
      await depositParameterVersion(transport, {
        companyId: config.companyId,
        body: {
          setCode,
          effectiveFrom,
          approval,
          approvedBy: approvedBy.trim(),
          approvedOn,
          sourceRef: sourceRef.trim(),
          /* **الوسمُ يقع هنا لا في الطبقة**: `asParameterAmount` يفحص النصّ بالنمط
             المنشور في العقد، فقيمةٌ لا تطابقه تُرفض **قبل أن تُرسَل** — لا بعد
             رحلةٍ إلى الخادم ورسالةٍ عامّة. */
          values: keys.map((entry) => ({
            key: entry.key,
            value: asParameterAmount((values[entry.key] ?? "").trim()),
          })),
        },
      });
      await versions.refetch();
      await review.refetch();
      setValues({});
      setSourceRef("");
      fireArrive();
    } catch (refused) {
      /* ورفضُ الوسم يُعرض كما يُعرض رفضُ الخادم: كلاهما «طلبٌ لا يُرسَل كما هو». */
      setFailure(refused);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [
    approval,
    approvedBy,
    approvedOn,
    config.companyId,
    effectiveFrom,
    fireArrive,
    fireRefuse,
    keys,
    ready,
    review,
    setCode,
    sourceRef,
    transport,
    values,
    versions,
  ]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-parameters-needs-company" />;

  return (
    <section className="stack" data-testid="setup-parameters-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.parameters.pageTitle")}</h1>
          <p className="sub">{t("screen.parameters.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup/parameters" />

      {/* ═══════════════════ ١ · العدّادات — وغيرُ المعتمَد أوّلها ═══════ */}
      <div className="stats-row">
        <StatCard
          label={t("screen.parameters.countAll")}
          count={read?.itemCount ?? 0}
          hint={t("screen.parameters.countAllHint")}
          testId="setup-parameters-count-all"
        />
        <StatCard
          label={t("screen.parameters.countUnapproved")}
          count={unapproved}
          hint={t("screen.parameters.countUnapprovedHint")}
          testId="setup-parameters-count-unapproved"
        />
        <StatCard
          label={t("screen.parameters.countSigned")}
          count={signed}
          hint={t("screen.parameters.countSignedHint")}
          testId="setup-parameters-count-signed"
        />
      </div>

      {/* ═══════════════════ ٢ · السجلّ — كلُّ إصدارٍ بحالة اعتماده ═════ */}
      <StatePanel
        title={t("screen.parameters.registerTitle")}
        note={t("screen.parameters.registerNote")}
        loading={versions.isPending && versions.fetchStatus === "fetching"}
        testId="setup-parameters-register"
      >
        {versions.isError ? (
          <ProblemPanel error={versions.error} onRetry={() => void versions.refetch()} />
        ) : read === null ? null : read.items.length === 0 ? (
          <EmptyState
            title={t("screen.parameters.emptyTitle")}
            body={t("screen.parameters.emptyBody")}
            testId="setup-parameters-empty"
          />
        ) : (
          <div className={"tablewrap " + arriveCls} data-testid="setup-parameters-table">
            <table className="data">
              <caption className="visually-hidden">{t("screen.parameters.registerTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col" className="start">{t("screen.parameters.colSet")}</th>
                  <th scope="col" className="start">{t("screen.parameters.colEffectiveFrom")}</th>
                  <th scope="col" className="start">{t("screen.parameters.colApproval")}</th>
                  <th scope="col" className="start">{t("screen.parameters.colValues")}</th>
                  <th scope="col" className="start">{t("screen.parameters.colApprovedBy")}</th>
                  <th scope="col" className="start">{t("screen.parameters.colSource")}</th>
                </tr>
              </thead>
              <tbody>
                {read.items.map((version) => (
                  <tr
                    key={version.id}
                    data-testid={"setup-parameters-row-" + version.id}
                    data-approval={version.approval}
                  >
                    <td className="start">
                      <span className="mono" dir="ltr">{version.setCode}</span>
                    </td>
                    <td className="start">
                      <span className="mono" dir="ltr">{version.effectiveFrom}</span>
                    </td>
                    <td className="start">
                      <SetupBadge
                        label={t("screen.parameters.approval." + approvalKey(version.approval))}
                        tone={toneOf(version.approval)}
                        title={t("screen.parameters.approvalTitle." + approvalKey(version.approval))}
                        testId={"setup-parameters-approval-" + version.id}
                      />{" "}
                      <SetupBadge
                        label={t("screen.parameters.scope." + version.scope)}
                        tone="info"
                        testId={"setup-parameters-scope-" + version.id}
                      />
                    </td>
                    <td className="start num">
                      <ul className="stp-tags">
                        {version.values.map((value) => (
                          <li key={value.key}>
                            <span className="mono" dir="ltr">{value.key}</span>{" "}
                            <span className="rate" data-testid={"setup-parameters-value-" + version.id + "-" + value.key}>
                              <Decimal value={value.value} />
                            </span>
                          </li>
                        ))}
                      </ul>
                    </td>
                    <td className="start">
                      {version.approvedBy === "" ? (
                        <span className="muted" data-testid={"setup-parameters-noapprover-" + version.id}>
                          {t("screen.parameters.noApprover")}
                        </span>
                      ) : (
                        <span>
                          {version.approvedBy}
                          {version.approvedOn === "" ? null : (
                            <>
                              {" "}
                              <span className="mono" dir="ltr">{version.approvedOn}</span>
                            </>
                          )}
                        </span>
                      )}
                    </td>
                    <td className="start">{version.sourceRef}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═══════════════════ ٣ · الإيداع — إصدارٌ جديد لا تعديل ════════ */}
      <StatePanel
        title={t("screen.parameters.depositTitle")}
        note={t("screen.parameters.depositNote")}
        testId="setup-parameters-deposit"
      >
        <div className="stack">
          <div className="grid fields-half">
            <SetupField
              id="stp-prm-set"
              label={t("screen.parameters.setLabel")}
              hint={t("screen.parameters.setHint")}
              required
            >
              <select
                id="stp-prm-set"
                className="ctl"
                value={setCode}
                data-testid="setup-parameters-set"
                onChange={(e) => {
                  setSetCode(e.target.value);
                  setValues({});
                }}
              >
                <option value="">{t("screen.parameters.setNone")}</option>
                {sets.map((code) => (
                  <option key={code} value={code}>
                    {code}
                  </option>
                ))}
              </select>
            </SetupField>

            <SetupField
              id="stp-prm-effective"
              label={t("screen.parameters.effectiveFromLabel")}
              hint={t("screen.parameters.effectiveFromHint")}
              required
            >
              <input
                id="stp-prm-effective"
                className="ctl"
                type="date"
                dir="ltr"
                value={effectiveFrom}
                data-testid="setup-parameters-effective"
                onChange={(e) => setEffectiveFrom(e.target.value)}
              />
            </SetupField>
          </div>

          <div className="grid fields-half">
            <SetupField
              id="stp-prm-approval"
              label={t("screen.parameters.approvalLabel")}
              hint={t("screen.parameters.approvalHint")}
              required
            >
              <select
                id="stp-prm-approval"
                className="ctl"
                value={approval}
                data-testid="setup-parameters-approval-choice"
                onChange={(e) => setApproval(e.target.value as DepositableApproval)}
              >
                {DEPOSITABLE.map((state) => (
                  <option key={state} value={state}>
                    {t("screen.parameters.approval." + approvalKey(state))}
                  </option>
                ))}
              </select>
            </SetupField>

            <SetupField
              id="stp-prm-approved-by"
              label={t("screen.parameters.approvedByLabel")}
              hint={t("screen.parameters.approvedByHint")}
              required
            >
              <input
                id="stp-prm-approved-by"
                className="ctl"
                type="text"
                value={approvedBy}
                data-testid="setup-parameters-approved-by"
                onChange={(e) => setApprovedBy(e.target.value)}
              />
            </SetupField>
          </div>

          <div className="grid fields-half">
            <SetupField
              id="stp-prm-approved-on"
              label={t("screen.parameters.approvedOnLabel")}
              hint={t("screen.parameters.approvedOnHint")}
              required
            >
              <input
                id="stp-prm-approved-on"
                className="ctl"
                type="date"
                dir="ltr"
                value={approvedOn}
                data-testid="setup-parameters-approved-on"
                onChange={(e) => setApprovedOn(e.target.value)}
              />
            </SetupField>

            <SetupField
              id="stp-prm-source"
              label={t("screen.parameters.sourceLabel")}
              hint={t("screen.parameters.sourceHint")}
              required
            >
              <input
                id="stp-prm-source"
                className="ctl"
                type="text"
                value={sourceRef}
                data-testid="setup-parameters-source"
                onChange={(e) => setSourceRef(e.target.value)}
              />
            </SetupField>
          </div>

          {setCode === "" ? (
            <p className="muted" data-testid="setup-parameters-pick-set">
              {t("screen.parameters.pickSetFirst")}
            </p>
          ) : (
            <div className="grid fields-half">
              {keys.map((entry) => (
                <SetupField
                  key={entry.key}
                  id={"stp-prm-value-" + entry.key}
                  label={entry.key}
                  hint={t("screen.parameters.valueHint." + entry.kind)}
                  required
                >
                  <input
                    id={"stp-prm-value-" + entry.key}
                    className="ctl"
                    type="text"
                    inputMode="decimal"
                    dir="ltr"
                    value={values[entry.key] ?? ""}
                    data-testid={"setup-parameters-input-" + entry.key}
                    onChange={(e) => setValues({ ...values, [entry.key]: e.target.value })}
                  />
                </SetupField>
              ))}
            </div>
          )}

          {failure === null ? null : (
            <ProblemPanel error={failure} />
          )}

          <Button
            label={t("screen.parameters.depositAction")}
            kind="primary"
            disabled={!ready}
            loading={busy}
            onClick={() => void runDeposit()}
            testId="setup-parameters-submit"
          />
        </div>
      </StatePanel>

      {/* ═══════════════════ ٤ · قائمةُ مراجعة المحاسب — من بابها ══════ */}
      <StatePanel
        title={t("screen.parameters.reviewTitle")}
        note={t("screen.parameters.reviewNote")}
        loading={review.isPending && review.fetchStatus === "fetching"}
        testId="setup-parameters-review"
      >
        {review.isError ? (
          <ProblemPanel error={review.error} onRetry={() => void review.refetch()} />
        ) : audit === null ? null : audit.items.length === 0 ? (
          <EmptyState
            title={t("screen.parameters.reviewEmptyTitle")}
            body={t("screen.parameters.reviewEmptyBody")}
            small
            testId="setup-parameters-review-empty"
          />
        ) : (
          <div className="stack">
            {audit.items.map((entry) => (
              <div key={entry.version.id} data-testid={"setup-parameters-review-row-" + entry.version.id}>
                <p className="k">
                  <span className="mono" dir="ltr">{entry.version.setCode}</span>{" "}
                  <span className="mono" dir="ltr">{entry.version.effectiveFrom}</span>{" "}
                  <SetupBadge
                    label={t("screen.parameters.approval." + approvalKey(entry.version.approval))}
                    tone={toneOf(entry.version.approval)}
                    testId={"setup-parameters-review-approval-" + entry.version.id}
                  />
                </p>
                {entry.usageCount === 0 ? (
                  <p className="muted" data-testid={"setup-parameters-unused-" + entry.version.id}>
                    {t("screen.parameters.notUsedYet")}
                  </p>
                ) : (
                  <ul className="stp-tags">
                    {entry.usages.map((usage) => (
                      <li key={usage.module + "/" + usage.documentType + "/" + usage.documentId}>
                        <span className="mono" dir="ltr">
                          {usage.module}/{usage.documentType}
                        </span>{" "}
                        <span className="mono" dir="ltr">{usage.postedOn}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        )}
      </StatePanel>
    </section>
  );
}
