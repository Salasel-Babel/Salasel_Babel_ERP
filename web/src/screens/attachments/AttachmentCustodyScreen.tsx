/* ═══════════════════════════════════════════════════════════════════════════
   عهدةُ السند — سلسلةُ إصداراته، وتصحيحُه، وسحبُه
   Attachment custody — its version chain, its correction, its withdrawal
   ───────────────────────────────────────────────────────────────────────────
   السؤال الذي تجيبه هذه الشاشة واحد: **«هل هذا السند هو الإصدار القائم؟ ومن
   أودعه ومتى؟ وأُصحّحه بإصدارٍ يحلّ محلّه أم أسحبه؟»** وهي **حكمٌ على صفٍّ
   قائم في السجلّ**، لا إدخالٌ فيه — والإدخال في `/attachments`.

   ولماذا شاشتان لا واحدة: `ADR-0080` يجعل **عدد نماذج الكتابة** هو ما يقسّم
   الشاشة، وحدُّه اثنان. وأبواب الكتابة في المرفقات **ثلاثة** — إيداعٌ
   وتصحيحٌ وسحب — فاجتماعها في شاشةٍ واحدة يخرق الحدّ. والقسمة ليست بالعدد
   وحده: الإيداع **إدخالُ واقعةٍ جديدة** يقع يومياً وأثناء العمل على مستند،
   والتصحيح والسحب **حكمان على ما أُدخل** يقعان نادراً وعن قصد، وأحدهما
   (السحب) **نهائيٌّ لا يُتراجع عنه**. وخلطُهما كان يضع زرّاً لا رجعة فيه
   بجانب زرٍّ يُضغط كل يوم.

   وأربعة قرارات تحكم الشاشة:

   ١ · **لا `PUT` ولا `DELETE` على المرفق، والعقد يقول ذلك بنيوياً**: دور
       التطبيق في القاعدة بلا `UPDATE` ولا `DELETE`، ومشغّلٌ يرفض الاثنين على
       كل دورٍ والمالكُ منهم. فالتصحيح **إصدارٌ يشير إلى سلفه**، والإزالة
       **علامةٌ في جدولٍ ثانٍ**. والشاشة تسمّي ذلك ولا تعرض زرّ حذف.

   ٢ · **السلسلة خطّية ولا تتفرّع، والقاعدة تفرضها لا الشيفرة.** فمرفقٌ له
       خلَف **يُعطَّل تصحيحه في الشاشة قبل الضغط** بالرمز الذي كان سيصل
       (`storage.attachment_already_superseded`)، لا بعد أن يرسل المستخدم
       بايتاتٍ يُرفض إيداعها.

   ٣ · **المسحوب لا يُصحَّح ولا يُسحب مرّتين** — والاثنان مُعلَنان قبل الضغط.

   ٤ · **السبب مفتاحٌ لا نصّ حرّ**، وشكلُه في العقد `^[a-z0-9._]{1,64}$`.
       والمقترحات تُعرض في `datalist` **اقتراحاً لا حبساً**: المجموعة يملكها
       المستدعي بنصّ العقد، فحبسُها في قائمة مغلقة يمنع مفتاحاً رابعاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { readAttachment, reviseAttachment, withdrawAttachment } from "../../api/generated/client";
import type { Attachment } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel } from "../../ui";
import {
  ACCEPTED_MEDIA_TYPES,
  AttachmentFlags,
  ChooseCompanyFirst,
  DownloadTicketPanel,
  MAX_ATTACHMENT_BYTES,
  ReadingSkeleton,
  RefusalNextStep,
  SOURCE_TYPE_RE,
} from "./shared";

/** شكل مفتاح سبب السحب كما ينشره العقد. */
const REASON_KEY_RE = /^[a-z0-9._]{1,64}$/;

/** شكل المعرّف الكوني. */
const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/** مفاتيح سببٍ شائعة — **اقتراحٌ في `datalist` لا قائمةٌ مغلقة**. */
const REASON_SUGGESTIONS: readonly string[] = [
  "wrong_document",
  "duplicate",
  "illegible",
  "superseded_by_paper",
];

/* ══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة عهدة السند: قراءتُه وتصحيحُه وسحبُه. */
export function AttachmentCustodyScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const [lookupId, setLookupId] = useState("");
  const [attachment, setAttachment] = useState<Attachment | null>(null);
  const [reading, setReading] = useState(false);
  const [readError, setReadError] = useState<unknown>(null);

  /* ── نموذج الكتابة الأول: التصحيح بإصدارٍ جديد ─────────────────────── */
  const [file, setFile] = useState<File | null>(null);
  const [sourceType, setSourceType] = useState("");
  const [sourceId, setSourceId] = useState("");
  const [revised, setRevised] = useState<Attachment | null>(null);

  /* ── نموذج الكتابة الثاني: السحب بمفتاح سبب ───────────────────────── */
  const [reasonKey, setReasonKey] = useState("");
  const [confirming, setConfirming] = useState(false);

  const [writeError, setWriteError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const lookupBad = lookupId.trim() !== "" && !UUID_RE.test(lookupId.trim());

  const read = useCallback(
    async (id: string) => {
      setReading(true);
      setReadError(null);
      setWriteError(null);
      setRevised(null);
      setConfirming(false);
      try {
        const found = await readAttachment(transport, {
          companyId: config.companyId,
          attachmentId: id,
        });
        setAttachment(found);
      } catch (failure) {
        setAttachment(null);
        setReadError(failure);
      } finally {
        setReading(false);
      }
    },
    [config.companyId, transport]
  );

  /* ═══════ الرفض قبل الضغط ═════════════════════════════════════════════ */
  const isWithdrawn = attachment !== null && attachment.withdrawal !== null;
  const isSuperseded = attachment !== null && attachment.supersededBy !== null;
  const depositHalfLink = (sourceType.trim() === "") !== (sourceId.trim() === "");
  const depositTypeBad = sourceType.trim() !== "" && !SOURCE_TYPE_RE.test(sourceType.trim());
  const depositIdBad = sourceId.trim() !== "" && !UUID_RE.test(sourceId.trim());
  const tooLarge = file !== null && file.size > MAX_ATTACHMENT_BYTES;
  const emptyFile = file !== null && file.size === 0;
  const reasonBad = reasonKey !== "" && !REASON_KEY_RE.test(reasonKey);

  const canRevise =
    attachment !== null &&
    !isWithdrawn &&
    !isSuperseded &&
    file !== null &&
    !tooLarge &&
    !emptyFile &&
    !depositHalfLink &&
    !depositTypeBad &&
    !depositIdBad;

  const canWithdraw =
    attachment !== null && !isWithdrawn && reasonKey !== "" && !reasonBad;

  const revise = useCallback(async () => {
    if (!attachment || !file) return;
    setBusy(true);
    setWriteError(null);
    try {
      const form = new FormData();
      form.append("content", file, file.name);
      if (sourceType.trim() !== "") {
        form.append("sourceDocumentType", sourceType.trim());
        form.append("sourceDocumentId", sourceId.trim());
      }
      const next = await reviseAttachment(transport, {
        companyId: config.companyId,
        attachmentId: attachment.id,
        body: form,
      });
      setRevised(next);
      /* السلف يُعاد قراءته: خلَفُه صار معروفاً، والشاشة لا تُخمّن حقلاً
         تعرفه القاعدة — والسلف **يبقى مقروءاً ببايتاته الأصلية** إلى الأبد. */
      await read(attachment.id);
    } catch (failure) {
      setWriteError(failure);
    } finally {
      setBusy(false);
    }
  }, [attachment, config.companyId, file, read, sourceId, sourceType, transport]);

  const withdraw = useCallback(async () => {
    if (!attachment) return;
    setBusy(true);
    setWriteError(null);
    try {
      const marked = await withdrawAttachment(transport, {
        companyId: config.companyId,
        attachmentId: attachment.id,
        body: { reasonKey },
      });
      setAttachment(marked);
      setConfirming(false);
    } catch (failure) {
      setWriteError(failure);
    } finally {
      setBusy(false);
    }
  }, [attachment, config.companyId, reasonKey, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="attachment-custody-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.attach.cus.title")}</h1>
          <p className="sub">{t("accounting.attach.cus.lede")}</p>
        </div>
      </header>

      <div className="filterbar" role="search">
        <Field
          id="attach-lookup"
          label={t("accounting.attach.cus.lookup")}
          hint={t("accounting.attach.cus.lookupHint")}
          error={lookupBad ? t("accounting.attach.cus.badUuid") : undefined}
        >
          <input
            id="attach-lookup"
            className={"ctl mono" + (lookupBad ? " is-invalid" : "")}
            dir="ltr"
            autoComplete="off"
            aria-invalid={lookupBad}
            data-testid="attach-lookup-id"
            value={lookupId}
            onChange={(e) => setLookupId(e.target.value)}
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("accounting.attach.cus.read")}
              kind="primary"
              disabled={lookupId.trim() === "" || lookupBad || reading}
              loading={reading}
              onClick={() => void read(lookupId.trim())}
              testId="attach-read"
            />
          </div>
        </div>
      </div>

      {reading ? <ReadingSkeleton /> : null}
      {readError ? (
        <>
          <ProblemPanel error={readError} />
          <RefusalNextStep error={readError} />
        </>
      ) : null}

      {attachment === null && !reading && readError === null ? (
        <EmptyState
          title={t("accounting.attach.cus.noneTitle")}
          body={t("accounting.attach.cus.noneBody")}
          testId="attach-custody-none"
        />
      ) : null}

      {attachment ? (
        <Panel
          title={t("accounting.attach.cus.descriptor")}
          note={t("accounting.attach.cus.descriptorNote")}
          aside={<AttachmentFlags attachment={attachment} />}
          testId="attach-descriptor"
        >
          <div className="kv">
            <div>
              <div className="k">{t("accounting.attach.reg.colFile")}</div>
              <div className="v">{attachment.fileName}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.sniffed")}</div>
              <div className="v code">{attachment.mediaType}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.colBytes")}</div>
              <div className="v n"><Num value={attachment.byteLength} /></div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.colVersion")}</div>
              <div className="v n"><Num value={attachment.version} /></div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.digest")}</div>
              <div className="v mono" dir="ltr">{attachment.contentHash}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.colStored")}</div>
              <div className="v mono" dir="ltr">{attachment.storedAt}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.cus.storedBy")}</div>
              <div className="v mono" dir="ltr">{attachment.storedBy}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.colSource")}</div>
              <div className="v code">
                {attachment.sourceDocumentType ?? t("accounting.attach.reg.noSource")}
              </div>
            </div>
          </div>

          <h3 className="card-hd"><strong>{t("accounting.attach.cus.chain")}</strong></h3>
          <p className="muted">{t("accounting.attach.cus.chainNote")}</p>
          <div className="inline-group" data-testid="attach-chain">
            {attachment.supersedes ? (
              <Button
                label={t("accounting.attach.cus.openPredecessor")}
                size="sm"
                onClick={() => {
                  setLookupId(attachment.supersedes ?? "");
                  void read(attachment.supersedes ?? "");
                }}
                testId="attach-open-predecessor"
              />
            ) : (
              <span className="muted" data-testid="attach-first-version">
                {t("accounting.attach.cus.firstVersion")}
              </span>
            )}
            {attachment.supersededBy ? (
              <Button
                label={t("accounting.attach.cus.openSuccessor")}
                size="sm"
                onClick={() => {
                  setLookupId(attachment.supersededBy ?? "");
                  void read(attachment.supersededBy ?? "");
                }}
                testId="attach-open-successor"
              />
            ) : (
              <span className="muted" data-testid="attach-no-successor">
                {t("accounting.attach.cus.noSuccessor")}
              </span>
            )}
          </div>

          {attachment.withdrawal ? (
            <section
              className="alert alert--warning"
              role="status"
              data-testid="attach-withdrawal-mark"
            >
              <p>{t("accounting.attach.cus.withdrawnMark")}</p>
              <div className="kv">
                <div>
                  <div className="k">{t("accounting.attach.cus.reasonKey")}</div>
                  <div className="v code">{attachment.withdrawal.reasonKey}</div>
                </div>
                <div>
                  <div className="k">{t("accounting.attach.cus.withdrawnAt")}</div>
                  <div className="v mono" dir="ltr">{attachment.withdrawal.withdrawnAt}</div>
                </div>
                <div>
                  <div className="k">{t("accounting.attach.cus.withdrawnBy")}</div>
                  <div className="v mono" dir="ltr">{attachment.withdrawal.withdrawnBy}</div>
                </div>
              </div>
            </section>
          ) : null}
        </Panel>
      ) : null}

      {attachment ? <DownloadTicketPanel attachment={attachment} /> : null}

      {attachment ? (
        <Panel
          title={t("accounting.attach.cus.revise")}
          note={t("accounting.attach.cus.reviseNote")}
          testId="attach-revise-form"
        >
          {isWithdrawn ? (
            <p
              className="alert alert--warning cine-refuse"
              role="status"
              data-testid="attach-revise-blocked"
              data-code="storage.attachment_withdrawn"
            >
              {t("accounting.attach.cus.cannotReviseWithdrawn")}
            </p>
          ) : null}
          {isSuperseded ? (
            <p
              className="alert alert--warning cine-refuse"
              role="status"
              data-testid="attach-revise-superseded"
              data-code="storage.attachment_already_superseded"
            >
              {t("accounting.attach.cus.cannotReviseSuperseded")}
            </p>
          ) : null}

          <div className="grid fields-3">
            <Field
              id="attach-rev-file"
              label={t("accounting.attach.cus.newBytes")}
              hint={t("accounting.attach.cus.newBytesHint")}
              error={
                tooLarge
                  ? t("accounting.attach.reg.tooLarge")
                  : emptyFile
                    ? t("accounting.attach.reg.emptyFile")
                    : undefined
              }
              required
            >
              <input
                id="attach-rev-file"
                className="ctl"
                type="file"
                accept={ACCEPTED_MEDIA_TYPES.join(",")}
                disabled={isWithdrawn || isSuperseded}
                aria-invalid={tooLarge || emptyFile}
                data-testid="attach-revise-file"
                onChange={(e) => {
                  setFile(e.target.files?.item(0) ?? null);
                  setRevised(null);
                  setWriteError(null);
                }}
              />
            </Field>
            <Field
              id="attach-rev-type"
              label={t("accounting.attach.reg.sourceType")}
              hint={t("accounting.attach.cus.reviseTypeHint")}
              error={depositTypeBad ? t("accounting.attach.reg.badTypeCode") : undefined}
            >
              <input
                id="attach-rev-type"
                className={"ctl mono" + (depositTypeBad ? " is-invalid" : "")}
                dir="ltr"
                autoComplete="off"
                disabled={isWithdrawn || isSuperseded}
                aria-invalid={depositTypeBad}
                data-testid="attach-revise-type"
                value={sourceType}
                onChange={(e) => setSourceType(e.target.value)}
              />
            </Field>
            <Field
              id="attach-rev-id"
              label={t("accounting.attach.reg.sourceId")}
              hint={t("accounting.attach.cus.reviseIdHint")}
              error={
                depositIdBad
                  ? t("accounting.attach.reg.badUuid")
                  : depositHalfLink
                    ? t("accounting.attach.reg.needBoth")
                    : undefined
              }
            >
              <input
                id="attach-rev-id"
                className={"ctl mono" + (depositIdBad ? " is-invalid" : "")}
                dir="ltr"
                autoComplete="off"
                disabled={isWithdrawn || isSuperseded}
                aria-invalid={depositIdBad}
                data-testid="attach-revise-id"
                value={sourceId}
                onChange={(e) => setSourceId(e.target.value)}
              />
            </Field>
          </div>

          <div className="inline-group">
            <Button
              label={busy ? t("common.state.loading") : t("accounting.attach.cus.reviseSubmit")}
              kind="primary"
              disabled={!canRevise || busy}
              loading={busy}
              onClick={() => void revise()}
              testId="attach-revise-submit"
            />
          </div>

          {revised ? (
            <section className="alert alert--success" role="status" data-testid="attach-revised">
              <p>{t("accounting.attach.cus.revised")}</p>
              <div className="kv">
                <div>
                  <div className="k">{t("accounting.attach.cus.newId")}</div>
                  <div className="v mono" dir="ltr">{revised.id}</div>
                </div>
                <div>
                  <div className="k">{t("accounting.attach.reg.colVersion")}</div>
                  <div className="v n"><Num value={revised.version} /></div>
                </div>
              </div>
              <Button
                label={t("accounting.attach.cus.openNew")}
                size="sm"
                onClick={() => {
                  setLookupId(revised.id);
                  void read(revised.id);
                }}
                testId="attach-open-new"
              />
            </section>
          ) : null}
        </Panel>
      ) : null}

      {attachment ? (
        <Panel
          title={t("accounting.attach.cus.withdraw")}
          note={t("accounting.attach.cus.withdrawNote")}
          testId="attach-withdraw-form"
        >
          {isWithdrawn ? (
            <p
              className="alert alert--warning cine-refuse"
              role="status"
              data-testid="attach-withdraw-blocked"
              data-code="storage.attachment_withdrawn"
            >
              {t("accounting.attach.cus.alreadyWithdrawn")}
            </p>
          ) : null}

          <div className="grid fields-half">
            <Field
              id="attach-reason"
              label={t("accounting.attach.cus.reasonKey")}
              hint={t("accounting.attach.cus.reasonHint")}
              error={reasonBad ? t("accounting.attach.cus.reasonBad") : undefined}
              required
            >
              <input
                id="attach-reason"
                className={"ctl mono" + (reasonBad ? " is-invalid" : "")}
                dir="ltr"
                autoComplete="off"
                list="attach-reason-list"
                disabled={isWithdrawn}
                aria-invalid={reasonBad}
                data-testid="attach-reason"
                value={reasonKey}
                onChange={(e) => {
                  setReasonKey(e.target.value);
                  setConfirming(false);
                }}
              />
            </Field>
            <Field
              id="attach-reason-note"
              label={t("accounting.attach.cus.reasonSetLabel")}
              hint={t("accounting.attach.cus.reasonSetHint")}
            >
              <input
                id="attach-reason-note"
                className="ctl mono"
                dir="ltr"
                readOnly
                data-testid="attach-reason-set"
                value={REASON_SUGGESTIONS.join(" · ")}
              />
            </Field>
          </div>
          <datalist id="attach-reason-list">
            {REASON_SUGGESTIONS.map((key) => (
              <option key={key} value={key} />
            ))}
          </datalist>

          <p className="alert alert--info" role="status" data-testid="attach-withdraw-rule">
            {t("accounting.attach.cus.withdrawRule")}
          </p>

          <div className="inline-group">
            {confirming ? (
              <>
                <Button
                  label={t("accounting.attach.cus.confirmWithdraw")}
                  kind="danger"
                  disabled={!canWithdraw || busy}
                  loading={busy}
                  onClick={() => void withdraw()}
                  testId="attach-withdraw-confirm"
                />
                <Button
                  label={t("common.action.cancel")}
                  onClick={() => setConfirming(false)}
                  testId="attach-withdraw-cancel"
                />
              </>
            ) : (
              <Button
                label={t("accounting.attach.cus.withdrawSubmit")}
                kind="danger"
                disabled={!canWithdraw || busy}
                onClick={() => setConfirming(true)}
                testId="attach-withdraw-start"
              />
            )}
          </div>
        </Panel>
      ) : null}

      {writeError ? (
        <>
          <ProblemPanel error={writeError} />
          <RefusalNextStep error={writeError} />
        </>
      ) : null}
    </section>
  );
}
