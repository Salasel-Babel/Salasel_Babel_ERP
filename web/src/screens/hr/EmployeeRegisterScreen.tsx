/* ═══════════════════════════════════════════════════════════════════════════
   /hr — سجلّ الموظفين  ·  The employee register
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة تُبنى على قاعدةٍ واحدة تسبق كل شيء آخر فيها: القناع.**

   ما يُعيده السطح المنشور عن الموظف هو: معرّفه، ورمزه المعتم، واسمه، وتصنيفه،
   ومركز تكلفته، وعلاقة عمله، **وقناعَي هويته**. ولا يُعيد رقم الهوية ولا
   الآيبان — لا مقروءَين ولا مشفّرَين ولا خلف صلاحية. فالواجهة **لا تملك ما
   تكشفه**، ولا تستطيع أن تعيد تركيبه من القناع، ولا يوجد في العقد بابٌ يطلبه.
   وزرُّ «اعرض غير مقنَّع» لا يظهر هنا لأنه لا يستطيع أن يوجد؛ ولو أراده المالك
   لكان **فعلاً على الخادم**: مُصرَّحاً به، ومُدقَّقاً، ومسجَّلاً باسم من طلبه —
   لا اختياراً في متصفّح.

   والموضع الوحيد الذي تلمس فيه هذه الشاشة الهوية **غير مقنَّعة** هو نموذج
   التسجيل: الرقم يُكتب مرّةً ويُرسَل ويُمحى من الحقل فوراً. ولا يُعاد عرضه، ولا
   يُحفَظ في المتصفّح، ولا يدخل حالةً تبقى بعد الاستجابة.

   ── وما لا تستطيع هذه الشاشة أن تفعله، مُعلَناً لا مسكوتاً عنه ────────────
   **لا سردَ للموظفين.** العقد المنشور فيه `POST /employees` و
   `GET /employees/{employeeId}` — **ولا باب سرد**. فالسجلّ هنا **بحثٌ بمعرّف**
   لا تصفّح، وقائمةٌ مخترعة من بياناتٍ في المتصفّح كانت ستكذب على من يبني
   عليها قراراً. والقرار على المالك.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addPayElement,
  listPayComponents,
  listPayElements,
  readEmployee,
  registerEmployee,
  terminateEmployee,
} from "../../api/generated/client";
import type { HrEmployee, HrPayElement, NameValue } from "../../api/generated/types";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useLocale, useT, Amount } from "../../i18n/react";
import { SOURCE } from "../../i18n/engine";
import { Button, EmptyState, Field, MOTION, Panel, useMoment } from "../../ui";
import { useHrFocus } from "./focus";
import {
  ChooseCompanyFirst,
  HrSectionNav,
  DeclaredGap,
  HrState,
  MaskedIdentityPanel,
  OpaqueCode,
  StatePanel,
  TranslatedName,
  isMoneyText,
  todayIso,
} from "./parts";
import { ACTIVE } from "./contract";
import "./hr.css";

/** ما يُكتب في نموذج التسجيل — **والهوية فيه عابرة تُمحى بعد الإرسال**. */
interface DraftEmployee {
  nameAr: string;
  translations: Record<string, string>;
  classCode: string;
  costCenterId: string;
  hiredOn: string;
  nationalId: string;
  iban: string;
  birthDate: string;
}

function emptyDraft(): DraftEmployee {
  return {
    nameAr: "",
    translations: {},
    classCode: "",
    costCenterId: "",
    hiredOn: todayIso(),
    nationalId: "",
    iban: "",
    birthDate: "",
  };
}

/** الشاشة كاملةً. */
export function EmployeeRegisterScreen(): ReactNode {
  const { t, tp } = useT();
  const { i18n } = useLocale();
  const { transport, config } = useApi();
  const [focus, setFocus] = useHrFocus();

  const [typedId, setTypedId] = useState(focus.employeeId);
  const [lookupId, setLookupId] = useState(focus.employeeId);
  const [arrived, fireArrived] = useMoment("arrive");

  /* ما تكتبه الشاشة: كلٌّ بحالته وخطئه، فلا يبتلع رفضُ نموذجٍ رفضَ الآخر. */
  const [draft, setDraft] = useState<DraftEmployee>(emptyDraft);
  const [registered, setRegistered] = useState<HrEmployee | null>(null);
  const [registerError, setRegisterError] = useState<unknown>(null);
  const [registerBusy, setRegisterBusy] = useState(false);

  const [elementCode, setElementCode] = useState("");
  const [elementFrom, setElementFrom] = useState(todayIso);
  const [elementAmount, setElementAmount] = useState("");
  const [elementError, setElementError] = useState<unknown>(null);
  const [elementBusy, setElementBusy] = useState(false);

  const [endedOn, setEndedOn] = useState(todayIso);
  const [reasonKey, setReasonKey] = useState("");
  const [terminateError, setTerminateError] = useState<unknown>(null);
  const [terminateBusy, setTerminateBusy] = useState(false);
  const [terminated, setTerminated] = useState<HrEmployee | null>(null);

  const employee = useQuery({
    queryKey: ["hr", "employee", config.baseUrl, config.token, config.companyId, lookupId],
    enabled: config.companyId !== "" && lookupId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readEmployee(transport, { companyId: config.companyId, employeeId: lookupId }, signal),
  });

  const elements = useQuery({
    queryKey: ["hr", "pay-elements", config.baseUrl, config.token, config.companyId, lookupId],
    enabled: config.companyId !== "" && lookupId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      listPayElements(transport, { companyId: config.companyId, employeeId: lookupId }, signal),
  });

  const components = useQuery({
    queryKey: ["hr", "pay-components", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listPayComponents(transport, { companyId: config.companyId }, signal),
  });

  /* الموظف المعروض: المقروء، أو ما ردّه إنهاءُ الخدمة (وهو أحدث). */
  const shown: HrEmployee | null = terminated ?? employee.data ?? null;

  const open = useCallback(
    (id: string) => {
      setLookupId(id);
      setTypedId(id);
      setFocus({ employeeId: id });
      setTerminated(null);
      setTerminateError(null);
      setElementError(null);
      fireArrived();
    },
    [fireArrived, setFocus]
  );

  const submitRegistration = useCallback(async () => {
    setRegisterBusy(true);
    setRegisterError(null);
    try {
      const nameTranslations: NameValue[] = Object.entries(draft.translations)
        .filter(([, value]) => value.trim() !== "")
        .map(([name, value]) => ({ name, value: value.trim() }));

      const created = await registerEmployee(transport, {
        companyId: config.companyId,
        body: {
          nameAr: draft.nameAr,
          ...(nameTranslations.length > 0 ? { nameTranslations } : {}),
          classCode: draft.classCode,
          costCenterId: draft.costCenterId,
          hiredOn: draft.hiredOn,
          identity: {
            nationalId: draft.nationalId,
            iban: draft.iban,
            birthDate: draft.birthDate,
          },
        },
      });
      /* **الهوية تُمحى هنا، لا بعد قليل**: الحقل الذي بقي فيه رقم هوية بعد
         نجاح الطلب يُلتقط في لقطة شاشة، ويُقرأ من فوق الكتف، ويعود مع زرّ
         «رجوع». والاستبدال بمسوّدة نظيفة أضمن من مسح ثلاثة حقول بأسمائها. */
      setDraft(emptyDraft());
      setRegistered(created);
      open(created.id);
    } catch (failure) {
      setRegisterError(failure);
    } finally {
      setRegisterBusy(false);
    }
  }, [config.companyId, draft, open, transport]);

  const submitElement = useCallback(async () => {
    setElementBusy(true);
    setElementError(null);
    try {
      await addPayElement(transport, {
        companyId: config.companyId,
        employeeId: lookupId,
        body: {
          componentCode: elementCode,
          effectiveFrom: elementFrom,
          /* المبلغ يصير Money هنا — والمُرمِّز المُولَّد يرفض أي شيء آخر في
             حقلٍ مالي، فلا طريق يمرّ منه رقم إلى السلك. */
          amount: Money.wire(elementAmount),
        },
      });
      setElementAmount("");
      await elements.refetch();
      fireArrived();
    } catch (failure) {
      setElementError(failure);
    } finally {
      setElementBusy(false);
    }
  }, [config.companyId, elementAmount, elementCode, elementFrom, elements, fireArrived, lookupId, transport]);

  const submitTermination = useCallback(async () => {
    setTerminateBusy(true);
    setTerminateError(null);
    try {
      const after = await terminateEmployee(transport, {
        companyId: config.companyId,
        employeeId: lookupId,
        body: { endedOn, reasonKey },
      });
      setTerminated(after);
      setFocus({ employmentId: after.employmentId });
      fireArrived();
    } catch (failure) {
      setTerminateError(failure);
    } finally {
      setTerminateBusy(false);
    }
  }, [config.companyId, endedOn, fireArrived, lookupId, reasonKey, setFocus, transport]);

  const componentOptions = components.data?.items ?? [];
  const elementRows: readonly HrPayElement[] = elements.data?.items ?? [];
  const amountBad = elementAmount !== "" && !isMoneyText(elementAmount);
  const elementReady = elementCode !== "" && elementFrom !== "" && elementAmount !== "" && !amountBad;
  const registerReady =
    draft.nameAr.trim() !== "" &&
    draft.classCode.trim() !== "" &&
    draft.hiredOn !== "" &&
    draft.nationalId.trim() !== "" &&
    draft.iban.trim() !== "" &&
    draft.birthDate !== "";

  const otherLocales = useMemo(
    () => i18n.catalogue.filter((entry) => entry.code !== SOURCE),
    [i18n]
  );

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-register-needs-company" />;

  return (
    <section className="stack" data-testid="hr-register-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.registerTitle")}</h1>
          <p className="sub">{t("hr.page.registerLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr" />

      <DeclaredGap
        title={t("hr.gap.listTitle")}
        body={t("hr.gap.listBody")}
        owed={t("hr.gap.listOwed")}
        testId="hr-gap-no-list"
      />

      <DeclaredGap
        title={t("hr.gap.leaveTitle")}
        body={t("hr.gap.leaveBody")}
        owed={t("hr.gap.leaveOwed")}
        testId="hr-gap-leave"
      />

      <Panel title={t("hr.employee.lookup")} note={t("hr.employee.lookupNote")} testId="hr-lookup">
        <div className="grid fields-2">
          <Field id="hr-employee-id" label={t("hr.field.employeeId")} hint={t("hr.field.employeeIdHint")} source="typed">
            <input
              id="hr-employee-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-employee-id"
              value={typedId}
              onChange={(e) => setTypedId(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && typedId !== "") open(typedId);
              }}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.read")}
              kind="primary"
              disabled={typedId === ""}
              onClick={() => open(typedId)}
              testId="hr-employee-read"
            />
          </div>
        </div>
      </Panel>

      {employee.isError ? <ProblemPanel error={employee.error} onRetry={() => void employee.refetch()} /> : null}

      {lookupId !== "" && employee.isPending && employee.fetchStatus === "fetching" ? (
        <StatePanel title={t("hr.employee.card")} loading testId="hr-employee-loading">
          {null}
        </StatePanel>
      ) : null}

      {shown ? (
        <div className="hr-grid2">
          <Panel
            title={t("hr.employee.card")}
            aside={<HrState state={shown.state} testId="hr-employee-state" />}
            className={arrived}
            testId="hr-employee-card"
          >
            <div className="kv">
              <div>
                <div className="k">{t("hr.code.label")}</div>
                <div className="v">
                  <OpaqueCode code={shown.code} />
                </div>
              </div>
              <div>
                <div className="k">{t("hr.field.classCode")}</div>
                <div className="v mono" dir="ltr">
                  {shown.classCode}
                </div>
              </div>
              <div>
                <div className="k">{t("hr.field.costCenter")}</div>
                <div className="v mono" dir="ltr">
                  {shown.costCenterId}
                </div>
              </div>
              <div>
                <div className="k">{t("hr.employee.name")}</div>
                <div className="v">
                  <TranslatedName
                    nameAr={shown.nameAr}
                    translations={shown.nameTranslations}
                    testId="hr-employee-name"
                  />
                </div>
              </div>
              <div>
                <div className="k">{t("hr.employee.employment")}</div>
                <div className="v mono" dir="ltr" data-testid="hr-employment-id">
                  {shown.employmentId}
                </div>
              </div>
              <div>
                <div className="k">{t("hr.field.hiredOn")}</div>
                <div className="v mono" dir="ltr">
                  {shown.startedOn}
                </div>
              </div>
              <div>
                <div className="k">{t("hr.field.endedOn")}</div>
                <div className="v mono" dir="ltr" data-testid="hr-ended-on">
                  {shown.endedOn ?? t("hr.employee.stillActive")}
                </div>
              </div>
            </div>
            <p className="hint">{t("hr.code.hint")}</p>
          </Panel>

          <MaskedIdentityPanel identity={shown.identity} />
        </div>
      ) : null}

      {shown ? (
        <StatePanel
          title={t("hr.employee.elements")}
          note={t("hr.employee.elementsNote")}
          aside={<span className="muted">{tp("hr.count.elements", elements.data?.itemCount ?? 0)}</span>}
          loading={elements.isPending && elements.fetchStatus === "fetching"}
          testId="hr-elements"
        >
          {elements.isError ? (
            <ProblemPanel error={elements.error} onRetry={() => void elements.refetch()} />
          ) : elementRows.length === 0 ? (
            <EmptyState
              small
              title={t("hr.employee.elementsEmpty")}
              body={t("hr.employee.elementsEmptyBody")}
              testId="hr-elements-empty"
            />
          ) : (
            <div className="hr-table" data-testid="hr-elements-table">
              <table>
                <caption className="visually-hidden">{t("hr.employee.elements")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("hr.field.componentCode")}</th>
                    <th scope="col">{t("hr.field.effectiveFrom")}</th>
                    <th scope="col" className="n">
                      {t("hr.field.amount")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {elementRows.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <span className="mono" dir="ltr">
                          {row.componentCode}
                        </span>
                      </td>
                      <td>
                        <span className="mono" dir="ltr">
                          {row.effectiveFrom}
                        </span>
                      </td>
                      <td className="n">
                        <Amount value={row.amount} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <h3 className="hr-split">{t("hr.employee.addElement")}</h3>
          <p className="muted">{t("hr.employee.addElementNote")}</p>
          {componentOptions.length === 0 ? (
            <EmptyState
              small
              title={t("hr.employee.componentsEmpty")}
              body={t("hr.employee.componentsEmptyBody")}
              testId="hr-components-empty"
            />
          ) : (
            <div className="grid fields-4">
              <Field id="hr-el-code" label={t("hr.field.componentCode")} source="typed" required>
                <select
                  id="hr-el-code"
                  className="ctl mono"
                  data-testid="hr-element-code"
                  value={elementCode}
                  onChange={(e) => setElementCode(e.target.value)}
                >
                  <option value="">{t("common.label.select")}</option>
                  {componentOptions.map((component) => (
                    <option key={component.id} value={component.code}>
                      {component.code + " · " + component.nameAr}
                    </option>
                  ))}
                </select>
              </Field>
              <Field id="hr-el-from" label={t("hr.field.effectiveFrom")} source="typed" required>
                <input
                  id="hr-el-from"
                  className="ctl mono"
                  type="date"
                  dir="ltr"
                  data-testid="hr-element-from"
                  value={elementFrom}
                  onChange={(e) => setElementFrom(e.target.value)}
                />
              </Field>
              <Field
                id="hr-el-amount"
                label={t("hr.field.amount")}
                hint={amountBad ? t("hr.field.amountBad") : t("hr.field.amountHint")}
                error={amountBad ? t("hr.field.amountBad") : undefined}
                source="typed"
                required
              >
                <input
                  id="hr-el-amount"
                  className={"ctl amt-input" + (amountBad ? " is-invalid" : "")}
                  inputMode="decimal"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  aria-invalid={amountBad}
                  data-testid="hr-element-amount"
                  value={elementAmount}
                  onChange={(e) => setElementAmount(e.target.value)}
                  placeholder="0.0000"
                />
              </Field>
              <div className="rowctl hr-act">
                <Button
                  label={t("hr.act.addElement")}
                  kind="primary"
                  loading={elementBusy}
                  disabled={!elementReady || elementBusy}
                  onClick={() => void submitElement()}
                  testId="hr-element-add"
                />
              </div>
            </div>
          )}
          {elementError ? <ProblemPanel error={elementError} /> : null}
        </StatePanel>
      ) : null}

      {shown ? (
        <Panel title={t("hr.employee.terminate")} note={t("hr.employee.terminateNote")} testId="hr-terminate">
          {shown.state !== ACTIVE ? (
            <p className="alert alert--info" role="status" data-testid="hr-already-terminated">
              {t("hr.employee.alreadyEnded")}
            </p>
          ) : (
            <div className="grid fields-3">
              <Field id="hr-ended-on-input" label={t("hr.field.endedOn")} source="typed" required>
                <input
                  id="hr-ended-on-input"
                  className="ctl mono"
                  type="date"
                  dir="ltr"
                  data-testid="hr-termination-date"
                  value={endedOn}
                  onChange={(e) => setEndedOn(e.target.value)}
                />
              </Field>
              <Field
                id="hr-reason-key"
                label={t("hr.field.reasonKey")}
                hint={t("hr.field.reasonKeyHint")}
                source="typed"
                required
              >
                <input
                  id="hr-reason-key"
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  data-testid="hr-termination-reason"
                  value={reasonKey}
                  onChange={(e) => setReasonKey(e.target.value)}
                  placeholder="resignation"
                />
              </Field>
              <div className="rowctl hr-act">
                <Button
                  label={t("hr.act.terminate")}
                  kind="danger"
                  loading={terminateBusy}
                  disabled={terminateBusy || endedOn === "" || reasonKey === ""}
                  onClick={() => void submitTermination()}
                  testId="hr-terminate-submit"
                />
              </div>
            </div>
          )}
          {terminated ? (
            <p className={"alert alert--info " + MOTION.arrive} role="status" data-testid="hr-terminated">
              {t("hr.employee.terminated")}
            </p>
          ) : null}
          {terminateError ? <ProblemPanel error={terminateError} /> : null}
        </Panel>
      ) : null}

      <Panel title={t("hr.employee.new")} note={t("hr.employee.newNote")} testId="hr-new-employee">
        <div className="grid fields-3">
          <Field id="hr-name-ar" label={t("hr.field.nameAr")} hint={t("hr.field.nameArHint")} source="typed" required>
            <input
              id="hr-name-ar"
              className="ctl"
              lang="ar"
              autoComplete="off"
              data-testid="hr-new-name-ar"
              value={draft.nameAr}
              onChange={(e) => setDraft({ ...draft, nameAr: e.target.value })}
            />
          </Field>
          <Field id="hr-class" label={t("hr.field.classCode")} hint={t("hr.field.classCodeHint")} source="typed" required>
            <input
              id="hr-class"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-new-class"
              value={draft.classCode}
              onChange={(e) => setDraft({ ...draft, classCode: e.target.value })}
            />
          </Field>
          <Field id="hr-cc" label={t("hr.field.costCenter")} hint={t("hr.field.costCenterHint")} source="typed">
            <input
              id="hr-cc"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-new-cost-center"
              value={draft.costCenterId}
              onChange={(e) => setDraft({ ...draft, costCenterId: e.target.value })}
            />
          </Field>
        </div>

        <h3 className="hr-split">{t("hr.employee.namesOther")}</h3>
        <p className="muted">{t("hr.employee.namesOtherNote")}</p>
        <div className="grid fields-3">
          {otherLocales.map((entry) => (
            <Field key={entry.code} id={"hr-name-" + entry.code} label={entry.native} source="typed">
              <input
                id={"hr-name-" + entry.code}
                className="ctl"
                lang={entry.code}
                dir={entry.dir}
                autoComplete="off"
                data-testid={"hr-new-name-" + entry.code}
                value={draft.translations[entry.code] ?? ""}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    translations: { ...draft.translations, [entry.code]: e.target.value },
                  })
                }
              />
            </Field>
          ))}
        </div>

        <h3 className="hr-split">{t("hr.identity.title")}</h3>
        <p className="alert alert--ai" role="note" data-testid="hr-identity-warning">
          {t("hr.identity.warning")}
        </p>
        <div className="grid fields-4">
          <Field id="hr-nid" label={t("hr.field.nationalId")} hint={t("hr.field.nationalIdHint")} source="typed" required>
            <input
              id="hr-nid"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-new-national-id"
              value={draft.nationalId}
              onChange={(e) => setDraft({ ...draft, nationalId: e.target.value })}
            />
          </Field>
          <Field id="hr-iban" label={t("hr.field.iban")} hint={t("hr.field.ibanHint")} source="typed" required>
            <input
              id="hr-iban"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-new-iban"
              value={draft.iban}
              onChange={(e) => setDraft({ ...draft, iban: e.target.value })}
            />
          </Field>
          <Field id="hr-birth" label={t("hr.field.birthDate")} source="typed" required>
            <input
              id="hr-birth"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="hr-new-birth-date"
              value={draft.birthDate}
              onChange={(e) => setDraft({ ...draft, birthDate: e.target.value })}
            />
          </Field>
          <Field id="hr-hired" label={t("hr.field.hiredOn")} source="typed" required>
            <input
              id="hr-hired"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="hr-new-hired-on"
              value={draft.hiredOn}
              onChange={(e) => setDraft({ ...draft, hiredOn: e.target.value })}
            />
          </Field>
        </div>

        <div className="inline-group">
          <Button
            label={t("hr.act.register")}
            kind="primary"
            loading={registerBusy}
            disabled={!registerReady || registerBusy}
            onClick={() => void submitRegistration()}
            testId="hr-register-submit"
          />
        </div>

        {registered ? (
          <div className={"alert alert--success " + MOTION.arrive} role="status" data-testid="hr-registered">
            <div className="body">
              <p className="title">{t("hr.employee.registered")}</p>
              <p>{t("hr.employee.registeredBody")}</p>
              <p>{t("hr.identity.cleared")}</p>
              <p className="mono" dir="ltr" data-testid="hr-registered-code">
                {registered.code}
              </p>
            </div>
          </div>
        ) : null}
        {registerError ? <ProblemPanel error={registerError} /> : null}
      </Panel>
    </section>
  );
}
