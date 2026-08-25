/* بطاقة العقد: ما وُلِّد منه هذا العميل، وبأي بصمة. */
import type { ReactNode } from "react";
import { CONTRACT } from "../../api/generated/contract";
import { useT } from "../../i18n/react";

/** شاشة العقد المنشور. */
export function ContractScreen(): ReactNode {
  const { t, tp } = useT();
  return (
    <section className="stack" data-testid="contract-screen">
      <h1 style={{ margin: 0, fontSize: "var(--font-size-h1)", fontFamily: "var(--font-display)" }}>
        {t("screen.contract.title")}
      </h1>
      <p className="muted">{t("screen.contract.sub")}</p>

      <div className="card">
        <dl className="problem-dl" style={{ display: "grid", gridTemplateColumns: "auto minmax(0,1fr)", gap: "var(--space-6) var(--space-12)", margin: 0 }}>
          <dt className="muted">{t("screen.contract.version")}</dt>
          <dd className="mono" style={{ margin: 0 }} data-testid="contract-version">
            {CONTRACT.version} · OpenAPI {CONTRACT.openapi}
          </dd>
          <dt className="muted">{t("screen.contract.digest")}</dt>
          <dd className="mono" style={{ margin: 0 }} data-testid="contract-sha">
            {CONTRACT.sourceSha256}
          </dd>
        </dl>
      </div>

      <div className="statline">
        <span data-testid="contract-operations">
          {tp("screen.contract.operations", CONTRACT.operationCount)}
        </span>
        <span data-testid="contract-schemas">
          {tp("screen.contract.schemas", CONTRACT.schemaCount)}
        </span>
      </div>

      <div className="tablewrap">
        <table className="tb">
          <thead>
            <tr>
              <th scope="col" className="start">{t("common.label.type")}</th>
              <th scope="col" className="start">{t("acct.columns.action")}</th>
              <th scope="col" className="start">{t("field.reference.label")}</th>
            </tr>
          </thead>
          <tbody>
            {CONTRACT.operations.map((op) => (
              <tr key={op.id}>
                <td className="start mono">{op.method}</td>
                <td className="start mono">{op.id}</td>
                <td className="start mono">{op.path}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="muted">{t("screen.contract.note")}</p>
      <p className="muted">{t("screen.contract.moneyNote")}</p>
    </section>
  );
}
