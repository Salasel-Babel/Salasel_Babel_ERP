/* نقطة الدخول. */
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import "./styles/tokens.css";
import "./styles/components.css";
import "./styles/app.css";
import { LocaleProvider } from "./i18n/react";
import { ApiProvider } from "./app/api-context";
import { createAppRouter } from "./app/router";

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: false } },
});

const router = createAppRouter();

const container = document.getElementById("root");
if (!container) throw new Error("لا عنصر جذر. / no root element.");

createRoot(container).render(
  <StrictMode>
    <LocaleProvider>
      <QueryClientProvider client={queryClient}>
        <ApiProvider>
          <RouterProvider router={router} />
        </ApiProvider>
      </QueryClientProvider>
    </LocaleProvider>
  </StrictMode>
);
