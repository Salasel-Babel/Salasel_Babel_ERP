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
  hrRegisterRoute,
  hrPayrollRoute,
  hrPayslipRoute,
  hrEndOfServiceRoute,
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
