/* ═══════════════════════════════════════════════════════════════════════════
   خطابات الضمان — ما هو قائمٌ على العقد، ومتى ينتهي
   Guarantees — what stands against the contract, and when it lapses
   ───────────────────────────────────────────────────────────────────────────
   **البابُ الذي لم يبلغه إنسان.** `addGuarantee` منشورٌ منذ أن نُشر القسم،
   و**لا شاشة في المنتج كلّه تستدعيه** — يبلغه الأمر المنطوق وحده، أي أنه
   يُنطَق ولا يُرى (وهي الطبقة الأولى بعينها في ADR-0077). و`readGuarantee`
   كان يعيش مخبوءاً داخل نموذج الدفعة المقدمة: حقلُ معرّفٍ يُلصَق فيُعرض
   ملخّصٌ من خمسة أسطر — قراءةٌ خادمةٌ لنموذجٍ آخر، لا سجلٌّ يُفتح.

   وخمسة قرارات تحكم هذا الملفّ:

   ١ · **الضمان سجلٌّ لا يُرحَّل**، والعقد يقول ذلك ببنيته: `Guarantee` لا
       يحمل `entryId` ولا `alreadyPosted`. فلا زرَّ ترحيلٍ هنا، ولا شارةَ
       حالةٍ فارغة تَعِد بدورةٍ لا وجود لها. وأثرُه المالي — إن وقع — يقع على
       المستخلص أو على الدفعة المقدمة التي يضمنها.

   ٢ · **واحدٌ من الاثنين إلزامي**، بنصّ العقد المنشور على `subcontractId`:
       الضمان يخصّ **عقد عميل** أو **عقد باطن**، لا كليهما ولا لا شيء. فالشاشة
       تجعله **اختياراً صريحاً** قبل الحقول لا حقلين فارغين متجاورين، وتقول
       الرفض قبل أن يقع.

   ٣ · **مبلغ الضمان لا يُحسَب ولا يُقارَن بمبلغ العقد.** نسبةُ خطاب حسن
       التنفيذ من قيمة العقد قاعدةٌ تعاقدية لا يعرفها هذا المستودع، وحسابُها
       في المتصفّح يخترع رقماً بلا مصدر — والمبلغ يُدخَل نصّاً كما هو في
       الخطاب، ويعبر السلك نصّاً.

   ٤ · **صنف الضمان ليس مجموعةً مغلقة في العقد**: الوصف يسمّي ثلاثة أصناف
       (ابتدائي · حسن تنفيذ · دفعة مقدمة) ويقول «برمزٍ يختاره المستأجر».
       فتُعرَض الثلاثة **اقتراحاً** في `datalist` ولا تُحبَس في قائمةٍ يخترعها
       هذا الملفّ — وحبسُها يمنع مستأجراً رمزُه الرابع من التسجيل أصلاً.

   ٥ · **ولا قائمةَ ضماناتٍ في العقد المنشور.** لا سردَ ضمانات عقدٍ ولا سردَ
       ضمانات منشأة: القراءة **بمعرّف** وحدها. فالشاشة تقول ذلك وتُبقي أمام
       العين ما سُجِّل في هذه الجلسة مقولاً بأنه ذاكرة تبويبة — ولا ترسم
       قائمةً تبدو سجلّاً وهي ليست به.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { addGuarantee, listAttachments, readGuarantee } from "../../api/generated/client";
import { Money } from "../../api/money";
import type { Guarantee } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, useT } from "../../i18n/react";
import { Button, Field, MOTION, Panel, StatusBadge } from "../../ui";
import {
  ContractingHead,
  ExplainedEmpty,
  Foldable,
  isMoneyText,
  LoadingPanel,
  NeedsCompany,
  ProjectContractPicker,
  ReadProblem,
  todayIso,
  useProjects,
} from "./shared";
import { useContractingSelection } from "./selection";

/** ما يخصّه الضمان — واحدٌ من الاثنين، لا كلاهما ولا لا شيء. */
const HOLDERS = ["contract", "subcontract"] as const;
type Holder = (typeof HOLDERS)[number];

/**
 * أصناف الضمان الثلاثة كما يسمّيها وصف الحقل في العقد نفسه. **وهي ليست
 * مجموعة مغلقة**: الحقل نصٌّ «برمزٍ يختاره المستأجر»، فتُعرَض اقتراحاً في
 * `datalist` ولا تُحبَس في قائمة.
 */
const KIND_SUGGESTIONS = ["bid", "performance", "advance_payment"] as const;

/** الأبواب التي **لا يحملها العقد** ويحتاجها سجلُّ ضماناتٍ حقيقي. */
const MISSING_LIST_OPERATIONS = [
  "GET /api/v1/companies/{companyId}/guarantees",
  "GET /api/v1/companies/{companyId}/project-contracts/{contractId}/guarantees",
];

/* ═══════════════════════════════════════════ عرضُ ضمانٍ كما وصل */

/**
 * ملفُّ ضمانٍ واحد — ولا شارةَ ترحيلٍ فيه: `Guarantee` لا يحمل `entryId`.
 * @param props الضمان كما وصل.
 */
function GuaranteeCard(props: { readonly guarantee: Guarantee; readonly testId?: string }): ReactNode {
  const { t } = useT();
  const g = props.guarantee;
  return (
    <div className={"stack " + MOTION.arrive} data-testid={props.testId ?? "guarantee-card"}>
      <div className="kv">
        <div>
          <div className="k">{t("contracting.common.number")}</div>
          <div className="v mono" dir="ltr" data-testid="guarantee-number">
            {g.number}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.kind")}</div>
          <div className="v mono" dir="ltr" data-testid="guarantee-kind">
            {g.kind}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.issuer")}</div>
          <div className="v">{g.issuerNameAr}</div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.amount")}</div>
          <div className="v" data-testid="guarantee-amount">
            <Amount value={g.amount} />
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.effectiveFrom")}</div>
          <div className="v mono" dir="ltr">
            {g.effectiveFrom}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.expires")}</div>
          <div className="v mono" dir="ltr" data-testid="guarantee-expires">
            {g.expiresOn}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.holder")}</div>
          <div className="v mono" dir="ltr" data-testid="guarantee-holder">
            {g.contractId ?? g.subcontractId ?? t("contracting.common.dash")}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.guarantee.attachment")}</div>
          <div className="v mono" dir="ltr">
            {g.attachmentId === "" ? t("contracting.common.dash") : g.attachmentId}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.common.state")}</div>
          <div className="v">
            <StatusBadge
              state="info"
              label={t("contracting.guarantee.neverPostsBadge")}
              title={t("contracting.guarantee.neverPosts")}
              testId="guarantee-no-posting"
            />
          </div>
        </div>
      </div>
      <p className="muted">{t("contracting.guarantee.neverPosts")}</p>
    </div>
  );
}

/* ═════════════════════════════════════════════ قراءة ضمانٍ بمعرّفه */

function ReadOneGuarantee(props: { readonly seed: string }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [typed, setTyped] = useState(props.seed);
  const [guaranteeId, setGuaranteeId] = useState("");

  const guarantee = useQuery({
    queryKey: ["contracting", "guarantee", config.baseUrl, config.token, config.companyId, guaranteeId],
    enabled: guaranteeId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readGuarantee(transport, { companyId: config.companyId, guaranteeId }, signal),
  });

  return (
    <Panel
      title={t("contracting.guarantee.readTitle")}
      note={t("contracting.guarantee.readNote")}
      testId="guarantee-read-panel"
    >
      <div className="filterbar">
        <Field
          id="gu-read-id"
          label={t("contracting.guarantee.idLabel")}
          hint={t("contracting.guarantee.idHint")}
        >
          <input
            id="gu-read-id"
            data-testid="gu-read-id"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("contracting.guarantee.read")}
              disabled={typed === ""}
              onClick={() => setGuaranteeId(typed)}
              testId="gu-read-go"
            />
          </div>
        </div>
      </div>

      {guaranteeId === "" ? (
        <ExplainedEmpty
          title={t("contracting.guarantee.noneReadTitle")}
          body={t("contracting.guarantee.noneReadBody")}
          testId="gu-read-none"
        />
      ) : guarantee.isError ? (
        <ReadProblem error={guarantee.error} onRetry={() => void guarantee.refetch()} />
      ) : guarantee.data ? (
        <GuaranteeCard guarantee={guarantee.data} testId="gu-read-out" />
      ) : (
        <LoadingPanel what={t("contracting.guarantee.title")} testId="gu-read-loading" />
      )}
    </Panel>
  );
}

/* ═════════════════════════════════════════════ نموذج تسجيل ضمان */

function NewGuaranteeForm(props: {
  readonly contractId: string;
  readonly onCreated: (guarantee: Guarantee) => void;
}): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [holder, setHolder] = useState<Holder>("contract");
  const [subcontractId, setSubcontractId] = useState("");
  const [number, setNumber] = useState("");
  const [kind, setKind] = useState("");
  const [issuerNameAr, setIssuerNameAr] = useState("");
  const [amount, setAmount] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(todayIso);
  const [expiresOn, setExpiresOn] = useState("");
  const [attachmentId, setAttachmentId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  /* ولا معرّف مرفقٍ يُكتب بيد حيث يوجد باب قراءة: الجرد يردّ الأحدث أولاً،
     ويبقى حقلُ اللصق لمعرّفٍ خارج الصفحة الأولى — والاثنان معاً لأن أحدهما
     وحده يترك حالةً لا مخرج منها. */
  const attachments = useQuery({
    queryKey: ["contracting", "attachments", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listAttachments(transport, { companyId: config.companyId }, signal),
  });

  const deposited = attachments.data?.items ?? [];

  /* الحاملُ المُصرَّح به وحده يعبر، والآخر `null` صراحةً — لا حقلٌ فارغ. */
  const holderId = holder === "contract" ? props.contractId : subcontractId;

  const ready =
    holderId !== "" &&
    number !== "" &&
    kind !== "" &&
    issuerNameAr !== "" &&
    isMoneyText(amount) &&
    effectiveFrom !== "" &&
    expiresOn !== "" &&
    attachmentId !== "";

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await addGuarantee(transport, {
        companyId: config.companyId,
        body: {
          number,
          kind,
          issuerNameAr,
          amount: Money.wire(amount),
          effectiveFrom,
          expiresOn,
          attachmentId,
          contractId: holder === "contract" ? props.contractId : null,
          subcontractId: holder === "subcontract" ? subcontractId : null,
        },
      });
      props.onCreated(created);
      setNumber("");
      setAmount("");
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [
    amount,
    attachmentId,
    config.companyId,
    effectiveFrom,
    expiresOn,
    holder,
    issuerNameAr,
    kind,
    number,
    props,
    subcontractId,
    transport,
  ]);

  return (
    <div className="stack">
      <div className="grid fields-half">
        <Field
          id="gu-holder"
          label={t("contracting.guarantee.holder")}
          hint={t("contracting.guarantee.holderHint")}
          required
        >
          <select
            id="gu-holder"
            data-testid="gu-holder"
            className="ctl"
            value={holder}
            onChange={(e) => setHolder(e.target.value as Holder)}
          >
            {HOLDERS.map((one) => (
              <option key={one} value={one}>
                {t("contracting.guarantee.holderKind." + one)}
              </option>
            ))}
          </select>
        </Field>
        {holder === "contract" ? (
          <Field
            id="gu-contract"
            label={t("contracting.common.contract")}
            hint={t("contracting.guarantee.contractHint")}
            source="read"
            required
          >
            <input
              id="gu-contract"
              data-testid="gu-contract"
              className="ctl mono"
              dir="ltr"
              readOnly
              value={props.contractId}
            />
          </Field>
        ) : (
          <Field
            id="gu-subcontract"
            label={t("contracting.guarantee.subcontract")}
            hint={t("contracting.guarantee.subcontractHint")}
            required
          >
            <input
              id="gu-subcontract"
              data-testid="gu-subcontract"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              value={subcontractId}
              onChange={(e) => setSubcontractId(e.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
        )}
      </div>

      <div className="grid fields-3">
        <Field
          id="gu-number"
          label={t("contracting.common.number")}
          hint={t("contracting.guarantee.numberHint")}
          required
        >
          <input
            id="gu-number"
            data-testid="gu-number"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={number}
            onChange={(e) => setNumber(e.target.value)}
          />
        </Field>
        <Field
          id="gu-kind"
          label={t("contracting.guarantee.kind")}
          hint={t("contracting.guarantee.kindHint")}
          required
        >
          <input
            id="gu-kind"
            data-testid="gu-kind"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            list="gu-kind-options"
            value={kind}
            onChange={(e) => setKind(e.target.value)}
          />
        </Field>
        <Field
          id="gu-issuer"
          label={t("contracting.guarantee.issuer")}
          hint={t("contracting.guarantee.issuerHint")}
          required
        >
          <input
            id="gu-issuer"
            data-testid="gu-issuer"
            className="ctl"
            lang="ar"
            value={issuerNameAr}
            onChange={(e) => setIssuerNameAr(e.target.value)}
          />
        </Field>
      </div>

      {/* والأصناف اقتراحٌ لا حبس: الحقل نصٌّ في العقد ورمزُه يختاره المستأجر. */}
      <datalist id="gu-kind-options">
        {KIND_SUGGESTIONS.map((one) => (
          <option key={one} value={one} />
        ))}
      </datalist>

      <div className="grid fields-3">
        <Field
          id="gu-amount"
          label={t("contracting.guarantee.amount")}
          hint={amount === "" || isMoneyText(amount) ? t("contracting.guarantee.amountHint") : t("contracting.common.moneyBad")}
          source="typed"
          required
        >
          <input
            id="gu-amount"
            data-testid="gu-amount"
            className={"ctl amt-input" + (amount !== "" && !isMoneyText(amount) ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={amount !== "" && !isMoneyText(amount)}
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.0000"
          />
        </Field>
        <Field
          id="gu-from"
          label={t("contracting.guarantee.effectiveFrom")}
          hint={t("contracting.guarantee.effectiveFromHint")}
          required
        >
          <input
            id="gu-from"
            data-testid="gu-from"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={effectiveFrom}
            onChange={(e) => setEffectiveFrom(e.target.value)}
          />
        </Field>
        <Field
          id="gu-to"
          label={t("contracting.guarantee.expires")}
          hint={t("contracting.guarantee.expiresHint")}
          required
        >
          <input
            id="gu-to"
            data-testid="gu-to"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={expiresOn}
            onChange={(e) => setExpiresOn(e.target.value)}
          />
        </Field>
      </div>

      <div className="grid fields-half">
        <Field
          id="gu-attachment-pick"
          label={t("contracting.guarantee.attachmentPick")}
          hint={t("contracting.guarantee.attachmentPickHint")}
        >
          <select
            id="gu-attachment-pick"
            data-testid="gu-attachment-pick"
            className="ctl"
            disabled={deposited.length === 0}
            value=""
            onChange={(e) => setAttachmentId(e.target.value)}
          >
            <option value="">{t("contracting.guarantee.attachmentNone")}</option>
            {deposited.map((one) => (
              <option key={one.id} value={one.id}>
                {one.fileName}
              </option>
            ))}
          </select>
        </Field>
        <Field
          id="gu-attachment"
          label={t("contracting.guarantee.attachment")}
          hint={t("contracting.guarantee.attachmentHint")}
          required
        >
          <input
            id="gu-attachment"
            data-testid="gu-attachment"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={attachmentId}
            onChange={(e) => setAttachmentId(e.target.value)}
          />
        </Field>
      </div>

      <p className="muted">{t("contracting.guarantee.attachmentNote")}</p>

      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.guarantee.save")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="guarantee-save"
        />
      </div>

      {holder === "contract" && props.contractId === "" ? (
        <p className="alert alert--warning" role="status" data-testid="guarantee-needs-contract">
          {t("contracting.guarantee.needsContract")}
        </p>
      ) : null}

      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** سجلّ خطابات الضمان على عقود المقاولة وعقود الباطن. */
export function GuaranteesScreen(): ReactNode {
  const { t } = useT();
  const { config } = useApi();
  const feed = useProjects();
  const selection = useContractingSelection();
  const [session, setSession] = useState<readonly Guarantee[]>([]);

  const remember = useCallback((created: Guarantee) => {
    setSession((current) => [created, ...current.filter((one) => one.id !== created.id)]);
  }, []);

  const latest = useMemo(() => session[0]?.id ?? "", [session]);

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="contracting-guarantees">
      <ContractingHead
        title={t("contracting.guarantee.screenTitle")}
        lede={t("contracting.guarantee.screenLede")}
        testId="guarantees-head"
      />

      <ProjectContractPicker feed={feed} selection={selection} testId="guarantee-picker" />

      <section className="re-pending" data-testid="guarantee-no-list">
        <div className="re-pending__head">
          <span className="pill pill--pending">{t("app.section.soon")}</span>
          <strong>{t("contracting.guarantee.noListTitle")}</strong>
        </div>
        <p className="muted">{t("contracting.guarantee.noListBody")}</p>
        <ul className="re-pending__ops">
          {MISSING_LIST_OPERATIONS.map((operation) => (
            <li key={operation} className="mono" dir="ltr">
              {operation}
            </li>
          ))}
        </ul>
      </section>

      <Foldable
        title={t("contracting.guarantee.newTitle")}
        note={t("contracting.guarantee.newNote")}
        openLabel={t("contracting.common.open")}
        closeLabel={t("contracting.common.close")}
        defaultOpen
        testId="fold-new-guarantee"
      >
        <NewGuaranteeForm contractId={selection.contractId} onCreated={remember} />
      </Foldable>

      <Panel
        title={t("contracting.guarantee.sessionTitle")}
        note={t("contracting.guarantee.sessionNote")}
        testId="guarantee-session-panel"
      >
        {session.length === 0 ? (
          <ExplainedEmpty
            title={t("contracting.guarantee.sessionEmptyTitle")}
            body={t("contracting.guarantee.sessionEmptyBody")}
            testId="guarantee-session-empty"
          />
        ) : (
          <div className="stack" data-testid="guarantee-session-list">
            {session.map((one) => (
              <div key={one.id} className="card card-pad" data-testid="guarantee-session-row">
                <GuaranteeCard guarantee={one} />
              </div>
            ))}
          </div>
        )}
      </Panel>

      <ReadOneGuarantee key={latest} seed={latest} />
    </section>
  );
}
