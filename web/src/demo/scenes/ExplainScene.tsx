/* ═══════════════════════════════════════════════════════════════════════════
   المشهد الثالث — «فسِّر هذا الرقم».
   ليست إجابة نموذج بل **تفكيكٌ**: الرقم ← سطوره ← قيده ← مستنده ← حلقته في
   السلسلة. ولذلك هو صحيح دائماً: لا شيء فيه مُستنتَج، كلّه مقروء.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, type ReactNode } from "react";
import { Amount, Num } from "../../i18n/react";
import { documentsById, money, snapshot, toScaled, wire, type SnapEntry } from "../data";
import { bagOf, useDemo } from "../useDemo";

const TARGET = "1301";

const TRAIL = [
  "الرقم في ميزان المراجعة",
  "السطور المكوِّنة له",
  "القيد الذي أنتج السطر",
  "مستند المصدر",
  "حلقته في سلسلة البصمات",
] as const;

/** المشهد. */
export function ExplainScene(): ReactNode {
  const state = useDemo();
  const step = Math.min(Math.max(bagOf<number>(state, "explainStep") ?? 0, 0), 4);
  const focusEntryNo = bagOf<number>(state, "focusEntry") ?? 1;

  const totals = useMemo(() => {
    const map = new Map<string, { debit: bigint; credit: bigint; nameAr: string }>();
    for (const account of snapshot.accounts) {
      map.set(account.accountCode, { debit: 0n, credit: 0n, nameAr: account.nameAr });
    }
    for (const entry of snapshot.entries) {
      for (const line of entry.lines) {
        const row = map.get(line.accountCode);
        if (!row) continue;
        row.debit += toScaled(line.debit);
        row.credit += toScaled(line.credit);
      }
    }
    return map;
  }, []);

  const contributors = useMemo(() => {
    const out: { entry: SnapEntry; amount: bigint }[] = [];
    for (const entry of snapshot.entries) {
      for (const line of entry.lines) {
        if (line.accountCode !== TARGET) continue;
        const amount = toScaled(line.debit);
        if (amount > 0n) out.push({ entry, amount });
      }
    }
    return out;
  }, []);

  const entry = snapshot.entries.find((e) => e.entryNo === focusEntryNo) ?? contributors[0]!.entry;
  const document = documentsById.get(entry.sourceDocId) ?? null;
  const target = totals.get(TARGET)!;

  return (
    <div style={{ display: "flex", flexDirection: "column", minHeight: 0, flex: "1 1 auto" }}>
      <div className="demo-trail">
        {TRAIL.map((label, index) => (
          <span key={label} style={{ display: "contents" }}>
            {index > 0 ? <span className="demo-trail__sep">←</span> : null}
            <span className="demo-trail__step" data-on={index <= step ? "1" : "0"}>
              {label}
            </span>
          </span>
        ))}
      </div>

      <div className="demo-grid demo-grid--2" key={step}>
        <section className="demo-panel demo-fade">
          {step === 0 ? (
            <>
              <h3 className="demo-panel__head">
                ميزان المراجعة — <strong>الشركة كاملةً</strong>
              </h3>
              <div className="demo-panel__body">
                <table className="demo-table">
                  <thead>
                    <tr>
                      <th>الحساب</th>
                      <th>مدين</th>
                      <th>دائن</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...totals.entries()].map(([code, row]) => (
                      <tr key={code} className={code === TARGET ? "demo-row-hit" : ""}>
                        <td>
                          <span className="demo-code">{code}</span> {row.nameAr}
                        </td>
                        <td className={row.debit === 0n ? "demo-zero" : "demo-debit"}>
                          <Amount value={money(row.debit)} />
                        </td>
                        <td className={row.credit === 0n ? "demo-zero" : "demo-credit"}>
                          <Amount value={money(row.credit)} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}

          {step === 1 ? (
            <>
              <h3 className="demo-panel__head">
                السطور المدينة على <strong>{TARGET}</strong>
                <span style={{ marginInlineStart: "auto" }} className="demo-code">
                  {contributors.length} سطراً
                </span>
              </h3>
              <div className="demo-panel__body" style={{ overflow: "hidden" }}>
                <table className="demo-table" style={{ fontSize: 17 }}>
                  <thead>
                    <tr>
                      <th>القيد</th>
                      <th>التاريخ</th>
                      <th>المستند</th>
                      <th>مدين</th>
                    </tr>
                  </thead>
                  <tbody>
                    {contributors.slice(0, 13).map((c) => (
                      <tr key={c.entry.entryNo} className={c.entry.entryNo === focusEntryNo ? "demo-row-hit" : ""}>
                        <td className="demo-code">#{c.entry.entryNo}</td>
                        <td className="demo-code">{c.entry.entryDate}</td>
                        <td>{c.entry.memoAr}</td>
                        <td className="demo-debit">
                          <Amount value={money(c.amount)} />
                        </td>
                      </tr>
                    ))}
                    <tr>
                      <td colSpan={4} className="demo-code demo-zero">
                        … و{contributors.length - 13} سطراً آخر
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </>
          ) : null}

          {step >= 2 ? (
            <>
              <h3 className="demo-panel__head">
                القيد <strong>#<Num value={entry.entryNo} /></strong>
                <span className="demo-code">{entry.eventCode}</span>
              </h3>
              <div className="demo-panel__body">
                <table className="demo-table">
                  <thead>
                    <tr>
                      <th>الحساب</th>
                      <th>الدور</th>
                      <th>مدين</th>
                      <th>دائن</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entry.lines.map((line) => (
                      <tr key={line.lineNo} className={line.accountCode === TARGET ? "demo-row-hit" : ""}>
                        <td>
                          <span className="demo-code">{line.accountCode}</span> {line.accountName}
                        </td>
                        <td className="demo-code">{line.roleCode}</td>
                        <td className={line.debit === "0.0000" ? "demo-zero" : "demo-debit"}>
                          <Amount value={wire(line.debit)} />
                        </td>
                        <td className={line.credit === "0.0000" ? "demo-zero" : "demo-credit"}>
                          <Amount value={wire(line.credit)} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <p className="demo-note">
                  مصدر القيد: <span className="demo-code">{entry.sourceModule}</span> ·{" "}
                  <span className="demo-code">{entry.sourceDocType}</span> · مُشغّله{" "}
                  <span className="demo-code">{entry.triggerCode}</span>
                </p>
              </div>
            </>
          ) : null}
        </section>

        <section className="demo-panel demo-fade">
          {step === 0 ? (
            <>
              <h3 className="demo-panel__head">الرقم المسؤول عنه</h3>
              <div className="demo-panel__body">
                <div className="demo-stat" style={{ padding: "26px 28px" }}>
                  <div className="demo-stat__k">{target.nameAr} — إجمالي المدين</div>
                  <div className="demo-stat__v" style={{ fontSize: 54 }}>
                    <Amount value={money(target.debit)} />
                  </div>
                </div>
                <p className="demo-note" style={{ fontSize: 20 }}>
                  السؤال الذي يقتل الثقة في أي نظام محاسبي: <strong>«من أين جاء هذا الرقم؟»</strong>
                  <br />
                  والجواب هنا ليس تفسيراً بل مساراً: خمس خطوات نزولاً حتى المستند الذي وقّعه إنسان.
                </p>
              </div>
            </>
          ) : null}

          {step === 1 ? (
            <>
              <h3 className="demo-panel__head">التفكيك</h3>
              <div className="demo-panel__body">
                <div className="demo-stat" style={{ padding: "22px 26px" }}>
                  <div className="demo-stat__k">مجموع السطور المدينة</div>
                  <div className="demo-stat__v" style={{ fontSize: 44 }}>
                    <Amount value={money(contributors.reduce((a, c) => a + c.amount, 0n))} />
                  </div>
                </div>
                <p className="demo-note" style={{ fontSize: 20 }}>
                  الرقم في الميزان ليس محفوظاً في خانة: هو ناتج <span className="demo-code">sum()</span>{" "}
                  على السطور غير القابلة للتعديل، يُحسب في PostgreSQL لا في المتصفّح. ولذلك لا
                  يمكن أن ينحرف عن مكوّناته.
                </p>
              </div>
            </>
          ) : null}

          {step === 2 ? (
            <>
              <h3 className="demo-panel__head">من أين جاء القيد</h3>
              <div className="demo-panel__body">
                <ul className="demo-list">
                  <li>
                    المستند: <strong>{document?.number ?? entry.sourceDocId}</strong>
                  </li>
                  <li>
                    الحدث: <span className="demo-code">{entry.eventCode}</span>
                  </li>
                  <li>
                    لحظة الترحيل: <span className="demo-code">{entry.postedAt}</span>
                  </li>
                  <li>
                    الفترة: <span className="demo-code">{entry.periodCode}</span> · الدفتر{" "}
                    <span className="demo-code">MAIN</span>
                  </li>
                </ul>
                <p className="demo-note" style={{ fontSize: 20 }}>
                  لم يكتب أحدٌ هذا القيد بيده. كتبَه <strong>محرّك الترحيل</strong> من مصفوفة
                  (نوع المستند × الحدث) — وهو الكاتب الوحيد في الدفتر.
                </p>
              </div>
            </>
          ) : null}

          {step === 3 && document ? (
            <>
              <h3 className="demo-panel__head">
                مستند المصدر — <strong>{document.number}</strong>
              </h3>
              <div className="demo-panel__body">
                <div className="demo-doc">
                  <h4>فاتورة مبيعات {document.number}</h4>
                  <div className="demo-doc__meta">
                    <span>العميل: {document.partyNameAr}</span>
                    <span>الرمز: {document.partyCode}</span>
                    <span>الإصدار: {document.issuedOn}</span>
                    <span>الاستحقاق: {document.dueOn}</span>
                    <span>الحالة: {document.state}</span>
                  </div>
                  <table>
                    <thead>
                      <tr>
                        <th>#</th>
                        <th>البيان</th>
                        <th>الكمية</th>
                        <th>سعر الوحدة</th>
                        <th>الصافي</th>
                        <th>الضريبة</th>
                      </tr>
                    </thead>
                    <tbody>
                      {document.lines.map((line) => (
                        <tr key={line.lineNo}>
                          <td>{line.lineNo}</td>
                          <td>{line.descriptionAr}</td>
                          <td className="num">{line.quantity}</td>
                          <td className="amt">
                            <Amount value={wire(line.unitPrice)} />
                          </td>
                          <td className="amt">
                            <Amount value={wire(line.lineNet)} />
                          </td>
                          <td className="amt">
                            <Amount value={wire(line.lineTax)} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="demo-doc__totals">
                    <span>
                      الصافي <b><Amount value={wire(document.netTotal)} /></b>
                    </span>
                    <span>
                      الضريبة <b><Amount value={wire(document.taxTotal)} /></b>
                    </span>
                    <span>
                      الإجمالي <b><Amount value={wire(document.grossTotal)} /></b>
                    </span>
                  </div>
                </div>
              </div>
            </>
          ) : null}

          {step === 4 ? (
            <>
              <h3 className="demo-panel__head">حلقة القيد في سلسلة البصمات</h3>
              <div className="demo-panel__body">
                <table className="demo-table" style={{ fontSize: 17 }}>
                  <tbody>
                    <tr>
                      <th>التسلسل</th>
                      <td className="demo-code">{entry.chainSeq}</td>
                    </tr>
                    <tr>
                      <th>إصدار الشكل القانوني</th>
                      <td className="demo-code">{entry.canonVersion}</td>
                    </tr>
                    <tr>
                      <th>بصمة ما قبله</th>
                      <td className="demo-code" style={{ wordBreak: "break-all", direction: "ltr" }}>
                        {entry.prevHash}
                      </td>
                    </tr>
                    <tr>
                      <th>بصمة هذا القيد</th>
                      <td className="demo-code" style={{ wordBreak: "break-all", direction: "ltr", color: "var(--stage-good)" }}>
                        {entry.entryHash}
                      </td>
                    </tr>
                  </tbody>
                </table>
                <p className="demo-note" style={{ fontSize: 20 }}>
                  خمس خطوات من رقمٍ في تقرير إلى بصمةِ ٣٢ بايت تُثبت أن هذا القيد بعينه لم
                  يُمسّ. ولا خطوة منها استنتاج.
                </p>
              </div>
            </>
          ) : null}
        </section>
      </div>
    </div>
  );
}
