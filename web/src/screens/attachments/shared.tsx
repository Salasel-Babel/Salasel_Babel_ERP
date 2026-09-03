/* ═══════════════════════════════════════════════════════════════════════════
   سجلّ المرفقات — ما تشترك فيه شاشتاه
   The attachment register — what its two screens share
   ───────────────────────────────────────────────────────────────────────────
   أربعة أشياء تعيش هنا لأن تكرارها في شاشتين كان سيجعلها تنحرف:

   ١ · **بوّابة المنشأة.** كل باب في هذا السجلّ يشتقّ نطاقه من المنشأة، فلا
       شاشةَ مرفقاتٍ بلا منشأة مختارة.

   ٢ · **الخطوة التالية بعد الرفض** — إلى جانب رسالة الخادم لا بدلاً منها،
       ومعتمدةً على **الرمز الثابت** لا على نصّ الرسالة.

   ٣ · **لوح الاستخراج: تذكرةٌ ثمّ تنزيل — خطوتان لا واحدة.**
       العقد يفصلهما فصلاً بنيوياً، فلا تدمجهما الشاشة:
         · `issueAttachmentDownloadTicket` يسكّ رمزاً موقّعاً بـHMAC-SHA256
           يحمل **المستأجر والمرفق والحامل ولحظة الانتهاء داخل البايتات
           الموقّعة**، وسقفُه خمس دقائق (ADR-0046 §3).
         · `downloadAttachment` **يطلب التذكرة في سلسلة استعلامه إلزاماً**،
           ويتحقّق من التوقيع ثمّ الانتهاء ثمّ مطابقة المستأجر، ثمّ **يُعيد
           المخزن حساب البصمة ويقارنها قبل تسليم أي بايتة**.
       ولماذا لا يُدمجان في زرٍّ واحد: التذكرة **قدرةٌ منفصلة عن الجلسة** —
       وهي ما كان سيُعطى للمتصفّح لولا أنّ إعطاءه إيّاها يضعها في تاريخ
       التصفّح وفي سجلّات الوسطاء. فالشاشة تُظهر التذكرة ومدّتها وأنّها **لا
       تُبطَل قبل انتهائها**، ثمّ تُنزّل بها عبر العميل المُولَّد. وزرٌّ واحد
       كان سيُخفي أنّ صلاحيةً قابلةً للتسرّب قد صدرت.

   ٤ · **الانتهاء يُقال قبل الضغط لا بعده.** تذكرةٌ مضى وقتُها تُعطِّل زرّ
       التنزيل وتُسمّي السبب باسمه — `storage.ticket_expired` — بدل أن يضغط
       المستخدم فيتلقّى 401 يقرؤها «انتهت جلستي».
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import {
  downloadAttachment,
  issueAttachmentDownloadTicket,
} from "../../api/generated/client";
import type { Attachment, AttachmentTicket } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { ProblemError } from "../../api/transport";
import { Num, useT } from "../../i18n/react";
import { Button, Field, Panel } from "../../ui";

/* ═══════════════════════════════════════════ حدودُ العقد كما نُشرت
   أرقامٌ **مقروءة من العقد ومن إعداد المخزن**، مكتوبةٌ هنا مرّةً كي تُقال
   للمستخدم **قبل** أن يضغط. ولا واحد منها يُحسب في المتصفّح. */

/** سقف بايتات الإيداع: عشرون ميبيبايت (`StorageOptions.DefaultMaximumBytes`). */
export const MAX_ATTACHMENT_BYTES = 20 * 1024 * 1024;

/** سقف عمر التذكرة بالثواني — `IssueAttachmentTicketRequest.lifetimeSeconds`. */
export const MAX_TICKET_SECONDS = 300;

/** الأنواع الستّة التي يقبلها الشمّ — `Attachment.mediaType` حرفاً بحرف. */
export const ACCEPTED_MEDIA_TYPES: readonly string[] = [
  "application/pdf",
  "image/heic",
  "image/jpeg",
  "image/png",
  "image/tiff",
  "image/webp",
];

/** شكل رمز نوع المستند المصدر كما ينشره العقد. */
export const SOURCE_TYPE_RE = /^[a-z0-9._]{1,64}$/;

/** شكل عمر التذكرة: عدد صحيح موجب، ويُفحص شكلُه **قبل** أي تحويل. */
export const TICKET_SECONDS_RE = /^[1-9][0-9]{0,2}$/;

/** شكل عدّاد الصفحة (`skip` و`take`) كما ينشره العقد. */
export const PAGE_NUMBER_RE = /^[0-9]{1,7}$/;

/** سقف حجم الصفحة في الجرد. */
export const MAX_PAGE_TAKE = 100;

/* ═════════════════════════════════════════════ ١ · بوّابة المنشأة */

/**
 * ما يُعرض حين لا منشأة مختارة: الطريق إلى الاختيار.
 * @param props معرّف الاختبار.
 */
export function ChooseCompanyFirst(props: { testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId ?? "attach-needs-company"}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("accounting.attach.needCompany")}</h3>
      <p>{t("accounting.attach.needCompanyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="attach-go-sign-in">
          {t("screen.signIn.action")}
        </Link>
      </div>
    </section>
  );
}

/** هيكلٌ عظمي أثناء أول قراءة. */
export function ReadingSkeleton(props: { testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <div className="card card-pad" data-testid={props.testId ?? "attach-loading"}>
      <strong>{t("accounting.attach.loading")}</strong>
      <p className="muted">{t("accounting.attach.loadingBody")}</p>
      <div className="skel skel-text w-90" />
      <div className="skel skel-text w-75" />
      <div className="skel skel-text w-60" />
    </div>
  );
}

/* ═══════════════════════════════════ ٢ · الخطوة التالية بعد الرفض */

/**
 * رموز رفض حدّ المرفقات التي **تُترجَم إلى خطوةٍ في هذه الواجهة**.
 * والمفاتيح مكتوبةٌ حرفاً بحرف كما يرسلها `AttachmentErrors`؛ ورمزٌ لا يظهر
 * هنا يُعرض برسالة الخادم وحدها — وهي تسمّي البند أصلاً.
 */
export const ATTACHMENT_NEXT_STEP: Readonly<Record<string, string>> = {
  "storage.content_empty": "accounting.attach.next.empty",
  "storage.content_too_large": "accounting.attach.next.tooLarge",
  "storage.content_not_recognised": "accounting.attach.next.notRecognised",
  "storage.declared_type_mismatch": "accounting.attach.next.typeMismatch",
  "storage.file_name_refused": "accounting.attach.next.fileName",
  "storage.source_document_incomplete": "accounting.attach.next.halfLink",
  "storage.source_document_type_refused": "accounting.attach.next.badTypeCode",
  "storage.page_refused": "accounting.attach.next.page",
  "storage.attachment_not_found": "accounting.attach.next.notFound",
  "storage.attachment_withdrawn": "accounting.attach.next.withdrawn",
  "storage.attachment_already_superseded": "accounting.attach.next.superseded",
  "storage.content_hash_mismatch": "accounting.attach.next.hashMismatch",
  "storage.content_missing": "accounting.attach.next.contentMissing",
  "storage.ticket_expired": "accounting.attach.next.ticketExpired",
  "storage.ticket_signature_invalid": "accounting.attach.next.ticketInvalid",
  "storage.ticket_lifetime_refused": "accounting.attach.next.ticketLifetime",
};

/**
 * يقرأ الرمز الثابت من خطأٍ ما، أو `null` إن لم يكن خطأ عقد.
 * @param error الخطأ كما وصل.
 */
export function problemCodeOf(error: unknown): string | null {
  return error instanceof ProblemError ? error.code : null;
}

/**
 * الخطوة التالية في هذه الواجهة بعد رفضٍ مُسمّى — **إلى جانب رسالة الخادم
 * لا بدلاً منها**. تمتنع عن الظهور حين لا يكون لها ما تقوله.
 * @param props الخطأ الواصل.
 */
export function RefusalNextStep(props: { error: unknown }): ReactNode {
  const { t } = useT();
  const code = problemCodeOf(props.error);
  const key = code === null ? undefined : ATTACHMENT_NEXT_STEP[code];
  if (!key) return null;
  return (
    <p
      className="alert alert--warning cine-refuse"
      role="status"
      data-testid="attach-next-step"
      data-code={code}
    >
      {t(key)}
    </p>
  );
}

/* ══════════════════════════════════════════ ٣ · لوح النقص المُعلَن */

/**
 * نقصٌ في السطح المنشور، معلَناً بحدوده وبالقرار المستحقّ على المالك.
 * @param props العنوان والشرح والقرار.
 */
export function SurfaceGap(props: {
  readonly title: string;
  readonly body: string;
  readonly owed?: string;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  return (
    <section className="card card-pad stack" data-testid={props.testId}>
      <div className="inline-group">
        <span className="pill pill--pending">{t("accounting.attach.gapBadge")}</span>
        <span className="muted">{t("accounting.attach.gap")}</span>
      </div>
      <h3>{props.title}</h3>
      <p>{props.body}</p>
      {props.owed ? <p className="muted">{props.owed}</p> : null}
    </section>
  );
}

/* ══════════════════════════════════ ٤ · وصفُ المرفق في كلماتٍ وشارات */

/** شارات حال المرفق: مسحوبٌ · مُصحَّح · إصدارٌ لاحق. */
export function AttachmentFlags(props: { attachment: Attachment }): ReactNode {
  const { t } = useT();
  const { attachment } = props;
  return (
    <div className="inline-group">
      {attachment.withdrawal ? (
        <span
          className="pill pill--rejected"
          data-testid="attach-flag-withdrawn"
          title={t("accounting.attach.withdrawnWhy")}
        >
          {t("accounting.attach.withdrawn")}
        </span>
      ) : null}
      {attachment.supersededBy ? (
        <span
          className="pill pill--archived"
          data-testid="attach-flag-superseded"
          title={t("accounting.attach.supersededWhy")}
        >
          {t("accounting.attach.superseded")}
        </span>
      ) : null}
      {attachment.supersedes ? (
        <span className="pill pill--info" data-testid="attach-flag-revision">
          {t("accounting.attach.isRevision")}
        </span>
      ) : null}
      {!attachment.withdrawal && !attachment.supersededBy ? (
        <span className="pill pill--posted" data-testid="attach-flag-current">
          {t("accounting.attach.current")}
        </span>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════ ٥ · الاستخراج: تذكرةٌ ثمّ تنزيل */

/** ما وصل من بايتات، ومقارنتُه بما سجّله الوصف. */
interface Delivered {
  readonly bytes: number;
  readonly href: string;
  readonly fileName: string;
}

/**
 * لوح الاستخراج: يسكّ تذكرةً ثمّ ينزّل بها — **خطوتان مُعلَنتان**.
 * @param props المرفق الذي تُفتح بايتاته.
 */
export function DownloadTicketPanel(props: { attachment: Attachment }): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const { attachment } = props;

  const [seconds, setSeconds] = useState("120");
  const [ticket, setTicket] = useState<AttachmentTicket | null>(null);
  const [delivered, setDelivered] = useState<Delivered | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  /* عقربٌ يدقّ كل ثانية **لغرضٍ واحد**: أن يصير زرّ التنزيل معطَّلاً في
     اللحظة التي تنتهي فيها التذكرة، لا بعد أن يضغطه أحد. */
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!ticket) return undefined;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [ticket]);

  /* عنوان الكائن يُبطَل عند استبداله أو عند مغادرة اللوح: تركُه حيّاً يُبقي
     بايتات المستند في ذاكرة التبويبة بعد أن انتهت الحاجة إليها. */
  useEffect(() => {
    const href = delivered?.href;
    return () => {
      if (href) URL.revokeObjectURL(href);
    };
  }, [delivered]);

  /* لحظةُ الانتهاء تُقرأ من التذكرة ولا تُشتقّ من العمر المطلوب: العمر طلبٌ
     والانتهاء حكمُ الخادم، والفارق بينهما هو زمنُ الشبكة. */
  const expiresMs = useMemo(
    () => (ticket ? Date.parse(ticket.expiresAt) : Number.NaN),
    [ticket]
  );
  const expired = ticket !== null && Number.isFinite(expiresMs) && now >= expiresMs;

  const badSeconds =
    !TICKET_SECONDS_RE.test(seconds) || seconds.length > 3 || Number(seconds) > MAX_TICKET_SECONDS;

  const mint = useCallback(async () => {
    setBusy(true);
    setError(null);
    setDelivered(null);
    try {
      /* التحويل الوحيد إلى عدد في هذا الملفّ، **وبعد** فحص الشكل بنمطٍ نصّي:
         العقد ينشر `lifetimeSeconds` عدداً صحيحاً بين ١ و٣٠٠، وهو مدىً دقيقٌ
         تماماً — بخلاف المال والكمّية، وهما نصّان لا يمرّان على `Number` أبداً. */
      const lifetimeSeconds = Number(seconds);
      const minted = await issueAttachmentDownloadTicket(transport, {
        companyId: config.companyId,
        attachmentId: attachment.id,
        body: { lifetimeSeconds },
      });
      setTicket(minted);
      setNow(Date.now());
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [attachment.id, config.companyId, seconds, transport]);

  const fetchBytes = useCallback(async () => {
    if (!ticket) return;
    setBusy(true);
    setError(null);
    try {
      const blob = await downloadAttachment(transport, {
        companyId: config.companyId,
        attachmentId: attachment.id,
        ticket: ticket.token,
      });
      setDelivered({
        bytes: blob.size,
        href: URL.createObjectURL(blob),
        fileName: attachment.fileName,
      });
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [attachment.fileName, attachment.id, config.companyId, ticket, transport]);

  return (
    <Panel
      title={t("accounting.attach.tkt.title")}
      note={t("accounting.attach.tkt.note")}
      aside={<span className="pill pill--info mono" dir="ltr">{attachment.id}</span>}
      testId="attach-ticket-panel"
    >
      <div className="grid fields-half">
        <Field
          id="attach-tkt-seconds"
          label={t("accounting.attach.tkt.seconds")}
          hint={t("accounting.attach.tkt.secondsHint")}
          error={badSeconds ? t("accounting.attach.tkt.secondsBad") : undefined}
        >
          <input
            id="attach-tkt-seconds"
            className={"ctl mono" + (badSeconds ? " is-invalid" : "")}
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            aria-invalid={badSeconds}
            data-testid="attach-ticket-seconds"
            value={seconds}
            onChange={(e) => setSeconds(e.target.value)}
          />
        </Field>
        <Field
          id="attach-tkt-file"
          label={t("accounting.attach.tkt.file")}
          hint={t("accounting.attach.tkt.fileHint")}
        >
          <input
            id="attach-tkt-file"
            className="ctl"
            readOnly
            data-testid="attach-ticket-file"
            value={attachment.fileName}
          />
        </Field>
      </div>

      <div className="inline-group">
        <Button
          label={t("accounting.attach.tkt.mint")}
          kind="primary"
          disabled={badSeconds || busy}
          loading={busy}
          onClick={() => void mint()}
          testId="attach-ticket-mint"
        />
        <Button
          label={t("accounting.attach.tkt.download")}
          disabled={ticket === null || expired || busy}
          onClick={() => void fetchBytes()}
          testId="attach-ticket-download"
        />
      </div>

      {ticket === null ? (
        <p className="muted" data-testid="attach-ticket-none">
          {t("accounting.attach.tkt.noneYet")}
        </p>
      ) : (
        <div className="kv" data-testid="attach-ticket-minted">
          <div>
            <div className="k">{t("accounting.attach.tkt.expiresAt")}</div>
            <div className="v mono" dir="ltr">{ticket.expiresAt}</div>
          </div>
          <div>
            <div className="k">{t("accounting.attach.tkt.path")}</div>
            <div className="v mono" dir="ltr">{ticket.contentPath}</div>
          </div>
        </div>
      )}

      {expired ? (
        <p
          className="alert alert--warning"
          role="status"
          data-testid="attach-ticket-expired"
          data-code="storage.ticket_expired"
        >
          {t("accounting.attach.tkt.expired")}
        </p>
      ) : null}

      {ticket !== null && !expired ? (
        <p className="muted" data-testid="attach-ticket-rules">
          {t("accounting.attach.tkt.noRevoke")}
        </p>
      ) : null}

      {delivered ? (
        <section className="alert alert--success" role="status" data-testid="attach-delivered">
          <p>
            {t(
              delivered.bytes === attachment.byteLength
                ? "accounting.attach.tkt.deliveredMatch"
                : "accounting.attach.tkt.deliveredDiffer"
            )}
          </p>
          <div className="kv">
            <div>
              <div className="k">{t("accounting.attach.tkt.recorded")}</div>
              <div className="v n"><Num value={attachment.byteLength} /></div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.tkt.received")}</div>
              <div className="v n"><Num value={delivered.bytes} /></div>
            </div>
          </div>
          <a
            className="btn btn-sm"
            href={delivered.href}
            download={delivered.fileName}
            data-testid="attach-save-link"
          >
            {t("accounting.attach.tkt.save")}
          </a>
        </section>
      ) : null}

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}
    </Panel>
  );
}
