/* ═══════════════════════════════════════════════════════════════════════════
   /sales/receivables — أعمار الذمم المدينة  ·  Receivables aging
   ───────────────────────────────────────────────────────────────────────────
   تقريرٌ يقرأ ولا يكتب، وثلاثة أشياء تحكم عرضه:

   ١ · **المجموع يأتي محسوباً ولا يُجمع هنا.** العقد ينصّ أن `total` «مجموع
       الشرائح بالضبط — يُرسَل محسوباً ولا يُترك لكل عميل أن يجمعه فيختلف
       تقريران عن الرقم نفسه». فلا جمعَ في المتصفّح، ولا حتّى لصفٍّ واحد.

   ٢ · **الأرقام تصطفّ في أعمدة**، فخاناتها متساوية العرض
       (`font-variant-numeric: tabular-nums`) واتجاهها معزول — وإلّا انزلق
       الرقم في السطر العربي وصار عمودٌ غير عمود.

   ٣ · **الشكل واحدٌ للمدينة والدائنة** كما ينصّ العقد: «شكلان مختلفان كانا
       سيجعلان مقارنة الذمم بالذمم عملاً يدوياً عند كل عميل». ولذلك تُعلَن
       الذمم الدائنة هنا **باباً منشوراً لا شاشةَ له بعد** بدل السكوت عنها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readReceivablesAging } from "../../api/generated/client";
import { PARAM_readReceivablesAging_asOf_RE } from "../../api/generated/formats";
import type { AgingBands } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { resolveTranslatedName } from "../../app/translated-name";
import { Amount, useLocale, useT } from "../../i18n/react";
import { EmptyState, StatCard, useMoment } from "../../ui";
import { peekVoiceDraft } from "../../voice";
import {
  AccField,
  AccRow,
  AccSectionNav,
  ChooseCompanyFirst,
  DeclaredGap,
  StatePanel,
  todayIso,
} from "./parts";
import "./accounting.css";

/** الشرائح الخمس بترتيب قراءتها — من غير المستحقّ إلى الأقدم. */
const BANDS = [
  { key: "notDue", label: "accounting.band.notDue" },
  { key: "days1To30", label: "accounting.band.days1To30" },
  { key: "days31To60", label: "accounting.band.days31To60" },
  { key: "days61To90", label: "accounting.band.days61To90" },
  { key: "over90", label: "accounting.band.over90" },
] as const;

/** الشاشة كاملةً. */
export function ReceivablesAgingScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const { locale } = useLocale();
  const [arriveCls] = useMoment("arrive");

  const spokenAsOf = useMemo(() => {
    const draft = peekVoiceDraft();
    if (
      draft?.intentId !== "accounting.receivables_aging.query" &&
      draft?.intentId !== "accounting.customer_balance.query"
    ) {
      return "";
    }
    return draft.fields.find((field) => field.name === "asOf")?.text ?? "";
  }, []);

  const [asOf, setAsOf] = useState(() => spokenAsOf || todayIso());
  const [filter, setFilter] = useState("");
  const asOfValid = PARAM_readReceivablesAging_asOf_RE.test(asOf);

  const report = useQuery({
    queryKey: ["accounting", "receivables-aging", config.baseUrl, config.token, config.companyId, asOf],
    enabled: config.companyId !== "" && asOfValid,
    retry: false,
    queryFn: ({ signal }) =>
      readReceivablesAging(transport, { companyId: config.companyId, asOf }, signal),
  });

  const data = report.data ?? null;

  /* ── مِصفاةٌ على ما وصل، لا استعلامٌ ثانٍ ─────────────────────────────
     تُضيّق **الصفوف المعروضة** ولا تمسّ المجاميع: المجاميع مجاميعُ التقرير
     كما أرسلها الخادم، و«مجموعٌ لِما رشّحته العين» رقمٌ ثالث لا مصدر له.
     ولذلك تبقى بطاقات الشرائح كما هي، ويقول اللوح أن الصفوف مُصفّاة. */
  const shown = useMemo(() => {
    const needle = filter.trim().toLocaleLowerCase();
    const parties = data?.parties ?? [];
    if (!needle) return parties;
    return parties.filter(
      (party) =>
        party.code.toLocaleLowerCase().includes(needle) ||
        party.name.ar.includes(filter.trim()) ||
        party.name.en.toLocaleLowerCase().includes(needle)
    );
  }, [data, filter]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-receivables-needs-company" />;

  return (
    <section className="stack" data-testid="acc-receivables-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.receivablesTitle")}</h1>
          <p className="sub">{t("accounting.page.receivablesLede")}</p>
        </div>
      </header>

      <AccSectionNav group="sales" current="/sales/receivables" />

      {/* ═════════════════════════════════════ ١ · تاريخ التقرير ══════ */}
      <StatePanel
        title={t("accounting.aging.asOfTitle")}
        note={t("accounting.aging.asOfNote")}
        testId="acc-receivables-asof"
      >
        <AccRow cols={2} testId="acc-receivables-asof-row">
          <AccField
            id="acc-ar-asof"
            label={t("accounting.field.asOf")}
            hint={t("accounting.field.asOfHint")}
            error={asOfValid ? undefined : t("accounting.field.asOfBad")}
            source={spokenAsOf ? "spoken" : "typed"}
            required
          >
            <input
              id="acc-ar-asof"
              className={"ctl mono" + (asOfValid ? "" : " is-invalid")}
              type="date"
              dir="ltr"
              aria-invalid={!asOfValid}
              data-testid="acc-receivables-asof-input"
              value={asOf}
              onChange={(e) => setAsOf(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-ar-filter"
            label={t("accounting.field.partyFilter")}
            hint={t("accounting.field.partyFilterHint")}
            source="typed"
          >
            <input
              id="acc-ar-filter"
              className="ctl"
              autoComplete="off"
              data-testid="acc-receivables-filter"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </AccField>
        </AccRow>
      </StatePanel>

      {/* ═════════════════════════════ ٢ · مجاميع الشرائح الخمس ══════ */}
      <StatePanel
        title={t("accounting.aging.totalsTitle")}
        note={t("accounting.aging.totalsNote")}
        aside={data ? <span className="muted">{tp("accounting.count.parties", data.parties.length)}</span> : null}
        loading={report.isPending && report.fetchStatus === "fetching"}
        testId="acc-receivables-totals"
      >
        {report.isError ? (
          <ProblemPanel error={report.error} onRetry={() => void report.refetch()} />
        ) : data ? (
          <div className="stack">
            <div className={"acc-stats " + arriveCls} data-testid="acc-receivables-bands">
              {BANDS.map((band) => (
                <StatCard
                  key={band.key}
                  label={t(band.label)}
                  amount={data.totals[band.key]}
                  hint={t("accounting.band.hint")}
                  tone={band.key === "over90" ? "bad" : "neutral"}
                  testId={"acc-receivables-band-" + band.key}
                />
              ))}
            </div>
            <div className="acc-stats acc-stats--3">
              <StatCard
                label={t("accounting.aging.total")}
                amount={data.totals.total}
                hint={t("accounting.aging.totalHint")}
                tone="debit"
                testId="acc-receivables-total"
              />
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ══════════════════════════════════ ٣ · الأطراف صفّاً صفّاً ═══ */}
      <StatePanel
        title={t("accounting.aging.partiesTitle")}
        note={filter.trim() === "" ? t("accounting.aging.partiesNote") : t("accounting.aging.filteredNote")}
        aside={data ? <span className="muted">{tp("accounting.count.parties", shown.length)}</span> : null}
        loading={report.isPending && report.fetchStatus === "fetching"}
        testId="acc-receivables-parties"
      >
        {data === null ? null : shown.length === 0 ? (
          <EmptyState
            title={filter.trim() === "" ? t("accounting.aging.emptyTitle") : t("accounting.aging.noMatchTitle")}
            body={filter.trim() === "" ? t("accounting.aging.emptyBody") : t("accounting.aging.noMatchBody")}
            testId="acc-receivables-empty"
          />
        ) : (
          <div className="acc-table" data-testid="acc-receivables-table">
            <table>
              <caption className="visually-hidden">{t("accounting.aging.partiesTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.field.partyCode")}</th>
                  <th scope="col">{t("accounting.field.partyName")}</th>
                  {BANDS.map((band) => (
                    <th key={band.key} scope="col" className="n">{t(band.label)}</th>
                  ))}
                  <th scope="col" className="n">{t("accounting.aging.total")}</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((party) => (
                  <tr key={party.partyId} data-testid={"acc-receivables-party-" + party.code}>
                    <td><span className="mono acc-id">{party.code}</span></td>
                    <td>
                      <span lang="ar" dir="rtl">{party.name.ar}</span>
                      {locale !== "ar" ? (
                        <>
                          {" "}
                          <span className="alt" lang="en" dir="ltr">
                            {resolveTranslatedName(party.name.ar, [{ name: "en", value: party.name.en }], locale).text}
                          </span>
                        </>
                      ) : null}
                    </td>
                    {BANDS.map((band) => (
                      <td key={band.key} className="n">
                        <Amount value={party.bands[band.key as keyof AgingBands]} />
                      </td>
                    ))}
                    <td className="n"><Amount value={party.bands.total} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </StatePanel>

      {/* ═══════════════════════ ٤ · بابٌ منشور لا شاشةَ له — مُعلَناً ═ */}
      <DeclaredGap
        title={t("accounting.gap.payablesTitle")}
        body={t("accounting.gap.payablesBody")}
        owed={t("accounting.gap.payablesOwed")}
        testId="acc-receivables-payables-gap"
      />
    </section>
  );
}
