/* ═══════════════════════════════════════════════════════════════════════════
   التقييم والمطابقة — ثلاثة طرقٍ مستقلّة إلى الرقم نفسه
   Valuation and reconciliation — three independent routes to the same number
   ───────────────────────────────────────────────────────────────────────────
   هذه الشاشة هي **ذكاء القسم المخزني المرئي**، وذكاؤها أنها لا تخترع شيئاً:

   ١ · **الطرق الثلاثة تُحسب في الخادم** — مجموع الحركات، ومجموع أرصدة
       الأصناف، ورصيد نقطة الضبط في الدفتر — ولا يُشتقّ أحدها من آخر. واثنان
       يكفيان لكشف انحرافٍ بين الوحدة والدفتر؛ **والثالث يكشف انحراف الوحدة
       عن نفسها**: رصيدٌ لا يساوي مجموع حركاته، وهو عطلٌ لا يراه أي فحصٍ
       يقارن طرفين.

   ٢ · **الحكم يصل محسوماً ولا يُعاد حسابه هنا.** `isReconciled` يعني الفارق
       **صفرٌ بالضبط** لا «قريبٌ من الصفر»، والمقارنة بين مبلغين قرارٌ عشري
       يقع حيث تقع الأرقام. ولو قارنت هذه الشاشة المبالغ بنفسها لأعادت فخّ
       العائم من بابه الثاني.

   ٣ · **الانحراف يُسمّى بمكانه.** كل مستندٍ منحرف يخرج بنوعه ومعرّفه وصنفه
       وسبب انحرافه، فلا تقول الشاشة «هناك مشكلة» بلا «أين». وسببُ الانحراف
       مجموعةٌ مغلقة من ثلاثة أعضاء تُقرأ حرفياً — ولا يُقبل رقمٌ مكان الاسم.

   ٤ · **الكشف المتدفّق يكشف ما وُجد فعلاً.** الطرق الثلاثة تظهر واحداً بعد
       آخر بمفردة `reveal` — «هذا ترتيب ما وجده النظام» — ولا تُخترع صفوفٌ
       ليبدو مشغولاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readInventoryValuation } from "../../api/generated/client";
import { PARAM_readInventoryValuation_asOf_RE } from "../../api/generated/formats";
import type { InventoryDivergence } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Button, EmptyState, Panel, StatCard, revealAt } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton } from "./shared";

/** أسباب الانحراف الثلاثة، بأسمائها في العقد حرفاً بحرف. */
const REASON_LABEL: Readonly<Record<InventoryDivergence["reasonCode"], string>> = {
  amount_mismatch: "inventory.valuation.reasonAmountMismatch",
  missing_in_control: "inventory.valuation.reasonMissingInControl",
  missing_in_subledger: "inventory.valuation.reasonMissingInSubledger",
};

/** اليوم بصيغة yyyy-MM-dd ميلادية. */
function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/** شاشة تقييم المخزون ومطابقته. */
export function InventoryValuationScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [asOf, setAsOf] = useState(todayIso);

  const asOfValid = PARAM_readInventoryValuation_asOf_RE.test(asOf);

  const result = useQuery({
    queryKey: ["inventory-valuation", config.baseUrl, config.token, config.companyId, asOf],
    enabled: config.companyId !== "" && asOfValid,
    retry: false,
    queryFn: ({ signal }) =>
      readInventoryValuation(transport, { companyId: config.companyId, asOf }, signal),
  });

  const reload = useCallback(() => {
    void result.refetch();
  }, [result]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  const data = result.data ?? null;

  return (
    <section className="stack" data-testid="inventory-valuation-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.valuation.title")}</h1>
          <p className="sub">{t("inventory.valuation.lede")}</p>
        </div>
      </header>

      <div className="filterbar" role="search">
        <div className="field">
          <label htmlFor="val-as-of">{t("inventory.valuation.asOf")}</label>
          <input
            id="val-as-of"
            className={"ctl mono" + (asOfValid ? "" : " is-invalid")}
            type="date"
            dir="ltr"
            aria-invalid={!asOfValid}
            aria-describedby="val-as-of-hint"
            data-testid="valuation-as-of"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
          />
          <span className="hint" id="val-as-of-hint">
            {asOfValid ? t("inventory.valuation.asOfHint") : t("inventory.valuation.asOfBad")}
          </span>
        </div>
        <div className="rowctl">
          <div className="inline-group">
            <Button label={t("common.action.refresh")} onClick={reload} testId="valuation-reload" />
          </div>
        </div>
        {data ? (
          <span
            className={"pill " + (data.isReconciled ? "pill--posted" : "pill--rejected")}
            data-testid="reconciled-pill"
            data-reconciled={String(data.isReconciled)}
          >
            {data.isReconciled
              ? t("inventory.valuation.reconciled")
              : t("inventory.valuation.notReconciled")}
          </span>
        ) : null}
      </div>

      {!asOfValid ? (
        <p className="field-error" role="alert" data-testid="valuation-as-of-error">
          {t("inventory.valuation.asOfBad")}
        </p>
      ) : null}

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? <ProblemPanel error={result.error} onRetry={reload} /> : null}

      {data ? (
        <>
          <div className="stats-row" data-testid="valuation-stats">
            <StatCard
              label={t("inventory.valuation.routeSubledger")}
              amount={data.subledgerTotal}
              testId="route-subledger"
            />
            <StatCard
              label={t("inventory.valuation.routeBalance")}
              amount={data.balanceTotal}
              testId="route-balance"
            />
            <StatCard
              label={t("inventory.valuation.routeControl")}
              amount={data.controlTotal}
              testId="route-control"
            />
            <StatCard
              label={t("inventory.valuation.divergence")}
              amount={data.divergence}
              tone={data.isReconciled ? "good" : "bad"}
              hint={
                data.isReconciled
                  ? t("inventory.valuation.reconciled")
                  : t("inventory.valuation.notReconciled")
              }
              testId="route-divergence"
            />
          </div>

          <Panel
            title={t("inventory.valuation.routesTitle")}
            note={t("inventory.valuation.routesNote")}
            aside={<span className="pill mono">{data.asOf}</span>}
            testId="valuation-routes"
          >
            <div className="stack">
              {(
                [
                  ["inventory.valuation.routeSubledger", data.subledgerTotal],
                  ["inventory.valuation.routeBalance", data.balanceTotal],
                  ["inventory.valuation.routeControl", data.controlTotal],
                ] as const
              ).map(([key, amount], index) => (
                <div className="inv-route cine-reveal" key={key} style={revealAt(index)}>
                  <span className="inv-route__k">{t(key)}</span>
                  <span className="inv-route__v">
                    <Amount value={amount} />
                  </span>
                </div>
              ))}
            </div>
            <p
              className={data.isReconciled ? "alert alert--success" : "alert alert--danger cine-refuse"}
              role="status"
              data-testid="reconciled-note"
            >
              {data.isReconciled
                ? t("inventory.valuation.reconciledBody")
                : t("inventory.valuation.notReconciledBody")}
            </p>
          </Panel>

          {data.divergences.length === 0 ? (
            <EmptyState
              small
              title={t("inventory.valuation.emptyTitle")}
              body={t("inventory.valuation.emptyBody")}
              testId="valuation-no-divergences"
            />
          ) : (
            <Panel
              title={t("inventory.valuation.notReconciled")}
              note={tp("inventory.valuation.divergenceCount", data.divergences.length)}
              testId="valuation-divergences"
            >
              <div className="ledger" data-state="ready" data-testid="divergence-table">
                <table>
                  <caption className="visually-hidden">
                    {t("inventory.valuation.notReconciled")}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("inventory.valuation.colDocType")}</th>
                      <th scope="col">{t("inventory.valuation.colDocId")}</th>
                      <th scope="col">{t("inventory.valuation.colItem")}</th>
                      <th scope="col">{t("inventory.valuation.colReason")}</th>
                      <th scope="col" className="n">{t("inventory.valuation.colSubledger")}</th>
                      <th scope="col" className="n">{t("inventory.valuation.colControl")}</th>
                      <th scope="col" className="n">{t("inventory.valuation.colDivergence")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.divergences.map((row) => (
                      <tr
                        key={row.documentType + "|" + row.documentId + "|" + row.itemId}
                        data-testid="divergence-row"
                      >
                        <td className="code">{row.documentType}</td>
                        <td className="code">{row.documentId}</td>
                        <td className="code">{row.itemId}</td>
                        <td data-reason={row.reasonCode}>{t(REASON_LABEL[row.reasonCode])}</td>
                        <td className="n"><Amount value={row.subledgerEffect} /></td>
                        <td className="n"><Amount value={row.controlEffect} /></td>
                        <td className="n"><Amount value={row.divergence} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="muted">{t("inventory.valuation.closeNote")}</p>
            </Panel>
          )}
        </>
      ) : null}
    </section>
  );
}
