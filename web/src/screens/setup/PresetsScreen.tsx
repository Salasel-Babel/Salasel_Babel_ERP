/* ═══════════════════════════════════════════════════════════════════════════
   /setup/presets — ثوابت الشركة  ·  The company presets
   ───────────────────────────────────────────────────────────────────────────
   **ما يُحسم هنا مرّةً يختفي من شاشات الإدخال.** الفاتورة كانت تسأل عن الفرع
   والتصنيف الضريبي ونسبته وطريقة التسوية وخزينتها في كل مرّة — وكلّها لا تتغيّر
   من فاتورة إلى فاتورة في منشأةٍ غير ميدانية. فتُضبط هنا، وتبقى في الفاتورة
   المتغيّرات القليلة: العميل والصنف والكمية والسعر.

   **والحقول تُرسم من العقد لا من قائمة مكتوبة بيد:** الخادم يعيد الكتالوج المغلق
   (المفتاح ونوعه وخياراته) وسلاسل الترقيم ببادئاتها الافتراضية، وهذه الشاشة
   تمرّ عليها. فما يُضاف إلى الكتالوج يظهر هنا بلا تعديل.

   **والقيمة الفارغة تزيل الثابت** — فيعود الحقل يُسأل عنه في شاشته.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readCompanyPresets, replaceCompanyPresets } from "../../api/generated/client";
import type { CompanyPresets, PresetKey } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { presetSlug } from "../../app/presets";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, useMoment } from "../../ui";
import { ChooseCompanyFirst, SetupBadge, SetupField, SetupSectionNav, StatePanel } from "./parts";

const NUMBER_PREFIX = "number.";

/** يفصل الكتالوج إلى ثوابت تشغيلية وبادئات ترقيم. */
function split(catalogue: readonly PresetKey[]): { readonly operational: readonly PresetKey[]; readonly numbering: readonly PresetKey[] } {
  return {
    operational: catalogue.filter((key) => !key.key.startsWith(NUMBER_PREFIX)),
    numbering: catalogue.filter((key) => key.key.startsWith(NUMBER_PREFIX)),
  };
}

function toMap(presets: CompanyPresets | undefined): Map<string, string> {
  const map = new Map<string, string>();
  for (const pair of presets?.values ?? []) map.set(pair.name, pair.value);
  return map;
}

export function PresetsScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const presets = useQuery({
    queryKey: ["presets", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readCompanyPresets(transport, { companyId: config.companyId }, signal),
  });

  /* النموذج: ما وصل من الخادم حتى يلمسه المستخدم، ثم نسخته المعدَّلة — اشتقاقٌ لا مزامنة. */
  const [edited, setEdited] = useState<Map<string, string> | null>(null);
  const touched = edited !== null;
  const draft: Map<string, string> = edited ?? toMap(presets.data);

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const edit = useCallback(
    (key: string, value: string) => {
      setSavedAt(null);
      setEdited((was) => {
        const next = new Map(was ?? toMap(presets.data));
        next.set(key, value);
        return next;
      });
    },
    [presets.data]
  );

  const save = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    try {
      const values = [...draft.entries()].map(([name, value]) => ({ name, value: value.trim() }));
      await replaceCompanyPresets(transport, { companyId: config.companyId, body: { values } });
      await presets.refetch();
      setEdited(null);
      setSavedAt(new Date().toISOString());
      fireArrive();
    } catch (error) {
      setFailure(error);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [config.companyId, draft, fireArrive, fireRefuse, presets, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-presets-needs-company" />;

  const catalogue = presets.data ? split(presets.data.catalogue) : { operational: [], numbering: [] };
  const seriesByCode = new Map((presets.data?.series ?? []).map((series) => [series.code, series]));
  const count = [...draft.values()].filter((value) => value.trim() !== "").length;

  const field = (key: PresetKey): ReactNode => {
    const slug = presetSlug(key.key);
    const id = "preset-" + slug;
    const value = draft.get(key.key) ?? "";
    const isSeries = key.key.startsWith(NUMBER_PREFIX);
    const seriesCode = isSeries ? key.key.slice(NUMBER_PREFIX.length) : "";
    const label = isSeries ? t("screen.presets.series." + presetSlug(seriesCode)) : t("screen.presets.label." + slug);
    const hint = isSeries
      ? t("screen.presets.seriesHint", { prefix: seriesByCode.get(seriesCode)?.defaultPrefix ?? "" })
      : t("screen.presets.hint." + slug);
    const source = value === "" ? "typed" : "attested";
    return (
      <SetupField key={key.key} id={id} label={label} hint={hint} source={source}>
        {key.kind === "Choice" ? (
          <select id={id} className="ctl" value={value} data-testid={id} onChange={(e) => edit(key.key, e.target.value)}>
            <option value="">{t("screen.presets.unset")}</option>
            {key.choices.map((choice) => (
              <option key={choice} value={choice}>
                {t("screen.presets.choice." + presetSlug(choice))}
              </option>
            ))}
          </select>
        ) : key.kind === "Boolean" ? (
          <select id={id} className="ctl" value={value} data-testid={id} onChange={(e) => edit(key.key, e.target.value)}>
            <option value="">{t("screen.presets.unset")}</option>
            <option value="true">{t("screen.presets.yes")}</option>
            <option value="false">{t("screen.presets.no")}</option>
          </select>
        ) : (
          <input
            id={id}
            className={"ctl" + (key.kind === "Text" ? "" : " mono")}
            dir={key.kind === "Text" ? undefined : "ltr"}
            inputMode={key.kind === "Rate" ? "decimal" : undefined}
            value={value}
            data-testid={id}
            placeholder={isSeries ? (seriesByCode.get(seriesCode)?.defaultPrefix ?? "") : undefined}
            onChange={(e) => edit(key.key, e.target.value)}
          />
        )}
      </SetupField>
    );
  };

  return (
    <section className="stack" data-testid="setup-presets-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.presets.pageTitle")}</h1>
          <p className="sub">{t("screen.presets.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup/presets" />

      <StatePanel
        title={t("screen.presets.operationalTitle")}
        note={t("screen.presets.operationalNote")}
        aside={<SetupBadge label={t("screen.presets.setCount", { count })} tone={count > 0 ? "posted" : "draft"} testId="setup-presets-count" />}
        loading={presets.isPending && presets.fetchStatus === "fetching"}
        testId="setup-presets-operational"
      >
        {presets.isError ? (
          <ProblemPanel error={presets.error} onRetry={() => void presets.refetch()} />
        ) : (
          <div className={"grid fields-3 " + arriveCls}>{catalogue.operational.map(field)}</div>
        )}
      </StatePanel>

      <StatePanel title={t("screen.presets.numberingTitle")} note={t("screen.presets.numberingNote")} testId="setup-presets-numbering">
        <div className="grid fields-4">{catalogue.numbering.map(field)}</div>
      </StatePanel>

      {failure ? <ProblemPanel error={failure} /> : null}

      <div className="actions">
        <Button
          label={t("screen.presets.save")}
          kind="primary"
          loading={busy}
          disabled={!touched || busy}
          onClick={() => void save()}
          testId="setup-presets-save"
        />
        {savedAt ? (
          <span className="muted" data-testid="setup-presets-saved">
            {t("screen.presets.saved")}
          </span>
        ) : null}
      </div>
    </section>
  );
}
