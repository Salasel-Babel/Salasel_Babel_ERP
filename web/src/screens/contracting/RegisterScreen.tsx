/* ═══════════════════════════════════════════════════════════════════════════
   سجلّ المشاريع والعقود — ملفّ العقد كاملاً قبل أن يُطلب منه مال
   The project and contract register — the whole dossier before money is asked of it
   ───────────────────────────────────────────────────────────────────────────
   خمسة قرارات تحكم هذا الملفّ:

   ١ · **البنود المعلَّقة تُعرض على العقد نفسه، لا عند أول محاولة ترحيل.**
       العقد المنشور يرسلها في جسم `ProjectContract` عمداً، ووظيفة هذه الشاشة
       أن تجعلها أول ما يُقرأ: من يفتح عقداً يعرف سلفاً أن مستخلصه سيُرفض،
       ولماذا، وأين يعيش السؤال — بدل أن يبني مستخلصاً كاملاً ثم يُصدَم.

   ٢ · **موقف العقد مشتقٌّ من المُرحَّل وحده، وهو بديلٌ لتقرير الربحية لا
       نسخةٌ منه.** فما يُعرض هنا ثلاثة أرقام يعرفها الخادم — عدد المستخلصات
       المُرحَّلة، والمحتجز القائم، والدفعة غير المستنفَدة — **ولا رقمَ رابعاً
       تحسبه هذه الشاشة**. وقاعدةُ تحميل تكلفة الموظف والمعدّة على المشروع لم
       تُحسم، فرقمُ ربحيةٍ مقنعٌ بلا قاعدة معلنة أسوأ من غيابه.

   ٣ · **نسبة المحتجز كسرٌ عشري لا نسبة مئوية** (العقد المنشور بنصّه: عشرة
       بالمئة تُكتب 0.10). فلا تُضرب في مئة ولا تُذيَّل بعلامة — وضربُها حسابٌ
       على قيمةٍ مالية الأثر، وعلامةٌ عليها تجعل «0.10» تُقرأ عُشر بالمئة.

   ٤ · **الكمّية تُعرض بوحدتها دائماً.** ولا وحدةَ تُحوَّل هنا: قاعدة التحويل
       يملكها المخزون، وسطرٌ تخالف وحدتُه وحدةَ بنده **يُرفض** ولا يُحوَّل.

   ٥ · **ولا رمز حسابٍ في هذه الشاشة ولا في نموذج بندٍ فيها.** البند وحدة
       تسعير داخل المشروع، والمصفوفة وحدها تقرّر الحساب (القاعدة 2).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  addProject,
  addProjectContract,
  readBoqItems,
  readContractChangeOrders,
  readContractClientCertificates,
  readContractPosition,
  readProjectContract,
} from "../../api/generated/client";
import { asRate } from "../../api/generated/brands";
import type { NameValue } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { Amount, Num } from "../../i18n/react";
import {
  Button,
  Field,
  MOTION,
  Panel,
  QuantityValue,
  RateValue,
  StatCard,
  StatusBadge,
  useMoment,
} from "../../ui";
import {
  BoqEditor,
  ContractingHead,
  ExplainedEmpty,
  Foldable,
  isCountText,
  isRateText,
  itemReady,
  newItem,
  countOf,
  LoadingPanel,
  NeedsCompany,
  PendingPolicyPanel,
  PolicySettledNote as SettledNote,
  ReadProblem,
  todayIso,
  toBoqRequest,
  TranslatedName,
  useProjects,
  type DraftItem,
} from "./shared";
import { selectContracting, useContractingSelection } from "./selection";

/* ═══════════════════════════════════════════════ نموذج تسجيل مشروع */

/** الأوسمة التي تُعرض للترجمة — الإنجليزية واحدة من N، لا حقلاً ثابتاً. */
const TRANSLATION_TAGS = ["en", "ur", "hi"] as const;

function NewProjectForm(props: { readonly onDone: () => void }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [names, setNames] = useState<Record<string, string>>({});
  const [startedOn, setStartedOn] = useState(todayIso);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [done, setDone] = useState<string | null>(null);

  const ready = code !== "" && nameAr !== "" && startedOn !== "";

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const translations: NameValue[] = TRANSLATION_TAGS.filter((tag) => (names[tag] ?? "") !== "").map(
        (tag) => ({ name: tag, value: names[tag] as string })
      );
      const created = await addProject(transport, {
        companyId: config.companyId,
        body: { code, nameAr, nameTranslations: translations, startedOn },
      });
      setDone(created.code);
      props.onDone();
      setCode("");
      setNameAr("");
      setNames({});
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [code, config.companyId, nameAr, names, props, startedOn, transport]);

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field id="np-code" label={t("contracting.common.code")} hint={t("contracting.register.codeHint")} required>
          <input id="np-code" className="ctl mono" dir="ltr" value={code} onChange={(e) => setCode(e.target.value)} />
        </Field>
        <Field id="np-name" label={t("contracting.register.nameAr")} hint={t("contracting.register.nameArHint")} required>
          <input id="np-name" className="ctl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field id="np-started" label={t("contracting.common.startedOn")} required>
          <input
            id="np-started"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={startedOn}
            onChange={(e) => setStartedOn(e.target.value)}
          />
        </Field>
      </div>
      <div className="grid fields-3">
        {TRANSLATION_TAGS.map((tag) => (
          <Field key={tag} id={"np-name-" + tag} label={t("contracting.register.translation." + tag)}>
            <input
              id={"np-name-" + tag}
              className="ctl"
              lang={tag}
              value={names[tag] ?? ""}
              onChange={(e) => setNames({ ...names, [tag]: e.target.value })}
            />
          </Field>
        ))}
      </div>
      <p className="muted">{t("contracting.register.translationNote")}</p>
      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.register.saveProject")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="new-project-save"
        />
        {done ? (
          <span className="pill pill--posted" data-testid="new-project-done">
            {done}
          </span>
        ) : null}
      </div>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═════════════════════════════════════════════ نموذج تسجيل عقد */

function NewContractForm(props: {
  readonly projectId: string;
  readonly onDone: () => void;
}): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [customerPartyId, setCustomerPartyId] = useState("");
  const [signedOn, setSignedOn] = useState(todayIso);
  const [retentionRate, setRetentionRate] = useState("");
  const [guaranteeMonths, setGuaranteeMonths] = useState("");
  const [items, setItems] = useState<DraftItem[]>(() => [newItem()]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [done, setDone] = useState<string | null>(null);

  const ready =
    number !== "" &&
    customerPartyId !== "" &&
    signedOn !== "" &&
    isRateText(retentionRate) &&
    isCountText(guaranteeMonths) &&
    items.length > 0 &&
    items.every(itemReady);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await addProjectContract(transport, {
        companyId: config.companyId,
        body: {
          number,
          projectId: props.projectId,
          customerPartyId,
          signedOn,
          retentionRate: asRate(retentionRate),
          guaranteeMonths: countOf(guaranteeMonths),
          items: items.map(toBoqRequest),
        },
      });
      setDone(created.number);
      selectContracting({ contractId: created.id, contractNumber: created.number });
      props.onDone();
      setNumber("");
      setItems([newItem()]);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, customerPartyId, guaranteeMonths, items, number, props, retentionRate, signedOn, transport]);

  return (
    <div className="stack">
      <div className="grid fields-3">
        <Field id="nc-number" label={t("contracting.common.number")} hint={t("contracting.register.numberHint")} required>
          <input id="nc-number" className="ctl mono" dir="ltr" value={number} onChange={(e) => setNumber(e.target.value)} />
        </Field>
        <Field
          id="nc-customer"
          label={t("contracting.common.customerParty")}
          hint={t("contracting.register.customerHint")}
          required
        >
          <input
            id="nc-customer"
            className="ctl mono"
            dir="ltr"
            value={customerPartyId}
            onChange={(e) => setCustomerPartyId(e.target.value)}
          />
        </Field>
        <Field id="nc-signed" label={t("contracting.common.signedOn")} required>
          <input
            id="nc-signed"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={signedOn}
            onChange={(e) => setSignedOn(e.target.value)}
          />
        </Field>
        <Field
          id="nc-retention"
          label={t("contracting.common.retentionRate")}
          hint={retentionRate === "" || isRateText(retentionRate) ? t("contracting.common.rateHint") : t("contracting.common.rateBad")}
          required
        >
          <input
            id="nc-retention"
            className={"ctl amt-input" + (retentionRate !== "" && !isRateText(retentionRate) ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            aria-invalid={retentionRate !== "" && !isRateText(retentionRate)}
            value={retentionRate}
            onChange={(e) => setRetentionRate(e.target.value)}
            placeholder="0.10"
          />
        </Field>
        <Field
          id="nc-guarantee"
          label={t("contracting.common.guaranteeMonths")}
          hint={t("contracting.register.guaranteeHint")}
          required
        >
          <input
            id="nc-guarantee"
            className="ctl mono"
            inputMode="numeric"
            dir="ltr"
            value={guaranteeMonths}
            onChange={(e) => setGuaranteeMonths(e.target.value)}
            placeholder="12"
          />
        </Field>
      </div>
      <h3 className="subhead">{t("contracting.boq.title")}</h3>
      <p className="muted">{t("contracting.boq.noAccountNote")}</p>
      <BoqEditor items={items} onChange={setItems} idPrefix="nc" />
      <div className="inline-group">
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.register.saveContract")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="new-contract-save"
        />
        {done ? (
          <span className="pill pill--posted" data-testid="new-contract-done">
            {done}
          </span>
        ) : null}
      </div>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════════ ملفّ العقد */

type Tab = "boq" | "changes" | "certificates";

/**
 * لوحُ تبويبٍ موسوم — الزرّ وحده لا يكفي: قارئ الشاشة يحتاج أن يعرف **أي محتوى**
 * يحكمه الزرّ الذي وقف عليه، وإلا صارت التبويبات ثلاثة أزرارٍ بلا أثرٍ مُعلَن.
 * @param props هوية اللوح والتبويب القائم ومحتواه.
 */
function TabPanel(props: { readonly id: Tab; readonly active: Tab; readonly children: ReactNode }): ReactNode {
  return (
    <div
      role="tabpanel"
      id={"con-panel-" + props.id}
      aria-labelledby={"con-tab-" + props.id}
      hidden={props.active !== props.id}
    >
      {props.active === props.id ? props.children : null}
    </div>
  );
}

function ContractDossier(props: { readonly contractId: string }): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>("boq");
  const [arrive, fireArrive] = useMoment("arrive");
  const scope = { companyId: config.companyId, contractId: props.contractId };
  const key = [config.baseUrl, config.token, config.companyId, props.contractId];

  const contract = useQuery({
    queryKey: ["contracting", "contract", ...key],
    retry: false,
    queryFn: ({ signal }) => readProjectContract(transport, scope, signal),
  });
  const position = useQuery({
    queryKey: ["contracting", "position", ...key],
    retry: false,
    queryFn: ({ signal }) => readContractPosition(transport, scope, signal),
  });
  const boq = useQuery({
    queryKey: ["contracting", "boq", ...key],
    retry: false,
    queryFn: ({ signal }) => readBoqItems(transport, scope, signal),
  });
  const changes = useQuery({
    queryKey: ["contracting", "changes", ...key],
    retry: false,
    queryFn: ({ signal }) => readContractChangeOrders(transport, scope, signal),
  });
  const certificates = useQuery({
    queryKey: ["contracting", "certificates", ...key],
    retry: false,
    queryFn: ({ signal }) => readContractClientCertificates(transport, scope, signal),
  });

  const view = contract.data ?? null;

  useEffect(() => {
    if (view) fireArrive();
  }, [view, fireArrive]);

  const reloadAll = useCallback(() => {
    void contract.refetch();
    void position.refetch();
    void boq.refetch();
    void changes.refetch();
    void certificates.refetch();
  }, [boq, certificates, changes, contract, position]);

  if (contract.isPending && contract.fetchStatus === "fetching") {
    return <LoadingPanel what={t("contracting.register.loadingContract")} />;
  }
  if (contract.isError) return <ReadProblem error={contract.error} onRetry={reloadAll} />;
  if (!view) return null;

  return (
    <div className="stack" data-testid="contract-dossier">
      <Panel
        title={t("contracting.register.dossier")}
        note={t("contracting.register.dossierNote")}
        testId="contract-head"
        aside={<Button label={t("contracting.common.refresh")} size="sm" onClick={reloadAll} />}
      >
        <div className={"kv " + arrive}>
          <div>
            <div className="k">{t("contracting.common.number")}</div>
            <div className="v mono" dir="ltr" data-testid="contract-number">
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
            <div className="v mono" dir="ltr">
              {view.currencyCode}
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.common.signedOn")}</div>
            <div className="v mono" dir="ltr">
              {view.signedOn}
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.common.retentionRate")}</div>
            <div className="v" data-testid="contract-retention-rate">
              <RateValue rate={view.retentionRate} />
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.common.guaranteeMonths")}</div>
            <div className="v">
              <Num value={view.guaranteeMonths} />
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.common.customerParty")}</div>
            <div className="v mono" dir="ltr">
              {view.customerPartyId}
            </div>
          </div>
        </div>
        <p className="muted">{t("contracting.common.retentionRateHint")}</p>
      </Panel>

      {view.pendingPolicy.length > 0 ? (
        <PendingPolicyPanel
          items={view.pendingPolicy}
          subject={t("contracting.pending.subjectContract", { number: view.number })}
          testId="contract-pending"
        />
      ) : (
        <SettledNote testId="contract-settled" />
      )}

      <Panel title={t("contracting.position.title")} note={t("contracting.position.note")} testId="contract-position">
        {position.isError ? (
          <ReadProblem error={position.error} onRetry={() => void position.refetch()} />
        ) : position.data ? (
          <div className="stats-row">
            <StatCard
              label={t("contracting.position.postedCertificates")}
              count={position.data.postedCertificateCount}
              hint={t("contracting.position.postedHint")}
              moment={arrive}
              testId="position-posted-count"
            />
            <StatCard
              label={t("contracting.position.retentionOutstanding")}
              amount={position.data.retentionOutstanding}
              hint={t("contracting.position.retentionHint")}
              moment={arrive}
              testId="position-retention"
            />
            <StatCard
              label={t("contracting.position.advanceOutstanding")}
              amount={position.data.advanceOutstanding}
              hint={t("contracting.position.advanceHint")}
              moment={arrive}
              testId="position-advance"
            />
          </div>
        ) : (
          <LoadingPanel what={t("contracting.position.title")} />
        )}
        <p className="muted">{t("contracting.position.noProfitability")}</p>
      </Panel>

      <Panel title={t("contracting.register.tabs")} testId="contract-tabs">
        <div className="con-tabs" role="tablist" aria-label={t("contracting.register.tabs")}>
          {(["boq", "changes", "certificates"] as const).map((id) => (
            <button
              key={id}
              type="button"
              role="tab"
              id={"con-tab-" + id}
              aria-controls={"con-panel-" + id}
              className={"btn" + (tab === id ? " btn-primary" : "")}
              aria-selected={tab === id}
              data-testid={"tab-" + id}
              onClick={() => setTab(id)}
            >
              {t("contracting.register.tab." + id)}
            </button>
          ))}
        </div>

        <TabPanel id="boq" active={tab}>
        {tab === "boq" ? (
          boq.isError ? (
            <ReadProblem error={boq.error} onRetry={() => void boq.refetch()} />
          ) : boq.data && boq.data.items.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.boq.emptyTitle")}
              body={t("contracting.boq.emptyBody")}
              testId="boq-empty"
            />
          ) : boq.data ? (
            <div className="ledger" data-testid="boq-table">
              <table>
                <caption className="visually-hidden">{t("contracting.boq.caption")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("contracting.boq.lineNo")}</th>
                    <th scope="col">{t("contracting.boq.code")}</th>
                    <th scope="col">{t("contracting.boq.description")}</th>
                    <th scope="col" className="n">
                      {t("contracting.boq.contractQuantity")}
                    </th>
                    <th scope="col" className="n">
                      {t("contracting.boq.unitRate")}
                    </th>
                    <th scope="col">{t("contracting.boq.origin")}</th>
                  </tr>
                </thead>
                <tbody>
                  {boq.data.items.map((item) => (
                    <tr key={item.id} className={MOTION.arrive} data-testid="boq-row">
                      <td className="code">
                        <Num value={item.lineNo} />
                      </td>
                      <td className="code">{item.code}</td>
                      <td>{item.descriptionAr}</td>
                      <td className="n">
                        <QuantityValue
                          magnitude={item.contractQuantity.magnitude}
                          unit={item.contractQuantity.unit}
                          /* المقياس كما وصل لا مقصوصاً: كمّيات المقاولات تُقرأ في عمودٍ
                             ويُقارَن بعمود، والمقياس الموحَّد هو ما يجعل المقارنة بالعين ممكنة. */
                          scale="wire"
                        />
                      </td>
                      <td className="n">
                        <Amount value={item.unitRate} />
                      </td>
                      <td>
                        {item.changeOrderId ? (
                          <StatusBadge state="info" label={t("contracting.boq.fromChangeOrder")} />
                        ) : (
                          <span className="muted">{t("contracting.boq.original")}</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <LoadingPanel what={t("contracting.boq.title")} />
          )
        ) : null}
        </TabPanel>

        <TabPanel id="changes" active={tab}>
        {tab === "changes" ? (
          changes.isError ? (
            <ReadProblem error={changes.error} onRetry={() => void changes.refetch()} />
          ) : changes.data && changes.data.changeOrders.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.changeOrder.emptyTitle")}
              body={t("contracting.changeOrder.emptyBody")}
              testId="changes-empty"
            />
          ) : changes.data ? (
            <div className="stack" data-testid="changes-list">
              {changes.data.changeOrders.map((order) => (
                <div key={order.id} className="card card-pad" data-testid="change-order">
                  <div className="statline">
                    <strong className="mono" dir="ltr">
                      {order.number}
                    </strong>
                    <span className="mono" dir="ltr">
                      {order.issuedOn}
                    </span>
                    <span className="muted">{tp("common.count.lines", order.addedItems.length)}</span>
                  </div>
                  <p>{order.reasonAr}</p>
                  <p className="muted">
                    {t("contracting.changeOrder.approvedBy") + ": " + order.approvedBy}
                  </p>
                </div>
              ))}
              <p className="muted">{t("contracting.changeOrder.neverPosts")}</p>
            </div>
          ) : (
            <LoadingPanel what={t("contracting.changeOrder.title")} />
          )
        ) : null}
        </TabPanel>

        <TabPanel id="certificates" active={tab}>
        {tab === "certificates" ? (
          certificates.isError ? (
            <ReadProblem error={certificates.error} onRetry={() => void certificates.refetch()} />
          ) : certificates.data && certificates.data.certificates.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.certificate.emptyTitle")}
              body={t("contracting.certificate.emptyBody")}
              action={
                <Button
                  label={t("contracting.certificate.openScreen")}
                  kind="primary"
                  onClick={() => void navigate({ to: "/contracting/certificate" })}
                />
              }
              testId="certificates-empty"
            />
          ) : certificates.data ? (
            <div className="stack" data-testid="certificates-list">
              {certificates.data.certificates.map((cert) => (
                <div key={cert.id} className="card card-pad" data-testid="certificate-row">
                  <div className="statline">
                    <strong className="mono" dir="ltr">
                      {cert.number}
                    </strong>
                    <span className="muted">
                      {t("contracting.certificate.sequence")}
                    </span>
                    <Num value={cert.sequenceNo} />
                    <span className="mono" dir="ltr">
                      {cert.periodFrom + " → " + cert.periodTo}
                    </span>
                    <StatusBadge
                      state={cert.state === "POSTED" ? "posted" : "draft"}
                      label={t("contracting.state." + cert.state)}
                    />
                    <span className="muted">{tp("common.count.lines", cert.lines.length)}</span>
                  </div>
                </div>
              ))}
              <Button
                label={t("contracting.certificate.openScreen")}
                kind="primary"
                onClick={() => void navigate({ to: "/contracting/certificate" })}
                testId="go-certificate"
              />
            </div>
          ) : (
            <LoadingPanel what={t("contracting.certificate.title")} />
          )
        ) : null}
        </TabPanel>
      </Panel>

      {/* الأمر التغييري **شاشتُه غير هذه** منذ ADR-جديد: هذه الشاشة تسجّل مشروعاً
          وعقداً — نموذجا كتابةٍ اثنان — ونموذجٌ ثالثٌ فيها يُسقط قاعدة «سؤالٌ
          واحد لكلّ شاشة». وما يبقى هنا طريقٌ إليها، لا نموذجُها. */}
      <Panel
        title={t("contracting.changeOrder.title")}
        note={t("contracting.changeOrder.movedNote")}
        testId="dossier-change-orders-link"
      >
        <Button
          label={t("contracting.changeOrder.openScreen")}
          kind="primary"
          onClick={() => void navigate({ to: "/contracting/change-orders" })}
          testId="go-change-orders"
        />
      </Panel>
    </div>
  );
}

/* ═══════════════════════════════════════════════════ الشاشة كاملةً */

/** سجلّ المشاريع والعقود. */
export function ContractingRegisterScreen(): ReactNode {
  const { t, tp } = useT();
  const { config } = useApi();
  const feed = useProjects();
  const selection = useContractingSelection();

  const project = useMemo(
    () => feed.projects.find((p) => p.id === selection.projectId) ?? null,
    [feed.projects, selection.projectId]
  );

  if (config.companyId === "") return <NeedsCompany />;

  const contractCount = feed.projects.reduce((total, p) => total + p.contracts.length, 0);

  return (
    <section className="stack" data-testid="contracting-register">
      <ContractingHead
        title={t("contracting.register.title")}
        lede={t("contracting.register.lede")}
        aside={<Button label={t("contracting.common.refresh")} onClick={feed.reload} testId="register-reload" />}
      />

      <div className="statline">
        <span className="muted" data-testid="register-project-count">
          {tp("contracting.count.projects", feed.projects.length)}
        </span>
        <span className="muted" data-testid="register-contract-count">
          {tp("contracting.count.contracts", contractCount)}
        </span>
      </div>

      {feed.loading ? <LoadingPanel what={t("contracting.register.loadingProjects")} testId="register-loading" /> : null}
      {feed.error ? <ReadProblem error={feed.error} onRetry={feed.reload} /> : null}

      {!feed.loading && !feed.error && feed.projects.length === 0 ? (
        <ExplainedEmpty
          title={t("contracting.register.emptyTitle")}
          body={t("contracting.register.emptyBody")}
          testId="register-empty"
        />
      ) : null}

      {feed.projects.length > 0 ? (
        <Panel title={t("contracting.register.projects")} note={t("contracting.register.projectsNote")}>
          <div className="con-cards" data-testid="project-cards">
            {feed.projects.map((p) => (
              <button
                key={p.id}
                type="button"
                className={"con-card " + MOTION.arrive}
                aria-pressed={p.id === selection.projectId}
                data-testid="project-card"
                onClick={() =>
                  selectContracting({
                    projectId: p.id,
                    projectCode: p.code,
                    contractId: "",
                    contractNumber: "",
                  })
                }
              >
                <span className="con-card__code mono" dir="ltr">
                  {p.code}
                </span>
                <span className="con-card__name">
                  <TranslatedName nameAr={p.nameAr} translations={p.nameTranslations} />
                </span>
                <span className="con-card__meta">
                  <span className="mono" dir="ltr">
                    {p.startedOn}
                  </span>
                  <StatusBadge
                    state={p.isActive ? "posted" : "archived"}
                    label={p.isActive ? t("contracting.common.active") : t("contracting.common.inactive")}
                  />
                  <span>{tp("contracting.count.contracts", p.contracts.length)}</span>
                </span>
              </button>
            ))}
          </div>
        </Panel>
      ) : null}

      {project ? (
        <Panel title={t("contracting.register.contracts")} note={t("contracting.register.contractsNote")}>
          {project.contracts.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.register.noContractsTitle")}
              body={t("contracting.register.noContractsBody")}
              testId="project-no-contracts"
            />
          ) : (
            <div className="inline-group" data-testid="contract-chips">
              {project.contracts.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  className={"btn" + (c.id === selection.contractId ? " btn-primary" : "")}
                  aria-pressed={c.id === selection.contractId}
                  data-testid="contract-chip"
                  onClick={() => selectContracting({ contractId: c.id, contractNumber: c.number })}
                >
                  {c.number + " · " + c.currencyCode}
                </button>
              ))}
            </div>
          )}
        </Panel>
      ) : null}

      {selection.contractId ? <ContractDossier contractId={selection.contractId} /> : null}

      <Foldable
        title={t("contracting.register.newProject")}
        note={t("contracting.register.newProjectNote")}
        openLabel={t("contracting.common.open")}
        closeLabel={t("contracting.common.close")}
        testId="fold-new-project"
      >
        <NewProjectForm onDone={feed.reload} />
      </Foldable>

      {project ? (
        <Foldable
          title={t("contracting.register.newContract")}
          note={t("contracting.register.newContractNote")}
          openLabel={t("contracting.common.open")}
          closeLabel={t("contracting.common.close")}
          testId="fold-new-contract"
        >
          <NewContractForm projectId={project.id} onDone={feed.reload} />
        </Foldable>
      ) : null}

      <p className="muted">{t("contracting.register.footnote")}</p>
    </section>
  );
}
