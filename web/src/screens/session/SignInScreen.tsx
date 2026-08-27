/* ═══════════════════════════════════════════════════════════════════════════
   الدخول واختيار المنشأة — أول شاشة يراها عميل حقيقي
   Sign in and choose a company — the first screen a real customer sees
   ───────────────────────────────────────────────────────────────────────────
   المشكلة التي جاءت هذه الشاشة لأجلها: معرّف الشركة جزء إلزامي من كل مسار،
   وهو معرّف بصيغة 8-4-4-4-12 — أي شيء **لا يستطيع إنسان أن يكتبه**. فكانت
   كل شاشات القراءة تعمل بينما الشاشة الأولى مستحيلة.

   وثلاثة قرارات تحكم هذا الملف:

   ١ · الاعتماد لا يُودَع في شيفرة ولا يُرسل إلى أي مكان غير الخادم المُعلَن.
       يُلصَق هنا، ويُحفظ في المتصفّح وحده، ويُمحى بضغطة «خروج».

   ٢ · الرفض يُقرأ **بالرمز** لا بنصّ الرسالة. والرسالتان العربية والإنجليزية
       تأتيان من الخادم كما هما، والشاشة تضيف فوقهما **الخطوة التالية** —
       وهي وحدها ما تعرفه الشاشة ولا يعرفه الخادم.

   ٣ · المنشأة غير المؤسَّسة تظهر ولا تُخفى. من يبلغ منشأة واحدة لم تُؤسَّس
       بعد يجب أن يقرأ «تنتظر التأسيس»، لا قائمةً فارغة يقرؤها «اعتمادي معطوب».
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { readSession } from "../../api/generated/client";
import type { Session, SessionCompany } from "../../api/generated/types";
import { fetchTransport, ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { RECORD_TAG, resolveTranslatedName } from "../../app/translated-name";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useLocale, useT, Num } from "../../i18n/react";

/** الخطوة التالية التي تعرفها الشاشة لكل رمز رفض — مفتاح ترجمة، لا نصّ. */
const NEXT_STEP: Readonly<Record<string, string>> = {
  "auth.credential_missing": "screen.signIn.next.missing",
  "auth.credential_rejected": "screen.signIn.next.rejected",
  "auth.credential_expired": "screen.signIn.next.expired",
  "session.no_reachable_company": "screen.signIn.next.noCompany",
};

/** حالة المنشأة كما ينشرها العقد — تُقرأ ولا تُقارَن بنصّ معروض. */
const READY = "Ready";

/** الشاشة كاملةً. */
export function SignInScreen(): ReactNode {
  const { t } = useT();
  const { locale } = useLocale();
  const { config, setConfig } = useApi();
  const navigate = useNavigate();

  const [baseUrl, setBaseUrl] = useState(config.baseUrl);
  const [token, setToken] = useState(config.token);
  const tokenRef = useRef<HTMLInputElement | null>(null);

  /* ما قُدِّم فعلاً — لا ما يُكتب الآن. والفصل بينهما هو ما يمنع نداءً لكل ضغطة
     مفتاح على حقل الاعتماد. ويبدأ من الاعتماد المحفوظ إن وُجد، فمن عاد إلى
     الصفحة باعتماد محفوظ لا يُطلب منه لصقه مرّة ثانية. */
  const [presented, setPresented] = useState<{ token: string; baseUrl: string } | null>(() =>
    config.token ? { token: config.token, baseUrl: config.baseUrl } : null
  );

  const query = useQuery({
    queryKey: ["sign-in", presented?.baseUrl ?? "", presented?.token ?? ""],
    enabled: presented !== null,
    retry: false,
    gcTime: 0,
    queryFn: ({ signal }) =>
      readSession(
        fetchTransport({
          baseUrl: presented?.baseUrl ?? "",
          ...(presented?.token ? { token: presented.token } : {}),
        }),
        signal
      ),
  });

  const session: Session | null = query.data ?? null;
  const error: unknown = query.isError ? query.error : null;
  const busy = query.isFetching;

  const signIn = useCallback(
    (presentedToken: string, presentedBase: string) => {
      setPresented({ token: presentedToken, baseUrl: presentedBase });
      void query.refetch();
    },
    [query]
  );

  const choose = useCallback(
    (company: SessionCompany) => {
      setConfig({ ...config, baseUrl, token, companyId: company.companyId });
      void navigate({ to: "/" });
    },
    [baseUrl, config, navigate, setConfig, token]
  );

  const signOut = useCallback(() => {
    setToken("");
    setPresented(null);
    setConfig({ ...config, token: "", companyId: "" });
    tokenRef.current?.focus();
  }, [config, setConfig]);

  const problemCode = error instanceof ProblemError ? error.code : null;
  const nextStepKey = problemCode ? NEXT_STEP[problemCode] : undefined;

  const companies = useMemo(() => session?.companies ?? [], [session]);

  return (
    <section className="stack" data-testid="sign-in-screen">
      <header className="statline">
        <h1 style={{ margin: 0, fontSize: "var(--font-size-h1)", fontFamily: "var(--font-display)" }}>
          {t("screen.signIn.title")}
        </h1>
        {session ? (
          <span className="pill pill--posted" data-testid="session-signed-in">
            {t("screen.signIn.signedIn")}
          </span>
        ) : null}
      </header>

      <p className="muted">{t("screen.signIn.lede")}</p>

      <form
        className="card card-pad"
        data-testid="sign-in-form"
        onSubmit={(e) => {
          e.preventDefault();
          signIn(token, baseUrl);
        }}
      >
        <div className="fields-half">
          <div className="field">
            <label htmlFor="si-token">{t("field.token.label")}</label>
            <input
              id="si-token"
              ref={tokenRef}
              className="ctl mono"
              type="password"
              autoComplete="off"
              spellCheck={false}
              data-testid="sign-in-token"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              placeholder={t("screen.signIn.tokenPh")}
            />
            <span className="hint">{t("field.token.hint")}</span>
          </div>
          <div className="field">
            <label htmlFor="si-base">{t("screen.signIn.serverLabel")}</label>
            <input
              id="si-base"
              className="ctl mono"
              dir="ltr"
              data-testid="sign-in-base"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
              placeholder={t("screen.signIn.serverPh")}
            />
            <span className="hint">{t("screen.signIn.serverHint")}</span>
          </div>
        </div>

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <button type="submit" className="btn btn-primary" data-testid="sign-in-submit" disabled={busy}>
            {busy ? t("common.state.loading") : t("screen.signIn.action")}
          </button>
          {session || token ? (
            <button type="button" className="btn" data-testid="sign-out" onClick={signOut}>
              {t("screen.signIn.signOut")}
            </button>
          ) : null}
        </div>
      </form>

      {error ? (
        <>
          <ProblemPanel error={error} onRetry={() => signIn(token, baseUrl)} />
          {nextStepKey ? (
            <p className="alert alert--info" role="status" data-testid="sign-in-next-step">
              {t(nextStepKey)}
            </p>
          ) : null}
        </>
      ) : null}

      {session ? (
        <section className="card card-pad" data-testid="company-picker">
          <h2 style={{ marginTop: 0 }}>{t("screen.signIn.pickCompany")}</h2>

          <div className="kv">
            <div>
              <div className="k">{t("screen.signIn.tenant")}</div>
              <div className="v mono" data-testid="session-tenant">
                {session.tenantId}
              </div>
            </div>
            <div>
              <div className="k">{t("screen.signIn.user")}</div>
              <div className="v mono" data-testid="session-user">
                {session.userId}
              </div>
            </div>
            <div>
              <div className="k">{t("screen.signIn.reachable")}</div>
              <div className="v" data-testid="session-count">
                <Num value={session.companyCount} />
              </div>
            </div>
          </div>

          <ul className="stack" style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {companies.map((company) => (
              <CompanyRow
                key={company.companyId}
                company={company}
                locale={locale}
                selected={company.companyId === config.companyId}
                onChoose={choose}
              />
            ))}
          </ul>
        </section>
      ) : null}
    </section>
  );
}

/** صفّ منشأة واحدة في قائمة الاختيار. */
function CompanyRow(props: {
  company: SessionCompany;
  locale: string;
  selected: boolean;
  onChoose: (company: SessionCompany) => void;
}): ReactNode {
  const { t } = useT();
  const { company, locale } = props;
  const ready = company.state === READY;

  /* الاسم: السجلّ العربي دائماً، والترجمة تحته حين تختلف لغة الواجهة. */
  const record = company.nameAr ?? "";
  const resolved = resolveTranslatedName(record, company.nameTranslations, locale);

  return (
    <li className="card card-pad" data-testid="company-option" data-company={company.companyId} data-state={company.state}>
      <div className="statline">
        {ready ? (
          <strong lang={RECORD_TAG} dir="rtl" data-testid="company-name-record">
            {record}
          </strong>
        ) : (
          <strong className="muted" data-testid="company-name-record">
            {t("screen.signIn.unnamed")}
          </strong>
        )}
        <span
          className={"pill " + (ready ? "pill--posted" : "pill--pending")}
          data-testid="company-state"
        >
          {ready ? t("screen.signIn.stateReady") : t("screen.signIn.stateNotSetUp")}
        </span>
        <span className="spacer" />
        <button
          type="button"
          className={"btn" + (ready ? " btn-primary" : "")}
          data-testid="company-choose"
          disabled={!ready}
          onClick={() => props.onChoose(company)}
        >
          {props.selected ? t("screen.signIn.reopen") : t("screen.signIn.open")}
        </button>
      </div>

      {ready && resolved.fallback === false && resolved.tag !== RECORD_TAG ? (
        <div className="muted" lang={resolved.tag} data-testid="company-name-translated">
          {resolved.text}
        </div>
      ) : null}

      <div className="mono muted" dir="ltr" data-testid="company-id">
        {company.companyId}
      </div>

      {ready ? (
        <div className="hint" data-testid="company-facts">
          {t("screen.signIn.decimalPlaces")}
          {": "}
          <Num value={company.decimalPlaces ?? 0} />
          {" · "}
          {t("screen.signIn.defaultCostCenter")}
          {": "}
          <span className="mono">{company.defaultCostCenter}</span>
        </div>
      ) : (
        <p className="hint" data-testid="company-not-set-up">
          {t("screen.signIn.notSetUpBody")}
        </p>
      )}
    </li>
  );
}
