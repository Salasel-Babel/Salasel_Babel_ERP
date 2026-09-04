/* ═══════════════════════════════════════════════════════════════════════════
   /admin/enrolment — الانتساب: من لا اعتماد له بعد
   Enrolment — for whoever holds no credential yet
   ───────────────────────────────────────────────────────────────────────────
   **السؤال الذي تجيبه هذه الشاشة وحدها:** «كيف أصير صاحب جلسة؟»

   وبابان اثنان لا ثالث لهما، **وهما البابان الوحيدان في العقد المنشور اللذان
   يُخدمان بلا اعتماد** (`security: []` في `contracts/openapi/v1.json`):

     · `registerTenant`  — منشأةٌ جديدة كاملةً، ومعها **اعتماد انتساب**.
     · `openSession`     — يبدّل اعتماد الانتساب بجلسة.

   وهما لحظةٌ واحدة لا لحظتان: الأول **يُنتج** ما يستهلكه الثاني. ولذلك يقفان
   على شاشةٍ واحدة — وهذا وحده ما يجعل السرّ يعبر من بابٍ إلى باب **بلا أن
   يُرسَم على الشاشة ولا أن يُلصَق بيد أحد**.

   ── ثلاثة حدودٍ تحكم هذا الملفّ ───────────────────────────────────────
   ١ · **لا اعتماد يُعرض.** اعتماد الانتساب يخرج من الخادم مرّةً واحدة
       (المُودَع بصمته)، ولا يُرسَم هنا في DOM ولا يُكتب في رابط ولا يُسجَّل.
       وما يُعرض عنه ثلاثة: **أنه صدر**، و**متى ينقضي**، و**أنه لن يُعاد**.
       ومن أراد تسليمه إلى غيره ينسخه إلى الحافظة بضغطة — والنسخُ ينقل ولا
       يعرض. (والقياس البصري في هذا المستودع يلتقط لقطاتٍ كاملة الصفحة،
       فسرٌّ مرسوم يصير سرّاً في ملفّ صورة.)

   ٢ · **مفتاح الطلب عشوائيّ يولّده العميل ويحتفظ به.** العقد ينصّ أن كل
       معرّفات التسجيل تُشتقّ منه حتمياً، فإعادةُ الإرسال به تردّ المستأجر
       نفسه ولا تُنشئ ثانياً. **ومفتاحٌ يكتبه إنسان يصير تخميناً**، فهو هنا
       من `crypto.getRandomValues` ولا يُترك للوحة المفاتيح.

   ٣ · **الرفض يُقرأ بالرمز لا بالنصّ.** و«استُعملت دعوتك»
       (`access.enrolment_consumed`) تفترق عن «اعتماد غير مقبول»
       (`access.credential_rejected`) عمداً — والشاشة تضيف فوق رسالة الخادم
       **الخطوة التالية**، وهي وحدها ما تعرفه الشاشة ولا يعرفه الخادم.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useRef, useState, type ReactNode } from "react";
import { openSession, registerTenant } from "../../api/generated/client";
import type { AccessSession, RegisteredTenant } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { RECORD_TAG } from "../../app/translated-name";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
import { holdRefreshCredential } from "./credential-hold";
import {
  AdminField,
  AdminSectionNav,
  DeclaredGap,
  Instant,
  RoleBadge,
  StatePanel,
} from "./parts";

/** أقصر اعتمادٍ يقبله العقد. ولا نحوَ ثانياً مكتوباً هنا: الشكل يفحصه الخادم. */
const CREDENTIAL_MIN = 16;

/** الخطوة التالية التي تعرفها الشاشة لكل رمز رفض — مفتاح ترجمة، لا نصّ. */
const NEXT_STEP: Readonly<Record<string, string>> = {
  "access.credential_rejected": "screen.enrolment.next.rejected",
  "access.enrolment_expired": "screen.enrolment.next.expired",
  "access.enrolment_consumed": "screen.enrolment.next.consumed",
  "http.too_many_requests": "screen.enrolment.next.throttled",
};

/**
 * مفتاح طلبٍ عشوائيّ بأربعٍ وستّين محرفاً من أبجدية العقد.
 * <p>**عشوائيٌّ من مولّد التعمية لا من `Math.random`**: مفتاحٌ قابل للتخمين
 * يجعل بابَ تسجيلٍ مفتوحاً طريقاً إلى مستأجرِ غيرك.</p>
 */
function freshRequestKey(): string {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
  const bytes = new Uint8Array(64);
  globalThis.crypto.getRandomValues(bytes);
  let out = "";
  for (const byte of bytes) out += alphabet.charAt(byte % alphabet.length);
  return out;
}

/** الشاشة كاملةً. */
export function EnrolmentScreen(): ReactNode {
  const { t } = useT();
  const { transport, config, setConfig } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── لوح التسجيل ───────────────────────────────────────────────────── */
  const [requestKey, setRequestKey] = useState(freshRequestKey);
  const [companyNameAr, setCompanyNameAr] = useState("");
  const [ownerNameAr, setOwnerNameAr] = useState("");
  const [companyNameEn, setCompanyNameEn] = useState("");
  const [registered, setRegistered] = useState<RegisteredTenant | null>(null);
  const [registerBusy, setRegisterBusy] = useState(false);
  const [registerFailure, setRegisterFailure] = useState<unknown>(null);
  const [copied, setCopied] = useState(false);

  /* اعتماد الانتساب الواصل — **في مرجعٍ لا في حالة**: ما يسكن الحالة يُرسَم
     بسهولة، وهذا لا يُرسَم أبداً. ويُمسح فور استعماله.
     و`mintedPresent` حالةٌ منفصلة تحمل **وجوده لا قيمته**: الرسم يحتاج أن
     يعرف أنّ اعتماداً صدر، ولا يحتاج — ولا يجوز له — أن يقرأه. */
  const minted = useRef<string | null>(null);
  const [mintedPresent, setMintedPresent] = useState(false);

  /* ── لوح تفعيل الدعوة ─────────────────────────────────────────────── */
  const [pasted, setPasted] = useState("");
  const [session, setSession] = useState<AccessSession | null>(null);
  const [openBusy, setOpenBusy] = useState(false);
  const [openFailure, setOpenFailure] = useState<unknown>(null);

  /* والنقل هو نقلُ التطبيق نفسه لا نقلٌ ثانٍ يُبنى هنا.
     ولماذا لا نقلٌ **بلا اعتماد** رغم أنّ البابين `security: []`: بناءُ نقلٍ
     ثانٍ داخل شاشةٍ يتجاوز نقطةَ الحقن التي يقوم عليها كلّ فحصٍ في هذا
     المستودع — فيصير البابان الوحيدين اللذين لا يستطيع اختبارٌ أن يقيسهما.
     والاعتماد إن وُجد لا يُستشار على بابٍ بلا مصادقة، والزائرُ الأول لا
     اعتماد له أصلاً؛ ومن أبطل جلسته يُمسح اعتماده من الإعداد في الشاشة
     الثانية فلا يبقى معلَّقاً هنا. */
  const anonymous = transport;

  const nameOk = companyNameAr.trim() !== "" && ownerNameAr.trim() !== "";

  const doRegister = useCallback(async () => {
    setRegisterBusy(true);
    setRegisterFailure(null);
    setCopied(false);
    try {
      const outcome = await registerTenant(anonymous, {
        body: {
          requestKey,
          companyNameAr: companyNameAr.trim(),
          ownerNameAr: ownerNameAr.trim(),
          ...(companyNameEn.trim() === ""
            ? {}
            : { nameTranslations: [{ name: "en", value: companyNameEn.trim() }] }),
        },
      });
      minted.current = outcome.enrolmentCredential;
      setMintedPresent(outcome.enrolmentCredential !== null);
      setRegistered(outcome);
      fireArrive();
    } catch (problem) {
      setRegisterFailure(problem);
    } finally {
      setRegisterBusy(false);
    }
  }, [anonymous, companyNameAr, companyNameEn, fireArrive, ownerNameAr, requestKey]);

  const doOpen = useCallback(
    async (credential: string) => {
      setOpenBusy(true);
      setOpenFailure(null);
      try {
        const opened = await openSession(anonymous, { body: { enrolmentCredential: credential } });
        /* الاعتماد الفاعل يذهب إلى إعداد النقل — وهو المسار القائم نفسه الذي
           تسلكه شاشة الدخول. واعتماد التجديد يُحجَز في الذاكرة وحدها. */
        setConfig({ ...config, token: opened.accessCredential });
        holdRefreshCredential(opened.refreshCredential, {
          expiresAt: opened.refreshExpiresAt,
          generation: opened.generation,
          sessionId: opened.sessionId,
        });
        minted.current = null;
        setMintedPresent(false);
        setPasted("");
        setSession(opened);
        fireArrive();
      } catch (problem) {
        setOpenFailure(problem);
      } finally {
        setOpenBusy(false);
      }
    },
    [anonymous, config, fireArrive, setConfig]
  );

  const copyMinted = useCallback(() => {
    const credential = minted.current;
    if (credential === null) return;
    void globalThis.navigator?.clipboard?.writeText(credential).then(
      () => setCopied(true),
      () => setCopied(false)
    );
  }, []);

  const openCode = openFailure instanceof ProblemError ? openFailure.code : null;
  const nextStepKey = openCode ? NEXT_STEP[openCode] : undefined;

  return (
    <section className="stack" data-testid="admin-enrolment-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.enrolment.title")}</h1>
          <p className="sub">{t("screen.enrolment.lede")}</p>
        </div>
      </header>

      <AdminSectionNav current="/admin/enrolment" />

      <div className="alert alert--info" role="note" data-testid="admin-enrolment-unauthenticated">
        <div className="body">
          <p>
        {t("screen.enrolment.unauthenticated")}</p>
        </div>
      </div>

      {/* ═══════════════════════════ ١ · تسجيل منشأة جديدة ═════════════ */}
      <StatePanel
        title={t("screen.enrolment.registerTitle")}
        note={t("screen.enrolment.registerNote")}
        testId="admin-enrolment-register"
      >
        <div className="grid fields-half">
          <AdminField
            id="adm-en-company"
            label={t("screen.enrolment.companyNameAr")}
            hint={t("screen.enrolment.companyNameArHint")}
            source="typed"
            required
          >
            <input
              id="adm-en-company"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-enrolment-company"
              value={companyNameAr}
              onChange={(e) => setCompanyNameAr(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-en-owner"
            label={t("screen.enrolment.ownerNameAr")}
            hint={t("screen.enrolment.ownerNameArHint")}
            source="typed"
            required
          >
            <input
              id="adm-en-owner"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-enrolment-owner"
              value={ownerNameAr}
              onChange={(e) => setOwnerNameAr(e.target.value)}
            />
          </AdminField>
        </div>

        <div className="grid fields-half">
          <AdminField
            id="adm-en-company-en"
            label={t("screen.enrolment.companyNameEn")}
            hint={t("screen.enrolment.companyNameEnHint")}
            source="typed"
          >
            <input
              id="adm-en-company-en"
              className="ctl"
              lang="en"
              dir="ltr"
              autoComplete="off"
              data-testid="admin-enrolment-company-en"
              value={companyNameEn}
              onChange={(e) => setCompanyNameEn(e.target.value)}
            />
          </AdminField>
          <div className="rowctl">
            <Button
              label={t("screen.enrolment.newKey")}
              onClick={() => {
                setRequestKey(freshRequestKey());
                setRegistered(null);
                minted.current = null;
                setMintedPresent(false);
                setCopied(false);
              }}
              testId="admin-enrolment-new-key"
            />
            <span className="hint">{t("screen.enrolment.requestKeyHint")}</span>
          </div>
        </div>

        <div className="inline-group">
          <Button
            label={t("screen.enrolment.register")}
            kind="primary"
            loading={registerBusy}
            disabled={!nameOk || registerBusy}
            onClick={() => void doRegister()}
            testId="admin-enrolment-register-go"
          />
          <span className="hint">{t("screen.enrolment.registerFooter")}</span>
        </div>

        {registerFailure ? (
          <ProblemPanel error={registerFailure} onRetry={() => void doRegister()} />
        ) : null}

        {registered ? (
          <div className={"kv " + arriveCls} data-testid="admin-enrolment-registered">
            <div>
              <div className="k">{t("screen.enrolment.tenantCode")}</div>
              <div className="v mono" dir="ltr" data-testid="admin-enrolment-tenant-code">
                {registered.tenantCode}
              </div>
            </div>
            <div>
              <div className="k">{t("screen.enrolment.companyId")}</div>
              <div className="v mono" dir="ltr" data-testid="admin-enrolment-company-id">
                {registered.companyId}
              </div>
            </div>
            <div>
              <div className="k">{t("screen.enrolment.ownerRole")}</div>
              <div className="v">
                <RoleBadge role={registered.owner.role} testId="admin-enrolment-owner-role" />
              </div>
            </div>
            <div>
              <div className="k">{t("screen.enrolment.planOpened")}</div>
              <div className="v mono" dir="ltr" data-testid="admin-enrolment-plan">
                {registered.subscription.planCode}
              </div>
            </div>
          </div>
        ) : null}

        {registered && registered.alreadyRegistered ? (
          <div className="alert alert--warning" role="status" data-testid="admin-enrolment-already">
            <div className="body">
              <p>
            {t("screen.enrolment.alreadyRegistered")}</p>
            </div>
          </div>
        ) : null}

        {registered && mintedPresent ? (
          <div className="alert alert--info" data-testid="admin-enrolment-minted">
            <div className="body">
              <span className="title">{t("screen.enrolment.mintedTitle")}</span>
              <p>{t("screen.enrolment.mintedBody")}</p>
              <p className="hint">
                {t("screen.enrolment.mintedExpires")}{" "}
                <Instant
                  value={registered.enrolmentExpiresAt ?? ""}
                  testId="admin-enrolment-minted-expires"
                />
              </p>
              <div className="actions">
                <Button
                  label={t("screen.enrolment.useNow")}
                  kind="primary"
                  loading={openBusy}
                  disabled={openBusy}
                  onClick={() => {
                    const credential = minted.current;
                    if (credential !== null) void doOpen(credential);
                  }}
                  testId="admin-enrolment-use-now"
                />
                <Button
                  label={copied ? t("screen.enrolment.copied") : t("screen.enrolment.copy")}
                  onClick={copyMinted}
                  testId="admin-enrolment-copy"
                />
              </div>
              <p className="hint">{t("screen.enrolment.copyHint")}</p>
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ═══════════════════════════ ٢ · تفعيل دعوةٍ وصلتني ════════════ */}
      <StatePanel
        title={t("screen.enrolment.openTitle")}
        note={t("screen.enrolment.openNote")}
        testId="admin-enrolment-open"
      >
        <div className="grid fields-half">
          <AdminField
            id="adm-en-credential"
            label={t("screen.enrolment.credential")}
            hint={t("screen.enrolment.credentialHint")}
            source="typed"
            required
          >
            <input
              id="adm-en-credential"
              className="ctl mono"
              type="password"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="admin-enrolment-credential"
              value={pasted}
              onChange={(e) => setPasted(e.target.value)}
            />
          </AdminField>
          <div className="rowctl">
            <Button
              label={t("screen.enrolment.open")}
              kind="primary"
              loading={openBusy}
              disabled={pasted.trim().length < CREDENTIAL_MIN || openBusy}
              onClick={() => void doOpen(pasted.trim())}
              testId="admin-enrolment-open-go"
            />
            <span className="hint">{t("screen.enrolment.openHint")}</span>
          </div>
        </div>

        {openFailure ? (
          <>
            <ProblemPanel error={openFailure} />
            {nextStepKey ? (
              <div className="alert alert--info" role="status" data-testid="admin-enrolment-next-step">
                <div className="body">
                  <p>
                {t(nextStepKey)}</p>
                </div>
              </div>
            ) : null}
          </>
        ) : null}

        {session === null ? (
          <EmptyState
            title={t("screen.enrolment.noSessionTitle")}
            body={t("screen.enrolment.noSessionBody")}
            small
            testId="admin-enrolment-no-session"
          />
        ) : (
          <div className="stack" data-testid="admin-enrolment-session">
            <div className={"kv " + arriveCls}>
              <div>
                <div className="k">{t("screen.admin.sessionId")}</div>
                <div className="v mono" dir="ltr" data-testid="admin-enrolment-session-id">
                  {session.sessionId}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.admin.tenantId")}</div>
                <div className="v mono" dir="ltr">
                  {session.tenantId}
                </div>
              </div>
              <div>
                <div className="k">{t("screen.admin.generation")}</div>
                <div className="v" data-testid="admin-enrolment-generation">
                  <Num value={session.generation} />
                </div>
              </div>
              <div>
                <div className="k">{t("screen.admin.accessExpires")}</div>
                <div className="v">
                  <Instant value={session.accessExpiresAt} />
                </div>
              </div>
            </div>

            {session.writeReachesNothing ? (
              <div
                className="alert alert--info"
                role="status"
                data-testid="admin-enrolment-write-reaches-nothing"
              >
                <div className="body">
                  <p>{t("screen.enrolment.writeReachesNothing")}</p>
                </div>
              </div>
            ) : null}

            <div className="tablewrap" data-testid="admin-enrolment-memberships">
              <table className="data">
                <caption className="visually-hidden">{t("screen.enrolment.membershipsCaption")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("screen.admin.companyId")}</th>
                    <th scope="col">{t("screen.admin.role.label")}</th>
                  </tr>
                </thead>
                <tbody>
                  {session.memberships.map((membership) => (
                    <tr key={membership.companyId} data-testid="admin-enrolment-membership">
                      <td>
                        <span className="mono" dir="ltr">{membership.companyId}</span>
                      </td>
                      <td>
                        <RoleBadge role={membership.role} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <p className="hint" data-testid="admin-enrolment-stored">
              {t("screen.enrolment.stored")}
            </p>
          </div>
        )}
      </StatePanel>

      {/* ═════════════════════════ ٣ · ما لا يستطيعه هذا الطريق ═══════ */}
      <DeclaredGap
        title={t("screen.enrolment.gapTitle")}
        body={t("screen.enrolment.gapBody")}
        owed={t("screen.enrolment.gapOwed")}
        testId="admin-enrolment-gap"
      />
    </section>
  );
}
