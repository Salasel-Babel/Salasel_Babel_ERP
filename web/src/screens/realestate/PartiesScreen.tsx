/* ═══════════════════════════════════════════════════════════════════════════
   المُلّاك والمستأجرون — طرفا عقد الإيجار قبل أن يُكتب
   Owners and lessees — the two parties of a lease, before it is written
   ───────────────────────────────────────────────────────────────────────────
   **لماذا انفصلت هذه الشاشة عن السجلّ العقاري.** كان `/realestate` يحمل
   **أربعة نماذج كتابة**: عقار، ووحدة، ومالك، ومستأجر — ضعفَ الحدّ الذي وضعه
   [ADR-0077] («بلوغُ شاشةٍ واحدة أكثر من نموذجَي كتابة ⇒ تُقسَّم»). والعقارُ
   ووحدتُه **واحد**: الوحدة تُنشأ تحت عقار ولا تُنشأ بغيره، ومسارُها في العقد
   نفسه يقول ذلك (`/properties/{propertyId}/units`). والطرفان **واحدٌ آخر**:
   مالكٌ نُحصّل له ومستأجرٌ نُحصّل منه — ولا يُنشآن تحت شيء، ومسارهما في العقد
   مستقلّان.

   **ولوحان لا لوحٌ واحد، ولا شاشتان.** المالك والمستأجر يتقاسمان جسماً واحداً
   في العقد (`RealEstatePartyRequest`) ويفترقان في بابهما وفي دورهما فقط —
   فجمعُهما في شاشةٍ واحدة يجيب سؤالاً واحداً («من أطراف هذا العقد؟») في لحظةٍ
   واحدة، وفصلُهما في لوحين يمنع أن يُسجَّل مالكٌ في سجلّ المستأجرين لأن
   حقولهما متطابقة. وهي القسمة نفسها التي أقرّها ADR-0077 §2 للسلف
   والاستقطاعات: **الجمع في الشاشة والفصل في اللوح**.

   **وثلاثة قيود يرثها هذا الملفّ من قسمه ولا يخالفها:**
     · لا باب سردٍ في العقد لمالكٍ ولا لمستأجر — القراءة **بمعرّف** وحدها.
       فما يُعرض قائمةً هو **ما سُجِّل في هذه الجلسة**، مقولاً بأنه ذاكرة
       تبويبة لا سجلّ منشأة.
     · لا رمز حسابٍ هنا: أمانات الملّاك وذمم المستأجرين تبلغ الدفتر عبر
       مصفوفة الترحيل.
     · والاسم العربي **سجلٌّ** وما عداه ترجمات بوسم لغةٍ — لا حقل ثابت
       للإنجليزية (ADR-0021).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { createLessee, createPropertyOwner, readLessee, readPropertyOwner } from "../../api/generated/client";
import type { RealEstateParty, RealEstatePartyRequest } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { Panel, StatusBadge } from "../../ui";
import {
  NeedsCompany,
  NotYetPublished,
  Refusal,
  SectionHead,
  SessionLog,
  TAX_RESIDENCIES,
  TranslatedName,
  TranslationEditor,
  residencyLabelKey,
  useWrite,
  wireTranslations,
  type SessionEntry,
  type TranslationRow,
} from "./parts";

/** ما يُقرأ بمعرّف في هذه الشاشة — بابا القراءة اللذان ينشرهما العقد للطرفين. */
const LOOKUPS = ["owner", "lessee"] as const;
type Lookup = (typeof LOOKUPS)[number];

/** الأبواب التي **لا يحملها العقد** ويحتاجها سجلُّ أطرافٍ حقيقي. */
const MISSING_LIST_OPERATIONS = [
  "GET /api/v1/companies/{companyId}/property-owners",
  "GET /api/v1/companies/{companyId}/lessees",
];

type Transport = ReturnType<typeof useApi>["transport"];

/** الشاشة كاملةً. */
export function PartiesScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [log, setLog] = useState<readonly SessionEntry[]>([]);

  const remember = useCallback((entry: SessionEntry) => {
    setLog((current) => [entry, ...current.filter((e) => e.id !== entry.id)]);
  }, []);

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="realestate-parties">
      <SectionHead
        here="parties"
        title={t("realestate.parties.title")}
        lede={t("realestate.parties.lede")}
      />

      <PartyLookup companyId={config.companyId} transport={transport} />

      <NotYetPublished
        title={t("realestate.parties.noListTitle")}
        body={t("realestate.parties.noListBody")}
        operations={MISSING_LIST_OPERATIONS}
        testId="realestate-parties-no-list"
      />

      <div className="re-two">
        <PartyForm
          companyId={config.companyId}
          transport={transport}
          onCreated={remember}
          role="owner"
        />
        <PartyForm
          companyId={config.companyId}
          transport={transport}
          onCreated={remember}
          role="lessee"
        />
      </div>

      <Panel
        title={t("realestate.common.sessionOnly")}
        note={t("realestate.common.sessionOnlyBody")}
        testId="realestate-parties-session-panel"
      >
        <SessionLog entries={log} />
      </Panel>
    </section>
  );
}

/* ═══════════════════════════════════════════ قراءة طرفٍ بمعرّفه ══════ */

/** قراءة مالكٍ أو مستأجرٍ بمعرّفه — وهو الطريق الوحيد الذي ينشره العقد. */
function PartyLookup(props: { companyId: string; transport: Transport }): ReactNode {
  const { t } = useT();
  const [kind, setKind] = useState<Lookup>("owner");
  const [id, setId] = useState("");
  const read = useWrite<RealEstateParty>("arrive");

  const submit = useCallback(() => {
    const { companyId, transport } = props;
    void read.run(() =>
      kind === "owner"
        ? readPropertyOwner(transport, { companyId, ownerId: id })
        : readLessee(transport, { companyId, lesseeId: id })
    );
  }, [id, kind, props, read]);

  const found = read.value;

  return (
    <Panel
      title={t("realestate.parties.lookup")}
      note={t("realestate.parties.lookupHint")}
      testId="realestate-parties-lookup"
    >
      <div className="grid fields-half">
        <div className="field">
          <label htmlFor="re-party-lookup-kind">{t("realestate.common.kind")}</label>
          <select
            id="re-party-lookup-kind"
            className="ctl"
            data-testid="re-party-lookup-kind"
            value={kind}
            onChange={(e) => setKind(e.target.value as Lookup)}
          >
            {LOOKUPS.map((one) => (
              <option key={one} value={one}>
                {t("realestate.kind." + one)}
              </option>
            ))}
          </select>
          <span className="hint">{t("realestate.parties.kindHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-party-lookup-id">{t("realestate.common.id")}</label>
          <input
            id="re-party-lookup-id"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-party-lookup-id"
            value={id}
            onChange={(e) => setId(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
          />
          <span className="hint">{t("realestate.common.idHint")}</span>
        </div>
      </div>

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-party-lookup-go"
          disabled={id === "" || read.busy}
          onClick={submit}
        >
          {read.busy ? t("common.state.loading") : t("realestate.common.read")}
        </button>
      </div>

      {read.error ? <Refusal error={read.error} testId="re-party-lookup-refusal" /> : null}

      {found ? (
        <div className={"card card-pad " + read.moment} data-testid="re-party-lookup-result">
          <PartyCard party={found} />
        </div>
      ) : null}
    </Panel>
  );
}

/* ═══════════════════════════════════════════════ بطاقة طرفٍ ═════════ */

function PartyCard(props: { party: RealEstateParty }): ReactNode {
  const { t } = useT();
  const { party } = props;
  return (
    <>
      <div className="kv">
        <div>
          <div className="k">{t("realestate.common.code")}</div>
          <div className="v code" data-testid="re-party-code">
            {party.code}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.common.nameAr")}</div>
          <div className="v">
            <TranslatedName nameAr={party.nameAr} translations={party.nameTranslations} />
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.party.role")}</div>
          <div className="v" data-testid="re-party-role">
            {t("realestate.kind." + party.role)}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.residency.label")}</div>
          <div className="v">{t(residencyLabelKey(party.taxResidency))}</div>
        </div>
      </div>
      <div className="row">
        <span className="k">{t("realestate.party.vatNumber")}</span>
        <span className="mono" dir="ltr" data-testid="re-party-vat">
          {party.vatNumber === "" ? t("common.label.dash") : party.vatNumber}
        </span>
      </div>
      <p className="re-id">{party.id}</p>
    </>
  );
}

/* ═══════════════════════════════════════════════ تسجيل طرفٍ ═════════ */

function PartyForm(props: {
  companyId: string;
  transport: Transport;
  role: "owner" | "lessee";
  onCreated: (entry: SessionEntry) => void;
}): ReactNode {
  const { t } = useT();
  const { role } = props;
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [residency, setResidency] = useState<string>(TAX_RESIDENCIES[0] as string);
  const [vatNumber, setVatNumber] = useState("");
  const [rows, setRows] = useState<readonly TranslationRow[]>([]);
  const write = useWrite<RealEstateParty>("arrive");

  const submit = useCallback(() => {
    void write.run(async () => {
      const body = {
        code,
        nameAr,
        taxResidency: residency,
        vatNumber,
        ...(rows.length > 0 ? { nameTranslations: wireTranslations(rows) } : {}),
      } as RealEstatePartyRequest;
      const created =
        role === "owner"
          ? await createPropertyOwner(props.transport, { companyId: props.companyId, body })
          : await createLessee(props.transport, { companyId: props.companyId, body });
      props.onCreated({
        id: created.id,
        kind: created.role,
        code: created.code,
        nameAr: created.nameAr,
        translations: created.nameTranslations,
      });
      return created;
    });
  }, [code, nameAr, props, residency, role, rows, vatNumber, write]);

  return (
    <Panel
      title={t("realestate.register.createParty." + role)}
      note={t("realestate.register.createPartyNote")}
      aside={<StatusBadge state="info" label={t("realestate.kind." + role)} />}
      testId={"realestate-party-form-" + role}
    >
      <div className="grid fields-half">
        <div className="field">
          <label htmlFor={"re-party-code-" + role}>
            {t("realestate.common.code")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id={"re-party-code-" + role}
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid={"re-party-code-" + role}
            value={code}
            onChange={(e) => setCode(e.target.value)}
          />
          <span className="hint">{t("realestate.parties.codeHint")}</span>
        </div>
        <div className="field">
          <label htmlFor={"re-party-name-" + role}>
            {t("realestate.common.nameAr")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id={"re-party-name-" + role}
            className="ctl"
            lang="ar"
            data-testid={"re-party-name-" + role}
            value={nameAr}
            onChange={(e) => setNameAr(e.target.value)}
          />
          <span className="hint">{t("realestate.parties.nameHint")}</span>
        </div>
        <div className="field">
          <label htmlFor={"re-party-res-" + role}>{t("realestate.residency.label")}</label>
          <select
            id={"re-party-res-" + role}
            className="ctl"
            data-testid={"re-party-res-" + role}
            value={residency}
            onChange={(e) => setResidency(e.target.value)}
          >
            {TAX_RESIDENCIES.map((one) => (
              <option key={one} value={one}>
                {t(residencyLabelKey(one))}
              </option>
            ))}
          </select>
          <span className="hint">{t("realestate.parties.residencyHint")}</span>
        </div>
        <div className="field">
          <label htmlFor={"re-party-vat-" + role}>{t("realestate.party.vatNumber")}</label>
          <input
            id={"re-party-vat-" + role}
            className="ctl mono"
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            data-testid={"re-party-vat-" + role}
            value={vatNumber}
            onChange={(e) => setVatNumber(e.target.value)}
          />
          <span className="hint">{t("realestate.party.vatNumberHint")}</span>
        </div>
      </div>

      {/* كلُّ حقلٍ في هذا الصفّ بوصف، والأوصافُ على قدر أعمدتها — وهو العلاج
          **التحريري** الذي يُسوّي قاعَ الحبر بعد أن سوّى ADR-0067 الصندوق.
          وخليّةٌ بلا وصفٍ بجانب أخرى بوصفٍ من سطرين تركت هنا 42.78px. */}
      <h3 className="k">{t("realestate.common.translations")}</h3>
      <TranslationEditor idPrefix={"re-party-" + role} rows={rows} onChange={setRows} />

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid={"re-party-save-" + role}
          disabled={code === "" || nameAr === "" || write.busy}
          onClick={submit}
        >
          {write.busy ? t("common.state.loading") : t("realestate.common.create")}
        </button>
      </div>

      {write.error ? <Refusal error={write.error} testId={"re-party-refusal-" + role} /> : null}
      {write.value ? (
        <p
          className={"alert alert--success " + write.moment}
          role="status"
          data-testid={"re-party-created-" + role}
        >
          {t("realestate.common.created")}
          {" · "}
          <span className="mono" dir="ltr">
            {write.value.id}
          </span>
        </p>
      ) : null}
    </Panel>
  );
}
