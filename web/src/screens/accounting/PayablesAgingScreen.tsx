/* ═══════════════════════════════════════════════════════════════════════════
   /purchasing/payables — أعمار الذمم الدائنة  ·  Payables aging
   ───────────────────────────────────────────────────────────────────────────
   **هذه الشاشة هي شقيقةُ `ReceivablesAgingScreen` بمصدرٍ آخر، لا شاشةٌ ثانية
   بتصميمٍ ثانٍ** — وذلك نصُّ العقد لا اجتهادُ من بناها: «شكلان مختلفان كانا
   سيجعلان مقارنة الذمم بالذمم عملاً يدوياً عند كل عميل». فالشرائحُ الخمس
   بترتيبها، والأعمدةُ بترتيبها، والمِصفاةُ بسلوكها — كلُّها واحدة. والفرقُ
   ثلاثةٌ لا رابع لها:

     ١ · البابُ `readPayablesAging` لا `readReceivablesAging`.
     ٢ · **لونُ المجموع دائنٌ لا مدين.** رصيدُ المورّد دائنٌ بطبيعته، ورمزُ
         اللون هو ما يقوله للعين قبل أن تقرأ العنوان — فلو ورثت الشاشةُ لونَ
         شقيقتها لقالت للناظر إن ما يراه أصلٌ وهو التزام.
     ٣ · المجموعةُ `purchasing` في شريط القسم، فالمشترياتُ تنتهي بالذمم
         الدائنة كما تنتهي المبيعاتُ بالمدينة.

   **والمجموع يأتي محسوباً ولا يُجمع هنا** — كما في شقيقتها: العقد ينصّ أن
   `total` «مجموع الشرائح بالضبط — يُرسَل محسوباً ولا يُترك لكل عميل أن يجمعه
   فيختلف تقريران عن الرقم نفسه». فلا جمعَ في المتصفّح ولا لصفٍّ واحد.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readPayablesAging } from "../../api/generated/client";
import { PARAM_readPayablesAging_asOf_RE } from "../../api/generated/formats";
import type { AgingBands } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { resolveTranslatedName } from "../../app/translated-name";
import { Amount, useLocale, useT } from "../../i18n/react";
import { EmptyState, StatCard, useMoment } from "../../ui";
import {
  AccField,
  AccRow,
  AccSectionNav,
  ChooseCompanyFirst,
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
export function PayablesAgingScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const { locale } = useLocale();
  const [arriveCls] = useMoment("arrive");

  const [asOf, setAsOf] = useState(() => todayIso());
  const [filter, setFilter] = useState("");
  const asOfValid = PARAM_readPayablesAging_asOf_RE.test(asOf);

  const report = useQuery({
    queryKey: ["accounting", "payables-aging", config.baseUrl, config.token, config.companyId, asOf],
    enabled: config.companyId !== "" && asOfValid,
    retry: false,
    queryFn: ({ signal }) =>
      readPayablesAging(transport, { companyId: config.companyId, asOf }, signal),
  });

  const data = report.data ?? null;

  /* ── مِصفاةٌ على ما وصل، لا استعلامٌ ثانٍ ─────────────────────────────
     تُضيّق **الصفوف المعروضة** ولا تمسّ المجاميع: المجاميع مجاميعُ التقرير
     كما أرسلها الخادم، و«مجموعٌ لِما رشّحته العين» رقمٌ ثالث لا مصدر له. */
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

  if (config.companyId === "") return <ChooseCompanyFirst testId="acc-payables-needs-company" />;

  return (
    <section className="stack" data-testid="acc-payables-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.page.payablesTitle")}</h1>
          <p className="sub">{t("accounting.page.payablesLede")}</p>
        </div>
      </header>

      <AccSectionNav group="purchasing" current="/purchasing/payables" />

      {/* ═════════════════════════════════════ ١ · تاريخ التقرير ══════ */}
      <StatePanel
        title={t("accounting.aging.asOfTitle")}
        note={t("accounting.aging.asOfNote")}
        testId="acc-payables-asof"
      >
        <AccRow cols={2} testId="acc-payables-asof-row">
          <AccField
            id="acc-ap-asof"
            label={t("accounting.field.asOf")}
            hint={t("accounting.field.asOfHint")}
            error={asOfValid ? undefined : t("accounting.field.asOfBad")}
            source="typed"
            required
          >
            <input
              id="acc-ap-asof"
              className={"ctl mono" + (asOfValid ? "" : " is-invalid")}
              type="date"
              dir="ltr"
              aria-invalid={!asOfValid}
              data-testid="acc-payables-asof-input"
              value={asOf}
              onChange={(e) => setAsOf(e.target.value)}
            />
          </AccField>
          <AccField
            id="acc-ap-filter"
            label={t("accounting.field.partyFilter")}
            hint={t("accounting.field.partyFilterHint")}
            source="typed"
          >
            <input
              id="acc-ap-filter"
              className="ctl"
              autoComplete="off"
              data-testid="acc-payables-filter"
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
        testId="acc-payables-totals"
      >
        {report.isError ? (
          <ProblemPanel error={report.error} onRetry={() => void report.refetch()} />
        ) : data ? (
          <div className="stack">
            <div className={"acc-stats " + arriveCls} data-testid="acc-payables-bands">
              {BANDS.map((band) => (
                <StatCard
                  key={band.key}
                  label={t(band.label)}
                  amount={data.totals[band.key]}
                  hint={t("accounting.band.hint")}
                  tone={band.key === "over90" ? "bad" : "neutral"}
                  testId={"acc-payables-band-" + band.key}
                />
              ))}
            </div>
            <div className="acc-stats acc-stats--3">
              <StatCard
                label={t("accounting.aging.total")}
                amount={data.totals.total}
                hint={t("accounting.aging.totalHint")}
                tone="credit"
                testId="acc-payables-total"
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
        testId="acc-payables-parties"
      >
        {data === null ? null : shown.length === 0 ? (
          <EmptyState
            title={filter.trim() === "" ? t("accounting.aging.emptyTitle") : t("accounting.aging.noMatchTitle")}
            body={filter.trim() === "" ? t("accounting.aging.emptyBody") : t("accounting.aging.noMatchBody")}
            testId="acc-payables-empty"
          />
        ) : (
          <div className="acc-table" data-testid="acc-payables-table">
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
                  <tr key={party.partyId} data-testid={"acc-payables-party-" + party.code}>
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
    </section>
  );
}
