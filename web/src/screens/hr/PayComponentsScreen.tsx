/* ═══════════════════════════════════════════════════════════════════════════
   /hr/pay-components — مكوّنات الأجر  ·  The pay components
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة هي الموضع الذي يصير فيه الأثر التنظيمي بياناتٍ لا شيفرة.**

   المكوّن يحمل وسمين اثنين، وهما كلّ ما تعرفه الوحدة عن الأجر: هل يدخل
   **وعاء اشتراك التأمينات**، وهل يدخل **وعاء مكافأة نهاية الخدمة**. والوحدة
   لا تعرف أيّ بدلٍ يدخل وأيّه لا يدخل — وذلك سؤالٌ نظامي غير محسوم في هذا
   المستودع، **ولا يُخترع في شيفرة**. فيملؤه المحاسب هنا، مرّةً، ويُشتقّ منه
   الوعاء في كل مسيّر بعده.

   ولذلك أُفردت لها شاشة: هذه **بياناتٌ قائمة** تُعرَّف مرّةً وتُقرأ كل شهر،
   وقارئُها هو من يؤسّس المنشأة لا من يشغّل مسيّر الشهر. ووضعُ قرارٍ دائم
   داخل شاشة عملٍ شهري يجعله يُقرأ قراراً شهرياً.

   ── ولا مبلغ هنا ولا نسبة ─────────────────────────────────────────────
   المكوّن **لا يحمل مبلغاً ولا نسبة**: القيمة تُسنَد بتاريخ سريان على بطاقة
   الموظف في `/hr`، والنسبة لا تدخل إلا من إعدادات الرواتب في `/hr/payroll`.
   وحقلٌ للمبلغ هنا كان سيجعل «الراتب الأساسي» رقماً واحداً للمنشأة كلّها.

   ── والاسم يُقرأ بلغة صاحبه ────────────────────────────────────────────
   اسم المكوّن هو ما يقرؤه العامل على قسيمته. فالسجلّ عربيّ، والترجمات صفوف
   **بلغات الواجهة كلّها** لا بالإنجليزية وحدها (ADR-0021)، وغيابُ الترجمة
   يُرتدّ إلى السجلّ العربي — وهو ما تعرضه شاشة القسيمة مُعلَناً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { addPayComponent, listPayComponents } from "../../api/generated/client";
import type { HrPayComponentRequest, NameValue } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { SOURCE } from "../../i18n/engine";
import { useLocale, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, StatCard } from "../../ui";
import { useMoment } from "../../ui";
import {
  ChooseCompanyFirst,
  HrSectionNav,
  StatePanel,
  TranslatedName,
} from "./parts";
import { COMPONENT_KINDS } from "./contract";
import "./hr.css";

/** نوع المكوّن كما يقبله العقد. */
type Kind = HrPayComponentRequest["kind"];

/** ما يُكتب قبل أن يعبر. **الوسمان نصّان** في الحالة ثم يصيران منطقاً عند الحدّ. */
interface ComponentDraft {
  code: string;
  nameAr: string;
  kind: Kind;
  entersContributoryWage: string;
  entersEndOfServiceBase: string;
  translations: Record<string, string>;
}

/** مسوّدة فارغة. */
function emptyDraft(): ComponentDraft {
  return {
    code: "",
    nameAr: "",
    kind: (COMPONENT_KINDS[0] ?? "") as Kind,
    /* **لا افتراض صامت على وسمٍ نظاميّ الأثر.** الوسم يبدأ بلا اختيار، فمن
       يعرّف مكوّناً يقول فيه «نعم» أو «لا» صراحةً. وقيمةٌ افتراضية هنا تجعل
       بدلاً يدخل الوعاء — أو لا يدخله — بلا أن يقرّر ذلك أحد. */
    entersContributoryWage: "",
    entersEndOfServiceBase: "",
    translations: {},
  };
}

/** الشاشة كاملةً. */
export function PayComponentsScreen(): ReactNode {
  const { t, tp } = useT();
  const { i18n } = useLocale();
  const { transport, config } = useApi();

  const [draft, setDraft] = useState<ComponentDraft>(emptyDraft);
  const [added, setAdded] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [refuseCls, fireRefuse] = useMoment("refuse");

  const components = useQuery({
    queryKey: ["hr", "pay-components", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listPayComponents(transport, { companyId: config.companyId }, signal),
  });

  const otherLocales = useMemo(
    () => i18n.catalogue.filter((entry) => entry.code !== SOURCE),
    [i18n]
  );

  const ready =
    draft.code.trim() !== "" &&
    draft.nameAr.trim() !== "" &&
    draft.entersContributoryWage !== "" &&
    draft.entersEndOfServiceBase !== "";

  const submit = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    try {
      /* الترجمات تعبر صفوفاً بوسم اللغة — والفارغ منها لا يعبر أصلاً، فلا
         يُودَع في السجلّ اسمٌ فارغ يُقرأ «تُرجم إلى لا شيء». */
      const translations: NameValue[] = otherLocales
        .map((entry) => ({ name: entry.code, value: (draft.translations[entry.code] ?? "").trim() }))
        .filter((entry) => entry.value !== "");
      const created = await addPayComponent(transport, {
        companyId: config.companyId,
        body: {
          code: draft.code.trim(),
          nameAr: draft.nameAr.trim(),
          kind: draft.kind,
          entersContributoryWage: draft.entersContributoryWage === "yes",
          entersEndOfServiceBase: draft.entersEndOfServiceBase === "yes",
          nameTranslations: translations,
        },
      });
      setAdded(created.code);
      setDraft(emptyDraft());
      await components.refetch();
      fireArrive();
    } catch (problem) {
      setFailure(problem);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [components, config.companyId, draft, fireArrive, fireRefuse, otherLocales, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-components-needs-company" />;

  const items = components.data?.items ?? [];

  return (
    <section className="stack" data-testid="hr-pay-components-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.componentsTitle")}</h1>
          <p className="sub">{t("hr.page.componentsLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/pay-components" />

      {components.isError ? (
        <ProblemPanel error={components.error} onRetry={() => void components.refetch()} />
      ) : null}

      <StatePanel
        title={t("hr.component.title")}
        note={t("hr.component.note")}
        aside={<span className="muted">{tp("hr.count.components", components.data?.itemCount ?? 0)}</span>}
        loading={components.isPending && components.fetchStatus === "fetching"}
        testId="hr-components-list"
      >
        {items.length === 0 ? (
          <EmptyState
            title={t("hr.component.emptyTitle")}
            body={t("hr.component.emptyBody")}
            testId="hr-components-empty"
          />
        ) : (
          <div className="hr-table" data-testid="hr-components-table">
            <table>
              <caption className="visually-hidden">{t("hr.component.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("hr.field.componentCode")}</th>
                  <th scope="col">{t("hr.payslip.componentName")}</th>
                  <th scope="col">{t("hr.field.kind")}</th>
                  <th scope="col">{t("hr.component.entersWage")}</th>
                  <th scope="col">{t("hr.component.entersEos")}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((component) => (
                  <tr key={component.id}>
                    <td><span className="mono" dir="ltr">{component.code}</span></td>
                    <td>
                      <TranslatedName
                        nameAr={component.nameAr}
                        translations={component.nameTranslations}
                        testId="hr-component-name"
                      />
                    </td>
                    <td>
                      <span
                        className={"pill " + (component.kind === "earning" ? "pill--debit" : "pill--credit")}
                        data-testid="hr-component-row-kind"
                      >
                        {t("hr.kind." + component.kind)}
                      </span>
                    </td>
                    <td data-testid="hr-component-wage-flag">
                      {component.entersContributoryWage ? t("hr.component.yes") : t("hr.component.no")}
                    </td>
                    <td data-testid="hr-component-eos-flag">
                      {component.entersEndOfServiceBase ? t("hr.component.yes") : t("hr.component.no")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="hint">{t("hr.component.flagsNote")}</p>
      </StatePanel>

      <Panel title={t("hr.component.newTitle")} note={t("hr.component.newNote")} testId="hr-component-new">
        <div className="grid fields-4">
          <Field
            id="hr-c-code"
            label={t("hr.field.componentCode")}
            hint={t("hr.field.componentCodeHint")}
            source="typed"
            required
          >
            <input
              id="hr-c-code"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-component-code"
              value={draft.code}
              onChange={(e) => setDraft({ ...draft, code: e.target.value })}
              placeholder="HOUSING"
            />
          </Field>
          <Field id="hr-c-kind" label={t("hr.field.kind")} hint={t("hr.field.kindHint")} source="typed" required>
            <select
              id="hr-c-kind"
              className="ctl"
              data-testid="hr-component-kind-input"
              value={draft.kind}
              onChange={(e) => setDraft({ ...draft, kind: e.target.value as Kind })}
            >
              {COMPONENT_KINDS.map((kind) => (
                <option key={kind} value={kind}>
                  {t("hr.kind." + kind)}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="hr-c-wage"
            label={t("hr.component.entersWage")}
            hint={t("hr.component.entersWageHint")}
            source="typed"
            required
          >
            <select
              id="hr-c-wage"
              className="ctl"
              data-testid="hr-component-wage"
              value={draft.entersContributoryWage}
              onChange={(e) => setDraft({ ...draft, entersContributoryWage: e.target.value })}
            >
              <option value="">{t("common.label.select")}</option>
              <option value="yes">{t("hr.component.yes")}</option>
              <option value="no">{t("hr.component.no")}</option>
            </select>
          </Field>
          <Field
            id="hr-c-eos"
            label={t("hr.component.entersEos")}
            hint={t("hr.component.entersEosHint")}
            source="typed"
            required
          >
            <select
              id="hr-c-eos"
              className="ctl"
              data-testid="hr-component-eos"
              value={draft.entersEndOfServiceBase}
              onChange={(e) => setDraft({ ...draft, entersEndOfServiceBase: e.target.value })}
            >
              <option value="">{t("common.label.select")}</option>
              <option value="yes">{t("hr.component.yes")}</option>
              <option value="no">{t("hr.component.no")}</option>
            </select>
          </Field>
        </div>

        <div className="grid fields-2">
          <Field
            id="hr-c-name"
            label={t("hr.field.nameAr")}
            hint={t("hr.field.nameArHint")}
            source="typed"
            required
          >
            <input
              id="hr-c-name"
              className="ctl"
              lang={SOURCE}
              dir="rtl"
              autoComplete="off"
              data-testid="hr-component-name-ar"
              value={draft.nameAr}
              onChange={(e) => setDraft({ ...draft, nameAr: e.target.value })}
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.addComponent")}
              kind="primary"
              loading={busy}
              disabled={!ready || busy}
              onClick={() => void submit()}
              testId="hr-component-submit"
            />
          </div>
        </div>

        <h3 className="hr-split">{t("hr.component.namesOther")}</h3>
        <p className="muted">{t("hr.component.namesOtherNote")}</p>
        <div className="grid fields-3">
          {otherLocales.map((entry) => (
            <Field
              key={entry.code}
              id={"hr-c-name-" + entry.code}
              label={entry.native}
              hint={t("hr.component.nameOtherHint")}
              source="typed"
            >
              <input
                id={"hr-c-name-" + entry.code}
                className="ctl"
                lang={entry.code}
                dir={entry.dir}
                autoComplete="off"
                data-testid={"hr-component-name-" + entry.code}
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

        {failure ? (
          <div className={refuseCls}>
            <ProblemPanel error={failure} />
          </div>
        ) : null}

        {added ? (
          <div className={"hr-receipt " + arriveCls} data-testid="hr-component-added">
            <h2>{t("hr.component.added")}</h2>
            <p>{t("hr.component.addedBody")}</p>
            <div className="stats-row hr-one">
              <StatCard label={t("hr.field.componentCode")} count={added} testId="hr-component-added-code" />
            </div>
          </div>
        ) : null}
      </Panel>
    </section>
  );
}
