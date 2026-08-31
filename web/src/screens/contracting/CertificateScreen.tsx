/* ═══════════════════════════════════════════════════════════════════════════
   المستخلص — حيث يكسب هذا القسم قوته، وحيث يرفض
   The payment certificate — where this section earns its keep, and where it refuses
   ───────────────────────────────────────────────────────────────────────────
   «تُرحّل ما قِيس وترفض ما لم يُحسَم». وهذه الشاشة هي تلك الجملة مرئيّةً،
   وستّة قرارات تحكمها:

   ١ · **الكمّية تراكمية، والسابقة من آخر مستخلصٍ مُرحَّل لا من آخر مسوّدة.**
       والعمودان متجاوران بترويستين تقولان مصدر كلٍّ منهما، ولا يُجمعان في
       عمودٍ واحد اسمه «الكمّية»: مسوّدةٌ تُزيح الأساس تُنتج إيراداً مضاعفاً
       أو ناقصاً بلا رسالة (فخ-44).

   ٢ · **ولا تُحسَب قيمة الفترة هنا طرحاً.** الخادم لا ينشرها، وحسابُها في
       المتصفّح إعادةُ بناءٍ لقاعدةٍ يملكها الخادم — تنحرف عنه عند أول تعديل،
       وتُظهر رقماً لا يقابله شيء في الدفتر. فالعمودان يُعرضان كما وصلا،
       والفرق يبقى حيث يُحسَب.

   ٣ · **ولا مبالغ محسوبة في هذا المستخلص أصلاً.** قيمةُ الأعمال والضريبة
       والمحتجز واسترداد الدفعة أربعةٌ لكلٍّ منها حاسبٌ يجب أن يعيش في وحدة
       المقاولات، **ولم يُبنَ أيٌّ منها** لأن أساسه بندٌ معلَّق. ولوح «ما لا
       يُحسَب هنا» يقول ذلك بأسماء الأربعة وأسبابها، بدل أن يترك المحاسب
       يبحث عن مجموعٍ ليس في الصفحة.

   ٤ · **البنود المعلَّقة حالةٌ أولى دائمة**، تُعرض قبل زرّ الترحيل لا بعده.

   ٥ · **الرفض يُقرأ برمزه لا بنصّ رسالته.** والرمزان اللذان يخصّان هذا الباب
       — `projects.contract_policy.pending` و`projects.penalty_line_has_no_template`
       — لكلٍّ منهما خطوةٌ تالية مكتوبة، لأن رفضاً بلا خطوةٍ تالية شكوى.

   ٦ · **وهوية الترحيل تُقال صراحةً:** إرسالٌ ثانٍ بالهوية نفسها يردّ الإيصال
       الأول ومعه `alreadyPosted = true`، والشاشة تقول «رُدّ إليك قيدٌ سابق»
       لا «رُحِّل» — فالثاني يُقرأ عملاً جديداً لم يقع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftClientCertificate,
  draftSubcontractorCertificate,
  postClientCertificate,
  postSubcontractorCertificate,
  readBoqItems,
  readClientCertificate,
  readContractClientCertificates,
  readSubcontract,
  readSubcontractLines,
  readSubcontractorCertificate,
} from "../../api/generated/client";
import { SCHEMAS } from "../../api/generated/runtime-schema";
import { asMagnitude } from "../../api/generated/brands";
import { Money } from "../../api/money";
import type { Certificate, CertificateLineRequest } from "../../api/generated/types";
import { ProblemError } from "../../api/transport";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import {
  Button,
  Field,
  MOTION,
  Panel,
  QuantityValue,
  RateValue,
  StatusBadge,
  useMoment,
} from "../../ui";
import {
  ContractingHead,
  ExplainedEmpty,
  Foldable,
  isCountText,
  isMagnitudeText,
  isMoneyText,
  countOf,
  LoadingPanel,
  NeedsCompany,
  PendingPolicyPanel,
  PolicySettledNote,
  ProjectContractPicker,
  ReadProblem,
  todayIso,
  useProjects,
} from "./shared";
import { selectContracting, useContractingSelection } from "./selection";

/* ── ما يُقرأ من العقد وقت التشغيل، لا يُكتب هنا ───────────────────────── */

/** أعضاء المجموعة المغلقة لحقلٍ كما ينشرها العقد. */
function members(schema: string, field: string): readonly string[] {
  const found = SCHEMAS[schema]?.fields[field]?.e;
  if (!found || found.length === 0) {
    throw new TypeError(
      "الحقل " + schema + "." + field + " ليس مجموعة مغلقة في العقد المُولَّد. " +
        "/ is not a closed set in the generated contract."
    );
  }
  return found;
}

/** أصناف سطور المستخلص كما ينشرها العقد — لا قائمة مكتوبة بيد. */
const LINE_KINDS = members("CertificateLineRequest", "lineKind");

/** صنف سطر العمل — وهو الوحيد الذي يحمل بنداً وكمّية. */
const WORK = "WORK";

for (const kind of [WORK]) {
  if (!LINE_KINDS.includes(kind)) {
    throw new TypeError("صنف سطرٍ غير منشور في العقد · unpublished line kind: " + kind);
  }
}

/** صاحب المستخلص: عقد عميل أو عقد باطن — بابان مختلفان على السطح. */
type Owner = "client" | "subcontractor";

/* ═══════════════════════════════════ ما لا يُحسَب هنا — الغياب مُعلَن */

/** المبالغ الأربعة التي تسمّيها المصفوفة ولا حاسبَ لأيٍّ منها بعد. */
const ABSENT_AMOUNTS = ["works", "tax", "retention", "advance"] as const;

/**
 * لوح «ما لا يُحسَب هنا» — أربعة مبالغ يسمّيها العقد المنشور بغيابها.
 * <p>
 * وعرضُ رقمٍ قبل أن يُحسم أساسه أسوأ من غيابه؛ لكنّ الغياب الصامت أسوأ من
 * الاثنين، لأن المحاسب يظنّه عطلاً في الشاشة فيبحث عن المجموع في مكانٍ آخر.
 * </p>
 */
function AbsentAmounts(): ReactNode {
  const { t } = useT();
  return (
    <Panel
      title={t("contracting.absent.title")}
      note={t("contracting.absent.note")}
      testId="absent-amounts"
    >
      <div className="con-absent">
        {ABSENT_AMOUNTS.map((name) => (
          <div key={name} className="con-absent__cell" data-testid={"absent-" + name}>
            <span className="con-absent__k">{t("contracting.absent." + name)}</span>
            <span className="con-absent__v">{t("contracting.absent.notComputed")}</span>
            <span className="con-absent__why">{t("contracting.absent.why." + name)}</span>
          </div>
        ))}
      </div>
    </Panel>
  );
}

/* ═════════════════════════════════════════════ عرض مستخلصٍ قائم */

/** سطرٌ محرَّر في مسوّدة مستخلص — كل قيمةٍ نصٌّ حتى لحظة الإرسال. */
interface DraftLine {
  key: string;
  /** معرّف البند، أو فراغٌ على سطر غرامة أو خصم. */
  itemId: string;
  itemCode: string;
  lineKind: string;
  descriptionAr: string;
  /** الكمّية التراكمية نصّاً. */
  cumulative: string;
  /** وحدة البند — **تُقرأ من البند ولا تُكتب**: سطرٌ تخالف وحدتُه وحدةَ بنده يُرفض. */
  unit: string;
  /** مبلغ الغرامة أو الخصم نصّاً. */
  amount: string;
}

let lineSequence = 0;
function extraLine(kind: string): DraftLine {
  lineSequence += 1;
  return {
    key: "x" + String(lineSequence),
    itemId: "",
    itemCode: "",
    lineKind: kind,
    descriptionAr: "",
    cumulative: "0",
    unit: "",
    amount: "",
  };
}

/**
 * جدول سطور المستخلص كما وصلت — العمودان لا يُخلطان، والغرامة تُميَّز بلونها.
 * <p>
 * ومُصدَّرٌ عمداً: العمودان التراكمي والسابق هما الموضع الذي يُخلط فيه رقمان
 * فيُقرأ إيرادُ فترةٍ مضاعفاً، فيجب أن يكون له اختبارٌ يمسكه مباشرةً.
 * </p>
 * @param props المستخلص.
 */
export function CertificateLines(props: { readonly certificate: Certificate }): ReactNode {
  const { t } = useT();
  const lines = props.certificate.lines;

  if (lines.length === 0) {
    return (
      <ExplainedEmpty
        title={t("contracting.certificate.noLinesTitle")}
        body={t("contracting.certificate.noLinesBody")}
        testId="certificate-no-lines"
      />
    );
  }

  return (
    <div className="ledger" data-testid="certificate-lines">
      <table>
        <caption className="visually-hidden">{t("contracting.certificate.caption")}</caption>
        <thead>
          <tr>
            <th scope="col">{t("contracting.boq.lineNo")}</th>
            <th scope="col">{t("contracting.certificate.kind")}</th>
            <th scope="col">{t("contracting.boq.code")}</th>
            <th scope="col">{t("contracting.boq.description")}</th>
            <th scope="col" className="n con-cum">
              {t("contracting.certificate.cumulative")}
              <span className="con-sub">{t("contracting.certificate.cumulativeSub")}</span>
            </th>
            <th scope="col" className="n con-prev">
              {t("contracting.certificate.previous")}
              <span className="con-sub">{t("contracting.certificate.previousSub")}</span>
            </th>
            <th scope="col" className="n">
              {t("contracting.certificate.lineAmount")}
            </th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <tr key={line.id} data-kind={line.lineKind} data-testid="certificate-line">
              <td className="code">
                <Num value={line.lineNo} />
              </td>
              <td>
                <StatusBadge
                  state={line.lineKind === WORK ? "info" : "pending"}
                  label={t("contracting.kind." + line.lineKind)}
                />
              </td>
              <td className="code">{line.itemCode}</td>
              <td>{line.descriptionAr}</td>
              <td className="n" data-testid="line-cumulative">
                {line.lineKind === WORK ? (
                  <QuantityValue
                    magnitude={line.cumulativeQuantity.magnitude}
                    unit={line.cumulativeQuantity.unit}
                    /* المقياس كما وصل لا مقصوصاً: كمّيات المقاولات تُقرأ في عمودٍ
                       ويُقارَن بعمود، والمقياس الموحَّد هو ما يجعل المقارنة بالعين ممكنة. */
                    scale="wire"
                  />
                ) : (
                  <span className="muted">{t("contracting.common.dash")}</span>
                )}
              </td>
              <td className="n" data-testid="line-previous">
                {line.lineKind === WORK ? (
                  <QuantityValue
                    magnitude={line.previousQuantity.magnitude}
                    unit={line.previousQuantity.unit}
                    /* المقياس كما وصل لا مقصوصاً: كمّيات المقاولات تُقرأ في عمودٍ
                       ويُقارَن بعمود، والمقياس الموحَّد هو ما يجعل المقارنة بالعين ممكنة. */
                    scale="wire"
                  />
                ) : (
                  <span className="muted">{t("contracting.common.dash")}</span>
                )}
              </td>
              <td className="n">
                {line.lineKind === WORK ? (
                  <span className="muted">{t("contracting.common.dash")}</span>
                ) : (
                  <Amount value={line.amount} />
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ═══════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة المستخلص. */
export function CertificateScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const feed = useProjects();
  const selection = useContractingSelection();

  const [owner, setOwner] = useState<Owner>("client");
  const [subcontractIdInput, setSubcontractIdInput] = useState("");
  const [certificateId, setCertificateId] = useState("");
  const [certificate, setCertificate] = useState<Certificate | null>(null);
  const [postError, setPostError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [posted, fireposted] = useMoment("post");

  const ownerId = owner === "client" ? selection.contractId : selection.subcontractId;

  /* ── عقد الباطن يُقرأ بمعرّفٍ يُلصَق: لا باب قائمة له في العقد المنشور،
        فلا تخترع الشاشة قائمةً لا تملكها. والمعرّف يُقرأ من الخادم قبل أن
        يُبنى عليه شيء. */
  const subcontract = useQuery({
    queryKey: ["contracting", "subcontract", config.baseUrl, config.token, config.companyId, selection.subcontractId],
    enabled: owner === "subcontractor" && selection.subcontractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontract(transport, { companyId: config.companyId, subcontractId: selection.subcontractId }, signal),
  });

  const contractCertificates = useQuery({
    queryKey: ["contracting", "certificates", config.baseUrl, config.token, config.companyId, selection.contractId],
    enabled: owner === "client" && selection.contractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readContractClientCertificates(
        transport,
        { companyId: config.companyId, contractId: selection.contractId },
        signal
      ),
  });

  const boq = useQuery({
    queryKey: ["contracting", "boq", config.baseUrl, config.token, config.companyId, selection.contractId],
    enabled: owner === "client" && selection.contractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readBoqItems(transport, { companyId: config.companyId, contractId: selection.contractId }, signal),
  });

  const subLines = useQuery({
    queryKey: ["contracting", "sublines", config.baseUrl, config.token, config.companyId, selection.subcontractId],
    enabled: owner === "subcontractor" && selection.subcontractId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readSubcontractLines(
        transport,
        { companyId: config.companyId, subcontractId: selection.subcontractId },
        signal
      ),
  });

  /* البنود القابلة للقياس — من جدول كميات العقد أو من بنود عقد الباطن. */
  const measurable = useMemo(() => {
    if (owner === "client") {
      return (boq.data?.items ?? []).map((item) => ({
        id: item.id,
        code: item.code,
        descriptionAr: item.descriptionAr,
        unit: item.contractQuantity.unit,
      }));
    }
    return (subLines.data?.lines ?? []).map((line) => ({
      id: line.id,
      code: line.code,
      descriptionAr: line.descriptionAr,
      unit: line.contractQuantity.unit,
    }));
  }, [boq.data, owner, subLines.data]);

  const readCertificate = useCallback(
    async (id: string) => {
      setPostError(null);
      const read =
        owner === "client"
          ? await readClientCertificate(transport, { companyId: config.companyId, certificateId: id })
          : await readSubcontractorCertificate(transport, { companyId: config.companyId, certificateId: id });
      setCertificate(read);
      setCertificateId(read.id);
    },
    [config.companyId, owner, transport]
  );

  const post = useCallback(async () => {
    if (!certificate) return;
    setBusy(true);
    setPostError(null);
    try {
      const receipt =
        owner === "client"
          ? await postClientCertificate(transport, {
              companyId: config.companyId,
              certificateId: certificate.id,
            })
          : await postSubcontractorCertificate(transport, {
              companyId: config.companyId,
              certificateId: certificate.id,
            });
      setCertificate(receipt);
      fireposted();
    } catch (failure) {
      setPostError(failure);
    } finally {
      setBusy(false);
    }
  }, [certificate, config.companyId, fireposted, owner, transport]);

  if (config.companyId === "") return <NeedsCompany />;

  /* الرفض يُقرأ برمزه لا بنصّه — ولكلٍّ خطوةٌ تالية مكتوبة. */
  const refusalCode = postError instanceof ProblemError ? postError.code : null;

  return (
    <section className="stack" data-testid="contracting-certificate">
      <ContractingHead
        title={t("contracting.certificate.title")}
        lede={t("contracting.certificate.lede")}
      />

      <Panel title={t("contracting.certificate.ownerLabel")} note={t("contracting.certificate.ownerNote")}>
        <div className="inline-group" role="group" aria-label={t("contracting.certificate.ownerLabel")}>
          {(["client", "subcontractor"] as const).map((kind) => (
            <button
              key={kind}
              type="button"
              className={"btn" + (owner === kind ? " btn-primary" : "")}
              aria-pressed={owner === kind}
              data-testid={"owner-" + kind}
              onClick={() => {
                setOwner(kind);
                setCertificate(null);
                setCertificateId("");
                setPostError(null);
              }}
            >
              {t("contracting.certificate.owner." + kind)}
            </button>
          ))}
        </div>

        {owner === "client" ? (
          <ProjectContractPicker feed={feed} selection={selection} testId="certificate-picker" />
        ) : (
          <div className="filterbar con-picker">
            <Field
              id="cert-subcontract"
              label={t("contracting.subcontract.idLabel")}
              hint={t("contracting.subcontract.idHint")}
            >
              <input
                id="cert-subcontract"
                data-testid="cert-subcontract"
                className="ctl mono"
                dir="ltr"
                autoComplete="off"
                value={subcontractIdInput}
                onChange={(e) => setSubcontractIdInput(e.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
              />
            </Field>
            <div className="inline-group">
              <Button
                label={t("contracting.subcontract.read")}
                onClick={() =>
                  selectContracting({ subcontractId: subcontractIdInput, subcontractNumber: "" })
                }
                disabled={subcontractIdInput === ""}
                testId="read-subcontract"
              />
            </div>
            {subcontract.isError ? <ReadProblem error={subcontract.error} /> : null}
            {subcontract.data ? (
              <p className="muted" data-testid="subcontract-read">
                {subcontract.data.number + " · " + subcontract.data.projectCode + " · " + subcontract.data.currencyCode}
              </p>
            ) : null}
          </div>
        )}
      </Panel>

      {owner === "subcontractor" && subcontract.data && subcontract.data.pendingPolicy.length > 0 ? (
        <PendingPolicyPanel
          items={subcontract.data.pendingPolicy}
          subject={t("contracting.pending.subjectSubcontract", { number: subcontract.data.number })}
          testId="subcontract-pending"
        />
      ) : null}

      {owner === "client" && selection.contractId !== "" ? (
        <Panel
          title={t("contracting.certificate.existing")}
          note={t("contracting.certificate.existingNote")}
          testId="existing-certificates"
        >
          {contractCertificates.isError ? (
            <ReadProblem error={contractCertificates.error} />
          ) : contractCertificates.data && contractCertificates.data.certificates.length === 0 ? (
            <ExplainedEmpty
              title={t("contracting.certificate.emptyTitle")}
              body={t("contracting.certificate.emptyBody")}
              testId="certificate-list-empty"
            />
          ) : contractCertificates.data ? (
            <div className="inline-group">
              {contractCertificates.data.certificates.map((cert) => (
                <button
                  key={cert.id}
                  type="button"
                  className={"btn" + (cert.id === certificateId ? " btn-primary" : "")}
                  aria-pressed={cert.id === certificateId}
                  data-testid="certificate-chip"
                  onClick={() => void readCertificate(cert.id)}
                >
                  {cert.number}
                </button>
              ))}
            </div>
          ) : (
            <LoadingPanel what={t("contracting.certificate.title")} />
          )}
        </Panel>
      ) : null}

      {owner === "subcontractor" ? (
        <Panel title={t("contracting.certificate.byId")} note={t("contracting.certificate.byIdNote")}>
          <div className="filterbar">
            <Field id="cert-id" label={t("contracting.certificate.idLabel")}>
              <input
                id="cert-id"
                data-testid="cert-id"
                className="ctl mono"
                dir="ltr"
                autoComplete="off"
                value={certificateId}
                onChange={(e) => setCertificateId(e.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
              />
            </Field>
            <div className="inline-group">
              <Button
                label={t("contracting.certificate.read")}
                onClick={() => void readCertificate(certificateId)}
                disabled={certificateId === ""}
                testId="read-certificate"
              />
            </div>
          </div>
        </Panel>
      ) : null}

      {certificate ? (
        <>
          <Panel
            title={t("contracting.certificate.dossier")}
            testId="certificate-head"
            aside={
              <StatusBadge
                state={certificate.state === "POSTED" ? "posted" : "draft"}
                label={t("contracting.state." + certificate.state)}
                testId="certificate-state"
              />
            }
          >
            <div className={"kv " + posted}>
              <div>
                <div className="k">{t("contracting.common.number")}</div>
                <div className="v mono" dir="ltr" data-testid="certificate-number">
                  {certificate.number}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.certificate.sequence")}</div>
                <div className="v" data-testid="certificate-sequence">
                  <Num value={certificate.sequenceNo} />
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.certificate.period")}</div>
                <div className="v mono" dir="ltr" data-testid="certificate-period">
                  {certificate.periodFrom + " → " + certificate.periodTo}
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.certificate.frozenRate")}</div>
                <div className="v" data-testid="certificate-rate">
                  <RateValue rate={certificate.retentionRate} />
                </div>
              </div>
              <div>
                <div className="k">{t("contracting.posting.entry")}</div>
                <div className="v mono" dir="ltr" data-testid="certificate-entry">
                  {certificate.entryId ?? t("contracting.common.dash")}
                </div>
              </div>
            </div>
            <p className="muted">{t("contracting.certificate.frozenRateNote")}</p>
            {certificate.alreadyPosted ? (
              <p className="alert alert--info" role="status" data-testid="certificate-already-posted">
                {t("contracting.posting.againBody")}
              </p>
            ) : null}
          </Panel>

          {certificate.pendingPolicy.length > 0 ? (
            <PendingPolicyPanel
              items={certificate.pendingPolicy}
              subject={t("contracting.pending.subjectCertificate", { number: certificate.number })}
              testId="certificate-pending"
            />
          ) : (
            <PolicySettledNote testId="certificate-settled" />
          )}

          <Panel
            title={t("contracting.certificate.measured")}
            note={t("contracting.certificate.measuredNote")}
            testId="certificate-measured"
            aside={<span className="muted">{tp("common.count.lines", certificate.lines.length)}</span>}
          >
            <CertificateLines certificate={certificate} />
            <p className="muted">{t("contracting.certificate.noPeriodColumn")}</p>
          </Panel>

          <AbsentAmounts />

          <Panel title={t("contracting.posting.title")} note={t("contracting.posting.note")} testId="certificate-posting">
            <div className="inline-group">
              <Button
                label={busy ? t("contracting.common.loading") : t("contracting.posting.post")}
                kind="primary"
                disabled={busy || certificate.state === "POSTED"}
                onClick={() => void post()}
                testId="post-certificate"
              />
              <Button
                label={t("contracting.certificate.reread")}
                onClick={() => void readCertificate(certificate.id)}
                testId="reread-certificate"
              />
            </div>

            {postError ? (
              <div className="stack" data-testid="posting-refusal">
                <ReadProblem error={postError} />
                {refusalCode === "projects.contract_policy.pending" ? (
                  <p className="alert alert--warning" role="status" data-testid="next-pending">
                    {t("contracting.posting.nextPending")}
                  </p>
                ) : null}
                {refusalCode === "projects.contract_policy.resolution_not_implemented" ? (
                  <p className="alert alert--warning" role="status" data-testid="next-resolution">
                    {t("contracting.posting.nextResolution")}
                  </p>
                ) : null}
                {refusalCode === "projects.penalty_line_has_no_template" ? (
                  <p className="alert alert--warning" role="status" data-testid="next-penalty">
                    {t("contracting.posting.nextPenalty")}
                  </p>
                ) : null}
                {refusalCode === "projects.cumulative_quantity_went_down" ? (
                  <p className="alert alert--warning" role="status" data-testid="next-went-down">
                    {t("contracting.posting.nextWentDown")}
                  </p>
                ) : null}
              </div>
            ) : null}
          </Panel>
        </>
      ) : null}

      {ownerId !== "" ? (
        <Foldable
          title={t("contracting.certificate.draft")}
          note={t("contracting.certificate.draftNote")}
          openLabel={t("contracting.common.open")}
          closeLabel={t("contracting.common.close")}
          testId="fold-draft-certificate"
        >
          <DraftCertificateForm
            owner={owner}
            ownerId={ownerId}
            items={measurable}
            onDrafted={(drafted) => {
              setCertificate(drafted);
              setCertificateId(drafted.id);
              setPostError(null);
              if (owner === "client") void contractCertificates.refetch();
            }}
          />
        </Foldable>
      ) : (
        <ExplainedEmpty
          title={t("contracting.certificate.pickOwnerTitle")}
          body={t("contracting.certificate.pickOwnerBody")}
          testId="certificate-pick-owner"
        />
      )}
    </section>
  );
}

/* ═════════════════════════════════════════════ مسوّدة مستخلصٍ جديد */

/** بندٌ قابل للقياس، مجرَّداً عن بابه: جدولُ كميات العقد أو بنود عقد الباطن. */
interface MeasurableItem {
  readonly id: string;
  readonly code: string;
  readonly descriptionAr: string;
  readonly unit: string;
}

function DraftCertificateForm(props: {
  readonly owner: Owner;
  readonly ownerId: string;
  readonly items: readonly MeasurableItem[];
  readonly onDrafted: (certificate: Certificate) => void;
}): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [number, setNumber] = useState("");
  const [sequenceNo, setSequenceNo] = useState("");
  const [periodFrom, setPeriodFrom] = useState(todayIso);
  const [periodTo, setPeriodTo] = useState(todayIso);
  const [measured, setMeasured] = useState<Record<string, string>>({});
  const [extras, setExtras] = useState<DraftLine[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  /*
   * ⚠ **ولا تُعرض هنا «الكمّية السابقة» قبل الإرسال.**
   * الخادم يشتقّها من **أعلى كمّية بلغها مستخلصٌ مُرحَّل** على البند — وهي
   * قاعدةٌ يملكها هو. وإعادةُ بنائها في المتصفّح (بأخذ أكبر تراكميٍّ بين
   * المستخلصات المُرحَّلة) نسخةٌ ثانية تنحرف عن أصلها عند أول تعديل، وتُظهر
   * للمحاسب أساساً غير الذي سيُطرح فعلاً. فالسابقة تصل **مع جواب المسوّدة**
   * وتُعرض حينها في الجدول، ولا تُخمَّن قبله.
   */

  const chosen = props.items.filter((item) => (measured[item.id] ?? "") !== "");
  const badQuantities = chosen.filter((item) => !isMagnitudeText(measured[item.id] as string));
  const badExtras = extras.filter((line) => !isMoneyText(line.amount) || line.descriptionAr === "");

  const ready =
    number !== "" &&
    isCountText(sequenceNo) &&
    periodFrom !== "" &&
    periodTo !== "" &&
    chosen.length + extras.length > 0 &&
    badQuantities.length === 0 &&
    badExtras.length === 0;

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const workLines: CertificateLineRequest[] = chosen.map((item) => ({
        itemId: item.id,
        lineKind: WORK,
        descriptionAr: item.descriptionAr,
        cumulativeQuantity: {
          magnitude: asMagnitude(measured[item.id] as string),
          /* الوحدة تُقرأ من البند ولا تُكتب: مخالفتها ترفض السطر، ولا تحويل. */
          unit: item.unit,
        },
        /* سطر العمل لا يحمل مبلغاً — قيمتُه تُشتقّ من الكمّية والسعر في الخادم. */
        amount: Money.wire("0"),
      }));

      const extraLines: CertificateLineRequest[] = extras.map((line) => ({
        itemId: null,
        /* التحويل الوحيد هنا وعند الحدّ: القيمة تأتي من قائمةٍ **مقروءة من
           العقد نفسه** وقت التشغيل، فهي عضوٌ في المجموعة المغلقة بحكم مصدرها
           — لكنّ TypeScript لا يعرف ذلك عن نصٍّ قرأه من runtime-schema. */
        lineKind: line.lineKind as CertificateLineRequest["lineKind"],
        descriptionAr: line.descriptionAr,
        /* سطر الغرامة بلا بندٍ وبلا كمّية: صفرٌ بلا وحدة، كما يصفه العقد. */
        cumulativeQuantity: { magnitude: asMagnitude("0"), unit: "" },
        amount: Money.wire(line.amount),
      }));

      const body = {
        number,
        ownerId: props.ownerId,
        sequenceNo: countOf(sequenceNo),
        periodFrom,
        periodTo,
        lines: [...workLines, ...extraLines],
      };

      const drafted =
        props.owner === "client"
          ? await draftClientCertificate(transport, { companyId: config.companyId, body })
          : await draftSubcontractorCertificate(transport, { companyId: config.companyId, body });

      props.onDrafted(drafted);
      setNumber("");
      setMeasured({});
      setExtras([]);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [chosen, config.companyId, extras, measured, number, periodFrom, periodTo, props, sequenceNo, transport]);

  return (
    <div className="stack">
      <div className="grid fields-4">
        <Field id="dc-number" label={t("contracting.common.number")} required>
          <input id="dc-number" data-testid="dc-number" className="ctl mono" dir="ltr" value={number} onChange={(e) => setNumber(e.target.value)} />
        </Field>
        <Field
          id="dc-sequence"
          label={t("contracting.certificate.sequence")}
          hint={t("contracting.certificate.sequenceHint")}
          required
        >
          <input
            id="dc-sequence"
            data-testid="dc-sequence"
            className="ctl mono"
            inputMode="numeric"
            dir="ltr"
            value={sequenceNo}
            onChange={(e) => setSequenceNo(e.target.value)}
            placeholder="1"
          />
        </Field>
        <Field id="dc-from" label={t("contracting.certificate.periodFrom")} required>
          <input
            id="dc-from"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={periodFrom}
            onChange={(e) => setPeriodFrom(e.target.value)}
          />
        </Field>
        <Field id="dc-to" label={t("contracting.certificate.periodTo")} required>
          <input
            id="dc-to"
            className="ctl mono"
            type="date"
            dir="ltr"
            value={periodTo}
            onChange={(e) => setPeriodTo(e.target.value)}
          />
        </Field>
      </div>

      <h3 className="subhead">{t("contracting.certificate.measureTitle")}</h3>
      <p className="muted">{t("contracting.certificate.measureNote")}</p>

      {props.items.length === 0 ? (
        <ExplainedEmpty
          title={t("contracting.boq.emptyTitle")}
          body={t("contracting.boq.emptyBody")}
          testId="draft-no-items"
        />
      ) : (
        <div className="ledger" data-testid="measure-table">
          <table>
            <caption className="visually-hidden">{t("contracting.certificate.measureTitle")}</caption>
            <thead>
              <tr>
                <th scope="col">{t("contracting.boq.code")}</th>
                <th scope="col">{t("contracting.boq.description")}</th>
                <th scope="col">{t("contracting.common.unit")}</th>
                <th scope="col" className="n con-cum">
                  {t("contracting.certificate.cumulative")}
                  <span className="con-sub">{t("contracting.certificate.cumulativeSub")}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {props.items.map((item) => {
                const value = measured[item.id] ?? "";
                const bad = value !== "" && !isMagnitudeText(value);
                return (
                  <tr key={item.id} data-testid="measure-row">
                    <td className="code">{item.code}</td>
                    <td>{item.descriptionAr}</td>
                    <td className="code">{item.unit}</td>
                    <td className="n">
                      <input
                        className={"ctl amt-input" + (bad ? " is-invalid" : "")}
                        inputMode="decimal"
                        dir="ltr"
                        autoComplete="off"
                        aria-invalid={bad}
                        aria-label={t("contracting.certificate.cumulative") + " — " + item.code}
                        value={value}
                        onChange={(e) => setMeasured({ ...measured, [item.id]: e.target.value })}
                        placeholder="0.000000"
                        data-testid="measure-input"
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <h3 className="subhead">{t("contracting.certificate.extraTitle")}</h3>
      <p className="muted">{t("contracting.certificate.extraNote")}</p>
      <div className="con-lines">
        {extras.map((line) => (
          <fieldset key={line.key} className="con-line" data-testid="extra-line">
            <Field id={"ex-kind-" + line.key} label={t("contracting.certificate.kind")}>
              <select
                id={"ex-kind-" + line.key}
                className="ctl"
                value={line.lineKind}
                onChange={(e) =>
                  setExtras(extras.map((x) => (x.key === line.key ? { ...x, lineKind: e.target.value } : x)))
                }
              >
                {LINE_KINDS.filter((kind) => kind !== WORK).map((kind) => (
                  <option key={kind} value={kind}>
                    {t("contracting.kind." + kind)}
                  </option>
                ))}
              </select>
            </Field>
            <Field id={"ex-desc-" + line.key} label={t("contracting.boq.description")} required>
              <input
                id={"ex-desc-" + line.key}
                className="ctl"
                lang="ar"
                value={line.descriptionAr}
                onChange={(e) =>
                  setExtras(extras.map((x) => (x.key === line.key ? { ...x, descriptionAr: e.target.value } : x)))
                }
              />
            </Field>
            <Field
              id={"ex-amount-" + line.key}
              label={t("contracting.certificate.lineAmount")}
              hint={line.amount === "" || isMoneyText(line.amount) ? t("contracting.common.moneyHint") : t("contracting.common.moneyBad")}
              required
            >
              <input
                id={"ex-amount-" + line.key}
                className={"ctl amt-input" + (line.amount !== "" && !isMoneyText(line.amount) ? " is-invalid" : "")}
                inputMode="decimal"
                dir="ltr"
                aria-invalid={line.amount !== "" && !isMoneyText(line.amount)}
                value={line.amount}
                onChange={(e) =>
                  setExtras(extras.map((x) => (x.key === line.key ? { ...x, amount: e.target.value } : x)))
                }
                placeholder="0.0000"
              />
            </Field>
            <div className="con-line__wide inline-group">
              <Button
                label={t("contracting.common.removeLine")}
                kind="danger"
                size="sm"
                onClick={() => setExtras(extras.filter((x) => x.key !== line.key))}
              />
            </div>
          </fieldset>
        ))}
        <button
          type="button"
          className="addline"
          data-testid="add-extra-line"
          onClick={() => setExtras([...extras, extraLine(LINE_KINDS.find((k) => k !== WORK) ?? WORK)])}
        >
          {t("contracting.certificate.addExtra")}
        </button>
      </div>
      <p className="alert alert--warning" role="status">
        {t("contracting.certificate.penaltyWarning")}
      </p>

      <div className={"inline-group " + MOTION.reveal}>
        <Button
          label={busy ? t("contracting.common.loading") : t("contracting.certificate.saveDraft")}
          kind="primary"
          disabled={!ready || busy}
          onClick={() => void submit()}
          testId="save-draft-certificate"
        />
      </div>
      {error ? <ReadProblem error={error} /> : null}
    </div>
  );
}
