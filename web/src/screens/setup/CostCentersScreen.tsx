/* ═══════════════════════════════════════════════════════════════════════════
   /setup/cost-centers — مراكز التكلفة  ·  The cost centres
   ───────────────────────────────────────────────────────────────────────────
   **بيتُها هنا لا تحت قسمٍ بعينه.** ‏`CostCenter` يعيش داخل `CompanySetup` في
   العقد المنشور، والأبواب الثلاثة كلّها تُعيد `CompanySetup` كاملاً؛ ومستهلكوها
   موزّعون على أقسامٍ أربعة — الموظف وفاتورة المصروف وأمر الشراء وقيد اليومية —
   فوضعُها تحت واحدٍ منها يجعل من لا يفتحه عاجزاً عن إنشاء مركز أصلاً
   ([ADR-0080](../../../../docs/decisions/ADR-0080-the-write-form-count-splits-the-screen.md) §7).

   وخمسةٌ تحكمها:

   ١ · **الرمز يسكّه الخادم ولا يُرسله العميل.** الرمز هوية تحملها سطور
       القيود، والاسم عرضٌ يتغيّر — فلا حقلَ رمزٍ في أيّ نموذجٍ هنا.

   ٢ · **إعادة التسمية لا تغيّر الرمز**، فسطور القيود المُرحَّلة تبقى مربوطة به
       وتُعرض بالاسم الجاري — وهو سلوك الحساب المعطَّل نفسه لا نمطٌ ثانٍ
       (ADR-0006).

   ٣ · **التعطيل حالةٌ تُقرأ لا غياب.** المُعلَّق **يبقى في الجدول بحالته**،
       ولا مرشّحَ افتراضيٍّ يُخفيه: ما يختفي يُظنّ محذوفاً. والمِصفاة هنا على
       النصّ وحده، والعدّان — العامل والموقوف — معروضان.

   ٤ · **والمركز الافتراضي لا يُوقَف.** يُرفض بـ409 و
       `cost_center.default_cannot_be_suspended`. فيُقال ذلك **قبل الضغط**
       وباسمه، لا برسالةٍ عامّة بعده.

   ٥ · **ولا حذف في هذا العقد أصلاً.** لا فعلَ حذفٍ على مركز تكلفة — والثابتة
       مفروضة بغياب العملية لا بفحصٍ عند مستدعٍ. فلا زرَّ حذفٍ هنا ولا يُخترَع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addCostCenter,
  readCompanySetup,
  renameCostCenter,
  suspendCostCenter,
} from "../../api/generated/client";
import type { CompanySetup, CostCenter, NameValue } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useLocale, useT } from "../../i18n/react";
import { Button, EmptyState, StatCard, useMoment } from "../../ui";
import {
  ChooseCompanyFirst,
  DeclaredGap,
  RecordName,
  SetupBadge,
  SetupField,
  SetupSectionNav,
  StatePanel,
  TranslationComposer,
} from "./parts";
import "./setup.css";

/** رمز الخادم حين لم تُؤسَّس المنشأة بعد. */
const NOT_FOUND_CODE = "company_setup.not_found";

/** رمز الخادم على محاولة إيقاف المركز الافتراضي. */
const DEFAULT_CANNOT_CODE = "cost_center.default_cannot_be_suspended";

/** رمز الخادم على مركزٍ موقوفٍ فعلاً. */
const ALREADY_SUSPENDED_CODE = "cost_center.already_suspended";

/** رمز الخادم على اسمٍ مكرَّر — ويُقال قبل الضغط لأن القائمة كلّها في اليد. */
const NAME_REPEATED_CODE = "cost_center.name_repeated";

/** رمز الخادم على سببٍ ناقص. */
const REASON_REQUIRED_CODE = "cost_center.suspension_reason_required";

/** أدنى طول لسبب الإيقاف كما تعلنه النواة — «لا سبب» ليس سبباً. */
const MINIMUM_REASON = 8;

/** الشاشة كاملةً. */
export function CostCentersScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const { locale } = useLocale();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const [filter, setFilter] = useState("");
  const [picked, setPicked] = useState("");

  /* ── نموذج الإضافة ────────────────────────────────────────────────── */
  const [addName, setAddName] = useState("");
  const [addTranslations, setAddTranslations] = useState<readonly NameValue[]>([]);
  const [addBusy, setAddBusy] = useState(false);
  const [addFailure, setAddFailure] = useState<unknown>(null);

  /* ── نموذج إعادة التسمية ──────────────────────────────────────────── */
  const [newName, setNewName] = useState("");
  const [newTranslations, setNewTranslations] = useState<readonly NameValue[]>([]);
  const [renameBusy, setRenameBusy] = useState(false);
  const [renameFailure, setRenameFailure] = useState<unknown>(null);

  /* ── نموذج الإيقاف ────────────────────────────────────────────────── */
  const [reason, setReason] = useState("");
  const [suspendBusy, setSuspendBusy] = useState(false);
  const [suspendFailure, setSuspendFailure] = useState<unknown>(null);

  const setup = useQuery({
    queryKey: ["setup", "company", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readCompanySetup(transport, { companyId: config.companyId }, signal),
  });

  const notFound =
    setup.isError && setup.error instanceof ProblemError && setup.error.code === NOT_FOUND_CODE;
  const current: CompanySetup | null = setup.data ?? null;
  const centres: readonly CostCenter[] = current?.costCenters ?? [];

  /* ــ المِصفاة على النصّ وحده، **ولا مرشّح حالةٍ افتراضي**: الموقوف يبقى
       ظاهراً بحالته، وما يختفي يُظنّ محذوفاً (القاعدة ٣ أعلاه). ــــــــــ */
  const shown = useMemo(() => {
    const needle = filter.trim().toLocaleLowerCase();
    if (needle === "") return centres;
    return centres.filter(
      (centre) =>
        centre.code.toLocaleLowerCase().includes(needle) ||
        centre.nameAr.includes(filter.trim()) ||
        centre.nameTranslations.some((n) => n.value.toLocaleLowerCase().includes(needle))
    );
  }, [centres, filter]);

  const chosen = centres.find((centre) => centre.code === picked) ?? null;

  /* ــ الرفض يُقال قبل الضغط، وباسم رمزه ــــــــــــــــــــــــــــــــ */
  const addRefusal =
    addName.trim() !== "" && centres.some((c) => c.nameAr === addName.trim())
      ? NAME_REPEATED_CODE
      : null;
  const renameRefusal =
    newName.trim() !== "" &&
    centres.some((c) => c.nameAr === newName.trim() && c.code !== picked)
      ? NAME_REPEATED_CODE
      : null;
  const suspendBlock =
    chosen === null
      ? null
      : chosen.isDefault
        ? DEFAULT_CANNOT_CODE
        : chosen.state === "Suspended"
          ? ALREADY_SUSPENDED_CODE
          : null;
  const reasonShort = reason.trim() !== "" && reason.trim().length < MINIMUM_REASON;

  const addReady = addName.trim() !== "" && addRefusal === null;
  const renameReady = chosen !== null && newName.trim() !== "" && renameRefusal === null;
  const suspendReady =
    chosen !== null && suspendBlock === null && reason.trim().length >= MINIMUM_REASON;

  const runAdd = useCallback(async () => {
    setAddBusy(true);
    setAddFailure(null);
    try {
      await addCostCenter(transport, {
        companyId: config.companyId,
        body: { nameAr: addName.trim(), nameTranslations: [...addTranslations] },
      });
      await setup.refetch();
      setAddName("");
      setAddTranslations([]);
      fireArrive();
    } catch (refused) {
      setAddFailure(refused);
      fireRefuse();
    } finally {
      setAddBusy(false);
    }
  }, [addName, addTranslations, config.companyId, fireArrive, fireRefuse, setup, transport]);

  const runRename = useCallback(async () => {
    setRenameBusy(true);
    setRenameFailure(null);
    try {
      await renameCostCenter(transport, {
        companyId: config.companyId,
        costCenterCode: picked,
        body: { nameAr: newName.trim(), nameTranslations: [...newTranslations] },
      });
      await setup.refetch();
      setNewName("");
      setNewTranslations([]);
      fireArrive();
    } catch (refused) {
      setRenameFailure(refused);
      fireRefuse();
    } finally {
      setRenameBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, newName, newTranslations, picked, setup, transport]);

  const runSuspend = useCallback(async () => {
    setSuspendBusy(true);
    setSuspendFailure(null);
    try {
      await suspendCostCenter(transport, {
        companyId: config.companyId,
        costCenterCode: picked,
        body: { reason: reason.trim() },
      });
      await setup.refetch();
      setReason("");
      fireArrive();
    } catch (refused) {
      setSuspendFailure(refused);
      fireRefuse();
    } finally {
      setSuspendBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, picked, reason, setup, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-cc-needs-company" />;

  return (
    <section className="stack" data-testid="setup-cost-centers-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.costCenter.pageTitle")}</h1>
          <p className="sub">{t("screen.costCenter.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup/cost-centers" />

      {/* ═════════════════════ ١ · السجلّ — العامل والموقوف معاً ═════════ */}
      <StatePanel
        title={t("screen.costCenter.registerTitle")}
        note={t("screen.costCenter.registerNote")}
        loading={setup.isPending && setup.fetchStatus === "fetching"}
        testId="setup-cc-register"
      >
        {notFound ? (
          <EmptyState
            title={t("screen.costCenter.unfoundedTitle")}
            body={t("screen.costCenter.unfoundedBody")}
            testId="setup-cc-unfounded"
          />
        ) : setup.isError ? (
          <ProblemPanel error={setup.error} onRetry={() => void setup.refetch()} />
        ) : current === null ? null : (
          <div className={"stack " + arriveCls}>
            <div className="stats-row">
              <StatCard
                label={t("screen.costCenter.activeCount")}
                count={centres.filter((c) => c.state === "Active").length}
                hint={t("screen.costCenter.activeCountHint")}
                tone="good"
                testId="setup-cc-active-count"
              />
              <StatCard
                label={t("screen.costCenter.suspendedCount")}
                count={centres.filter((c) => c.state === "Suspended").length}
                hint={t("screen.costCenter.suspendedCountHint")}
                testId="setup-cc-suspended-count"
              />
            </div>

            <div className="grid fields-half">
              <SetupField
                id="stp-cc-filter"
                label={t("screen.costCenter.filterLabel")}
                hint={t("screen.costCenter.filterHint")}
                source="typed"
              >
                <input
                  id="stp-cc-filter"
                  className="ctl"
                  autoComplete="off"
                  data-testid="setup-cc-filter"
                  value={filter}
                  onChange={(e) => setFilter(e.target.value)}
                />
              </SetupField>
              <SetupField
                id="stp-cc-picked"
                label={t("screen.costCenter.pickedLabel")}
                hint={t("screen.costCenter.pickedHint")}
                source="typed"
              >
                <select
                  id="stp-cc-picked"
                  className="ctl mono"
                  dir="ltr"
                  data-testid="setup-cc-picked"
                  value={picked}
                  onChange={(e) => {
                    setPicked(e.target.value);
                    setNewName("");
                    setNewTranslations([]);
                    setReason("");
                    setRenameFailure(null);
                    setSuspendFailure(null);
                  }}
                >
                  <option value="">{t("screen.costCenter.pickNone")}</option>
                  {centres.map((centre) => (
                    <option key={centre.code} value={centre.code}>
                      {centre.code}
                    </option>
                  ))}
                </select>
              </SetupField>
            </div>

            {shown.length === 0 ? (
              <EmptyState
                title={t("screen.costCenter.noMatchTitle")}
                body={t("screen.costCenter.noMatchBody")}
                small
                testId="setup-cc-no-match"
              />
            ) : (
              <div className="tablewrap" data-testid="setup-cc-table">
                <table className="data">
                  <caption className="visually-hidden">{t("screen.costCenter.registerTitle")}</caption>
                  <thead>
                    <tr>
                      <th scope="col" className="start">{t("screen.costCenter.colCode")}</th>
                      <th scope="col" className="start">{t("screen.costCenter.colName")}</th>
                      <th scope="col" className="start">{t("screen.costCenter.colState")}</th>
                      <th scope="col" className="start">{t("screen.costCenter.colReason")}</th>
                      <th scope="col" className="start">{t("screen.costCenter.colPick")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {shown.map((centre) => (
                      <tr
                        key={centre.code}
                        data-testid={"setup-cc-row-" + centre.code}
                        data-state={centre.state}
                        aria-selected={centre.code === picked}
                      >
                        <td className="start">
                          <span className="mono" dir="ltr">{centre.code}</span>
                        </td>
                        <td className="start">
                          <RecordName
                            nameAr={centre.nameAr}
                            translations={centre.nameTranslations}
                            locale={locale}
                          />
                        </td>
                        <td className="start">
                          <SetupBadge
                            label={t("screen.costCenter.state." + centre.state)}
                            tone={centre.state === "Active" ? "posted" : "archived"}
                            testId={"setup-cc-state-" + centre.code}
                          />
                          {centre.isDefault ? (
                            <>
                              {" "}
                              <SetupBadge
                                label={t("screen.costCenter.isDefault")}
                                tone="info"
                                title={t("screen.costCenter.isDefaultTitle")}
                                testId={"setup-cc-default-" + centre.code}
                              />
                            </>
                          ) : null}
                        </td>
                        <td className="start">
                          {centre.suspensionReason === "" ? (
                            <span className="muted">{t("screen.costCenter.noReason")}</span>
                          ) : (
                            <span data-testid={"setup-cc-reason-" + centre.code}>
                              {centre.suspensionReason}
                            </span>
                          )}
                        </td>
                        <td className="start">
                          <Button
                            label={t("screen.costCenter.pick")}
                            kind="ghost"
                            size="sm"
                            onClick={() => {
                              setPicked(centre.code);
                              setNewName("");
                              setNewTranslations([]);
                              setReason("");
                              setRenameFailure(null);
                              setSuspendFailure(null);
                            }}
                            testId={"setup-cc-pick-" + centre.code}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </StatePanel>

      {/* ══════════════════════════════ ٢ · إضافة مركز ═══════════════════ */}
      <StatePanel
        title={t("screen.costCenter.addTitle")}
        note={t("screen.costCenter.addNote")}
        testId="setup-cc-add"
      >
        <div className="stack">
          <div className="grid fields-half">
            <SetupField
              id="stp-cc-add-name"
              label={t("screen.costCenter.nameArLabel")}
              hint={t("screen.costCenter.nameArHint")}
              {...(addRefusal ? { error: t("screen.costCenter.nameRepeated") } : {})}
              source="typed"
              required
            >
              <input
                id="stp-cc-add-name"
                className="ctl"
                lang="ar"
                dir="rtl"
                autoComplete="off"
                aria-invalid={addRefusal !== null}
                data-testid="setup-cc-add-name"
                value={addName}
                onChange={(e) => setAddName(e.target.value)}
              />
            </SetupField>
            <div className="rowctl">
              <Button
                label={t("screen.costCenter.add")}
                kind="primary"
                loading={addBusy}
                disabled={!addReady || addBusy}
                onClick={() => void runAdd()}
                testId="setup-cc-add-go"
              />
              <span className="hint">{t("screen.costCenter.addButtonHint")}</span>
            </div>
          </div>
          <TranslationComposer
            idPrefix="stp-cc-add-tr"
            testId="setup-cc-add-translations"
            value={addTranslations}
            onChange={setAddTranslations}
          />
          {addFailure ? <ProblemPanel error={addFailure} /> : null}
        </div>
      </StatePanel>

      {/* ═══════════ ٣ · إعادة التسمية والإيقاف — على المركز المختار ═════ */}
      <StatePanel
        title={t("screen.costCenter.changeTitle")}
        note={t("screen.costCenter.changeNote")}
        aside={
          chosen ? (
            <span className="mono" dir="ltr" data-testid="setup-cc-chosen">{chosen.code}</span>
          ) : null
        }
        testId="setup-cc-change"
      >
        {chosen === null ? (
          <EmptyState
            title={t("screen.costCenter.pickFirstTitle")}
            body={t("screen.costCenter.pickFirstBody")}
            testId="setup-cc-pick-first"
          />
        ) : (
          <div className="stack">
            <div className="grid fields-half">
              <SetupField
                id="stp-cc-new-name"
                label={t("screen.costCenter.newNameLabel")}
                hint={t("screen.costCenter.newNameHint")}
                {...(renameRefusal ? { error: t("screen.costCenter.nameRepeated") } : {})}
                source="typed"
                required
              >
                <input
                  id="stp-cc-new-name"
                  className="ctl"
                  lang="ar"
                  dir="rtl"
                  autoComplete="off"
                  aria-invalid={renameRefusal !== null}
                  data-testid="setup-cc-new-name"
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                />
              </SetupField>
              <div className="rowctl">
                <Button
                  label={t("screen.costCenter.rename")}
                  loading={renameBusy}
                  disabled={!renameReady || renameBusy}
                  onClick={() => void runRename()}
                  testId="setup-cc-rename-go"
                />
                <span className="hint">{t("screen.costCenter.renameButtonHint")}</span>
              </div>
            </div>
            <TranslationComposer
              idPrefix="stp-cc-new-tr"
              testId="setup-cc-new-translations"
              value={newTranslations}
              onChange={setNewTranslations}
            />
            {renameFailure ? <ProblemPanel error={renameFailure} /> : null}

            <div className="grid fields-half">
              <SetupField
                id="stp-cc-reason"
                label={t("screen.costCenter.reasonLabel")}
                hint={t("screen.costCenter.reasonHint")}
                {...(reasonShort ? { error: t("screen.costCenter.reasonShort") } : {})}
                source="typed"
                required
              >
                <input
                  id="stp-cc-reason"
                  className="ctl"
                  dir="auto"
                  autoComplete="off"
                  aria-invalid={reasonShort}
                  data-testid="setup-cc-reason"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </SetupField>
              <div className="rowctl">
                <Button
                  label={t("screen.costCenter.suspend")}
                  kind="danger"
                  loading={suspendBusy}
                  disabled={!suspendReady || suspendBusy}
                  onClick={() => void runSuspend()}
                  testId="setup-cc-suspend-go"
                />
                <span className="hint">
                  {suspendBlock === null
                    ? t("screen.costCenter.suspendButtonHint")
                    : t("screen.costCenter.blocked." + (suspendBlock === DEFAULT_CANNOT_CODE ? "isDefault" : "already"))}
                </span>
              </div>
            </div>

            {suspendBlock ? (
              <div
                className="alert alert--info"
                role="status"
                data-testid="setup-cc-suspend-blocked"
                data-code={suspendBlock}
              >
                <div className="body">
                  <span className="title">
                    {suspendBlock === DEFAULT_CANNOT_CODE
                      ? t("screen.costCenter.defaultBlockedTitle")
                      : t("screen.costCenter.alreadyBlockedTitle")}
                  </span>
                  <p>
                    {suspendBlock === DEFAULT_CANNOT_CODE
                      ? t("screen.costCenter.defaultBlockedBody")
                      : t("screen.costCenter.alreadyBlockedBody")}{" "}
                    <span className="mono" dir="ltr">{suspendBlock}</span>
                  </p>
                </div>
              </div>
            ) : (
              <p className="hint" data-testid="setup-cc-reason-note">
                {t("screen.costCenter.reasonRecorded")}{" "}
                <span className="mono" dir="ltr">{REASON_REQUIRED_CODE}</span>
              </p>
            )}
            {suspendFailure ? <ProblemPanel error={suspendFailure} /> : null}
          </div>
        )}
      </StatePanel>

      {/* ═════════════════ ٤ · بابان ينقصان — مُعلَنان لا مسكوتٌ عنهما ═══ */}
      <DeclaredGap
        title={t("screen.costCenter.gapReinstateTitle")}
        body={t("screen.costCenter.gapReinstateBody")}
        owed={t("screen.costCenter.gapReinstateOwed")}
        testId="setup-cc-gap-reinstate"
      />
      <DeclaredGap
        title={t("screen.costCenter.gapDefaultTitle")}
        body={t("screen.costCenter.gapDefaultBody")}
        owed={t("screen.costCenter.gapDefaultOwed")}
        testId="setup-cc-gap-default"
      />
    </section>
  );
}
