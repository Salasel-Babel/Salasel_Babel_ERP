/* ═══════════════════════════════════════════════════════════════════════════
   إعداد الاتصال بالخادم — ولا شيء منه مكتوب داخل مكوّن.
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة مصادر بأسبقية معلَنة: ?معامل في الرابط ← المحفوظ ← الافتراض.
   والاعتماد لا يُكتب في شيفرة ولا يُودَع في المستودع: يُلصَق في الشاشة
   ويُحفظ في المتصفّح وحده.
   ═══════════════════════════════════════════════════════════════════════════ */

/** ما يحتاجه العميل ليخاطب الخادم. */
export interface ApiConfig {
  /** أصل الخادم. الفراغ يعني الأصل نفسه (وسيط التطوير). */
  baseUrl: string;
  /** الاعتماد — Bearer. */
  token: string;
  /**
   * اعتمادُ التجديد — يدور كلَّ مرّة ويعيش أربعة عشر يوماً.
   * <p>
   * <b>ويُحفظ في مخزن المتصفّح مع أخيه، والمقايضةُ تُقال لا تُسكَت:</b> ما في
   * <code>localStorage</code> تبلغه أي شيفرةٍ تعمل في هذا الأصل، فثغرةُ XSS تبلغه.
   * والبديلُ ملفُّ ارتباطٍ <code>httpOnly</code> لا تبلغه الشيفرة — وهو يحتاج من
   * الخادم أن يضعه ويقرأه، وسطحُنا يحمل الاعتماد في ترويسة لا في ارتباط.
   * </p>
   * <p>
   * وما يخفّف الأثر قائمٌ اليوم: الاعتمادُ الفاعل يعيش خمس عشرة دقيقة، واعتمادُ
   * التجديد <b>يُستهلك عند أوّل استعمال</b> فيُكشَف المسروق حين يعود، والإبطال
   * فوريّ. فالنافذةُ قصيرة والسرقةُ تُكشَف — لا أنها مستحيلة.
   * </p>
   */
  refreshToken: string;
  /** لحظةُ انقضاء الاعتماد الفاعل بصيغة ISO — منها يُعرَف متى يُجدَّد قبل أن ينقضي. */
  tokenExpiresAt: string;
  /** معرّف الشركة. النطاق يُطابَق بالاعتماد على الخادم. */
  companyId: string;
  /** الدفتر. */
  book: string;
  /** رمز الفترة، أو الفراغ لكل الفترات. */
  period: string;
}

const KEY = "sb-api-config";

/** الافتراض: الأصل نفسه، ودفتر MAIN، وكل الفترات. */
export const DEFAULT_CONFIG: ApiConfig = {
  baseUrl: "",
  token: "",
  refreshToken: "",
  tokenExpiresAt: "",
  companyId: "",
  book: "MAIN",
  period: "",
};

function fromSearch(search: string): Partial<ApiConfig> {
  const q = new URLSearchParams(search);
  const out: Partial<ApiConfig> = {};
  /* **واعتمادُ التجديد لا يُقرأ من الرابط بقصد:** الروابط تُنسَخ وتُلصَق وتُسجَّل
     في سجلّات الوسطاء، واعتمادٌ عمرُه أربعة عشر يوماً في رابطٍ يعيش حيث يعيش الرابط.
     والفاعلُ يُقرأ منه لأنه يعيش ربعَ ساعة وهو طريقُ الاختبارات وطريقُ العرض. */
  for (const key of ["baseUrl", "token", "companyId", "book", "period"] as const) {
    const value = q.get(key);
    if (value !== null) out[key] = value;
  }
  return out;
}

function fromStorage(): Partial<ApiConfig> {
  try {
    const raw = globalThis.localStorage?.getItem(KEY);
    return raw ? (JSON.parse(raw) as Partial<ApiConfig>) : {};
  } catch {
    return {};
  }
}

/**
 * يجمع الإعداد بالأسبقية المعلَنة.
 * @param search نصّ الاستعلام.
 */
export function loadConfig(search: string): ApiConfig {
  return { ...DEFAULT_CONFIG, ...fromStorage(), ...fromSearch(search) };
}

/**
 * يحفظ الإعداد في المتصفّح وحده.
 * @param config الإعداد.
 */
export function saveConfig(config: ApiConfig): void {
  try {
    globalThis.localStorage?.setItem(KEY, JSON.stringify(config));
  } catch {
    /* التصفّح الخاص: الإعداد لا يُحفظ، والشاشة تعمل. */
  }
}
