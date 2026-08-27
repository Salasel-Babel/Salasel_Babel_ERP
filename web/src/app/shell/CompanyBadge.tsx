/* ═══════════════════════════════════════════════════════════════════════════
   شارة المنشأة الجارية — واسمها لا معرّفها
   ───────────────────────────────────────────────────────────────────────────
   ما دام معرّف الشركة جزءاً من كل مسار، فالمستخدم يحتاج أن يرى **في كل شاشة**
   أي منشأة يعمل عليها الآن. وأخطر عطل في منتج متعدّد المنشآت ليس أن يعجز
   المستخدم عن الوصول، بل أن **يرحّل في منشأة وهو يظنّ أنه في أخرى** — ولا
   شيء في الشاشة يقول له ذلك.

   ولذلك: الاسم بارز، والمعرّف تحته للتشخيص، والتبديل بضغطة واحدة.
   والاسم يُقرأ من الجلسة نفسها لا من نصّ محفوظ في المتصفّح: نصّ محفوظ يبقى
   على اسم قديم بعد إعادة تسمية المنشأة.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { readSession } from "../../api/generated/client";
import { useApi } from "../api-context";
import { resolveTranslatedName } from "../translated-name";
import { useLocale, useT } from "../../i18n/react";

/** اسم المنشأة الجارية ومعرّفها، وزرّ التبديل. */
export function CompanyBadge(): ReactNode {
  const { t } = useT();
  const { locale } = useLocale();
  const { transport, config } = useApi();

  const session = useQuery({
    queryKey: ["session", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readSession(transport, signal),
  });

  const chosen = session.data?.companies.find((c) => c.companyId === config.companyId) ?? null;
  const resolved = chosen?.nameAr
    ? resolveTranslatedName(chosen.nameAr, chosen.nameTranslations, locale)
    : null;

  if (config.companyId === "") {
    return (
      <Link to="/sign-in" className="btn btn-sm" data-testid="company-badge-empty">
        {t("app.company.choose")}
      </Link>
    );
  }

  return (
    <Link
      to="/sign-in"
      className="chip"
      data-testid="company-badge"
      data-company={config.companyId}
      title={config.companyId}
    >
      <span data-testid="company-badge-name">
        {resolved ? resolved.text : t("app.company.unknown")}
      </span>
      <span aria-hidden="true" className="muted">·</span>
      <span className="muted mono" dir="ltr" data-testid="company-badge-id">
        {config.companyId.slice(0, 8)}
      </span>
    </Link>
  );
}
