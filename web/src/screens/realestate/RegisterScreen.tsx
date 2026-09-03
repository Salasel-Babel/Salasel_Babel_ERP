/* ═══════════════════════════════════════════════════════════════════════════
   السجلّ العقاري — العقار ووحداته وأطرافه
   The property register — a property, its units and its parties
   ───────────────────────────────────────────────────────────────────────────
   **وأصدق ما في هذه الشاشة ما لا تدّعيه.** العقد المنشور لا يحمل باباً واحداً
   يسرد: لا سرد عقارات، ولا سرد وحدات تحت عقار، ولا سرد ملّاك ولا مستأجرين —
   كل قراءةٍ **بمعرّف**. فالشاشة تفعل ما يسمح به العقد بالضبط: تُسجّل، وتقرأ
   بمعرّف، وتُبقي أمام العين ما سُجِّل **في هذه الجلسة** مقولاً بأنه كذلك؛
   وتُعلن ما ينقص باباً باباً بدل أن ترسم قائمةً مُختلَقة تبدو كاملة.

   **وثلاثة رفضٍ تسمّيها هذه الشاشة قبل أن تقع:**
     · نموذج «مُدار لصالح الغير» **بلا مالك** — الأجرة فيه التزام تجاه المالك،
       وسطر أمانات الملاك يحمل طرفاً في دفتره المساعد. والرفض يصل من الخادم
       برمز `realestate.managed_property_needs_an_owner`، والشاشة تقوله قبله.
     · الملكية الذاتية **بمالكٍ خارجي** — المنشأة هي المالك ولا أمانات عليه.
     · عقارٌ بأكثر من مالك — الشكل يحتمل الحصص، و**سياسة القسمة قرار مالك
       مفتوح (ق-ع-18)**: لا يُرحَّل بقسمةٍ يخترعها النظام.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { createProperty, createUnit, readProperty, readUnit } from "../../api/generated/client";
import type { Property, PropertyRequest, Unit, UnitRequest } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { Num } from "../../i18n/react";
import { Panel } from "../../ui";
import {
  MANAGED_FOR_OTHERS,
  NeedsCompany,
  NotYetPublished,
  OWNERSHIP_MODELS,
  Refusal,
  SectionHead,
  ownershipLabelKey,
  SessionLog,
  TranslatedName,
  TranslationEditor,
  UNIT_USAGES,
  VAT_TREATMENTS,
  useWrite,
  wireTranslations,
  type SessionEntry,
  type TranslationRow,
} from "./parts";

/** ما يُقرأ بمعرّف في هذه الشاشة — بابا العقار ووحدته. والطرفان لهما شاشتهما
    (`/realestate/parties`) منذ ADR-0080: أربعةُ نماذج كتابةٍ في صفحةٍ واحدة
    ضِعفُ الحدّ الذي وضعه ADR-0077، والوحدةُ تُنشأ **تحت** عقار والطرفُ لا. */
const LOOKUPS = ["property", "unit"] as const;
type Lookup = (typeof LOOKUPS)[number];

/** الأبواب التي **لا يحملها العقد** وتحتاجها قائمةٌ حقيقية. */
const MISSING_LIST_OPERATIONS = [
  "GET /api/v1/companies/{companyId}/properties",
  "GET /api/v1/companies/{companyId}/properties/{propertyId}/units",
];

/** حصّةٌ كاملة: المقام واحد. وما زاد عليه يعني ملكيةً مشتركة. */
const WHOLE = "1";

/**
 * النموذج الذي تُفتَح عليه استمارةٌ جديدة. **ولماذا يُسمّى ولا يُؤخذ أولَ
 * المجموعة:** ترتيب أعضاء المجموعة في العقد أبجديّ لا دلاليّ، وأولُه
 * `managed_for_others` — وهو النموذج الذي يستلزم مالكاً، فتُفتَح الاستمارة
 * حمراءَ على حقلٍ لم يلمسه أحد. والأبسط يُفتَح افتراضاً، والأثقل يُختار.
 */
const DEFAULT_OWNERSHIP = "own_property";

if (!OWNERSHIP_MODELS.includes(DEFAULT_OWNERSHIP)) {
  throw new TypeError(
    "نموذج ملكية افتراضي غير منشور في العقد · unpublished default ownership model: " +
      DEFAULT_OWNERSHIP
  );
}

/** الشاشة كاملةً. */
export function RegisterScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [log, setLog] = useState<readonly SessionEntry[]>([]);

  const remember = useCallback((entry: SessionEntry) => {
    setLog((current) => [entry, ...current.filter((e) => e.id !== entry.id)]);
  }, []);

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="realestate-register">
      <SectionHead
        here="register"
        title={t("realestate.register.title")}
        lede={t("realestate.register.lede")}
      />

      <LookupPanel companyId={config.companyId} transport={transport} />

      <NotYetPublished
        title={t("realestate.register.noListTitle")}
        body={t("realestate.register.noListBody")}
        operations={MISSING_LIST_OPERATIONS}
        testId="realestate-no-list"
      />

      <div className="re-two">
        <PropertyForm companyId={config.companyId} transport={transport} onCreated={remember} />
        <UnitForm companyId={config.companyId} transport={transport} onCreated={remember} />
      </div>

      <Panel
        title={t("realestate.common.sessionOnly")}
        note={t("realestate.common.sessionOnlyBody")}
        testId="realestate-session-panel"
      >
        <SessionLog entries={log} />
      </Panel>
    </section>
  );
}

/* ════════════════════════════════════════════════ القراءة بمعرّف ═════ */

type Transport = ReturnType<typeof useApi>["transport"];

/** قراءة أي من الأربعة بمعرّفه — وهو الطريق الوحيد الذي ينشره العقد. */
function LookupPanel(props: { companyId: string; transport: Transport }): ReactNode {
  const { t } = useT();
  const [kind, setKind] = useState<Lookup>("property");
  const [id, setId] = useState("");
  const read = useWrite<Property | Unit>("arrive");

  const submit = useCallback(() => {
    const { companyId, transport } = props;
    void read.run(() => {
      if (kind === "property") return readProperty(transport, { companyId, propertyId: id });
      return readUnit(transport, { companyId, unitId: id });
    });
  }, [id, kind, props, read]);

  const found = read.value;

  return (
    <Panel
      title={t("realestate.register.lookup")}
      note={t("realestate.register.lookupHint")}
      testId="realestate-lookup"
    >
      <div className="grid fields-3">
        <div className="field">
          <label htmlFor="re-lookup-kind">{t("realestate.common.kind")}</label>
          <select
            id="re-lookup-kind"
            className="ctl"
            data-testid="re-lookup-kind"
            value={kind}
            onChange={(e) => setKind(e.target.value as Lookup)}
          >
            {LOOKUPS.map((one) => (
              <option key={one} value={one}>
                {t("realestate.kind." + one)}
              </option>
            ))}
          </select>
          {/* وصفٌ لهذه الخليّة كي لا يبقى قاعُ حبرها فوق جارتها: الاستعارة
              تُسوّي الصندوق لا الحبر، والعلاج تحريريّ (ADR-0077 · ما قِيس). */}
          <span className="hint">{t("realestate.register.lookupKindHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-lookup-id">{t("realestate.common.id")}</label>
          <input
            id="re-lookup-id"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-lookup-id"
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
          data-testid="re-lookup-go"
          disabled={id === "" || read.busy}
          onClick={submit}
        >
          {read.busy ? t("common.state.loading") : t("realestate.common.read")}
        </button>
      </div>

      {read.error ? <Refusal error={read.error} testId="re-lookup-refusal" /> : null}

      {found ? (
        <div className={"card card-pad " + read.moment} data-testid="re-lookup-result">
          {"ownershipModel" in found ? <PropertyCard property={found} /> : null}
          {"usage" in found ? <UnitCard unit={found} /> : null}
        </div>
      ) : null}
    </Panel>
  );
}

/** بطاقة عقار — ونموذج الملكية والحصّة معروضان لأنهما يحكمان كل قيدٍ عليه. */
function PropertyCard(props: { property: Property }): ReactNode {
  const { t } = useT();
  const { property } = props;
  const shared = property.ownerShareDenominator !== WHOLE;
  return (
    <>
      <div className="kv">
        <div>
          <div className="k">{t("realestate.common.code")}</div>
          <div className="v code" data-testid="re-property-code">
            {property.code}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.common.nameAr")}</div>
          <div className="v">
            <TranslatedName nameAr={property.nameAr} translations={property.nameTranslations} />
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.ownership.label")}</div>
          <div className="v" data-testid="re-property-model">
            {t(ownershipLabelKey(property.ownershipModel))}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.ownership.owner")}</div>
          <div className="v re-id" data-testid="re-property-owner">
            {property.ownerId ?? t("common.label.dash")}
          </div>
        </div>
      </div>
      <div className="row">
        <span className="k">{t("realestate.ownership.share")}</span>
        <span className="mono" dir="ltr" data-testid="re-property-share">
          <Num value={property.ownerShareNumerator} />
          {" / "}
          <Num value={property.ownerShareDenominator} />
        </span>
      </div>
      {shared ? (
        <p className="alert alert--warning" role="status" data-testid="re-property-share-open">
          {t("realestate.ownership.shareOpen")}
        </p>
      ) : null}
      <p className="re-id">{property.id}</p>
    </>
  );
}

/** بطاقة وحدة — والمعاملة الضريبية تُنسَخ إلى كل فاتورة إيجارٍ عليها. */
function UnitCard(props: { unit: Unit }): ReactNode {
  const { t } = useT();
  const { unit } = props;
  return (
    <>
      <div className="kv">
        <div>
          <div className="k">{t("realestate.common.code")}</div>
          <div className="v code" data-testid="re-unit-code">
            {unit.code}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.common.nameAr")}</div>
          <div className="v">
            <TranslatedName nameAr={unit.nameAr} translations={unit.nameTranslations} />
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.usage.label")}</div>
          <div className="v" data-testid="re-unit-usage">
            {t("realestate.usage." + unit.usage)}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.vat.label")}</div>
          <div className="v" data-testid="re-unit-vat">
            {t("realestate.vat." + unit.vatTreatment)}
          </div>
        </div>
      </div>
      <p className="muted">{t("realestate.register.unitProperty")}</p>
      <p className="re-id">{unit.propertyId}</p>
      <p className="re-id">{unit.id}</p>
    </>
  );
}

/** بطاقة طرف — مالكاً كان أو مستأجراً؛ والدور يصل من الخادم لا من الطلب. */
/* ═══════════════════════════════════════════════════ تسجيل عقار ═════ */

function PropertyForm(props: {
  companyId: string;
  transport: Transport;
  onCreated: (entry: SessionEntry) => void;
}): ReactNode {
  const { t } = useT();
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [model, setModel] = useState<string>(DEFAULT_OWNERSHIP);
  const [ownerId, setOwnerId] = useState("");
  const [rows, setRows] = useState<readonly TranslationRow[]>([]);
  const write = useWrite<Property>("arrive");

  /* الرفضان اللذان تعرفهما الشاشة قبل الخادم — يُقالان **قبل** الإرسال، ولا
     يُعطَّل بهما الزرّ صامتاً: الحكم النهائي للخادم، والقول هنا مساعدة. */
  const managed = model === MANAGED_FOR_OTHERS;
  const missingOwner = managed && ownerId === "";
  const ownerNotWanted = !managed && ownerId !== "";

  const submit = useCallback(() => {
    void write.run(async () => {
      /* التحويل الوحيد هنا وعند الحدّ: القيمة تأتي من مجموعةٍ **مقروءة من العقد
         نفسه** وقت التشغيل، فهي عضوٌ فيها بحكم مصدرها — لكن TypeScript لا يعرف
         ذلك عن نصٍّ قرأه من runtime-schema. */
      const created = await createProperty(props.transport, {
        companyId: props.companyId,
        body: {
          code,
          nameAr,
          ownershipModel: model,
          ...(ownerId === "" ? {} : { ownerId }),
          ...(rows.length > 0 ? { nameTranslations: wireTranslations(rows) } : {}),
        } as PropertyRequest,
      });
      props.onCreated({
        id: created.id,
        kind: "property",
        code: created.code,
        nameAr: created.nameAr,
        translations: created.nameTranslations,
      });
      return created;
    });
  }, [code, model, nameAr, ownerId, props, rows, write]);

  return (
    <Panel
      title={t("realestate.register.createProperty")}
      note={t("realestate.register.createPropertyNote")}
      testId="realestate-property-form"
    >
      <div className="grid fields-half">
        <div className="field">
          <label htmlFor="re-prop-code">
            {t("realestate.common.code")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-prop-code"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-prop-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="PRP-001"
          />
          <span className="hint">{t("realestate.common.codeHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-prop-name">
            {t("realestate.common.nameAr")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-prop-name"
            className="ctl"
            lang="ar"
            data-testid="re-prop-name"
            value={nameAr}
            onChange={(e) => setNameAr(e.target.value)}
          />
          <span className="hint">{t("realestate.common.nameArHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-prop-model">{t("realestate.ownership.label")}</label>
          <select
            id="re-prop-model"
            className="ctl"
            data-testid="re-prop-model"
            value={model}
            onChange={(e) => setModel(e.target.value)}
          >
            {OWNERSHIP_MODELS.map((one) => (
              <option key={one} value={one}>
                {t(ownershipLabelKey(one))}
              </option>
            ))}
          </select>
          <span className="hint">{t("realestate.ownership.hint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-prop-owner">{t("realestate.ownership.owner")}</label>
          <input
            id="re-prop-owner"
            className={"ctl mono" + (missingOwner || ownerNotWanted ? " is-invalid" : "")}
            dir="ltr"
            autoComplete="off"
            aria-invalid={missingOwner || ownerNotWanted}
            data-testid="re-prop-owner"
            value={ownerId}
            onChange={(e) => setOwnerId(e.target.value)}
          />
          {missingOwner ? (
            <span className="field-error" role="alert" data-testid="re-prop-needs-owner">
              {t("realestate.ownership.managedNeedsOwner")}
            </span>
          ) : null}
          {ownerNotWanted ? (
            <span className="field-error" role="alert" data-testid="re-prop-no-owner">
              {t("realestate.ownership.ownedTakesNoOwner")}
            </span>
          ) : null}
          {!missingOwner && !ownerNotWanted ? (
            <span className="hint">{t("realestate.ownership.ownerHint")}</span>
          ) : null}
        </div>
      </div>

      <h3 className="k">{t("realestate.common.translations")}</h3>
      <TranslationEditor idPrefix="re-prop" rows={rows} onChange={setRows} />

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-prop-save"
          disabled={code === "" || nameAr === "" || write.busy}
          onClick={submit}
        >
          {write.busy ? t("common.state.loading") : t("realestate.common.create")}
        </button>
      </div>

      {write.error ? <Refusal error={write.error} testId="re-prop-refusal" /> : null}
      {write.value ? (
        <p className={"alert alert--success " + write.moment} role="status" data-testid="re-prop-created">
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

/* ═══════════════════════════════════════════════════ تسجيل وحدة ═════ */

function UnitForm(props: {
  companyId: string;
  transport: Transport;
  onCreated: (entry: SessionEntry) => void;
}): ReactNode {
  const { t } = useT();
  const [propertyId, setPropertyId] = useState("");
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [usage, setUsage] = useState<string>(UNIT_USAGES[0] as string);
  const [vat, setVat] = useState<string>(VAT_TREATMENTS[0] as string);
  const [rows, setRows] = useState<readonly TranslationRow[]>([]);
  const write = useWrite<Unit>("arrive");

  const submit = useCallback(() => {
    void write.run(async () => {
      const created = await createUnit(props.transport, {
        companyId: props.companyId,
        propertyId,
        body: {
          code,
          nameAr,
          usage,
          vatTreatment: vat,
          ...(rows.length > 0 ? { nameTranslations: wireTranslations(rows) } : {}),
        } as UnitRequest,
      });
      props.onCreated({
        id: created.id,
        kind: "unit",
        code: created.code,
        nameAr: created.nameAr,
        translations: created.nameTranslations,
      });
      return created;
    });
  }, [code, nameAr, propertyId, props, rows, usage, vat, write]);

  return (
    <Panel
      title={t("realestate.register.createUnit")}
      note={t("realestate.register.createUnitNote")}
      testId="realestate-unit-form"
    >
      <div className="grid fields-half">
        <div className="field">
          <label htmlFor="re-unit-property">
            {t("realestate.register.propertyId")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-unit-property"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-unit-property"
            value={propertyId}
            onChange={(e) => setPropertyId(e.target.value)}
          />
          <span className="hint">{t("realestate.register.propertyIdHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-unit-code">
            {t("realestate.common.code")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-unit-code"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-unit-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="UNT-001"
          />
          {/* وصفٌ لكلّ خليّة في الصفّ — الاستعارة تُسوّي الصندوق لا الحبر،
              وخليّةٌ بلا وصفٍ بجانب أخرى بوصفٍ من ثلاثة أسطر تركت هنا
              61.17px عند en-1024 (ADR-0077 · ما قِيس). */}
          <span className="hint">{t("realestate.register.unitCodeHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-unit-name">
            {t("realestate.common.nameAr")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-unit-name"
            className="ctl"
            lang="ar"
            data-testid="re-unit-name"
            value={nameAr}
            onChange={(e) => setNameAr(e.target.value)}
          />
          <span className="hint">{t("realestate.register.unitNameHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-unit-usage">{t("realestate.usage.label")}</label>
          <select
            id="re-unit-usage"
            className="ctl"
            data-testid="re-unit-usage-select"
            value={usage}
            onChange={(e) => setUsage(e.target.value)}
          >
            {UNIT_USAGES.map((one) => (
              <option key={one} value={one}>
                {t("realestate.usage." + one)}
              </option>
            ))}
          </select>
          <span className="hint">{t("realestate.register.unitUsageHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-unit-vat-select">{t("realestate.vat.label")}</label>
          <select
            id="re-unit-vat-select"
            className="ctl"
            data-testid="re-unit-vat-select"
            value={vat}
            onChange={(e) => setVat(e.target.value)}
          >
            {VAT_TREATMENTS.map((one) => (
              <option key={one} value={one}>
                {t("realestate.vat." + one)}
              </option>
            ))}
          </select>
          <span className="hint">{t("realestate.vat.hint")}</span>
        </div>
      </div>

      <h3 className="k">{t("realestate.common.translations")}</h3>
      <TranslationEditor idPrefix="re-unit" rows={rows} onChange={setRows} />

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-unit-save"
          disabled={propertyId === "" || code === "" || nameAr === "" || write.busy}
          onClick={submit}
        >
          {write.busy ? t("common.state.loading") : t("realestate.common.create")}
        </button>
      </div>

      {write.error ? <Refusal error={write.error} testId="re-unit-refusal" /> : null}
      {write.value ? (
        <p className={"alert alert--success " + write.moment} role="status" data-testid="re-unit-created">
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
