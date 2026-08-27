/* ═══════════════════════════════════════════════════════════════════════════
   المشهد الخامس — رمز الفاتورة الإلكترونية.
   الحمولة أدناه هي **المتّجه الذهبي المُودَع** في `tests/golden/zatca-vectors.v1.json`،
   وما يظهر من حقول ومن رفضٍ هو مُخرَج `ZatcaQrReader` **المشحون** حرفياً — يُشغّله
   سكربت التسجيل بـ`dotnet run demo/showcase/read-qr.cs`. لا نسخة ثانية من المُفكِّك
   في المتصفّح.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { bagOf, useDemo } from "../useDemo";

/** ناتج القارئ كما يصل من السكربت. */
export interface QrResult {
  readonly refused: boolean;
  readonly reason?: string;
  readonly sellerName?: string;
  readonly sellerVatNumber?: string;
  readonly issuedAt?: string;
  readonly grossTotal?: string;
  readonly taxTotal?: string;
  readonly phase?: string;
  readonly attested?: boolean;
  readonly tags?: readonly { tag: number; bytes: number }[];
}

const FIELD_LABEL: Record<string, string> = {
  sellerName: "اسم البائع",
  sellerVatNumber: "الرقم الضريبي",
  issuedAt: "لحظة الإصدار",
  grossTotal: "الإجمالي شامل الضريبة",
  taxTotal: "مبلغ الضريبة",
};

/** المشهد. */
export function QrScene(): ReactNode {
  const state = useDemo();
  const payload = bagOf<string>(state, "qrPayload") ?? "";
  const label = bagOf<string>(state, "qrLabel") ?? "";
  const result = bagOf<QrResult>(state, "qrResult");

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel">
        <h3 className="demo-panel__head">
          الحمولة كما مُسحت — <strong>{label}</strong>
        </h3>
        <div className="demo-panel__body">
          <div
            className="demo-code"
            style={{
              direction: "ltr",
              wordBreak: "break-all",
              background: "#070d18",
              border: "1px solid var(--stage-line)",
              borderRadius: 12,
              padding: "14px 16px",
              fontSize: 15,
              lineHeight: 1.7,
              maxHeight: 168,
              overflow: "hidden",
            }}
          >
            {payload}
          </div>

          {result && !result.refused && result.tags ? (
            <>
              <h4 style={{ margin: "20px 0 8px", fontSize: 19, color: "var(--stage-text-2)" }}>
                أطوال الوسوم — <strong style={{ color: "var(--stage-text)" }}>بالبايت لا بالمحرف</strong>
              </h4>
              <table className="demo-table" style={{ fontSize: 17 }}>
                <thead>
                  <tr>
                    <th>الوسم</th>
                    <th>الطول بالبايت</th>
                    <th>المعنى</th>
                  </tr>
                </thead>
                <tbody>
                  {result.tags.map((tag) => (
                    <tr key={tag.tag}>
                      <td className="demo-code">{tag.tag}</td>
                      <td className="demo-code" style={{ color: "var(--stage-brand)" }}>
                        {tag.bytes}
                      </td>
                      <td>
                        {tag.tag === 1
                          ? "اسم البائع — ٢٥ حرفاً عربياً تكلّف ٤٧ بايتاً"
                          : tag.tag === 2
                            ? "الرقم الضريبي"
                            : tag.tag === 3
                              ? "الطابع الزمني"
                              : tag.tag === 4
                                ? "الإجمالي"
                                : "الضريبة"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="demo-note">
                قارئٌ يعدّ <strong>المحارف</strong> بدل البايتات ينزلق داخل الوسم التالي فيقرأ رقماً
                ضريبياً مقطوعاً وتاريخاً معقولاً — <strong>ولا يشتكي</strong>. وهذا العطل مُسجَّل
                باسمه في سجلّ المصائد، وله اختبار يحرسه.
              </p>
            </>
          ) : null}
        </div>
      </section>

      <section className="demo-panel">
        <h3 className="demo-panel__head">
          ما قاله القارئ المشحون — <span className="demo-code">ZatcaQrReader</span>
        </h3>
        <div className="demo-panel__body">
          {result && !result.refused ? (
            <>
              <table className="demo-table" style={{ fontSize: 20 }}>
                <tbody>
                  {(["sellerName", "sellerVatNumber", "issuedAt", "grossTotal", "taxTotal"] as const).map((key) => (
                    <tr key={key}>
                      <th style={{ width: "40%" }}>{FIELD_LABEL[key]}</th>
                      <td style={{ fontWeight: 600 }}>{result[key]}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div className="demo-verdict" data-tone={result.attested ? "good" : "warn"}>
                <div className="demo-verdict__code">
                  {result.phase} · {result.attested ? "مُصدَّق تشفيرياً" : "بلا مادة تشفيرية"}
                </div>
                <div className="demo-verdict__why">
                  {result.attested
                    ? "الحقول أعلاه موقّعة من المُصدِر — تصل مُصدَّقة لا مقروءة ضوئياً."
                    : "رمز المرحلة الأولى لا يحمل بصمةً ولا توقيعاً، فالقارئ يقول ذلك بصراحة بدل أن يُوهم بالتصديق. والنظام الذي يعلن حدود دليله أصدق من الذي يخفيها."}
                </div>
              </div>
            </>
          ) : null}

          {result?.refused ? (
            <div className="demo-verdict" data-tone="bad">
              <div className="demo-verdict__code">مرفوض</div>
              <div className="demo-verdict__why" style={{ fontSize: 19 }}>
                {result.reason}
              </div>
            </div>
          ) : null}

          <p className="demo-note" style={{ fontSize: 19 }}>
            الرفض هنا ليس رسالة عامة: يسمّي الوسم، والطول المُعلَن، والموضع المتوقّع. ونظامٌ
            يرفض بصوت مسموع أنفع من نظامٍ «يعمل» ويعيد حقلاً معقولاً وخاطئاً.
          </p>
        </div>
      </section>
    </div>
  );
}
