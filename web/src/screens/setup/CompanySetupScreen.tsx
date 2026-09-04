/* ═══════════════════════════════════════════════════════════════════════════
   /setup — تأسيس المنشأة  ·  The company setup
   ───────────────────────────────────────────────────────────────────────────
   **فعلٌ يقع مرّةً واحدة في عمر المنشأة**، ولذلك شاشةٌ لا لوحٌ في شاشة: وضعُ
   قرارٍ دائم داخل شاشةِ عملٍ متكرّر يجعله يُقرأ قراراً متكرّراً (ADR-0077 §2).
   وأربعةٌ تحكمها:

   ١ · **الحالة تُقرأ ولا تُخمَّن.** ‏`readCompanySetup` يردّ 200 بمنشأةٍ
       مؤسَّسة و404 بـ`company_setup.not_found` بمنشأةٍ لم تُؤسَّس. فالشاشة
       تعرف أيَّ الحالتين هي فيها **قبل** أن ترسم شيئاً، ولا تعرض الحالة
       الثانية خطأً: «لم تُؤسَّس بعد» حالةٌ مشروعة لا عطل.

   ٢ · **والتأسيس الثاني يُقال قبل الضغط لا بعده.** منشأةٌ مؤسَّسة لا يُرسم
       لها نموذجٌ ثانٍ: الوصول الثاني يُرفض بـ409 و`company_setup.already_initialised`
       مهما تغيّرت حمولته — **ولا أثر له**. فيُقال ذلك باسم الرمز، ويُعرض ما
       أُسنِد فعلاً.

   ٣ · **وسؤال مراكز التكلفة يُطرح هنا وحده.** ‏`One` يجعل اسمَ المنشأة نفسه
       اسمَ المركز الافتراضي — فلا يُرسَل معه اسمٌ آخر ويُرفض إرساله بـ
       `company_setup.first_cost_center_name_not_expected`؛ و`Multiple` يجعل
       اسم أول مركز **إلزامياً**. فالحقل يظهر ويختفي بالجواب، والسبب مكتوب.

   ٤ · **وعدد الخانات ليس مالاً.** هو صحيحٌ محدود بين 0 و4 يحكم **العرض
       والإدخال البشري وحدهما**: التخزين بأربع خانات، والمبالغ المحسوبة لا
       يقيّدها ولا تُقرَّب عنده. ويُختار من قائمةٍ مغلقة، فلا يُقرأ رقمٌ من
       نصّ ولا يُستدعى محوّلٌ عددي في هذا الملفّ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { initialiseCompanySetup, readCompanySetup } from "../../api/generated/client";
import type { CompanySetup, NameValue } from "../../api/generated/types";
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

/** رمز الخادم حين لم تُؤسَّس المنشأة بعد — حالةٌ تُقرأ لا عطل. */
const NOT_FOUND_CODE = "company_setup.not_found";

/** رمز الخادم على التأسيس الثاني. */
const ALREADY_CODE = "company_setup.already_initialised";

/** رمز الخادم حين يُرسل اسم أول مركز مع الجواب «واحد». */
const NAME_NOT_EXPECTED_CODE = "company_setup.first_cost_center_name_not_expected";

/** رمز الخادم حين يغيب اسم أول مركز مع الجواب «عدّة». */
const NAME_REQUIRED_CODE = "company_setup.first_cost_center_name_required";

/**
 * خانات العرض المقبولة — **قائمةٌ مغلقة لا نصٌّ يُحوَّل**. الحدّ الأعلى هو
 * مقياس التخزين نفسه: عرضٌ بخانات أكثر ممّا يُخزَّن يُظهر أصفاراً مخترَعة
 * يظنّها القارئ دقّة.
 */
const PLACES = [0, 1, 2, 3, 4] as const;

/** الجوابان عن سؤال مراكز التكلفة — كما ينشرهما العقد حرفاً. */
const ANSWERS = ["One", "Multiple"] as const;

/** الشاشة كاملةً. */
export function CompanySetupScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const { i18n, locale } = useLocale();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  /* ── النموذج ──────────────────────────────────────────────────────── */
  const [companyNameAr, setCompanyNameAr] = useState("");
  const [companyTranslations, setCompanyTranslations] = useState<readonly NameValue[]>([]);
  const [answer, setAnswer] = useState<(typeof ANSWERS)[number]>("One");
  const [places, setPlaces] = useState<(typeof PLACES)[number]>(2);
  const [firstNameAr, setFirstNameAr] = useState("");
  const [firstTranslations, setFirstTranslations] = useState<readonly NameValue[]>([]);

  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [founded, setFounded] = useState<CompanySetup | null>(null);

  const setup = useQuery({
    queryKey: ["setup", "company", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readCompanySetup(transport, { companyId: config.companyId }, signal),
  });

  /* ــ الحالة الثالثة: «لم تُؤسَّس بعد» ليست عطلاً ولا تُعرض عطلاً ــــــــ */
  const notFound =
    setup.isError && setup.error instanceof ProblemError && setup.error.code === NOT_FOUND_CODE;
  const current: CompanySetup | null = founded ?? setup.data ?? null;
  const initialised = current !== null;

  /* ــ الرفض يُقال قبل الضغط، وباسمه ــــــــــــــــــــــــــــــــــــــ */
  const firstNameRefusal =
    answer === "Multiple" && firstNameAr.trim() === "" ? NAME_REQUIRED_CODE : null;
  const ready = companyNameAr.trim() !== "" && firstNameRefusal === null;

  const submit = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    try {
      const done = await initialiseCompanySetup(transport, {
        companyId: config.companyId,
        body: {
          companyNameAr: companyNameAr.trim(),
          companyNameTranslations: [...companyTranslations],
          costCenters: answer,
          decimalPlaces: places,
          /* **ولا اسمَ أوّلَ مع الجواب «واحد»**: اسمه هناك اسم المنشأة بعينه،
             وإرساله يُرفض بـ`first_cost_center_name_not_expected`. */
          ...(answer === "Multiple"
            ? {
                firstCostCenterNameAr: firstNameAr.trim(),
                firstCostCenterTranslations: [...firstTranslations],
              }
            : {}),
        },
      });
      setFounded(done);
      fireArrive();
    } catch (refused) {
      setFailure(refused);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [
    answer,
    companyNameAr,
    companyTranslations,
    config.companyId,
    firstNameAr,
    firstTranslations,
    fireArrive,
    fireRefuse,
    places,
    transport,
  ]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-company-needs-company" />;

  return (
    <section className="stack" data-testid="setup-company-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.setup.pageTitle")}</h1>
          <p className="sub">{t("screen.setup.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup" />

      {/* ══════════════════════ ١ · ثوابتُ المنشأة كما أُسنِدت ═══════════ */}
      <StatePanel
        title={t("screen.setup.constantsTitle")}
        note={t("screen.setup.constantsNote")}
        aside={
          initialised ? (
            <SetupBadge
              label={t("screen.setup.stateFounded")}
              tone="posted"
              testId="setup-company-state"
            />
          ) : (
            <SetupBadge
              label={t("screen.setup.stateUnfounded")}
              tone="draft"
              testId="setup-company-state"
            />
          )
        }
        loading={setup.isPending && setup.fetchStatus === "fetching"}
        testId="setup-company-constants"
      >
        {setup.isError && !notFound ? (
          <ProblemPanel error={setup.error} onRetry={() => void setup.refetch()} />
        ) : current === null ? (
          <EmptyState
            title={t("screen.setup.unfoundedTitle")}
            body={t("screen.setup.unfoundedBody")}
            testId="setup-company-unfounded"
          />
        ) : (
          <div className={"stack " + arriveCls}>
            <div className="stats-row">
              <StatCard
                label={t("screen.setup.places")}
                count={current.decimalPlaces}
                hint={t("screen.setup.placesHint")}
                testId="setup-company-places"
              />
              <StatCard
                label={t("screen.setup.centreCount")}
                count={current.costCenters.length}
                hint={t("screen.setup.centreCountHint")}
                testId="setup-company-centre-count"
              />
              <StatCard
                label={t("screen.setup.suspendedCount")}
                count={current.costCenters.filter((c) => c.state === "Suspended").length}
                hint={t("screen.setup.suspendedCountHint")}
                testId="setup-company-suspended-count"
              />
            </div>
            <div className="kv">
              <div>
                <div className="k">{t("screen.setup.companyName")}</div>
                <div className="v" data-testid="setup-company-name">
                  <RecordName
                    nameAr={current.nameAr}
                    translations={current.nameTranslations}
                    locale={locale}
                  />
                </div>
              </div>
              <div>
                <div className="k">{t("screen.setup.defaultCentre")}</div>
                <div className="v mono" dir="ltr" data-testid="setup-company-default">
                  {current.defaultCostCenter}
                </div>
              </div>
            </div>
            <p className="hint">{t("screen.setup.defaultCentreHint")}</p>
          </div>
        )}
      </StatePanel>

      {/* ═════════════════════ ٢ · التأسيس — أو إعلانُ وقوعه سلفاً ═══════ */}
      <StatePanel
        title={t("screen.setup.actTitle")}
        note={initialised ? t("screen.setup.actDoneNote") : t("screen.setup.actNote")}
        testId="setup-company-act"
      >
        {initialised ? (
          <div className="alert alert--info" role="status" data-testid="setup-company-already">
            <div className="body">
              <span className="title">{t("screen.setup.alreadyTitle")}</span>
              <p>
                {t("screen.setup.alreadyBody")}{" "}
                <span className="mono" dir="ltr">{ALREADY_CODE}</span>
              </p>
              <p className="hint">{t("screen.setup.alreadyNoEffect")}</p>
            </div>
          </div>
        ) : (
          <div className="stack">
            <div className="grid fields-half">
              <SetupField
                id="stp-company-name"
                label={t("screen.setup.companyNameAr")}
                hint={t("screen.setup.companyNameArHint")}
                source="typed"
                required
              >
                <input
                  id="stp-company-name"
                  className="ctl"
                  lang="ar"
                  dir="rtl"
                  autoComplete="off"
                  data-testid="setup-company-name-input"
                  value={companyNameAr}
                  onChange={(e) => setCompanyNameAr(e.target.value)}
                />
              </SetupField>
              <SetupField
                id="stp-company-places"
                label={t("screen.setup.placesLabel")}
                hint={t("screen.setup.placesFieldHint")}
                source="typed"
                required
              >
                <select
                  id="stp-company-places"
                  className="ctl mono"
                  dir="ltr"
                  data-testid="setup-company-places-input"
                  value={String(places)}
                  onChange={(e) => {
                    const picked = PLACES.find((p) => String(p) === e.target.value);
                    if (picked !== undefined) setPlaces(picked);
                  }}
                >
                  {PLACES.map((p) => (
                    /* **الرقم في خيارٍ لا يقبل عنصراً.** `<option>` لا يحمل
                       إلا نصّاً، فيُكتب الشكل الآلي — وهو هنا الشكل المعروض
                       نفسه: اللغات الأربع كلّها تُعلن `digits: "latn"` في
                       `i18n/locales/*.base.ts`، فلا فرق بين الشكلين. */
                    <option key={p} value={String(p)}>
                      {i18n.integer(p).machine}
                    </option>
                  ))}
                </select>
              </SetupField>
            </div>

            <TranslationComposer
              idPrefix="stp-company-tr"
              testId="setup-company-translations"
              value={companyTranslations}
              onChange={setCompanyTranslations}
            />

            <div className="grid fields-half">
              <SetupField
                id="stp-company-answer"
                label={t("screen.setup.answerLabel")}
                hint={
                  answer === "One"
                    ? t("screen.setup.answerOneHint")
                    : t("screen.setup.answerMultipleHint")
                }
                source="typed"
                required
              >
                <select
                  id="stp-company-answer"
                  className="ctl"
                  data-testid="setup-company-answer"
                  value={answer}
                  onChange={(e) => {
                    const picked = ANSWERS.find((a) => a === e.target.value);
                    if (picked !== undefined) setAnswer(picked);
                  }}
                >
                  {ANSWERS.map((a) => (
                    <option key={a} value={a}>
                      {t("screen.setup.answer." + a)}
                    </option>
                  ))}
                </select>
              </SetupField>
              {answer === "Multiple" ? (
                <SetupField
                  id="stp-company-first"
                  label={t("screen.setup.firstCentreLabel")}
                  hint={t("screen.setup.firstCentreHint")}
                  {...(firstNameRefusal ? { error: t("screen.setup.firstCentreMissing") } : {})}
                  source="typed"
                  required
                >
                  <input
                    id="stp-company-first"
                    className="ctl"
                    lang="ar"
                    dir="rtl"
                    autoComplete="off"
                    aria-invalid={firstNameRefusal !== null}
                    data-testid="setup-company-first"
                    value={firstNameAr}
                    onChange={(e) => setFirstNameAr(e.target.value)}
                  />
                </SetupField>
              ) : (
                <div className="rowctl" data-testid="setup-company-first-absent">
                  <span className="pill pill--info">{t("screen.setup.firstCentreOmitted")}</span>
                  <span className="hint">
                    {t("screen.setup.firstCentreOmittedHint")}{" "}
                    <span className="mono" dir="ltr">{NAME_NOT_EXPECTED_CODE}</span>
                  </span>
                </div>
              )}
            </div>

            {answer === "Multiple" ? (
              <TranslationComposer
                idPrefix="stp-first-tr"
                testId="setup-first-translations"
                value={firstTranslations}
                onChange={setFirstTranslations}
              />
            ) : null}

            <div className="inline-group">
              <Button
                label={t("screen.setup.found")}
                kind="primary"
                loading={busy}
                disabled={!ready || busy}
                onClick={() => void submit()}
                testId="setup-company-found"
              />
              <span className="hint">{t("screen.setup.foundHint")}</span>
            </div>
          </div>
        )}

        {failure ? <ProblemPanel error={failure} /> : null}
      </StatePanel>

      {/* ═════════ ٣ · ما لا يستطيعه العقد المنشور — مُعلَناً لا مسكوتاً ═ */}
      <DeclaredGap
        title={t("screen.setup.gapAmendTitle")}
        body={t("screen.setup.gapAmendBody")}
        owed={t("screen.setup.gapAmendOwed")}
        testId="setup-company-gap-amend"
      />
    </section>
  );
}
