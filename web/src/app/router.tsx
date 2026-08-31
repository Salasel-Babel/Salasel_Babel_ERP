/* المسارات — مُعرَّفة بالشيفرة، ونوعها مُستنتَج لا مكتوب. */
import { createRootRoute, createRoute, createRouter, createMemoryHistory } from "@tanstack/react-router";
import { AppShell } from "./App";
import { TrialBalanceScreen } from "../screens/trial-balance/TrialBalanceScreen";
import { ContractScreen } from "../screens/contract/ContractScreen";
import { SignInScreen } from "../screens/session/SignInScreen";
import { JournalVoucherScreen } from "../screens/voucher/JournalVoucherScreen";
/* مسار العرض — طبقة عرض تُرمى بعد التسجيل (ADR-0028). ثلاثة أسطر لا أكثر. */
import { DemoStage } from "../demo/DemoStage";
/* صفحة العرض الحيّة لنظام التصميم — هي عقد الطبقة البصرية مع من يبني الأقسام. */
import { DesignScreen } from "../screens/design/DesignScreen";
/* القسم المخزني. */
import { InventoryItemsScreen } from "../screens/inventory/ItemsScreen";
import { InventoryStockScreen } from "../screens/inventory/StockScreen";
import { InventoryMovementsScreen } from "../screens/inventory/MovementsScreen";
import { InventoryValuationScreen } from "../screens/inventory/ValuationScreen";

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

const routeTree = rootRoute.addChildren([
  trialBalanceRoute,
  signInRoute,
  voucherRoute,
  contractRoute,
  designRoute,
  demoRoute,
  inventoryStockRoute,
  inventoryItemsRoute,
  inventoryMovementsRoute,
  inventoryValuationRoute,
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
