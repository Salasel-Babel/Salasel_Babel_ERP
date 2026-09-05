/* المسارات — مُعرَّفة بالشيفرة، ونوعها مُستنتَج لا مكتوب. */
import { createRootRoute, createRoute, createRouter, createMemoryHistory } from "@tanstack/react-router";
import { AppShell } from "./App";
import { TrialBalanceScreen } from "../screens/trial-balance/TrialBalanceScreen";
import { ContractScreen } from "../screens/contract/ContractScreen";
import { SignInScreen } from "../screens/session/SignInScreen";
import { JournalVoucherScreen } from "../screens/voucher/JournalVoucherScreen";
/* الأمر المنطوق — الأقسام الخمسة في شاشة واحدة، لا تدفّقٌ واحد في شاشة. */
import { VoiceScreen } from "../screens/voice/VoiceScreen";
/* مسار العرض — طبقة عرض تُرمى بعد التسجيل (ADR-0028). ثلاثة أسطر لا أكثر. */
import { DemoStage } from "../demo/DemoStage";
/* صفحة العرض الحيّة لنظام التصميم — هي عقد الطبقة البصرية مع من يبني الأقسام. */
import { DesignScreen } from "../screens/design/DesignScreen";
/* قسم العقارات — العقارُ ووحداته، وطرفا العقد، والعقد وجدوله، والمتأخرات. */
import { RegisterScreen } from "../screens/realestate/RegisterScreen";
import { PartiesScreen } from "../screens/realestate/PartiesScreen";
import { LeaseScreen } from "../screens/realestate/LeaseScreen";
import { ArrearsScreen } from "../screens/realestate/ArrearsScreen";
/* المقاولات — سبع شاشات بترتيب العمل: السجلّ، وأوامر التغيير، وخطابات الضمان،
   والمستخلص، والباطن، ودفعته المقدمة، والمحتجزات. */
import { ContractingRegisterScreen } from "../screens/contracting/RegisterScreen";
import { ChangeOrdersScreen } from "../screens/contracting/ChangeOrdersScreen";
import { GuaranteesScreen } from "../screens/contracting/GuaranteesScreen";
import { CertificateScreen } from "../screens/contracting/CertificateScreen";
import { SubcontractingScreen } from "../screens/contracting/SubcontractingScreen";
import { SubcontractorAdvancesScreen } from "../screens/contracting/SubcontractorAdvancesScreen";
import { RetentionScreen } from "../screens/contracting/RetentionScreen";
/* القسم المخزني. */
import { InventoryItemsScreen } from "../screens/inventory/ItemsScreen";
import { InventoryStockScreen } from "../screens/inventory/StockScreen";
import { InventoryMovementsScreen } from "../screens/inventory/MovementsScreen";
import { InventoryValuationScreen } from "../screens/inventory/ValuationScreen";
import { InventoryWarehousesScreen } from "../screens/inventory/WarehousesScreen";
import { InventoryPlacementScreen } from "../screens/inventory/PlacementScreen";
import { InventoryTransfersScreen } from "../screens/inventory/TransfersScreen";
import { InventoryUnitsScreen } from "../screens/inventory/UnitsScreen";
import { InventoryPlacementBalancesScreen } from "../screens/inventory/PlacementBalancesScreen";
/* دورة المستندات المحاسبية — سبعُ شاشاتٍ على مجموعتين: المبيعات والمشتريات.
   وهي الدورة التي وصفها صاحب المصلحة — فاتورة، سند قبض — ولم تكن لها شاشة. */
import { SalesInvoiceScreen } from "../screens/accounting/SalesInvoiceScreen";
import { CustomerReceiptScreen } from "../screens/accounting/CustomerReceiptScreen";
import { ReceivablesAgingScreen } from "../screens/accounting/ReceivablesAgingScreen";
import { PurchaseOrderScreen } from "../screens/accounting/PurchaseOrderScreen";
import { GoodsReceiptScreen } from "../screens/accounting/GoodsReceiptScreen";
import { SupplierBillScreen } from "../screens/accounting/SupplierBillScreen";
import { SupplierPaymentScreen } from "../screens/accounting/SupplierPaymentScreen";
/* الموارد البشرية — ثماني شاشات بترتيب العمل: مكوّنات الأجر تُعرَّف مرّةً،
   ثم السجلّ، ثم ما يُقيَّد على الموظف قبل الشهر، ثم المسيّر وقسيمته، ثم سداد
   التأمينات، ثم نهاية الخدمة، ثم المطابقة عند الإقفال. */
import { EmployeeRegisterScreen } from "../screens/hr/EmployeeRegisterScreen";
import { PayrollRunScreen } from "../screens/hr/PayrollRunScreen";
import { PayslipScreen } from "../screens/hr/PayslipScreen";
import { EndOfServiceScreen } from "../screens/hr/EndOfServiceScreen";
import { PayComponentsScreen } from "../screens/hr/PayComponentsScreen";
import { AdvancesDeductionsScreen } from "../screens/hr/AdvancesDeductionsScreen";
import { SocialInsuranceScreen } from "../screens/hr/SocialInsuranceScreen";
import { SubledgerReconciliationScreen } from "../screens/hr/SubledgerReconciliationScreen";
/* ── سجلّ المرفقات وعهدةُ سنده، وحالُ الصنف — كتلةٌ واحدة متّصلة ────────────
   شاشتان للمرفقات لأن أبواب الكتابة فيها ثلاثة والحدّ اثنان (ADR-0080)،
   وشاشةٌ واحدة للصنف لأن أبواب الكتابة فيه اثنان بالضبط. والتبرير كاملاً في
   `ADR-0082-attachments-split-by-hand-items-do-not`. */
import { AttachmentRegisterScreen } from "../screens/attachments/AttachmentRegisterScreen";
import { AttachmentCustodyScreen } from "../screens/attachments/AttachmentCustodyScreen";
import { InventoryItemLifecycleScreen } from "../screens/inventory/ItemLifecycleScreen";
/* ── ما بعد الترحيل — أربعُ شاشاتٍ كتلةً واحدة متّصلة ──────────────────────
   «ما رُحّل خطأً، كيف يُصحَّح، وكيف نُثبت أنه لم يُعدَّل؟» أربعُ أيدٍ يجيب
   كلٌّ منها سؤالاً واحداً: القيدُ يُعكَس بقيدٍ مضادّ، والمستندُ التجاري
   يُصحَّح بمرتجعٍ إلى المورّد أو بإشعارٍ دائن إلى العميل، ثم يُحكَم على
   سلامة السلسلة. والتبرير كاملاً في
   `ADR-after-posting-is-a-group-and-a-reversal-is-not-a-delete`. */
import { JournalEntryScreen } from "../screens/ledger/JournalEntryScreen";
import { PurchaseReturnScreen } from "../screens/ledger/PurchaseReturnScreen";
import { CreditNoteScreen } from "../screens/ledger/CreditNoteScreen";
import { LedgerChainScreen } from "../screens/ledger/LedgerChainScreen";
/* ── الإدارة والاشتراك — أربعُ شاشاتٍ بترتيب العمل: كيف أدخل أوّل مرّة ← ما
   الذي بيدي الآن ← من يدخل معي ← ماذا اشتريتُ وما الذي يعمل. وهي **مجموعةٌ
   إدارية** لا قسمٌ سادس: عقدُ الملاحة خماسيّ، وهذه الأربع خلفها مالكُ اشتراكٍ
   أو مسؤول لا محاسبٌ يكتب مستنداً — فلها عنوانها الخاصّ في الملاحة. */
import { EnrolmentScreen } from "../screens/admin/EnrolmentScreen";
import { SessionScreen } from "../screens/admin/SessionScreen";
import { MembersScreen } from "../screens/admin/MembersScreen";
import { SubscriptionScreen } from "../screens/admin/SubscriptionScreen";
/* ── التأسيس والثوابت — أربعُ شاشاتٍ بترتيب العمل: ما يقع مرّةً فيؤسّس
   المنشأة ← ما يُبوَّب عليه كلُّ سطرٍ بعده ← ما يُرخَّص من حقول المستندات ←
   ما يقبل السطر أصلاً. والتبرير كاملاً في
   `ADR-0085-setup-is-a-group-and-a-verdict-is-not-a-write`. */
import { CompanySetupScreen } from "../screens/setup/CompanySetupScreen";
import { CostCentersScreen } from "../screens/setup/CostCentersScreen";
import { DocumentShapesScreen } from "../screens/setup/DocumentShapesScreen";
import { ChartOfAccountsScreen } from "../screens/setup/ChartOfAccountsScreen";
import { ParametersScreen } from "../screens/setup/ParametersScreen";

const rootRoute = createRootRoute({ component: AppShell });

const trialBalanceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: TrialBalanceScreen,
});

const contractRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contract",
  component: ContractScreen,
});

/* الدخول واختيار المنشأة — الطريق الوحيد إلى معرّف شركة، فلا يُكتب بيد. */
const signInRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/sign-in",
  component: SignInScreen,
});

/* أول شاشة تكتب. */
const voucherRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/voucher",
  component: JournalVoucherScreen,
});

const voiceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/voice",
  component: VoiceScreen,
});

const designRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/design",
  component: DesignScreen,
});

const demoRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/demo",
  component: DemoStage,
});

const realEstateRegisterRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/realestate",
  component: RegisterScreen,
});

const realEstatePartiesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/realestate/parties",
  component: PartiesScreen,
});

const realEstateLeaseRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/realestate/lease",
  component: LeaseScreen,
});

const realEstateArrearsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/realestate/arrears",
  component: ArrearsScreen,
});

/* ── المقاولات ─────────────────────────────────────────────────────────── */
const contractingRegisterRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting",
  component: ContractingRegisterScreen,
});

const contractingChangeOrdersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/change-orders",
  component: ChangeOrdersScreen,
});

const contractingGuaranteesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/guarantees",
  component: GuaranteesScreen,
});

const contractingCertificateRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/certificate",
  component: CertificateScreen,
});

const contractingSubcontractingRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/subcontracting",
  component: SubcontractingScreen,
});

const contractingAdvancesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/advances",
  component: SubcontractorAdvancesScreen,
});

const contractingRetentionRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/contracting/retention",
  component: RetentionScreen,
});

/* ── القسم المخزني — تسعُ شاشات على أبوابٍ قائمة في العقد وحدها ────────────
   وكانت أربعاً حين كان «النقل بين موقعين» و«سجلّ المستودعات» لوحَي نقصٍ
   مُعلَن لا بابَ لهما. ثم نزلت أبوابهما — التسكين ثلاثة مستويات، والنقل
   مسوّدةً وتنفيذاً، ووحدات القياس ومعاملاتها ومسبار تحويلها، والأرصدة
   بأسماء مواضعها — فصار النقص المُعلَن **شاشاتٍ تُفتح**، لا نصّاً يعتذر. */
const inventoryStockRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/stock",
  component: InventoryStockScreen,
});

const inventoryItemsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/items",
  component: InventoryItemsScreen,
});

const inventoryMovementsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/movements",
  component: InventoryMovementsScreen,
});

const inventoryValuationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/valuation",
  component: InventoryValuationScreen,
});

const inventoryWarehousesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/warehouses",
  component: InventoryWarehousesScreen,
});

const inventoryPlacementRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/placement",
  component: InventoryPlacementScreen,
});

const inventoryTransfersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/transfers",
  component: InventoryTransfersScreen,
});

const inventoryUnitsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/units",
  component: InventoryUnitsScreen,
});

const inventoryPlacementBalancesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/placement-balances",
  component: InventoryPlacementBalancesScreen,
});

/* ── الموارد البشرية ──────────────────────────────────────────────────── */
const hrRegisterRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr",
  component: EmployeeRegisterScreen,
});

const hrPayrollRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/payroll",
  component: PayrollRunScreen,
});

const hrPayslipRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/payslip",
  component: PayslipScreen,
});

const hrEndOfServiceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/end-of-service",
  component: EndOfServiceScreen,
});

const hrPayComponentsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/pay-components",
  component: PayComponentsScreen,
});

const hrAdvancesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/advances-deductions",
  component: AdvancesDeductionsScreen,
});

const hrSocialInsuranceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/social-insurance",
  component: SocialInsuranceScreen,
});

const hrReconciliationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/hr/subledger-reconciliation",
  component: SubledgerReconciliationScreen,
});

/* ── دورة المستندات: المبيعات ──────────────────────────────────────────── */
const salesInvoiceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/sales/invoice",
  component: SalesInvoiceScreen,
});

const salesReceiptRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/sales/receipt",
  component: CustomerReceiptScreen,
});

const salesReceivablesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/sales/receivables",
  component: ReceivablesAgingScreen,
});

/* ── دورة المستندات: المشتريات — بترتيب الدورة لا بترتيب الحروف ────────
   أمرٌ ← استلام ← فاتورة ← صرف. وأمر الشراء **لا يُرحَّل**: لا مورد
   `…/posting` له في العقد، والشاشة تقول ذلك نصّاً لا بزرٍّ غائب. */
const purchasingOrderRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/purchasing/order",
  component: PurchaseOrderScreen,
});

const purchasingGoodsReceiptRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/purchasing/goods-receipt",
  component: GoodsReceiptScreen,
});

const purchasingBillRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/purchasing/bill",
  component: SupplierBillScreen,
});

const purchasingPaymentRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/purchasing/payment",
  component: SupplierPaymentScreen,
});

/* ── المرفقات وحالُ الصنف — كتلةٌ واحدة متّصلة. */
const attachmentsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/attachments",
  component: AttachmentRegisterScreen,
});

const attachmentCustodyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/attachments/custody",
  component: AttachmentCustodyScreen,
});

const inventoryItemLifecycleRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/inventory/item-lifecycle",
  component: InventoryItemLifecycleScreen,
});

/* ── ما بعد الترحيل — كتلةٌ واحدة متّصلة ──────────────────────────────── */
const ledgerEntryRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/ledger/entry",
  component: JournalEntryScreen,
});

const ledgerPurchaseReturnRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/ledger/purchase-return",
  component: PurchaseReturnScreen,
});

const ledgerCreditNoteRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/ledger/credit-note",
  component: CreditNoteScreen,
});

const ledgerChainRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/ledger/chain",
  component: LedgerChainScreen,
});

/* ── الإدارة والاشتراك ─────────────────────────────────────────────────── */
const adminEnrolmentRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin/enrolment",
  component: EnrolmentScreen,
});

const adminSessionRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin/session",
  component: SessionScreen,
});

const adminMembersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin/members",
  component: MembersScreen,
});

const adminSubscriptionRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin/subscription",
  component: SubscriptionScreen,
});

/* ── التأسيس والثوابت ──────────────────────────────────────────────────── */
const setupCompanyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/setup",
  component: CompanySetupScreen,
});

const setupCostCentersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/setup/cost-centers",
  component: CostCentersScreen,
});

const setupDocumentShapesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/setup/document-shapes",
  component: DocumentShapesScreen,
});

const setupChartRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/setup/chart-of-accounts",
  component: ChartOfAccountsScreen,
});

const setupParametersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/setup/parameters",
  component: ParametersScreen,
});

const routeTree = rootRoute.addChildren([
  trialBalanceRoute,
  signInRoute,
  voucherRoute,
  contractRoute,
  voiceRoute,
  designRoute,
  demoRoute,
  realEstateRegisterRoute,
  realEstatePartiesRoute,
  realEstateLeaseRoute,
  realEstateArrearsRoute,
  contractingRegisterRoute,
  contractingChangeOrdersRoute,
  contractingGuaranteesRoute,
  contractingCertificateRoute,
  contractingSubcontractingRoute,
  contractingAdvancesRoute,
  contractingRetentionRoute,
  inventoryStockRoute,
  inventoryItemsRoute,
  inventoryMovementsRoute,
  inventoryValuationRoute,
  inventoryWarehousesRoute,
  inventoryPlacementRoute,
  inventoryTransfersRoute,
  inventoryUnitsRoute,
  inventoryPlacementBalancesRoute,
  hrRegisterRoute,
  hrPayrollRoute,
  hrPayslipRoute,
  hrEndOfServiceRoute,
  hrPayComponentsRoute,
  hrAdvancesRoute,
  hrSocialInsuranceRoute,
  hrReconciliationRoute,
  salesInvoiceRoute,
  salesReceiptRoute,
  salesReceivablesRoute,
  purchasingOrderRoute,
  purchasingGoodsReceiptRoute,
  purchasingBillRoute,
  purchasingPaymentRoute,
  attachmentsRoute,
  attachmentCustodyRoute,
  inventoryItemLifecycleRoute,
  ledgerEntryRoute,
  ledgerPurchaseReturnRoute,
  ledgerCreditNoteRoute,
  ledgerChainRoute,
  adminEnrolmentRoute,
  adminSessionRoute,
  adminMembersRoute,
  adminSubscriptionRoute,
  setupCompanyRoute,
  setupCostCentersRoute,
  setupDocumentShapesRoute,
  setupChartRoute,
  setupParametersRoute,
]);

/** ينشئ موجّهاً. الاختبارات تمرّر تاريخاً في الذاكرة فلا تحتاج متصفّحاً. */
export function createAppRouter(options?: { initialPath?: string; memory?: boolean }) {
  return createRouter({
    routeTree,
    ...(options?.memory
      ? { history: createMemoryHistory({ initialEntries: [options.initialPath ?? "/"] }) }
      : {}),
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
