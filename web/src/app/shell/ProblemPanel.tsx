/* ═══════════════════════════════════════════════════════════════════════════
   سطح الخطأ — يعرض تفاصيل المشكلة بصيغة RFC 9457 كما ترسلها الخلفية.
   ───────────────────────────────────────────────────────────────────────────
   الرسالتان العربية والإنجليزية تأتيان من الخادم ولا تُترجَمان هنا: الخادم
   يعرف لماذا رفض، والواجهة لا تخمّن. والرمز الثابت هو نقطة الاعتماد الوحيدة
   — لا يُقرأ نصّ رسالة لاتخاذ قرار.
   وكل الأخطاء تُعرض لا أوّلها فقط: قيدٌ يخالف ثلاث قواعد يُرى بثلاثها.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { useT } from "../../i18n/react";
import { ProblemError } from "../../api/transport";

/**
 * يعرض خطأ الطلب.
 * @param props الخطأ ومحاولة الإعادة.
 */
export function ProblemPanel(props: { error: unknown; onRetry?: () => void }): ReactNode {
  const { t, tp } = useT();
  const error = props.error;
  const problem = error instanceof ProblemError ? error.problem : null;
  const code = error instanceof ProblemError ? error.code : "client.exception";
  const message = error instanceof Error ? error.message : String(error);

  return (
    <section className="problem" role="alert" data-testid="problem-panel" data-code={code}>
      <h2>{problem ? problem.titleAr : t("common.problem.title")}</h2>
      {problem ? <p className="en" dir="ltr" lang="en">{problem.title}</p> : null}

      <p>{problem ? problem.detailAr : t("common.problem.network")}</p>
      {problem ? (
        <p className="en" dir="ltr" lang="en">
          {problem.detail}
        </p>
      ) : (
        <p className="mono">{message}</p>
      )}

      <dl>
        <dt>{t("common.problem.code")}</dt>
        <dd className="mono" data-testid="problem-code">
          {code}
        </dd>
        {problem ? (
          <>
            <dt>{t("common.problem.status")}</dt>
            <dd className="mono">{problem.status}</dd>
            <dt>{t("common.problem.trace")}</dt>
            <dd className="mono" data-testid="problem-trace">
              {problem.traceId}
            </dd>
          </>
        ) : null}
      </dl>

      {problem && problem.errors.length > 0 ? (
        <>
          <p className="muted" data-testid="problem-count">
            {tp("common.problem.count", problem.errors.length)}
          </p>
          <ul>
            {problem.errors.map((e, i) => (
              <li key={e.code + ":" + (e.field ?? "") + ":" + i}>
                <span className="mono">{e.code}</span>
                {e.field ? (
                  <>
                    {" · "}
                    <span className="muted">{t("common.problem.field")}: </span>
                    <span className="mono">{e.field}</span>
                  </>
                ) : null}
                <br />
                {e.messageAr}
                <br />
                <span className="en" dir="ltr" lang="en">
                  {e.messageEn}
                </span>
              </li>
            ))}
          </ul>
        </>
      ) : null}

      {!problem && error instanceof ProblemError ? (
        <p className="muted">{t("common.problem.noContract")}</p>
      ) : null}

      {props.onRetry ? (
        <div>
          <button type="button" className="btn" onClick={props.onRetry}>
            {t("common.action.retry")}
          </button>
        </div>
      ) : null}
    </section>
  );
}
