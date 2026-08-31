/* المسارات — مُعرَّفة بالشيفرة، ونوعها مُستنتَج لا مكتوب. */
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  createHashHistory,
} from "@tanstack/react-router";
import { AppShell } from "./App";
import { TrialBalanceScreen } from "../screens/trial-balance/TrialBalanceScreen";
import { ContractScreen } from "../screens/contract/ContractScreen";
import { SignInScreen } from "../screens/session/SignInScreen";
import { JournalVoucherScreen } from "../screens/voucher/JournalVoucherScreen";
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

/* ── القسم المخزني — أربع شاشات على أبوابٍ قائمة في العقد وحدها ────────────
   ولا خامسة: النقل بين موقعين وقائمة المستودعات لا بابَ لهما اليوم، فهما
   لوحا «نقصٍ مُعلَن» داخل الشاشتين لا مسارَين يقودان إلى بياناتٍ مُختلَقة. */
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

const routeTree = rootRoute.addChildren([
  trialBalanceRoute,
  signInRoute,
  voucherRoute,
  contractRoute,
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
  hrRegisterRoute,
  hrPayrollRoute,
  hrPayslipRoute,
  hrEndOfServiceRoute,
]);

/**
 * ينشئ موجّهاً. الاختبارات تمرّر تاريخاً في الذاكرة فلا تحتاج متصفّحاً.
 *
 * و`hash` لصفحة العرض المفردة وحدها: ملفٌّ واحد يُفتح من مسارٍ لا يعرفه أحد
 * سلفاً، وتاريخُ المتصفّح المعتاد يجعل `/realestate/lease` طلبَ ملفٍّ غير
 * موجود عند أول إعادة تحميل. **والافتراض لا يتغيّر**: التطبيق الحقيقي يُخدَم
 * من جذرٍ يعرفه خادمه.
 */
export function createAppRouter(options?: { initialPath?: string; memory?: boolean; hash?: boolean }) {
  const history = options?.memory
    ? createMemoryHistory({ initialEntries: [options.initialPath ?? "/"] })
    : options?.hash
      ? createHashHistory()
      : null;
  return createRouter({ routeTree, ...(history ? { history } : {}) });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
