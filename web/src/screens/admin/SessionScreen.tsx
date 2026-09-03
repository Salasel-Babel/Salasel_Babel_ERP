/* ═══════════════════════════════════════════════════════════════════════════
   /admin/session — جلستي: أُجدّدها، أم أُنهيها؟
   My session — renew it, or end it?
   ───────────────────────────────────────────────────────────────────────────
   **السؤال الذي تجيبه هذه الشاشة وحدها:** «ما الذي بيدي الآن، وما الفعلان
   اللذان يقعان عليه؟»

   وثلاثةُ أبوابٍ على شاشةٍ واحدة، **ولوحان يكتبان لا ثلاثة**:
     · `readSession`   — من أنا، وأي شركاتٍ يبلغها اعتمادي (قراءة).
     · `renewSession`  — يستهلك اعتماد التجديد ويُصدر زوجاً جديداً.
     · `revokeSession` — يُبطل العائلة كلّها **فوراً**.

   ── ولماذا التجديدُ والإبطال معاً، والتسجيلُ في شاشةٍ أخرى ────────────
   لأنهما **الطريقان الوحيدان اللذان تنتهي بهما العائلة**، والعقد يقول ذلك
   بنصّه: «تقديم اعتماد التجديد مرّتين سرقة، والجواب إسقاط العائلة كلّها»
   (`access.refresh_replayed`). فالتجديد الخاطئ **هو** إبطالٌ لا رجعة فيه،
   وفصلُه عن الإبطال يجعل التحذير الواحد يُكتب مرّتين ويُقرأ مرّةً واحدة.
   أمّا `registerTenant` و`openSession` فيقعان **قبل** وجود جلسة، وذلك حدٌّ
   يقرأه العقد نفسه: البابان الوحيدان الآخران بـ`security: []`.

   ── والإبطال: أثرُه يُقال قبل الضغط، لا بعده ──────────────────────────
   `revokeSession` **بلا جسم**: لا يختار المستعمِل جلسةً يُبطلها، والاعتماد
   المُقدَّم هو الذي يسمّي ما يُبطَل. فالجواب على «من يخرج؟» واحدٌ دائماً:
   **صاحب هذه الجلسة نفسه، على كل جهازٍ يحمل اعتماداً من هذه العائلة**. ومن
   ضغط ولم يُقَل له ذلك يقرأ «خروج» ويجد نفسه خارج كل شيء — وشاشةٌ تُخرج
   مستعملَها بلا تحذير عطلٌ لا ميزة.

   ── ولا اعتماد يُعرض هنا إطلاقاً ───────────────────────────────────────
   الاعتماد الفاعل يعيش في إعداد النقل، واعتماد التجديد في حجزٍ داخل الذاكرة
   (`credential-hold.ts`). وما يُعرض عنهما وقائعُهما وحدها: أي دورة، ومتى
   ينقضي كلٌّ منهما، وأي عائلة. والحقل الذي يُلصَق فيه اعتماد تجديدٍ يدويّ
   `type="password"` ولا يُودَع في أي مكان.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, useSyncExternalStore, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readSession, renewSession, revokeSession } from "../../api/generated/client";
import type { AccessSession, SessionRevocation } from "../../api/generated/types";
import { fetchTransport, ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
import {
  credentialHold,
  holdRefreshCredential,
  releaseRefreshCredential,
  subscribeToHold,
  takeRefreshCredential,
} from "./credential-hold";
import {
  AdminField,
  AdminSectionNav,
  DeclaredGap,
  Instant,
  Irreversible,
  StatePanel,
} from "./parts";

/** أقصر اعتمادٍ يقبله العقد. ولا نحوَ ثانياً مكتوباً هنا. */
const CREDENTIAL_MIN = 16;

/** الرمز الذي يردّ به الخادم اعتمادَ تزويدٍ لا عائلة له. */
const NOT_ISSUED_HERE = "access.session_not_issued_here";

/** الشاشة كاملةً. */
export function SessionScreen(): ReactNode {
  const { t } = useT();
  const { transport, config, setConfig } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");

  const hold = useSyncExternalStore(subscribeToHold, credentialHold, credentialHold);

  const session = useQuery({
    queryKey: ["admin", "session", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    queryFn: ({ signal }) => readSession(transport, signal),
  });

  const [pasted, setPasted] = useState("");
  const [renewed, setRenewed] = useState<AccessSession | null>(null);
  const [renewBusy, setRenewBusy] = useState(false);
  const [renewFailure, setRenewFailure] = useState<unknown>(null);

  const [revocation, setRevocation] = useState<SessionRevocation | null>(null);
  const [revokeBusy, setRevokeBusy] = useState(false);
  const [revokeFailure, setRevokeFailure] = useState<unknown>(null);

  /* التجديد بابٌ بلا مصادقة (`security: []`)، فاعتماد التجديد وحده يسمّي
     العائلة. وإرسال الاعتماد الفاعل معه لا يضيف شيئاً ويجعل رفضاً واحداً
     يُقرأ رفضين. */
  const anonymous = fetchTransport({ baseUrl: config.baseUrl });

  const doRenew = useCallback(
    async (credential: string) => {
      setRenewBusy(true);
      setRenewFailure(null);
      try {
        const next = await renewSession(anonymous, { body: { refreshCredential: credential } });
        setConfig({ ...config, token: next.accessCredential });
        holdRefreshCredential(next.refreshCredential, {
          expiresAt: next.refreshExpiresAt,
          generation: next.generation,
          sessionId: next.sessionId,
        });
        setPasted("");
        setRenewed(next);
        fireArrive();
      } catch (problem) {
        /* الاعتماد المُقدَّم استُهلك بهذا النداء أياً كان الجواب — فالمحجوز
           لم يعد صالحاً، وإبقاؤه يُغري بإعادة تقديمه، وإعادةُ تقديمه تُسقط
           العائلة. فيُطلَق فوراً. */
        releaseRefreshCredential();
        setRenewFailure(problem);
      } finally {
        setRenewBusy(false);
      }
    },
    [anonymous, config, fireArrive, setConfig]
  );

  const doRevoke = useCallback(async () => {
    setRevokeBusy(true);
    setRevokeFailure(null);
    try {
      const done = await revokeSession(transport);
      setRevocation(done);
      /* الاعتماد الفاعل صار مرفوضاً عند الحدّ على الطلب التالي مباشرةً؛
         وإبقاؤه في الإعداد يجعل كل شاشةٍ بعدها تعرض رفضاً بلا سبب مفهوم. */
      releaseRefreshCredential();
      setConfig({ ...config, token: "" });
      setRenewed(null);
    } catch (problem) {
      setRevokeFailure(problem);
    } finally {
      setRevokeBusy(false);
    }
  }, [config, setConfig, transport]);

  const who = session.data ?? null;
  const revokeCode = revokeFailure instanceof ProblemError ? revokeFailure.code : null;
  const pastedOk = pasted.trim().length >= CREDENTIAL_MIN;

  return (
    <section className="stack" data-testid="admin-session-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.session.title")}</h1>
          <p className="sub">{t("screen.session.lede")}</p>
        </div>
      </header>

      <AdminSectionNav current="/admin/session" />

      {/* ═══════════════════════════ ١ · من أنا الآن ═══════════════════ */}
      <StatePanel
        title={t("screen.session.whoTitle")}
        note={t("screen.session.whoNote")}
        loading={session.isPending && session.fetchStatus === "fetching"}
        testId="admin-session-who"
      >
        {config.token === "" ? (
          <EmptyState
            title={t("screen.session.noCredentialTitle")}
            body={t("screen.session.noCredentialBody")}
            small
            testId="admin-session-no-credential"
          />
        ) : session.isError ? (
          <ProblemPanel error={session.error} onRetry={() => void session.refetch()} />
        ) : who ? (
          <div className="stack">
            <div className={"kv " + arriveCls}>
              <div>
                <div className="k">{t("screen.admin.tenantId")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-session-tenant">
                  {who.tenantId}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.admin.userId")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-session-user">
                  {who.userId}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.session.reachable")}</div>
                <div className="v" data-testid="admin-session-count">
                  <Num value={who.companyCount} />
                </div>
              </div>
              <div>
                <div className="k">{t("screen.session.family")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-session-family">
                  {hold.sessionId ?? t("screen.session.familyUnknown")}
                </div>
              </div>
            </div>

            <div className="tablewrap" data-testid="admin-session-companies">
              <table className="data">
                <caption className="visually-hidden">{t("screen.session.companiesCaption")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("screen.admin.companyId")}</th>
                    <th scope="col">{t("screen.session.companyState")}</th>
                  </tr>
                </thead>
                <tbody>
                  {who.companies.map((company) => (
                    <tr key={company.companyId}>
                      <td>
                        <span className="mono" dir="ltr">{company.companyId}</span>
                      </td>
                      <td>{company.state}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ═══════════════════════════ ٢ · التجديد ══════════════════════ */}
      <StatePanel
        title={t("screen.session.renewTitle")}
        note={t("screen.session.renewNote")}
        testId="admin-session-renew"
      >
        <p className="alert alert--warn" role="note" data-testid="admin-session-replay-warning">
          {t("screen.session.replayWarning")}
        </p>

        {hold.present ? (
          <div className="stack" data-testid="admin-session-hold">
            <div className="kv">
              <div>
                <div className="k">{t("screen.session.holdGeneration")}</div>
                <div className="v" data-testid="admin-session-hold-generation">
                  <Num value={hold.generation ?? 0} />
                </div>
              </div>
              <div>
                <div className="k">{t("screen.session.holdExpires")}</div>
                <div className="v">
                  <Instant value={hold.expiresAt ?? ""} testId="admin-session-hold-expires" />
                </div>
              </div>
            </div>
            <div className="inline-group">
              <Button
                label={t("screen.session.renewHeld")}
                kind="primary"
                loading={renewBusy}
                disabled={renewBusy}
                onClick={() => {
                  const credential = takeRefreshCredential();
                  if (credential !== null) void doRenew(credential);
                }}
                testId="admin-session-renew-held"
              />
              <span className="hint">{t("screen.session.renewHeldHint")}</span>
            </div>
          </div>
        ) : (
          <p className="hint" data-testid="admin-session-no-hold">
            {t("screen.session.noHold")}
          </p>
        )}

        <div className="grid fields-half">
          <AdminField
            id="adm-se-refresh"
            label={t("screen.session.refreshCredential")}
            hint={t("screen.session.refreshCredentialHint")}
            source="typed"
          >
            <input
              id="adm-se-refresh"
              className="ctl mono"
              type="password"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="admin-session-refresh"
              value={pasted}
              onChange={(e) => setPasted(e.target.value)}
            />
          </AdminField>
          <div className="rowctl">
            <Button
              label={t("screen.session.renewPasted")}
              loading={renewBusy}
              disabled={!pastedOk || renewBusy}
              onClick={() => void doRenew(pasted.trim())}
              testId="admin-session-renew-pasted"
            />
            <span className="hint">{t("screen.session.renewPastedHint")}</span>
          </div>
        </div>

        {renewFailure ? <ProblemPanel error={renewFailure} /> : null}

        {renewed ? (
          <div className={"kv " + arriveCls} data-testid="admin-session-renewed">
            <div>
              <div className="k">{t("screen.admin.generation")}</div>
              <div className="v" data-testid="admin-session-renewed-generation">
                <Num value={renewed.generation} />
              </div>
            </div>
            <div>
              <div className="k">{t("screen.admin.sessionId")}</div>
              <div className="v mono" dir="ltr">
                {renewed.sessionId}
              </div>
            </div>
            <div>
              <div className="k">{t("screen.admin.accessExpires")}</div>
              <div className="v">
                <Instant value={renewed.accessExpiresAt} />
              </div>
            </div>
            <div>
              <div className="k">{t("screen.session.writes")}</div>
              <div className="v" data-testid="admin-session-renewed-writes">
                {renewed.writeReachesNothing
                  ? t("screen.session.writesNothing")
                  : t("screen.session.writesSomewhere")}
              </div>
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ═══════════════════════════ ٣ · الإبطال ══════════════════════ */}
      <StatePanel
        title={t("screen.session.revokeTitle")}
        note={t("screen.session.revokeNote")}
        testId="admin-session-revoke"
      >
        {revocation ? (
          <div className="stack" data-testid="admin-session-revoked">
            <p className="alert alert--danger" role="status">
              {t("screen.session.revokedBody")}
            </p>
            <div className="kv">
              <div>
                <div className="k">{t("screen.admin.sessionId")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-session-revoked-id">
                  {revocation.sessionId}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.session.revokedReason")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-session-revoked-reason">
                  {revocation.reason}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.session.revokedAt")}</div>
                <div className="v">
                  <Instant value={revocation.revokedAt} />
                </div>
              </div>
            </div>
          </div>
        ) : (
          <Irreversible
            title={t("screen.session.revokeAskTitle")}
            effect={t("screen.session.revokeEffect")}
            acknowledge={t("screen.session.revokeAck")}
            action={t("screen.session.revokeAction")}
            busy={revokeBusy}
            {...(config.token === "" ? { blocked: t("screen.session.revokeBlocked") } : {})}
            onConfirm={() => void doRevoke()}
            testId="admin-session-revoke-confirm"
          >
            <ul className="adm-effects" data-testid="admin-session-revoke-effects">
              <li>{t("screen.session.revokeEffectSelf")}</li>
              <li>{t("screen.session.revokeEffectFamily")}</li>
              <li>{t("screen.session.revokeEffectRefresh")}</li>
              <li>{t("screen.session.revokeEffectReturn")}</li>
            </ul>
          </Irreversible>
        )}

        {revokeFailure ? (
          <>
            <ProblemPanel error={revokeFailure} />
            {revokeCode === NOT_ISSUED_HERE ? (
              <p className="alert alert--info" role="status" data-testid="admin-session-provisioning">
                {t("screen.session.provisioningCredential")}
              </p>
            ) : null}
          </>
        ) : null}
      </StatePanel>

      {/* ═════════════════════════ ٤ · ما لا يستطيعه هذا الطريق ═══════ */}
      <DeclaredGap
        title={t("screen.session.gapTitle")}
        body={t("screen.session.gapBody")}
        owed={t("screen.session.gapOwed")}
        testId="admin-session-gap"
      />
    </section>
  );
}
