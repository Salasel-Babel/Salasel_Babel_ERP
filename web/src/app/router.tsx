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
/* قسم العقارات — السجلّ، والعقد وجدوله، والمتأخرات وسندات القبض. */
import { RegisterScreen } from "../screens/realestate/RegisterScreen";
import { LeaseScreen } from "../screens/realestate/LeaseScreen";
import { ArrearsScreen } from "../screens/realestate/ArrearsScreen";
/* المقاولات — أربع شاشات: السجلّ والمستخلص والباطن والمحتجزات. */
import { ContractingRegisterScreen } from "../screens/contracting/RegisterScreen";
import { CertificateScreen } from "../screens/contracting/CertificateScreen";
import { SubcontractingScreen } from "../screens/contracting/SubcontractingScreen";
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
/* الموارد البشرية — أربع شاشات: السجلّ، والمسيّر، والقسيمة، ونهاية الخدمة. */
import { EmployeeRegisterScreen } from "../screens/hr/EmployeeRegisterScreen";
import { PayrollRunScreen } from "../screens/hr/PayrollRunScreen";
import { PayslipScreen } from "../screens/hr/PayslipScreen";
import { EndOfServiceScreen } from "../screens/hr/EndOfServiceScreen";

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

const routeTree = rootRoute.addChildren([
  trialBalanceRoute,
  signInRoute,
  voucherRoute,
  contractRoute,
  voiceRoute,
  designRoute,
  demoRoute,
  realEstateRegisterRoute,
  realEstateLeaseRoute,
  realEstateArrearsRoute,
  contractingRegisterRoute,
  contractingCertificateRoute,
  contractingSubcontractingRoute,
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
  salesInvoiceRoute,
  salesReceiptRoute,
  salesReceivablesRoute,
  purchasingOrderRoute,
  purchasingGoodsReceiptRoute,
  purchasingBillRoute,
  purchasingPaymentRoute,
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
