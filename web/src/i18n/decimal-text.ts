/* ═══════════════════════════════════════════════════════════════════════════
   حساب نصّي على العشري — منقول من design/components/behaviors.js §٢
   Textual decimal handling — ported from design/components/behaviors.js §2
   ───────────────────────────────────────────────────────────────────────────
   المبلغ decimal لا float. الخادم يرسله نصّاً بمقياس ثابت، والواجهة تُقرِّب
   للعرض وتضيف الفواصل. لا parseFloat، ولا toLocaleString، ولا Intl.
   ═══════════════════════════════════════════════════════════════════════════ */

/* تقريب نصّي (نصف بعيداً عن الصفر) بلا أي عملية عائمة. */
function roundDigits(intPart: string, fracPart: string, scale: number): [string, string] {
  if (fracPart.length <= scale) {
    return [intPart, fracPart.padEnd(scale, "0")];
  }
  const keep = fracPart.slice(0, scale);
  const next = fracPart.charCodeAt(scale) - 48;
  if (next < 5) return [intPart, keep];
  const digits = (intPart + keep).split("");
  let i = digits.length - 1;
  while (i >= 0) {
    if (digits[i] === "9") {
      digits[i] = "0";
      i--;
    } else {
      digits[i] = String(Number(digits[i]) + 1);
      break;
    }
  }
  if (i < 0) digits.unshift("1");
  const all = digits.join("");
  const cut = all.length - scale;
  return [all.slice(0, cut) || "0", all.slice(cut)];
}

/* تحويل الأرقام العربية-الهندية والفارسية والديفاناغرية إلى لاتينية — عند الحدّ فقط.
   ⚠ الديفاناغري (U+0966) كان ناقصاً في النموذج المعتمد: لصقٌ من لوحة مفاتيح
   هندية كان يمرّ كما هو فيُرفض المبلغ أو — أسوأ — يُقرأ خطأً. */
const DIGIT_BASES = [0x0660, 0x06f0, 0x0966];

/**
 * يطبّع كل أشكال الأرقام إلى اللاتينية.
 * @param value النصّ.
 */
export function toLatinDigits(value: string): string {
  return String(value).replace(/[\u0660-\u0669\u06F0-\u06F9\u0966-\u096F]/g, (d) => {
    const c = d.charCodeAt(0);
    for (const base of DIGIT_BASES) {
      if (c >= base && c <= base + 9) return String(c - base);
    }
    return d;
  });
}

function group3(intPart: string): string {
  let out = "";
  let n = 0;
  for (let i = intPart.length - 1; i >= 0; i--) {
    out = intPart[i] + out;
    if (++n % 3 === 0 && i > 0) out = "," + out;
  }
  return out;
}

/**
 * الشكل القانوني للعرض: تقريب نصّي ثم تجميع بفاصلة إنجليزية.
 * "10000.5" → "10,000.50"  ·  "-3.005" → "-3.01"
 * @param value القيمة نصّاً.
 * @param scale عدد الخانات العشرية.
 */
export function moneyText(value: string | null | undefined, scale = 2): string | null {
  if (value === null || value === undefined) return "";
  let s = String(value).trim();
  if (s === "" || s === "-" || s === "–") return "";
  s = s.replace(/[\u066B\u066C]/g, (c) => (c === "\u066B" ? "." : ","));
  s = toLatinDigits(s).replace(/[,\s\u00A0\u202F\u066C]/g, "");
  let neg = false;
  if (s.charAt(0) === "-") {
    neg = true;
    s = s.slice(1);
  } else if (s.charAt(0) === "+") {
    s = s.slice(1);
  }
  if (!/^\d*(\.\d*)?$/.test(s) || s === "" || s === ".") return null;
  const parts = s.split(".");
  const ip = (parts[0] || "0").replace(/^0+(?=\d)/, "") || "0";
  const fp = parts[1] || "";
  const r = roundDigits(ip, fp, scale);
  let text = group3(r[0]) + (scale > 0 ? "." + r[1] : "");
  if (neg && /[1-9]/.test(r[0] + r[1])) text = "-" + text;
  return text;
}
