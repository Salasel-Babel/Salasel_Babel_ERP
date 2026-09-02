/* ═══════════════════════════════════════════════════════════════════════════
   القسم المخزني — ما تشترك فيه شاشاته الأربع
   The inventory section — what its four screens share
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة أشياء تعيش هنا لأن تكرارها في أربع شاشات كان سيجعلها تنحرف:

   ١ · **بوّابة المنشأة.** كل باب في هذا القسم يشتقّ نطاقه من المنشأة، ولا
       يوجد باب بلا منشأة. فالشاشة بلا منشأةٍ مختارة تعرض **الطريق إلى
       الاختيار**، لا حقل معرّفٍ يُكتب بيد ولا جدولاً فارغاً بلا سبب.

   ٢ · **الخطوة التالية بعد الرفض.** الخادم يرسل رمزاً ثابتاً ورسالتين، وهو
       ما يعرضه `ProblemPanel`. وما لا يعرفه الخادم هو **أين يذهب المستخدم
       الآن في هذه الواجهة** — «سجّل المعامل على الصنف في شاشة الأصناف». وهذه
       الخريطة تُترجم الرمز إلى تلك الجملة، **ولا تُعيد كتابة رسالة الخادم**:
       الاثنان يُعرضان معاً، لا أحدهما بدل الآخر.
       ⚠ والاعتماد على **الرمز** لا على نصّ الرسالة — قاعدةٌ حاكمة في هذا
       المستودع: نصّ الرسالة عرضٌ يتغيّر، والرمز عقد.

   ٣ · **لوح النقص المُعلَن.** حين لا يكون في العقد بابٌ لشيءٍ يحتاجه القسم،
       تقول الشاشة ذلك **صراحةً وتسمّي القرار المستحقّ على المالك**. وهذا
       ليس رفضاً — النظام لم يرفض شيئاً، لا باب أصلاً — وليس فراغاً. وشاشةٌ
       تكذب على المستخدم ببياناتٍ مُختلَقة أسوأ من شاشةٍ ناقصة معلَنة.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { ProblemError } from "../../api/transport";
import "./inventory.css";

/* ═════════════════════════════════════════════ ١ · بوّابة المنشأة */

/**
 * ما يُعرض حين لا منشأة مختارة: الطريق إلى الاختيار.
 * @param props معرّف الاختبار.
 */
export function ChooseCompanyFirst(props: { testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId ?? "inventory-needs-company"}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("inventory.shell.needCompany")}</h3>
      <p>{t("inventory.shell.needCompanyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="inventory-go-sign-in">
          {t("screen.signIn.action")}
        </Link>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════ ٢ · الخطوة التالية بعد الرفض */

/**
 * رموز رفض وحدة المخزون التي **تُترجَم إلى خطوةٍ في هذه الواجهة**.
 * والمفاتيح مكتوبةٌ حرفاً بحرف كما يرسلها `InventoryErrors`؛ ورمزٌ لا يظهر
 * هنا يُعرض برسالة الخادم وحدها — وهي كافية، لأنها تسمّي البند أصلاً.
 */
export const INVENTORY_NEXT_STEP: Readonly<Record<string, string>> = {
  "inventory.no_cost_basis": "inventory.movements.nextNoCostBasis",
  "inventory.unit_not_convertible": "inventory.movements.nextUnitNotConvertible",
  "inventory.unit_conversion_not_exact": "inventory.movements.nextConversionNotExact",
  "inventory.receipt_cost_not_positive": "inventory.movements.nextReceiptCost",
  "inventory.duplicate_document_number": "inventory.movements.nextDuplicateNumber",
  "inventory.item_not_found": "inventory.movements.nextItemNotFound",
  "inventory.posting_refused": "inventory.movements.nextPostingRefused",
  "inventory.duplicate_item_code": "inventory.items.nextDuplicate",
  "inventory.unit_ratio_not_positive": "inventory.items.nextRatio",

  /* المكان صار كياناً: هذه الأربعة تُترجَم إلى «سجّله في شاشة المستودعات» أو
     «أعِد تفعيله»، وهو ما لا يعرفه الخادم لأنه لا يعرف هذه الواجهة. */
  "inventory.warehouse_not_found": "inventory.movements.nextWarehouseNotFound",
  "inventory.location_not_in_warehouse": "inventory.movements.nextLocationNotInWarehouse",
  "inventory.warehouse_inactive": "inventory.movements.nextPlaceInactive",
  "inventory.location_inactive": "inventory.movements.nextPlaceInactive",
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
  const key = code === null ? undefined : INVENTORY_NEXT_STEP[code];
  if (!key) return null;
  return (
    <p
      className="alert alert--warning cine-refuse"
      role="status"
      data-testid="inventory-next-step"
      data-code={code}
    >
      {t(key)}
    </p>
  );
}

/* ═════════════════════════════════════════ ٣ · لوح النقص المُعلَن */

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
    <section className="inv-gap" data-testid={props.testId}>
      <div className="inline-group">
        <span className="pill pill--pending">{t("inventory.shell.gapBadge")}</span>
        <span className="muted">{t("inventory.shell.gap")}</span>
      </div>
      <h3>{props.title}</h3>
      <p>{props.body}</p>
      {props.owed ? <p className="muted">{props.owed}</p> : null}
    </section>
  );
}

/* ═══════════════════════════════════════════ ٤ · هيكلٌ أثناء القراءة
   حالة التحميل **مصمَّمة لا افتراضية**: تقول ما يُقرأ، وتقول إن القراءة لا
   تغيّر شيئاً — وهي الجملة التي تُطمئن محاسباً ضغط زرّاً ولم يتغيّر شيء. */

/**
 * هيكلٌ عظمي أثناء أول قراءة.
 * @param props معرّف الاختبار.
 */
export function ReadingSkeleton(props: { testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <div className="card card-pad" data-testid={props.testId ?? "inventory-loading"}>
      <strong>{t("inventory.shell.loading")}</strong>
      <p className="muted">{t("inventory.shell.loadingBody")}</p>
      <div className="skel skel-text w-90" />
      <div className="skel skel-text w-75" />
      <div className="skel skel-text w-60" />
    </div>
  );
}
