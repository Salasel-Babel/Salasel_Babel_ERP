/* ═══════════════════════════════════════════════════════════════════════════
   المشهد الأول — كشف العبث. **كلّ ما يُعرَض هنا حقيقي**:
   الأوامر تُنفَّذ فعلاً على PostgreSQL، والأحكام تعود من الخادم عبر HTTP،
   وسكربت التسجيل يحقن مُخرَجها الحرفي. لا سطر مُخرَج مكتوب بيد في المستودع.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Amount, Num } from "../../i18n/react";
import { snapshot, wire } from "../data";
import { bagOf, useDemo } from "../useDemo";

/** سطر في الطرفية كما يحقنه السكربت. */
export interface TermLine {
  readonly kind: "cmd" | "sql" | "out" | "err" | "ok" | "note";
  readonly text: string;
}

/** حكم الميزان كما عاد من الخادم. */
interface BalanceVerdict {
  readonly balanced: boolean;
  readonly totalDebit: string;
  readonly totalCredit: string;
}

/** حكم السلسلة كما عاد من الخادم. */
interface ChainVerdict {
  readonly ok: boolean;
  readonly verdict: string;
  readonly checked: number;
  readonly firstDivergentSequence: string | null;
  readonly reasonAr: string;
}

/** المشهد. */
export function TamperScene(): ReactNode {
  const state = useDemo();
  const entryNo = bagOf<number>(state, "entryNo") ?? 1;
  const entry = snapshot.entries.find((e) => e.entryNo === entryNo) ?? snapshot.entries[0]!;
  const term = bagOf<readonly TermLine[]>(state, "term") ?? [];
  const balance = bagOf<BalanceVerdict>(state, "balanceVerdict");
  const chain = bagOf<ChainVerdict>(state, "chainVerdict");
  const altered = bagOf<readonly number[]>(state, "alteredLines") ?? [];
  const overrides = bagOf<Readonly<Record<string, string>>>(state, "lineOverrides") ?? {};

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel">
        <h3 className="demo-panel__head">
          قيدٌ مُرحَّل — <strong>رقم <Num value={entry.entryNo} /></strong>
          <span className="demo-code">{entry.memoAr}</span>
          <span style={{ marginInlineStart: "auto" }} className="demo-code">
            {entry.entryDate} · سلسلة #{entry.chainSeq}
          </span>
        </h3>
        <div className="demo-panel__body">
          <table className="demo-table">
            <thead>
              <tr>
                <th>#</th>
                <th>الحساب</th>
                <th>الدور</th>
                <th>مركز التكلفة</th>
                <th>مدين</th>
                <th>دائن</th>
              </tr>
            </thead>
            <tbody>
              {entry.lines.map((line) => {
                const hit = altered.includes(line.lineNo);
                const debit = overrides["d" + line.lineNo] ?? line.debit;
                const credit = overrides["c" + line.lineNo] ?? line.credit;
                const cc = overrides["cc" + line.lineNo] ?? line.costCenter ?? "—";
                return (
                  <tr key={line.lineNo} className={hit ? "demo-row-bad" : ""}>
                    <td className="demo-code">{line.lineNo}</td>
                    <td>
                      <span className="demo-code">{line.accountCode}</span> {line.accountName}
                    </td>
                    <td className="demo-code">{line.roleCode}</td>
                    <td className="demo-code">{cc}</td>
                    <td className="demo-debit">
                      <Amount value={wire(debit)} />
                    </td>
                    <td className="demo-credit">
                      <Amount value={wire(credit)} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {balance ? (
            <div className="demo-verdict" data-tone={balance.balanced ? "good" : "bad"}>
              <div className="demo-verdict__code">
                {balance.balanced ? "الميزان متوازن ✓" : "الميزان غير متوازن ✗"}
              </div>
              <div className="demo-verdict__why">
                مدين <Amount value={wire(balance.totalDebit)} /> = دائن{" "}
                <Amount value={wire(balance.totalCredit)} /> — الفحص التقليدي يمرّ.
              </div>
            </div>
          ) : null}

          {chain ? (
            <div className="demo-verdict" data-tone={chain.ok ? "good" : "bad"}>
              <div className="demo-verdict__code">
                {chain.verdict}
                {chain.firstDivergentSequence ? " · أول تسلسل منحرف: " + chain.firstDivergentSequence : ""}
              </div>
              <div className="demo-verdict__why">
                {chain.reasonAr} <span className="demo-code">({chain.checked} سجلاً فُحص)</span>
              </div>
            </div>
          ) : null}
        </div>
      </section>

      <section className="demo-panel demo-panel--flush">
        <div className="demo-term">
          {term.map((line, index) => (
            <div className="demo-term__line" data-kind={line.kind} key={index}>
              {line.text}
            </div>
          ))}
          <div className="demo-term__line" data-kind="out">
            <span className="demo-term__caret" />
          </div>
        </div>
      </section>
    </div>
  );
}
