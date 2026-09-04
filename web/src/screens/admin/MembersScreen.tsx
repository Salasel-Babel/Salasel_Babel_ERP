/* ═══════════════════════════════════════════════════════════════════════════
   /admin/members — من يدخل هذه المنشأة، وبأيّ دور؟
   Who reaches this company, and in what role?
   ───────────────────────────────────────────────────────────────────────────
   **السؤال الذي تجيبه هذه الشاشة وحدها:** «من يعمل في هذه المنشأة، ومن أدخله،
   وبأي دور — وماذا أفعل بذلك الآن؟»

   وأربعةُ أبوابٍ على قائمةٍ واحدة: `readMemberships` تقرأ الصفوف، و
   `grantMembership` يضيف صفّاً، و`changeMembershipRole` و`revokeMembership`
   يقعان **على صفٍّ بعينه**. فهي قائمةٌ وأفعالُها، لا أربع شاشات.

   ── ودورُ «قراءةٌ فقط» يُحترَم بأن يُقال، لا بأن يُخفى زرّ ───────────────
   **الإخفاء ليس منعاً.** والمنع في الخادم: جلسةُ `Reader` تُردّ على كل فعلٍ
   غير آمن في منشأتها بـ403 و`membership.read_only`، وغيرُ المالك يُردّ على
   الدعوة بـ`membership.inviter_is_not_an_owner`. فالشاشة تفعل ثلاثة أشياء
   ولا تفعل رابعاً:
     ١ · تقرأ دور صاحب الجلسة **من القائمة نفسها** (`userId` من `readSession`
         مطابَقاً بصفوف `readMemberships`) وتُعلنه قبل أي ضغطة.
     ٢ · تُبقي الأزرار عاملة — فلا تدّعي منعاً ليس لها.
     ٣ · تُظهر الرفض **برمزه** كما ورد، وتضيف فوقه الخطوة التالية.
   وما لا تفعله: لا تُعطّل زرّاً بسبب دور، ولا تُخفيه، ولا تُخمّن قبولاً.

   ── ومعرّف العضوية هو معرّف عضوها ────────────────────────────────────
   العقد ينصّ: «هوية العضوية (المنشأة، العضو)، والمنشأة في المسار سلفاً». فلا
   يُخترَع هنا معرّفٌ ثانٍ ولا يُطلب من الخادم صفٌّ إضافي.

   ── ولا اعتماد يُعرض ──────────────────────────────────────────────────
   `grantMembership` هو **الاستجابة الوحيدة** التي يخرج فيها اعتماد انتساب،
   ويخرج **مرّة واحدة**. فلا يُرسَم هنا: يُقال إنه صدر، ومتى ينقضي، وأنه لن
   يُعاد — ويُنسخ إلى الحافظة بضغطة لمن يسلّمه بيده.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  changeMembershipRole,
  grantMembership,
  readMemberships,
  readSession,
  revokeMembership,
} from "../../api/generated/client";
import type { GrantedMembership, Membership } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { RECORD_TAG } from "../../app/translated-name";
import { useT } from "../../i18n/react";
import { Button, EmptyState, useMoment } from "../../ui";
import {
  AdminField,
  AdminSectionNav,
  ChooseCompanyFirst,
  DeclaredGap,
  Instant,
  Irreversible,
  ReadOnlyNotice,
  ROLES,
  RoleBadge,
  StatePanel,
} from "./parts";

/** رمز رفض الخادم لدعوةٍ من غير مالك — يُعرض كما ينشره العقد. */
const NOT_OWNER_CODE = "membership.inviter_is_not_an_owner";

/** الخطوة التالية التي تعرفها الشاشة لكل رمز رفض — مفتاح ترجمة، لا نصّ. */
const NEXT_STEP: Readonly<Record<string, string>> = {
  "membership.read_only": "screen.members.next.readOnly",
  [NOT_OWNER_CODE]: "screen.members.next.notOwner",
  "membership.already_granted": "screen.members.next.alreadyGranted",
  "entitlement.read_only": "screen.members.next.entitlement",
  "tenancy.company_out_of_scope": "screen.members.next.outOfScope",
};

/** لوحةُ خطوةٍ تالية تحت رفض — أو لا شيء حين لا تعرف الشاشة خطوةً. */
function NextStep(props: { readonly error: unknown; readonly testId: string }): ReactNode {
  const { t } = useT();
  const code = props.error instanceof ProblemError ? props.error.code : null;
  const key = code ? NEXT_STEP[code] : undefined;
  if (!key) return null;
  return (
    <div className="alert alert--info" role="status" data-testid={props.testId} data-code={code}>
      <div className="body">
        <p>
      {t(key)}</p>
      </div>
    </div>
  );
}

/** الشاشة كاملةً. */
export function MembersScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");

  const key = [config.baseUrl, config.token, config.companyId] as const;

  const session = useQuery({
    queryKey: ["admin", "members", "session", ...key],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readSession(transport, signal),
  });

  const list = useQuery({
    queryKey: ["admin", "members", "list", ...key],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readMemberships(transport, { companyId: config.companyId }, signal),
  });

  /* ── لوح الدعوة ────────────────────────────────────────────────────── */
  const [displayNameAr, setDisplayNameAr] = useState("");
  const [role, setRole] = useState<(typeof ROLES)[number]>("Contributor");
  const [granted, setGranted] = useState<GrantedMembership | null>(null);
  const [grantBusy, setGrantBusy] = useState(false);
  const [grantFailure, setGrantFailure] = useState<unknown>(null);
  const [copied, setCopied] = useState(false);
  const minted = useRef<string | null>(null);

  /* ── أفعالٌ على صفٍّ بعينه ─────────────────────────────────────────── */
  const [rowBusy, setRowBusy] = useState("");
  const [rowFailure, setRowFailure] = useState<unknown>(null);
  const [pendingRole, setPendingRole] = useState<Readonly<Record<string, string>>>({});
  const [confirmRevoke, setConfirmRevoke] = useState("");

  const members = useMemo(() => list.data?.members ?? [], [list.data]);

  /* دور صاحب الجلسة **في هذه المنشأة** — مقروءٌ من القائمة نفسها لا مفترضاً. */
  const myRole = useMemo(() => {
    const me = session.data?.userId;
    if (!me) return "";
    return members.find((member) => member.userId === me)?.role ?? "";
  }, [members, session.data]);

  const refresh = useCallback(() => {
    void list.refetch();
  }, [list]);

  const doGrant = useCallback(async () => {
    setGrantBusy(true);
    setGrantFailure(null);
    setCopied(false);
    try {
      const outcome = await grantMembership(transport, {
        companyId: config.companyId,
        body: { displayNameAr: displayNameAr.trim(), role },
      });
      minted.current = outcome.enrolmentCredential;
      setGranted(outcome);
      setDisplayNameAr("");
      fireArrive();
      refresh();
    } catch (problem) {
      setGrantFailure(problem);
    } finally {
      setGrantBusy(false);
    }
  }, [config.companyId, displayNameAr, fireArrive, refresh, role, transport]);

  const doChangeRole = useCallback(
    async (member: Membership, next: string) => {
      setRowBusy(member.userId);
      setRowFailure(null);
      try {
        await changeMembershipRole(transport, {
          companyId: config.companyId,
          membershipId: member.userId,
          body: { role: next as (typeof ROLES)[number] },
        });
        fireArrive();
        refresh();
      } catch (problem) {
        setRowFailure(problem);
      } finally {
        setRowBusy("");
      }
    },
    [config.companyId, fireArrive, refresh, transport]
  );

  const doRevoke = useCallback(
    async (member: Membership) => {
      setRowBusy(member.userId);
      setRowFailure(null);
      try {
        await revokeMembership(transport, {
          companyId: config.companyId,
          membershipId: member.userId,
        });
        setConfirmRevoke("");
        fireArrive();
        refresh();
      } catch (problem) {
        setRowFailure(problem);
      } finally {
        setRowBusy("");
      }
    },
    [config.companyId, fireArrive, refresh, transport]
  );

  const copyMinted = useCallback(() => {
    const credential = minted.current;
    if (credential === null) return;
    void globalThis.navigator?.clipboard?.writeText(credential).then(
      () => setCopied(true),
      () => setCopied(false)
    );
  }, []);

  if (config.companyId === "") return <ChooseCompanyFirst testId="admin-members-needs-company" />;

  const meId = session.data?.userId ?? "";

  return (
    <section className="stack" data-testid="admin-members-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.members.title")}</h1>
          <p className="sub">{t("screen.members.lede")}</p>
        </div>
      </header>

      <AdminSectionNav current="/admin/members" />

      <ReadOnlyNotice role={myRole} testId="admin-members-read-only" />

      {myRole !== "" && myRole !== "Owner" ? (
        <div className="alert alert--info" role="status" data-testid="admin-members-not-owner">
          <div className="body">
            <p>
          {t("screen.members.notOwnerNotice")}{" "}
          <span className="mono" dir="ltr">{NOT_OWNER_CODE}</span></p>
          </div>
        </div>
      ) : null}

      {/* ═══════════════════════════ ١ · الأعضاء ══════════════════════ */}
      <StatePanel
        title={t("screen.members.listTitle")}
        note={t("screen.members.listNote")}
        aside={
          list.data ? (
            <span className="muted" data-testid="admin-members-count">
              {tp("screen.members.count", list.data.memberCount)}
            </span>
          ) : null
        }
        loading={list.isPending && list.fetchStatus === "fetching"}
        testId="admin-members-list"
      >
        {list.isError ? (
          <>
            <ProblemPanel error={list.error} onRetry={refresh} />
            <NextStep error={list.error} testId="admin-members-list-next" />
          </>
        ) : members.length === 0 ? (
          <EmptyState
            title={t("screen.members.emptyTitle")}
            body={t("screen.members.emptyBody")}
            testId="admin-members-empty"
          />
        ) : (
          <div className={"tablewrap " + arriveCls} data-testid="admin-members-table">
            <table className="data">
              <caption className="visually-hidden">{t("screen.members.listTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("screen.members.name")}</th>
                  <th scope="col">{t("screen.admin.userId")}</th>
                  <th scope="col">{t("screen.admin.role.label")}</th>
                  <th scope="col">{t("screen.members.grantedAt")}</th>
                  <th scope="col">{t("screen.members.rowActions")}</th>
                </tr>
              </thead>
              <tbody>
                {members.map((member) => {
                  const chosen = pendingRole[member.userId] ?? member.role;
                  const isMe = member.userId === meId;
                  const busy = rowBusy === member.userId;
                  return (
                    <tr key={member.userId} data-testid="admin-members-row" data-user={member.userId}>
                      <td>
                        <span lang={RECORD_TAG} dir="rtl">{member.displayNameAr}</span>
                        {isMe ? (
                          <>
                            {" "}
                            <span className="pill pill--info" data-testid="admin-members-is-me">
                              {t("screen.members.thisIsYou")}
                            </span>
                          </>
                        ) : null}
                      </td>
                      <td>
                        <span className="mono" dir="ltr">{member.userId}</span>
                      </td>
                      <td>
                        <RoleBadge role={member.role} />
                      </td>
                      <td>
                        <Instant value={member.grantedAt} />
                      </td>
                      <td>
                        <div className="row">
                          <select
                            className="ctl ctl-sm"
                            aria-label={t("screen.members.chooseRole")}
                            data-testid="admin-members-role-select"
                            value={chosen}
                            onChange={(e) =>
                              setPendingRole({ ...pendingRole, [member.userId]: e.target.value })
                            }
                          >
                            {ROLES.map((option) => (
                              <option key={option} value={option}>
                                {t("screen.admin.role." + option)}
                              </option>
                            ))}
                          </select>
                          <Button
                            label={t("screen.members.applyRole")}
                            size="sm"
                            loading={busy}
                            disabled={chosen === member.role || busy}
                            onClick={() => void doChangeRole(member, chosen)}
                            testId="admin-members-apply-role"
                          />
                          <Button
                            label={t("screen.members.revoke")}
                            kind="ghost"
                            size="sm"
                            disabled={busy}
                            onClick={() => setConfirmRevoke(member.userId)}
                            testId="admin-members-revoke"
                          />
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {confirmRevoke !== "" ? (
          <RevokeConfirm
            member={members.find((m) => m.userId === confirmRevoke)}
            isMe={confirmRevoke === meId}
            busy={rowBusy === confirmRevoke}
            onCancel={() => setConfirmRevoke("")}
            onConfirm={(member) => void doRevoke(member)}
          />
        ) : null}

        {rowFailure ? (
          <>
            <ProblemPanel error={rowFailure} />
            <NextStep error={rowFailure} testId="admin-members-row-next" />
          </>
        ) : null}
      </StatePanel>

      {/* ═══════════════════════════ ٢ · الدعوة ═══════════════════════ */}
      <StatePanel
        title={t("screen.members.inviteTitle")}
        note={t("screen.members.inviteNote")}
        testId="admin-members-invite"
      >
        <div className="grid fields-half">
          <AdminField
            id="adm-me-name"
            label={t("screen.members.inviteName")}
            hint={t("screen.members.inviteNameHint")}
            source="typed"
            required
          >
            <input
              id="adm-me-name"
              className="ctl"
              lang={RECORD_TAG}
              dir="rtl"
              autoComplete="off"
              data-testid="admin-members-invite-name"
              value={displayNameAr}
              onChange={(e) => setDisplayNameAr(e.target.value)}
            />
          </AdminField>
          <AdminField
            id="adm-me-role"
            label={t("screen.members.inviteRole")}
            hint={t("screen.members.inviteRoleHint")}
            source="typed"
            required
          >
            <select
              id="adm-me-role"
              className="ctl"
              data-testid="admin-members-invite-role"
              value={role}
              onChange={(e) => setRole(e.target.value as (typeof ROLES)[number])}
            >
              {ROLES.map((option) => (
                <option key={option} value={option}>
                  {t("screen.admin.role." + option)}
                </option>
              ))}
            </select>
          </AdminField>
        </div>

        <p className="hint" data-testid="admin-members-role-meaning">
          {t("screen.admin.means." + role)}
        </p>

        <div className="inline-group">
          <Button
            label={t("screen.members.invite")}
            kind="primary"
            loading={grantBusy}
            disabled={displayNameAr.trim() === "" || grantBusy}
            onClick={() => void doGrant()}
            testId="admin-members-invite-go"
          />
          <span className="hint">{t("screen.members.inviteFooter")}</span>
        </div>

        {grantFailure ? (
          <>
            <ProblemPanel error={grantFailure} />
            <NextStep error={grantFailure} testId="admin-members-invite-next" />
          </>
        ) : null}

        {granted ? (
          <div className="alert alert--info" data-testid="admin-members-granted">
            <div className="body">
              <span className="title">{t("screen.members.grantedTitle")}</span>
              <p>
                <span lang={RECORD_TAG} dir="rtl" data-testid="admin-members-granted-name">
                  {granted.member.displayNameAr}
                </span>{" "}
                · <RoleBadge role={granted.member.role} testId="admin-members-granted-role" />
              </p>
              <p>{t("screen.members.grantedBody")}</p>
              <p className="hint">
                {t("screen.members.grantedExpires")}{" "}
                <Instant value={granted.enrolmentExpiresAt} testId="admin-members-granted-expires" />
              </p>
              <div className="actions">
                <Button
                  label={copied ? t("screen.members.copied") : t("screen.members.copy")}
                  onClick={copyMinted}
                  testId="admin-members-copy"
                />
              </div>
              <p className="hint">{t("screen.members.copyHint")}</p>
            </div>
          </div>
        ) : null}
      </StatePanel>

      {/* ═════════════════════════ ٣ · ما لا يستطيعه هذا الطريق ═══════ */}
      <DeclaredGap
        title={t("screen.members.gapTitle")}
        body={t("screen.members.gapBody")}
        owed={t("screen.members.gapOwed")}
        testId="admin-members-gap"
      />
    </section>
  );
}

/** لوحُ تأكيد سحب عضوية: يسمّي من يخرج قبل الضغط. */
function RevokeConfirm(props: {
  readonly member: Membership | undefined;
  readonly isMe: boolean;
  readonly busy: boolean;
  readonly onCancel: () => void;
  readonly onConfirm: (member: Membership) => void;
}): ReactNode {
  const { t } = useT();
  const { member } = props;
  if (!member) return null;
  return (
    <div className="stack" data-testid="admin-members-revoke-confirm-wrap">
      <Irreversible
        title={t("screen.members.revokeTitle", { name: member.displayNameAr })}
        effect={t("screen.members.revokeEffect", { name: member.displayNameAr })}
        acknowledge={t("screen.members.revokeAck")}
        action={t("screen.members.revokeAction")}
        busy={props.busy}
        onConfirm={() => props.onConfirm(member)}
        testId="admin-members-revoke-confirm"
      >
        <ul className="adm-effects" data-testid="admin-members-revoke-effects">
          <li>{t("screen.members.revokeEffectRow")}</li>
          <li>{t("screen.members.revokeEffectAudit")}</li>
          <li>{t("screen.members.revokeEffectCredential")}</li>
          {props.isMe ? (
            <li data-testid="admin-members-revoke-effect-self">{t("screen.members.revokeEffectSelf")}</li>
          ) : null}
        </ul>
      </Irreversible>
      <div className="inline-group">
        <Button
          label={t("common.action.cancel")}
          kind="ghost"
          onClick={props.onCancel}
          testId="admin-members-revoke-cancel"
        />
      </div>
    </div>
  );
}
