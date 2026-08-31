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
  companyId: "",
  book: "MAIN",
  period: "",
};

function fromSearch(search: string): Partial<ApiConfig> {
  const q = new URLSearchParams(search);
  const out: Partial<ApiConfig> = {};
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
