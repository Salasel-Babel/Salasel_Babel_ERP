/* ═══════════════════════════════════════════════════════════════════════════
   /setup/document-shapes — قدراتُ المستندات وشكلُها  ·  Capabilities and shapes
   ───────────────────────────────────────────────────────────────────────────
   وخمسةٌ تحكمها، وأوّلها فارقٌ جوهريّ يسهل الخطأ فيه:

   ١ · **`admitDocument` حكمٌ لا كتابة.** يعرض **أسماء حقول** مستندٍ على ملفّ
       الشركة فيقبله أو يرفضه — بلا قيمٍ ولا مبالغ ولا أثر. فلا مسوّدةَ هنا
       ولا ترحيلَ ولا شيء يُخزَّن؛ ولذلك يقف في لوحٍ اسمُه «عرضٌ على الملفّ»
       لا «حفظ»، وزرُّه يقول «اعرِض» لا «أرسِل».

   ٢ · **وقدرةٌ مُطفأة يُرفض بها المستند كلُّه.** نصُّ العقد: «حقلٌ ترخّصه قدرة
       مُطفأة يُرفض به المستند كلُّه — لأن قدرةً يمكن ممارستها بإرسال الحقل رغم
       إطفائها ليست قدرة بل زينة». فالمُطفأة **تُعرض مُسمّاةً** لا تُحذف من
       القائمة، وحقلٌ خارج الشكل يُقال عنه ذلك **قبل الضغط** ومعه أن المُطفأة
       أحدُ سببيه المحتملين.

   ٣ · **والكتابة تستبدل الملفّ كلَّه.** `PUT` يستبدل، فما لا يُرسَل يسقط —
       ولذلك تُعاد **كلُّ** أنواع المستندات في الجسم، **وقيمُها الافتراضية
       كما وصلت** حرفاً بحرف. وإسقاطُها هنا كان سيمسحها بلا أن يطلب أحدٌ ذلك.

   ٤ · **والاتجاه الخطر هو الإطفاء لا التشغيل.** إطفاء قدرةٍ كانت مُشغَّلة
       يجعل مستنداً مفتوحاً يحملها غير مقبول، ويُرفض بلا `withdrawalReason`
       مكتوب. فالشاشة **تحسب المسحوبات بالفرق** بين ما قُرئ وما سيُرسَل،
       وتسمّيها، وتفتح حقل السبب عندها وحدها.

   ٥ · **وشكلُ نوعٍ واحد يُقرأ من بابه.** `readDocumentShape` هو مصدر اللوح
       الذي يُحكَم عليه: الملفّ كلُّه يعطي القائمة والمفاتيح، وشكلُ النوع
       المختار يأتي من بابه بعد كلّ كتابة — فما يُعرَض هو ما يشتقّه الخادم
       الآن، لا نسخةٌ في الذاكرة قد تكون سبقت آخر استبدال.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  admitDocument,
  readCapabilityProfile,
  readDocumentShape,
  writeCapabilityProfile,
} from "../../api/generated/client";
import type {
  CapabilityProfile,
  DocumentAdmission,
  DocumentShape,
} from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { useT } from "../../i18n/react";
import { Button, EmptyState, StatCard, useMoment } from "../../ui";
import { RECORD_TAG } from "../../app/translated-name";
import {
  ChooseCompanyFirst,
  DeclaredGap,
  SetupBadge,
  SetupField,
  SetupSectionNav,
  StatePanel,
} from "./parts";
import "./setup.css";

/** نوع المستند كما ينشره العقد — مجموعةٌ مغلقة تُقرأ ولا تُخترع. */
type DocumentType = DocumentShape["documentType"];

/** رمز القدرة كما ينشره العقد. */
type Capability = DocumentShape["availableCapabilities"][number];

/** رمز الخادم على قدرةٍ لا يخدمها حدثٌ في مصفوفة الترحيل. */
const NOT_SERVED_CODE = "capability_profile.capability_not_served_by_matrix";

/** أدنى طول لسبب السحب — «لا سبب» ليس سبباً. */
const MINIMUM_REASON = 8;

/** الشاشة كاملةً. */
export function DocumentShapesScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [, fireRefuse] = useMoment("refuse");

  const [chosen, setChosen] = useState<DocumentType | "">("");
  /* مفاتيح القدرات كما هي في النموذج: نوعُ المستند ← القدرة ← مُشغَّلة؟ */
  const [switches, setSwitches] = useState<Readonly<Record<string, boolean>>>({});
  const [touched, setTouched] = useState(false);
  const [withdrawalReason, setWithdrawalReason] = useState("");
  const [writeBusy, setWriteBusy] = useState(false);
  const [writeFailure, setWriteFailure] = useState<unknown>(null);

  /* ── لوح الحكم: أسماء حقولٍ لا قيم ────────────────────────────────── */
  const [fieldName, setFieldName] = useState("");
  const [fields, setFields] = useState<readonly string[]>([]);
  const [verdict, setVerdict] = useState<DocumentAdmission | null>(null);
  const [admitBusy, setAdmitBusy] = useState(false);
  const [admitFailure, setAdmitFailure] = useState<unknown>(null);

  const profile = useQuery({
    queryKey: ["setup", "capability-profile", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readCapabilityProfile(transport, { companyId: config.companyId }, signal),
  });

  const shape = useQuery({
    queryKey: [
      "setup",
      "document-shape",
      config.baseUrl,
      config.token,
      config.companyId,
      chosen,
    ],
    enabled: config.companyId !== "" && chosen !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readDocumentShape(
        transport,
        { companyId: config.companyId, documentType: chosen as DocumentType },
        signal
      ),
  });

  const read: CapabilityProfile | null = profile.data ?? null;

  /* ــ المفاتيح تُبذَر من الملفّ المقروء مرّةً، ثم تملكها اليد ــــــــــــ */
  useEffect(() => {
    if (!read || touched) return;
    const seeded: Record<string, boolean> = {};
    for (const document of read.documents) {
      for (const capability of document.availableCapabilities) {
        seeded[document.documentType + "/" + capability] =
          document.enabledCapabilities.includes(capability);
      }
    }
    setSwitches(seeded);
    const first = read.documents[0];
    if (chosen === "" && first) setChosen(first.documentType);
  }, [chosen, read, touched]);

  /* ــ **المسحوبات بالفرق لا بالظنّ**: مُشغَّلةٌ في المقروء ومُطفأةٌ في
       المُرسَل. وهي وحدها ما يوجب سبباً مكتوباً. ــــــــــــــــــــــــ */
  const withdrawn = useMemo(() => {
    if (!read) return [] as readonly string[];
    const out: string[] = [];
    for (const document of read.documents) {
      for (const capability of document.enabledCapabilities) {
        const key = document.documentType + "/" + capability;
        if (switches[key] === false) out.push(key);
      }
    }
    return out;
  }, [read, switches]);

  const reasonNeeded = withdrawn.length > 0;
  /* **النقص والقِصَر ليسا شيئاً واحداً.** حقلٌ لم يُكتب بعدُ ليس «أقصر من
     ثمانية»، فالرسالة لا تظهر قبل أوّل حرف — والزرُّ مُقفَلٌ في الحالين لأن
     المُدخَل ناقص، لا لأن الشاشة تمنع فعلاً يسمح به الخادم. */
  const reasonShort =
    reasonNeeded &&
    withdrawalReason.trim() !== "" &&
    withdrawalReason.trim().length < MINIMUM_REASON;
  const writeReady =
    read !== null && (!reasonNeeded || withdrawalReason.trim().length >= MINIMUM_REASON);

  const currentShape: DocumentShape | null = shape.data ?? null;

  /* ــ ما ليس في الشكل يُسمّى قبل الضغط، ولا يُترك للخادم أن يكتشفه ــــ */
  const strangers = useMemo(() => {
    if (!currentShape) return [] as readonly string[];
    return fields.filter((name) => !currentShape.fields.includes(name));
  }, [currentShape, fields]);

  const disabledHere = useMemo(() => {
    if (!currentShape) return [] as readonly Capability[];
    return currentShape.availableCapabilities.filter(
      (capability) => !currentShape.enabledCapabilities.includes(capability)
    );
  }, [currentShape]);

  const runWrite = useCallback(async () => {
    if (!read) return;
    setWriteBusy(true);
    setWriteFailure(null);
    try {
      await writeCapabilityProfile(transport, {
        companyId: config.companyId,
        body: {
          documents: read.documents.map((document) => ({
            documentType: document.documentType,
            capabilities: document.availableCapabilities.map((capability) => ({
              capability,
              enabled: switches[document.documentType + "/" + capability] ?? false,
            })),
            /* **القيم الافتراضية تُعاد كما وصلت**: الاستبدال كلّي، وإسقاطُها
               هنا يمسحها بلا أن يطلب أحدٌ ذلك. */
            defaults: [...document.defaults],
          })),
          ...(reasonNeeded ? { withdrawalReason: withdrawalReason.trim() } : {}),
        },
      });
      await profile.refetch();
      if (chosen !== "") await shape.refetch();
      setWithdrawalReason("");
      setTouched(false);
      fireArrive();
    } catch (refused) {
      setWriteFailure(refused);
      fireRefuse();
    } finally {
      setWriteBusy(false);
    }
  }, [
    chosen,
    config.companyId,
    fireArrive,
    fireRefuse,
    profile,
    read,
    reasonNeeded,
    shape,
    switches,
    transport,
    withdrawalReason,
  ]);

  const runAdmit = useCallback(async () => {
    if (chosen === "") return;
    setAdmitBusy(true);
    setAdmitFailure(null);
    setVerdict(null);
    try {
      const answer = await admitDocument(transport, {
        companyId: config.companyId,
        documentType: chosen,
        body: { fields: [...fields] },
      });
      setVerdict(answer);
      fireArrive();
    } catch (refused) {
      setAdmitFailure(refused);
      fireRefuse();
    } finally {
      setAdmitBusy(false);
    }
  }, [chosen, config.companyId, fields, fireArrive, fireRefuse, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-shape-needs-company" />;

  return (
    <section className="stack" data-testid="setup-document-shapes-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.docShape.pageTitle")}</h1>
          <p className="sub">{t("screen.docShape.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup/document-shapes" />

      {/* ═══════════════════ ١ · مفاتيح القدرات — نموذج الكتابة الوحيد ═══ */}
      <StatePanel
        title={t("screen.docShape.switchesTitle")}
        note={t("screen.docShape.switchesNote")}
        loading={profile.isPending && profile.fetchStatus === "fetching"}
        testId="setup-shape-switches"
      >
        {profile.isError ? (
          <ProblemPanel error={profile.error} onRetry={() => void profile.refetch()} />
        ) : read === null ? null : read.documents.length === 0 ? (
          <EmptyState
            title={t("screen.docShape.emptyTitle")}
            body={t("screen.docShape.emptyBody")}
            testId="setup-shape-empty"
          />
        ) : (
          <div className={"stack " + arriveCls}>
            {read.documents.map((document) => (
              <div key={document.documentType} data-testid={"setup-shape-doc-" + document.documentType}>
                <p className="k">
                  {/* **الاسم العربي هو الارتداد المضمون** بنصّ العقد: حين لا
                      تتوفّر ترجمة يُعرض هذا النصّ — لا المفتاح ولا الفراغ. */}
                  <span lang={RECORD_TAG} dir="rtl">{document.nameAr}</span>{" "}
                  <span className="mono" dir="ltr">{document.documentType}</span>
                </p>
                <div className="stp-caps">
                  {document.availableCapabilities.map((capability) => {
                    const key = document.documentType + "/" + capability;
                    const on = switches[key] ?? false;
                    const wasOn = document.enabledCapabilities.includes(capability);
                    return (
                      <label key={key} className="check" htmlFor={"stp-cap-" + key}>
                        <input
                          id={"stp-cap-" + key}
                          type="checkbox"
                          checked={on}
                          data-testid={"setup-shape-switch-" + key}
                          onChange={(e) => {
                            setTouched(true);
                            setSwitches((prior) => ({ ...prior, [key]: e.target.checked }));
                          }}
                        />
                        <span className="mono" dir="ltr">{capability}</span>
                        {wasOn && !on ? (
                          <SetupBadge
                            label={t("screen.docShape.withdrawing")}
                            tone="reversed"
                            testId={"setup-shape-withdrawing-" + key}
                          />
                        ) : null}
                        {!wasOn && on ? (
                          <SetupBadge
                            label={t("screen.docShape.granting")}
                            tone="pending"
                            testId={"setup-shape-granting-" + key}
                          />
                        ) : null}
                      </label>
                    );
                  })}
                </div>
              </div>
            ))}

            <div className="grid fields-half">
              <SetupField
                id="stp-shape-reason"
                label={t("screen.docShape.reasonLabel")}
                hint={
                  reasonNeeded
                    ? t("screen.docShape.reasonNeededHint")
                    : t("screen.docShape.reasonIdleHint")
                }
                {...(reasonShort ? { error: t("screen.docShape.reasonShort") } : {})}
                source="typed"
                {...(reasonNeeded ? { required: true } : {})}
              >
                <input
                  id="stp-shape-reason"
                  className="ctl"
                  dir="auto"
                  autoComplete="off"
                  disabled={!reasonNeeded}
                  aria-invalid={reasonShort}
                  data-testid="setup-shape-reason"
                  value={withdrawalReason}
                  onChange={(e) => setWithdrawalReason(e.target.value)}
                />
              </SetupField>
              <div className="rowctl">
                <Button
                  label={t("screen.docShape.save")}
                  kind="primary"
                  loading={writeBusy}
                  disabled={!writeReady || writeBusy}
                  onClick={() => void runWrite()}
                  testId="setup-shape-save"
                />
                <span className="hint">{t("screen.docShape.saveHint")}</span>
              </div>
            </div>

            {withdrawn.length > 0 ? (
              <div
                className="alert alert--warning"
                role="status"
                data-testid="setup-shape-withdrawals"
              >
                <div className="body">
                  <span className="title">{t("screen.docShape.withdrawTitle")}</span>
                  <p>{t("screen.docShape.withdrawBody")}</p>
                  <ul className="stp-tags">
                    {withdrawn.map((key) => (
                      <li key={key}>
                        <span className="mono" dir="ltr">{key}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            ) : null}

            <p className="hint">
              {t("screen.docShape.matrixNote")}{" "}
              <span className="mono" dir="ltr">{NOT_SERVED_CODE}</span>
            </p>

            {writeFailure ? <ProblemPanel error={writeFailure} /> : null}
          </div>
        )}
      </StatePanel>

      {/* ═════════════ ٢ · شكلُ نوعٍ واحد — مُشتقٌّ لا مؤلَّف ═══════════ */}
      <StatePanel
        title={t("screen.docShape.shapeTitle")}
        note={t("screen.docShape.shapeNote")}
        loading={shape.isPending && shape.fetchStatus === "fetching"}
        testId="setup-shape-one"
      >
        <div className="stack">
          <div className="grid fields-half">
            <SetupField
              id="stp-shape-type"
              label={t("screen.docShape.typeLabel")}
              hint={t("screen.docShape.typeHint")}
              source="typed"
              required
            >
              <select
                id="stp-shape-type"
                className="ctl mono"
                dir="ltr"
                data-testid="setup-shape-type"
                value={chosen}
                onChange={(e) => {
                  const picked = read?.documents.find((d) => d.documentType === e.target.value);
                  setChosen(picked ? picked.documentType : "");
                  setVerdict(null);
                  setAdmitFailure(null);
                }}
              >
                <option value="">{t("screen.docShape.typeNone")}</option>
                {(read?.documents ?? []).map((document) => (
                  <option key={document.documentType} value={document.documentType}>
                    {document.documentType}
                  </option>
                ))}
              </select>
            </SetupField>
            <div className="rowctl">
              <Button
                label={t("screen.docShape.copyFields")}
                disabled={currentShape === null}
                onClick={() => setFields(currentShape ? [...currentShape.fields] : [])}
                testId="setup-shape-copy-fields"
              />
              <span className="hint">{t("screen.docShape.copyFieldsHint")}</span>
            </div>
          </div>

          {shape.isError ? (
            <ProblemPanel error={shape.error} onRetry={() => void shape.refetch()} />
          ) : currentShape === null ? (
            <EmptyState
              title={t("screen.docShape.pickTitle")}
              body={t("screen.docShape.pickBody")}
              small
              testId="setup-shape-pick-first"
            />
          ) : (
            <div className="stack">
              <div className="stats-row">
                <StatCard
                  label={t("screen.docShape.fieldCount")}
                  count={currentShape.fields.length}
                  hint={t("screen.docShape.fieldCountHint")}
                  testId="setup-shape-field-count"
                />
                <StatCard
                  label={t("screen.docShape.enabledCount")}
                  count={currentShape.enabledCapabilities.length}
                  hint={t("screen.docShape.enabledCountHint")}
                  tone="good"
                  testId="setup-shape-enabled-count"
                />
                <StatCard
                  label={t("screen.docShape.offCount")}
                  count={disabledHere.length}
                  hint={t("screen.docShape.offCountHint")}
                  tone={disabledHere.length > 0 ? "bad" : "neutral"}
                  testId="setup-shape-off-count"
                />
              </div>

              {/* **المُطفأة تُعرض مُسمّاةً**: قدرةٌ تُحذف من القائمة لا تُقرأ
                  مُطفأةً بل غير موجودة، والفارق هو الفارق بين شاشةٍ تشرح رفضاً
                  وشاشةٍ تُخفي سببه. */}
              <div data-testid="setup-shape-capabilities">
                <p className="k">{t("screen.docShape.capsTitle")}</p>
                <div className="stp-caps">
                  {currentShape.availableCapabilities.map((capability) => {
                    const on = currentShape.enabledCapabilities.includes(capability);
                    return (
                      <span key={capability} data-testid={"setup-shape-cap-" + capability}>
                        <span className="mono" dir="ltr">{capability}</span>
                        <SetupBadge
                          label={on ? t("screen.docShape.capOn") : t("screen.docShape.capOff")}
                          tone={on ? "posted" : "archived"}
                          title={on ? undefined : t("screen.docShape.capOffTitle")}
                          testId={"setup-shape-cap-state-" + capability}
                        />
                      </span>
                    );
                  })}
                </div>
                {disabledHere.length > 0 ? (
                  <p className="hint" data-testid="setup-shape-off-warning">
                    {t("screen.docShape.offWarning")}
                  </p>
                ) : null}
              </div>

              <div className="tablewrap" data-testid="setup-shape-fields">
                <table className="data">
                  <caption className="visually-hidden">{t("screen.docShape.shapeTitle")}</caption>
                  <thead>
                    <tr>
                      <th scope="col" className="start">{t("screen.docShape.colField")}</th>
                      <th scope="col" className="start">{t("screen.docShape.colDefault")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {currentShape.fields.map((name) => {
                      const preset = currentShape.defaults.find((d) => d.name === name);
                      return (
                        <tr key={name} data-testid={"setup-shape-field-" + name}>
                          <td className="start">
                            <span className="mono" dir="ltr">{name}</span>
                          </td>
                          <td className="start">
                            {preset ? (
                              <span className="mono" dir="auto">{preset.value}</span>
                            ) : (
                              <span className="muted">{t("screen.docShape.noDefault")}</span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </StatePanel>

      {/* ══════════════ ٣ · العرض على الملفّ — حكمٌ لا كتابة ════════════ */}
      <StatePanel
        title={t("screen.docShape.admitTitle")}
        note={t("screen.docShape.admitNote")}
        testId="setup-shape-admit"
      >
        <div className="stack">
          <div className="grid fields-half">
            <SetupField
              id="stp-shape-field"
              label={t("screen.docShape.fieldLabel")}
              hint={t("screen.docShape.fieldHint")}
              source="typed"
            >
              <input
                id="stp-shape-field"
                className="ctl mono"
                dir="ltr"
                lang="en"
                autoComplete="off"
                spellCheck={false}
                data-testid="setup-shape-field-input"
                value={fieldName}
                onChange={(e) => setFieldName(e.target.value)}
              />
            </SetupField>
            <div className="rowctl">
              <Button
                label={t("screen.docShape.addField")}
                disabled={fieldName.trim() === "" || fields.includes(fieldName.trim())}
                onClick={() => {
                  setFields((prior) => [...prior, fieldName.trim()]);
                  setFieldName("");
                  setVerdict(null);
                }}
                testId="setup-shape-add-field"
              />
              <span className="hint">{t("screen.docShape.addFieldHint")}</span>
            </div>
          </div>

          {fields.length === 0 ? (
            <p className="hint" data-testid="setup-shape-no-fields">{t("screen.docShape.noFields")}</p>
          ) : (
            <ul className="stp-tags" data-testid="setup-shape-field-list">
              {fields.map((name) => (
                <li key={name}>
                  <span className="mono" dir="ltr">{name}</span>
                  {strangers.includes(name) ? (
                    <SetupBadge
                      label={t("screen.docShape.stranger")}
                      tone="reversed"
                      title={t("screen.docShape.strangerTitle")}
                      testId={"setup-shape-stranger-" + name}
                    />
                  ) : null}
                  <Button
                    label={t("screen.docShape.dropField")}
                    kind="ghost"
                    size="sm"
                    onClick={() => {
                      setFields((prior) => prior.filter((x) => x !== name));
                      setVerdict(null);
                    }}
                    testId={"setup-shape-drop-" + name}
                  />
                </li>
              ))}
            </ul>
          )}

          {/* **الرفض يُقال قبل الضغط**: اسمٌ خارج الشكل يُسمّى، ومعه أن قدرةً
              مُطفأةً على هذا النوع أحدُ سببيه — والثاني اسمٌ لا وجود له. */}
          {strangers.length > 0 ? (
            <div className="alert alert--warning" role="status" data-testid="setup-shape-strangers">
              <div className="body">
                <span className="title">{t("screen.docShape.strangersTitle")}</span>
                <p>{t("screen.docShape.strangersBody")}</p>
                <ul className="stp-tags">
                  {strangers.map((name) => (
                    <li key={name}>
                      <span className="mono" dir="ltr">{name}</span>
                    </li>
                  ))}
                </ul>
                {disabledHere.length > 0 ? (
                  <p className="hint" data-testid="setup-shape-strangers-cause">
                    {t("screen.docShape.strangersCause")}{" "}
                    {disabledHere.map((capability) => (
                      <span key={capability} className="mono" dir="ltr">{capability} </span>
                    ))}
                  </p>
                ) : null}
              </div>
            </div>
          ) : null}

          <div className="inline-group">
            <Button
              label={t("screen.docShape.admit")}
              kind="primary"
              loading={admitBusy}
              disabled={chosen === "" || admitBusy}
              onClick={() => void runAdmit()}
              testId="setup-shape-admit-go"
            />
            <span className="hint">{t("screen.docShape.admitButtonHint")}</span>
          </div>

          {verdict ? (
            <div
              className="alert alert--info"
              role="status"
              data-testid="setup-shape-verdict"
              data-admitted={verdict.admitted ? "true" : "false"}
            >
              <div className="body">
                <span className="title">{t("screen.docShape.verdictTitle")}</span>
                <p>{t("screen.docShape.verdictBody")}</p>
                <ul className="stp-tags">
                  {verdict.fields.map((name) => (
                    <li key={name}>
                      <span className="mono" dir="ltr">{name}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ) : null}

          {/* والرفض **يخرج مشكلةً بالرمز 422 لا حكماً في حقل**، فيُعرض بلوح
              الرفض نفسه الذي تعرضه كلّ شاشةٍ في هذا المنتج. */}
          {admitFailure ? <ProblemPanel error={admitFailure} /> : null}
        </div>
      </StatePanel>

      {/* ═════════════════ ٤ · ما لا يستطيعه العقد — مُعلَناً ═══════════ */}
      <DeclaredGap
        title={t("screen.docShape.gapDefaultsTitle")}
        body={t("screen.docShape.gapDefaultsBody")}
        owed={t("screen.docShape.gapDefaultsOwed")}
        testId="setup-shape-gap-defaults"
      />
    </section>
  );
}
