/* ═══════════════════════════════════════════════════════════════════════════
   أين تهبط المسوّدة المنطوقة — جدولٌ في طبقة التطبيق، لا في مكوّن الصوت.
   ───────────────────────────────────────────────────────────────────────────
   مكوّن `voice/` **لا يعرف الشاشات ولا المسارات** — هذا حدُّه المُعلَن في
   `web/src/voice/README.md`، وهو ما يجعله قابلاً للنقل والاختبار وحده. والمسارات
   يملكها من يسجّلها: `app/router.tsx`. فالجدول هنا.

   ⚠ **وغيابُ الوجهة يُقال ولا يُخترَع.** شاشةُ مستندٍ لم تهبط بعد تعني `null`،
   واللوحة تُظهر ذلك **نصّاً على الشاشة**: «المسوّدة جاهزة، وشاشة هذا المستند لم
   تهبط بعد». وقفزةٌ إلى مسارٍ غير مسجَّل تُنتج شاشة «لا يوجد» فيظنّ المستخدم أن
   أمره ضاع — وقد وصل تماماً.

   ⚠ **ويُفحص التسجيل الفعلي لا الجدول وحده.** المسار المكتوب هنا قد يكون لشاشةٍ
   تهبط في فرعٍ آخر لم يُدمج بعد، فتُقرأ مسارات الموجّه القائم ويُقارَن بها.
   ═══════════════════════════════════════════════════════════════════════════ */

/**
 * النيّة ← مسار شاشة مستندها. **والغائب غائبٌ بقصد**: النيّات التي لا مفتاح لها
 * هنا لا شاشة لمستندها اليوم في أي فرع.
 * <p>
 * وقد كانت ثلاثَ عشرةَ نيّةً محاسبيةً بلا وجهة؛ فلمّا هبطت شاشات دورة المستندات
 * صار لتسعٍ منها وجهة، **وبقيت أربع** لأن مستنداتها الأربعة لم تُبنَ لها شاشة —
 * وأبوابها منشورةٌ في العقد:
 * </p>
 * <ul>
 *   <li>{@code accounting.credit_note.draft} — الإشعار الدائن
 *       ({@code draftCreditNote} · {@code …/credit-notes}).</li>
 *   <li>{@code accounting.purchase_return.draft} — مرتجع المشتريات
 *       ({@code draftPurchaseReturn}).</li>
 *   <li>{@code accounting.stock_bill.capture} — الفاتورة المخزنية
 *       ({@code draftStockBill}) — وهي **مستندٌ آخر** لا فاتورة المصروف.</li>
 *   <li>{@code accounting.payables_aging.query} — أعمار الذمم الدائنة
 *       ({@code readPayablesAging}) — بالشكل نفسه الذي تُقرأ به المدينة.</li>
 * </ul>
 * <p>
 * وهذه الأربع تُترك {@code null} **عمداً**: مسارٌ يقود إلى شاشةٍ لا تخدم
 * المستند المنطوق أسوأ من لا شيء، لأن اللوحة تقول حينئذ «مسوّدتك وصلت» وهي
 * لم تصل. واللوحة تعرض غيابَ الوجهة نصّاً على الشاشة.
 * </p>
 */
export const VOICE_DESTINATIONS: Readonly<Record<string, string>> = {
  /* ── المحاسبة — دورة المستندات ───────────────────────────────────────────
     كانت هذه النيّات كلّها بلا وجهة، وهي أكثر ما يدين به مالك المنتج (خطة
     الصوت §9). وقد هبطت شاشاتها، فصارت لها وجهات.

     **والوجهة تتبع `operationId` المنشور في الكتالوج لا تشابه الأسماء**:
     `accounting.customer_balance.query` عمليتُه `readReceivablesAging`
     بنصّ العقد، فوجهتُه شاشةُ ذلك التقرير — لا شاشةٌ أخرى تشبه اسمَه. */
  "accounting.sales_invoice.draft": "/sales/invoice",
  "accounting.customer_receipt.record": "/sales/receipt",
  "accounting.receivables_aging.query": "/sales/receivables",
  "accounting.customer_balance.query": "/sales/receivables",
  "accounting.purchase_order.draft": "/purchasing/order",
  "accounting.goods_receipt.draft": "/purchasing/goods-receipt",
  "accounting.supplier_bill.capture": "/purchasing/bill",
  "accounting.supplier_payment.record": "/purchasing/payment",
  /* وقيدُ اليومية اليدوي شاشتُه قائمةٌ منذ أول يوم (`/voucher`) ولم يكن
     موصولاً بها — والنيّة `AwaitingOwnerDecision` بلا `operationId`، فالوجهة
     تُوصلها إلى الشاشة التي تكتب القيد بيد، ولا تُنشئ شيئاً بنفسها. */
  "accounting.journal_entry.draft": "/voucher",

  /* المقاولات */
  "contracting.client_certificate.measure": "/contracting/certificate",
  "contracting.subcontractor_certificate.measure": "/contracting/certificate",
  "contracting.subcontractor_advance.record": "/contracting/subcontracting",
  "contracting.subcontractor_statement.query": "/contracting/subcontracting",
  "contracting.retention_release.draft": "/contracting/retention",
  "contracting.retention_collection.draft": "/contracting/retention",
  "contracting.retention_register.query": "/contracting/retention",
  "contracting.contract_position.query": "/contracting",
  "contracting.change_order.draft": "/contracting",
  "contracting.guarantee.draft": "/contracting",

  /* الموارد البشرية */
  "hr.payroll_run.draft": "/hr/payroll",
  "hr.payroll_payment.draft": "/hr/payroll",
  "hr.social_insurance_payment.draft": "/hr/payroll",
  "hr.end_of_service_provision.draft": "/hr/end-of-service",
  "hr.end_of_service_settlement.draft": "/hr/end-of-service",
  "hr.employee_advance.record": "/hr",
  "hr.employee_deduction.record": "/hr",
  "hr.employee.query": "/hr",

  /* المخزون */
  "inventory.count_adjustment.record": "/inventory/movements",
  "inventory.issue_to_project.record": "/inventory/movements",
  "inventory.warehouse_transfer.record": "/inventory/movements",
  "inventory.stock_movement.query": "/inventory/movements",
  "inventory.stock_balance.query": "/inventory/stock",
  "inventory.valuation.query": "/inventory/valuation",

  /* العقارات */
  "realestate.lease_contract.draft": "/realestate/lease",
  "realestate.tenant_arrears.query": "/realestate/arrears",
  "realestate.tenant_receipt.record": "/realestate",
  "realestate.rent_invoice.draft": "/realestate",
  "realestate.maintenance_expense.record": "/realestate",
  "realestate.unit_status.query": "/realestate",
};

/** موجّهٌ يكفي لقراءة مساراته — بلا ارتباطٍ بنوع المكتبة كاملاً. */
interface RouteReader {
  readonly routesById?: Readonly<Record<string, unknown>>;
}

/**
 * المسارات المسجَّلة فعلاً في الموجّه القائم.
 * @param router الموجّه.
 */
export function registeredPaths(router: RouteReader | null | undefined): readonly string[] {
  const byId = router?.routesById;
  if (!byId) return [];
  return Object.keys(byId).filter((id) => id.startsWith("/"));
}

/**
 * وجهةُ نيّةٍ **إن كانت شاشتُها مسجَّلة**، وإلّا `null`.
 * @param intentId معرّف النيّة.
 * @param paths المسارات المسجَّلة كما قرأها {@link registeredPaths}.
 */
export function destinationOf(intentId: string, paths: readonly string[]): string | null {
  const wanted = VOICE_DESTINATIONS[intentId];
  if (wanted === undefined) return null;
  return paths.includes(wanted) ? wanted : null;
}
