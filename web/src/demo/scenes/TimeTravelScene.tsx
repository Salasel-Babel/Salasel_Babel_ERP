/* ═══════════════════════════════════════════════════════════════════════════
   المشهد الثاني — رحلة عبر الزمن.
   دفترٌ يُضاف إليه فقط **هو** سلسلة زمنية: لا حالة سابقة تُعاد كتابتها، فإعادة
   التشغيل تراكمٌ بسيط لا استرجاع نسخة احتياطية. ونظام دفتره قابل للتعديل لا
   يستطيع هذا بصدق، لأنه لا يعرف ما كان الرقم عليه في ذلك اليوم.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, type ReactNode } from "react";
import { Amount, Num } from "../../i18n/react";
import { calendarDays, money, replay, type ReplayState } from "../data";
import { bagOf, useDemo } from "../useDemo";

/** المشهد. */
export function TimeTravelScene(): ReactNode {
  const state = useDemo();
  const days = useMemo(() => calendarDays(), []);
  const states = useMemo(() => replay(days), [days]);
  const index = Math.min(Math.max(bagOf<number>(state, "dayIndex") ?? 0, 0), states.length - 1);
  const now: ReplayState = states[index]!;
  const progress = states.length <= 1 ? 1 : index / (states.length - 1);

  const peak = useMemo(() => {
    let max = 0n;
    for (const s of states) for (const row of s.rows) {
      if (row.debit > max) max = row.debit;
      if (row.credit > max) max = row.credit;
    }
    return max === 0n ? 1n : max;
  }, [states]);

  const width = (v: bigint): string => {
    /* نسبة عرض شريط — عرضٌ محض، لا حساب على مال: القسمة على أعداد صحيحة
       بمقياس ١٠٠٠٠ ثم تحويل النسبة المئوية وحدها إلى رقم. */
    const percent = Number((v * 1000n) / peak) / 10;
    return percent.toFixed(1) + "%";
  };

  const balancedDays = useMemo(() => states.filter((s) => s.balanced && s.entryCount > 0).length, [states]);
  const withEntries = useMemo(() => states.filter((s) => s.entryCount > 0).length, [states]);

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel">
        <h3 className="demo-panel__head">
          الدفتر كما كان في <strong>ذلك اليوم</strong>
          <span style={{ marginInlineStart: "auto" }} className="demo-code">
            {now.rows.length} حساباً بحركة
          </span>
        </h3>
        <div className="demo-panel__body">
          <table className="demo-table demo-table--dense">
            <thead>
              <tr>
                <th>الحساب</th>
                <th>مدين تراكمي</th>
                <th style={{ width: "22%" }}></th>
                <th>دائن تراكمي</th>
                <th style={{ width: "22%" }}></th>
              </tr>
            </thead>
            <tbody>
              {now.rows.map((row) => (
                <tr key={row.accountCode}>
                  <td>
                    <span className="demo-code">{row.accountCode}</span> {row.nameAr}
                  </td>
                  <td className={row.debit === 0n ? "demo-zero" : "demo-debit"}>
                    <Amount value={money(row.debit)} />
                  </td>
                  <td>
                    <div className="demo-bar" style={{ width: width(row.debit) }} />
                  </td>
                  <td className={row.credit === 0n ? "demo-zero" : "demo-credit"}>
                    <Amount value={money(row.credit)} />
                  </td>
                  <td>
                    <div className="demo-bar demo-bar--credit" style={{ width: width(row.credit) }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="demo-panel">
        <h3 className="demo-panel__head">المؤشّر الزمني</h3>
        <div className="demo-panel__body">
          <div className="demo-timeline">
            <div className="demo-day">{now.day}</div>
            <div className="demo-timeline__rail">
              <div className="demo-timeline__fill" style={{ width: (progress * 100).toFixed(2) + "%" }} />
            </div>
            <div className="demo-timeline__ticks">
              <span>{states[0]!.day}</span>
              <span>{states[states.length - 1]!.day}</span>
            </div>
          </div>

          <div className="demo-stats" style={{ marginTop: 22 }}>
            <div className="demo-stat">
              <div className="demo-stat__k">قيود حتى هذا اليوم</div>
              <div className="demo-stat__v">
                <Num value={now.entryCount} />
              </div>
            </div>
            <div className="demo-stat" data-tone={now.balanced ? "good" : "bad"}>
              <div className="demo-stat__k">حالة الميزان</div>
              <div className="demo-stat__v">{now.balanced ? "متوازن" : "منحرف"}</div>
            </div>
          </div>

          <div className="demo-stats" style={{ marginTop: 14 }}>
            <div className="demo-stat">
              <div className="demo-stat__k">مجموع المدين</div>
              <div className="demo-stat__v" style={{ fontSize: 26 }}>
                <Amount value={money(now.totalDebit)} />
              </div>
            </div>
            <div className="demo-stat">
              <div className="demo-stat__k">مجموع الدائن</div>
              <div className="demo-stat__v" style={{ fontSize: 26 }}>
                <Amount value={money(now.totalCredit)} />
              </div>
            </div>
          </div>

          <p className="demo-note">
            إعادة التشغيل تراكمٌ على سطور غير قابلة للتعديل — لا نسخة احتياطية تُستعاد، ولا
            جدول «تاريخ» يُكتب بجانب الدفتر. والميزان متوازن في{" "}
            <strong style={{ color: "var(--stage-good)" }}>
              <Num value={balancedDays} /> من <Num value={withEntries} />
            </strong>{" "}
            يوماً فيه حركة — وهو فحصٌ يُجرى على كل يوم لا على اليوم الأخير وحده.
          </p>
        </div>
      </section>
    </div>
  );
}
