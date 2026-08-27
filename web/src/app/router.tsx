/* المسارات — مُعرَّفة بالشيفرة، ونوعها مُستنتَج لا مكتوب. */
import { createRootRoute, createRoute, createRouter, createMemoryHistory } from "@tanstack/react-router";
import { AppShell } from "./App";
import { TrialBalanceScreen } from "../screens/trial-balance/TrialBalanceScreen";
import { ContractScreen } from "../screens/contract/ContractScreen";

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

const routeTree = rootRoute.addChildren([trialBalanceRoute, contractRoute]);

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
