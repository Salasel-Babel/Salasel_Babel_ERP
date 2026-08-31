/* نقطة الدخول. */
import { StrictMode, type ReactNode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import "./styles/tokens.css";
import "./styles/components.css";
import "./styles/app.css";
import { LocaleProvider } from "./i18n/react";
import { ApiProvider } from "./app/api-context";
import { createAppRouter } from "./app/router";
import type { Transport } from "./api/transport";

/* ═══════════════════════════════════════════════════════════════════════════
   بناء العرض — رايةٌ وقت بناءٍ لا فرعٌ وقت تشغيل
   ───────────────────────────────────────────────────────────────────────────
   `VITE_BABEL_DEMO` ثابتةٌ يعرفها المُجمِّع، فبناءُ المنتج **لا يحمل شيئاً**
   من هذه الطبقة: الاستيراد وحده يسقط مع الشرط. ولا سطر في شاشةٍ يسأل عنها.
   ═══════════════════════════════════════════════════════════════════════════ */
const SHOWCASE = import.meta.env.VITE_BABEL_DEMO === "1";

let transport: Transport | undefined;
let Note: (() => ReactNode) | null = null;

if (SHOWCASE) {
  const { installShowcase } = await import("./showcase/install");
  const { showcaseTransport } = await import("./showcase/transport");
  const { ShowcaseNote } = await import("./showcase/Note");
  installShowcase();
  transport = showcaseTransport();
  Note = ShowcaseNote;
}

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: false } },
});

const router = createAppRouter(SHOWCASE ? { hash: true } : undefined);

const container = document.getElementById("root");
if (!container) throw new Error("لا عنصر جذر. / no root element.");

createRoot(container).render(
  <StrictMode>
    <LocaleProvider>
      <QueryClientProvider client={queryClient}>
        <ApiProvider {...(transport ? { transport } : {})}>
          <RouterProvider router={router} />
          {Note ? <Note /> : null}
        </ApiProvider>
      </QueryClientProvider>
    </LocaleProvider>
  </StrictMode>
);
