/* ═══════════════════════════════════════════════════════════════════════════
   الدفعة المقدمة للمقاول من الباطن — ما صُرف، وهل بلغ الدفتر
   The subcontractor advance — what was paid out, and whether it reached the ledger
   ───────────────────────────────────────────────────────────────────────────
   **لماذا انفصلت.** شاشة الباطن كانت تحمل **ثلاثة نماذج كتابة**: تسجيل مقاول،
   وتسجيل عقد باطن، وصرف دفعةٍ مقدمة تُرحَّل. والأولان **تعريفٌ يقع مرّة**،
   والثالث **مستندٌ مالي يتكرّر كل شهر ويدخل الدفتر** — وقاعدة ADR-0077 تقول
   إن شاشةً بلغت أكثر من نموذجَي كتابة سقطت فيها قاعدة «سؤالٌ واحد لكلّ شاشة»
   وتُقسَّم قبل أن تصير هي العطل. ومن يصرف دفعةً مقدمة يسأل سؤالاً واحداً —
   «كم صُرف لهذا المقاول، وهل رُحِّل؟» — لا «كيف أسجّل مقاولاً».

   **وبابُ القراءة اليتيم يجد بيته هنا.** `readSubcontractorAdvance` منشورٌ
   ولا يبلغه شيء في الواجهة كلّها: مسوّدةٌ حُفظت ثم أُعيد تحميل الصفحة **لا
   تُعاد فتحُها**، فلا تُرحَّل ولا تُقرأ. وهو النقص الذي أعلنه ADR-0077 في
   قسم الموارد البشرية ولم يُغلق؛ وهنا يُغلق.

   وثلاثة قرارات تحكم هذا الملفّ:

   ١ · **المسوّدة ثمّ الترحيل خطوتان، وإعادةُ الترحيل آمنة.** الخادم يردّ
       الإيصال نفسه بـ`alreadyPosted = true` ورمز 200، والشاشة تقول ذلك
       صراحةً — «رُدّ إليك القيدُ الأول» لا «رُحِّل مرّتين». وإخفاؤه يجعل
       المستخدم يقرأ عملاً جديداً حيث لا عمل.

   ٢ · **مبلغ الدفعة واقعةٌ يُدخلها المستخدم** — ما صُرف فعلاً — لا رقمٌ
       يشتقّه حاسبٌ من نسبةٍ ووعاء. ولذلك **لا بند معلَّق يمنع ترحيلها**،
       وهي الترحيل الذي يقع فعلاً في هذا القسم بينما تنتظر بقيّةُ المستندات
       قراراتِ محاسب. والمبلغ نصٌّ في الاتجاهين ولا يمرّ بعائم.

   ٣ · **ولا رمز حساب**: `settlementMethod` مؤهّلُ دورٍ تقرؤه مصفوفة الترحيل،
       و`treasuryPartyId` معرّفٌ مبهم في دفتر الخزينة المساعد — لا رقم حساب.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftSubcontractorAdvance,
  postSubcontractorAdvance,
  readGuarantee,
  readSubcontract,
  readSubcontractorAdvance,
} from "../../api/generated/client";
import { Money } from "../../api/money";
import type { ProjectsDocument } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, Field, MOTION, Panel, RateValue } from "../../ui";
import {
  ContractingHead,
  DocumentReceipt,
  ExplainedEmpty,
  isMoneyText,
  LoadingPanel,
  NeedsCompany,
  PendingPolicyPanel,
  ReadProblem,
  todayIso,
  useProjects,
} from "./shared";
import { selectContracting, useContractingSelection } from "./selection";

/* ═══════════════════════════════════════ عقد الباطن الذي تُصرف عليه */

function SubcontractHead(props: { readonly subcontractId: string }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const subcontract = useQuery({
    queryKey: [
      "contracting",
      "subcontract",
      config.baseUrl,
      config.token,
      config.companyId,
      props.subcontractId,
    ],
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontract(
        transport,
        { companyId: config.companyId, subcontractId: props.subcontractId },
        signal
      ),
  });

  if (subcontract.isError) {
    return <ReadProblem error={subcontract.error} onRetry={() => void subcontract.refetch()} />;
  }
  if (!subcontract.data) {
    return <LoadingPanel what={t("contracting.subcontract.title")} testId="adv-subcontract-loading" />;
  }

  const view = subcontract.data;
  return (
    <>
      <div className={"kv " + MOTION.arrive} data-testid="adv-subcontract">
        <div>
          <div className="k">{t("contracting.common.number")}</div>
          <div className="v mono" dir="ltr" data-testid="adv-subcontract-number">
            {view.number}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.common.projectCode")}</div>
          <div className="v mono" dir="ltr">
            {view.projectCode}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.common.currency")}</div>
          <div className="v mono" dir="ltr" data-testid="adv-currency">
            {view.currencyCode}
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.common.retentionRate")}</div>
          <div className="v">
            <RateValue rate={view.retentionRate} />
          </div>
        </div>
        <div>
          <div className="k">{t("contracting.common.guaranteeMonths")}</div>
          <div className="v">
            <Num value={view.guaranteeMonths} />
          </div>
        </div>
      </div>
      {view.pendingPolicy.length > 0 ? (
        <PendingPolicyPanel
          items={view.pendingPolicy}
          subject={t("contracting.pending.subjectSubcontract", { number: view.number })}
          testId="adv-subcontract-pending"
        />
      ) : null}
    </>
  );
}

/* ═══════════════════════════════════════════════ نموذج صرف الدفعة */

function AdvanceForm(props: { readonly subcontractId: string }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [paidOn, setPaidOn] = useState(todayIso);
  const [amount, setAmount] = useState("");
  const [settlementMethod, setSettlementMethod] = useState("");
  const [treasuryPartyId, setTreasuryPartyId] = useState("");
  const [guaranteeId, setGuaranteeId] = useState("");
  const [document, setDocument] = useState<ProjectsDocument | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const guarantee = useQuery({
    queryKey: ["contracting", "guarantee", config.baseUrl, config.token, config.companyId, guaranteeId],
    enabled: guaranteeId !== "",
    retry: false,
    queryFn: ({ signal }) => readGuarantee(transport, { companyId: config.companyId, guaranteeId }, signal),
  });

  const ready =
    number !== "" && paidOn !== "" && isMoneyText(amount) && settlementMethod !== "" && treasuryPartyId !== "";

  const draft = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await draftSubcontractorAdvance(transport, {
        companyId: config.companyId,
        body: {
          number,
          subcontractId: props.subcontractId,
          paidOn,
          amount: Money.wire(amount),
          settlementMethod,
          treasuryPartyId,
          guaranteeId: guaranteeId === "" ? null : guaranteeId,
        },
      });
      setDocument(created);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [
    amount,
    config.companyId,
    guaranteeId,
    number,
    paidOn,
    props.subcontractId,
    settlementMethod,
    transport,
    treasuryPartyId,
  ]);

  const post = useCallback(async () => {
    if (!document) return;
    setBusy(true);
    setError(null);
    try {
      const receipt = await postSubcontractorAdvance(transport, {
        companyId: config.companyId,
        advanceId: document.id,
      });
      setDocument(receipt);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, document, transport]);

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field
          id="ad-number"
          label={t("contracting.common.number")}
          hint={t("contracting.advance.numberHint")}
          required
        >
          <input
            id="ad-number"
            data-testid="ad-number"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={number}
            onChange={(e) => setNumber(e.target.value)}
          />
        </Field>
        <Field
          id="ad-paid"
          label={t("contracting.advance.paidOn")}
          hint={t("contracting.advance.paidOnHint")}
          required
        >
          <input
            id="ad-paid"
            data-testid="ad-paid"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={paidOn}
            onChange={(e) => setPaidOn(e.target.value)}
          />
        </Field>
        <Field
          id="ad-amount"
          label={t("contracting.advance.amount")}
          hint={amount === "" || isMoneyText(amount) ? t("contracting.advance.amountHint") : t("contracting.common.moneyBad")}
          source="typed"
          required
        >
          <input
            id="ad-amount"
            data-testid="ad-amount"
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
      </div>

      <div className="grid fields-3">
        <Field
          id="ad-method"
          label={t("contracting.advance.settlementMethod")}
          hint={t("contracting.advance.methodRowHint")}
          required
        >
          <input
            id="ad-method"
            data-testid="ad-method"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={settlementMethod}
            onChange={(e) => setSettlementMethod(e.target.value)}
          />
        </Field>
        <Field
          id="ad-treasury"
          label={t("contracting.advance.treasury")}
          hint={t("contracting.advance.treasuryRowHint")}
          required
        >
          <input
            id="ad-treasury"
            data-testid="ad-treasury"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={treasuryPartyId}
            onChange={(e) => setTreasuryPartyId(e.target.value)}
          />
        </Field>
        <Field
          id="ad-guarantee"
          label={t("contracting.guarantee.idLabel")}
          hint={t("contracting.advance.guaranteeRowHint")}
        >
          <input
            id="ad-guarantee"
            data-testid="ad-guarantee"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            value={guaranteeId}
            onChange={(e) => setGuaranteeId(e.target.value)}
          />
        </Field>
      </div>

      {/* الشرحان الطويلان تحت الصفّ لا داخل خليّتين منه — والسبب مقيسٌ في
          ADR-0077: الاستعارة تُسوّي الصندوق لا الحبر. */}
      <p className="muted">{t("contracting.advance.settlementHint")}</p>
      <p className="muted">{t("contracting.guarantee.idHint")}</p>

      {guaranteeId !== "" ? (
        guarantee.isError ? (
          <ReadProblem error={guarantee.error} />
        ) : guarantee.data ? (
          <div className="kv" data-testid="guarantee-read">
            <div>
              <div className="k">{t("contracting.common.number")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.number}
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.kind")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.kind}
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.issuer")}</div>
              <div className="v">{guarantee.data.issuerNameAr}</div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.amount")}</div>
              <div className="v">
                <Amount value={guarantee.data.amount} />
              </div>
            </div>
            <div>
              <div className="k">{t("contracting.guarantee.expires")}</div>
              <div className="v mono" dir="ltr">
                {guarantee.data.expiresOn}
              </div>
            </div>
          </div>
        ) : (
          <LoadingPanel what={t("contracting.guarantee.title")} />
        )
      ) : null}

      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.advance.saveDraft")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void draft()}
          testId="advance-draft"
        />
        {document ? (
          <Button
            label={t("contracting.posting.post")}
            kind="primary"
            disabled={busy}
            onClick={() => void post()}
            testId="advance-post"
          />
        ) : null}
      </div>

      {document ? (
        <DocumentReceipt
          document={document}
          onRepeat={document.state === "POSTED" ? () => void post() : undefined}
          busy={busy}
          testId="advance-receipt"
        />
      ) : null}
      {error ? <ReadProblem error={error} /> : null}
      <p className="muted">{t("contracting.advance.whyItPosts")}</p>
    </div>
  );
}

/* ═════════════════════════ إعادة فتح دفعةٍ بمعرّفها — البابُ اليتيم يجد بيته */

function ReopenAdvance(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [typed, setTyped] = useState("");
  const [advanceId, setAdvanceId] = useState("");
  const [reposted, setReposted] = useState<ProjectsDocument | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const advance = useQuery({
    queryKey: ["contracting", "advance", config.baseUrl, config.token, config.companyId, advanceId],
    enabled: advanceId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontractorAdvance(transport, { companyId: config.companyId, advanceId }, signal),
  });

  const shown = reposted ?? advance.data ?? null;

  const post = useCallback(async () => {
    if (!shown) return;
    setBusy(true);
    setError(null);
    try {
      setReposted(
        await postSubcontractorAdvance(transport, { companyId: config.companyId, advanceId: shown.id })
      );
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, shown, transport]);

  return (
    <Panel
      title={t("contracting.advance.reopenTitle")}
      note={t("contracting.advance.reopenNote")}
      testId="advance-reopen-panel"
    >
      <div className="filterbar">
        <Field
          id="ad-open-id"
          label={t("contracting.advance.idLabel")}
          hint={t("contracting.advance.idHint")}
        >
          <input
            id="ad-open-id"
            data-testid="ad-open-id"
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
              label={t("contracting.advance.reopen")}
              disabled={typed === ""}
              onClick={() => {
                setReposted(null);
                setError(null);
                setAdvanceId(typed);
              }}
              testId="ad-open-go"
            />
          </div>
        </div>
      </div>

      {advanceId === "" ? (
        <ExplainedEmpty
          title={t("contracting.advance.reopenNoneTitle")}
          body={t("contracting.advance.reopenNoneBody")}
          testId="ad-open-none"
        />
      ) : advance.isError ? (
        <ReadProblem error={advance.error} onRetry={() => void advance.refetch()} />
      ) : shown ? (
        <div className="stack" data-testid="ad-open-out">
          <DocumentReceipt
            document={shown}
            onRepeat={shown.state === "POSTED" ? () => void post() : undefined}
            busy={busy}
            testId="ad-open-receipt"
          />
          {shown.state !== "POSTED" ? (
            <div className="inline-group">
              <Button
                label={t("contracting.posting.post")}
                kind="primary"
                disabled={busy}
                onClick={() => void post()}
                testId="ad-open-post"
              />
            </div>
          ) : null}
          {error ? <ReadProblem error={error} /> : null}
        </div>
      ) : (
        <LoadingPanel what={t("contracting.advance.title")} testId="ad-open-loading" />
      )}
    </Panel>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** الدفعة المقدمة للمقاولين من الباطن: صرفٌ، وترحيل، وإعادةُ فتح. */
export function SubcontractorAdvancesScreen(): ReactNode {
  const { t } = useT();
  const { config } = useApi();
  const selection = useContractingSelection();
  const [typed, setTyped] = useState("");

  /* المشاريع تُقرأ ليبقى مخزنُ الاختيار حيّاً بين الشاشتين — والاسمُ يُقرأ
     ولا يُستعمل هنا مباشرةً، فالعقدُ الباطن هو مفتاح هذه الشاشة. */
  useProjects();

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="contracting-advances">
      <ContractingHead
        title={t("contracting.advance.screenTitle")}
        lede={t("contracting.advance.screenLede")}
        testId="advances-head"
      />

      <Panel
        title={t("contracting.advance.subcontractTitle")}
        note={t("contracting.advance.subcontractNote")}
        testId="advance-subcontract-panel"
      >
        <div className="filterbar">
          <Field
            id="ad-subc-id"
            label={t("contracting.subcontract.idLabel")}
            hint={t("contracting.subcontract.idHint")}
          >
            <input
              id="ad-subc-id"
              data-testid="ad-subc-id"
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
                label={t("contracting.subcontract.read")}
                disabled={typed === ""}
                onClick={() => selectContracting({ subcontractId: typed, subcontractNumber: "" })}
                testId="ad-subc-read"
              />
            </div>
          </div>
        </div>

        {selection.subcontractId === "" ? (
          <ExplainedEmpty
            title={t("contracting.advance.needSubcontractTitle")}
            body={t("contracting.advance.needSubcontractBody")}
            testId="advance-needs-subcontract"
          />
        ) : (
          <SubcontractHead subcontractId={selection.subcontractId} />
        )}
      </Panel>

      {selection.subcontractId !== "" ? (
        <Panel
          title={t("contracting.advance.title")}
          note={t("contracting.advance.note")}
          testId="advance-panel"
        >
          <AdvanceForm subcontractId={selection.subcontractId} />
        </Panel>
      ) : null}

      <ReopenAdvance />
    </section>
  );
}
