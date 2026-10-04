/* ═══════════════════════════════════════════════════════════════════════════
   ثوابتُ الشركة في المتصفّح  ·  Company presets in the browser
   ───────────────────────────────────────────────────────────────────────────
   ما يُحسم مرّةً في «ثوابت الشركة» (/setup/presets) يُقرأ هنا مرّةً ويُوزَّع
   على شاشات الإدخال: الفرع، والتصنيف الضريبي ونسبته، وطريقة التسوية وخزينتها،
   والمستودع والموقع، وبادئات الترقيم. فالشاشة التي تملك ثابتاً **لا تسأل عنه**
   في الواجهة المبسّطة، وتبقى فيها المتغيّرات القليلة.

   **والرقم يُخصَّص من الخادم لا يُكتب بيد:** `allocateDocumentNumber` يعود
   بالرقم التالي في سلسلة المستند، والشاشة ترسله في المستند كما كانت دائماً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { allocateDocumentNumber, readCompanyPresets, readCompanySetup, readMemberships, readSession } from "../api/generated/client";
import type { CompanyPresets } from "../api/generated/types";
import { useApi } from "./api-context";
import { useShowAdvanced } from "./shell/simple-mode";

/** مفاتيح الثوابت التشغيلية — نسخةٌ من الكتالوج المغلق في الخادم، بالاسم لا بالسحر. */
export const PRESET = {
  branch: "branch.default",
  taxClassification: "tax.classification",
  taxRate: "tax.rate",
  taxRecoverable: "tax.recoverable",
  expenseCategory: "expense.category",
  settlementMethod: "settlement.method",
  treasuryCash: "treasury.cash",
  treasuryBank: "treasury.bank",
  warehouse: "warehouse.default",
  location: "location.default",
  hrClass: "hr.class",
  hrSettlementMethod: "hr.settlement.method",
  hrTreasury: "hr.treasury",
} as const;

/** ما تراه الشاشة من الثوابت. */
export interface PresetView {
  /** قيمة ثابت، أو نصّ فارغ إن لم يُضبط. */
  readonly value: (key: string) => string;
  /** هل ضُبط هذا الثابت؟ */
  readonly has: (key: string) => boolean;
  /**
   * هل يُخفى حقلٌ يغطّيه هذا الثابت؟ يُخفى في الواجهة المبسّطة حين يكون الثابت
   * مضبوطاً — وفي «كل الشاشات» يبقى ظاهراً مملوءاً بقيمته.
   */
  readonly hides: (key: string) => boolean;
  /** ثوابت الخادم كما وصلت، أو `undefined` قبل الوصول. */
  readonly data: CompanyPresets | undefined;
  readonly isPending: boolean;
}

/**
 * يقرأ ثوابت الشركة مرّةً للجلسة ويعرضها للشاشات.
 */
export function usePresets(): PresetView {
  const { transport, config } = useApi();
  const showAdvanced = useShowAdvanced();
  const query = useQuery({
    queryKey: ["presets", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readCompanyPresets(transport, { companyId: config.companyId }, signal),
  });

  const values = new Map<string, string>();
  for (const pair of query.data?.values ?? []) values.set(pair.name, pair.value);

  const value = (key: string): string => values.get(key) ?? "";
  const has = (key: string): boolean => values.has(key);
  return {
    value,
    has,
    hides: (key) => !showAdvanced && values.has(key),
    data: query.data,
    isPending: query.isPending && config.companyId !== "",
  };
}

/**
 * يخصّص رقم مستند من سلسلته على تاريخه.
 * @returns دالةٌ تعود بالرقم المركّب.
 */
export function useAllocateNumber(): (series: string, on: string) => Promise<string> {
  const { transport, config } = useApi();
  return useCallback(
    async (series: string, on: string) => {
      const allocated = await allocateDocumentNumber(transport, { companyId: config.companyId, series, body: { on } });
      return allocated.number;
    },
    [config.companyId, transport]
  );
}

/** يحوّل مفتاح ثابت أو رمز سلسلة إلى اسم مفتاح ترجمة: `tax.rate` ← `taxRate`، `sales_invoice` ← `salesInvoice`. */
export function presetSlug(key: string): string {
  return key
    .split(/[._]/)
    .map((part, index) => (index === 0 ? part : part.charAt(0).toUpperCase() + part.slice(1)))
    .join("");
}

/** هل الواجهة مبسّطة؟ المبسّطة تُخفي كل حقلٍ له افتراض: التاريخ اليوم، والرقم من الخادم. */
export function useSimple(): boolean {
  return !useShowAdvanced();
}

/**
 * يملأ حقلاً فارغاً بقيمة ثابته حين تصل، ويقول هل يُخفى الحقل.
 * <p>
 * الملء مرّةً وعلى الفراغ وحده: ما كتبه المستخدم لا يُستبدل، وما مُلئ يُرسل كما لو كُتب.
 * والإخفاء في الواجهة المبسّطة حين يكون الثابت مضبوطاً — وفي «كل الشاشات» يبقى الحقل
 * ظاهراً مملوءاً للحالة الاستثنائية.
 * </p>
 * @returns هل يُخفى الحقل.
 */
export function usePresetFill(key: string, value: string, setValue: (next: string) => void): boolean {
  const presets = usePresets();
  const preset = presets.value(key);
  const hidden = presets.hides(key);
  useEffect(() => {
    /* حقلٌ مخفيّ يحمل ثابته حرفاً: لا قيمةَ أخرى تصل منه إلى السلك. */
    if (hidden && preset !== value) setValue(preset);
    else if (preset !== "" && value === "") setValue(preset);
    /* `setValue` ليس في التبعيات عمداً: الشاشات تمرّر دالةً جديدة كل رسم، والملء يتبع
       وصول الثابت وفراغ الحقل لا هويّة الدالة. */
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hidden, preset, value]);
  return hidden;
}

/**
 * رقم المستند: مخفيٌّ في الواجهة المبسّطة ويُخصَّص من الخادم عند الإرسال إن تُرك فارغاً.
 * @param series رمز سلسلة الترقيم.
 */
export function useDocumentNumber(series: string): {
  readonly hidden: boolean;
  readonly resolve: (current: string, on: string) => Promise<string>;
} {
  const allocate = useAllocateNumber();
  const hidden = useSimple();
  const resolve = useCallback(
    async (current: string, on: string) => (current.trim() !== "" ? current : allocate(series, on)),
    [allocate, series]
  );
  return { hidden, resolve };
}

/**
 * يملأ حقلاً فارغاً بقيمةٍ افتراضية من مصدرٍ آخر (مركز التكلفة الافتراضي، أو اسم
 * المستخدم الحالي)، بالدلالة نفسها التي لـ`usePresetFill`: الملء على الفراغ وحده،
 * والإخفاء في الواجهة المبسّطة حين تكون القيمة الافتراضية موجودة.
 * @returns هل يُخفى الحقل.
 */
export function useFillFrom(fallback: string, value: string, setValue: (next: string) => void): boolean {
  const simple = useSimple();
  const hidden = simple && fallback !== "";
  useEffect(() => {
    if (hidden && fallback !== value) setValue(fallback);
    else if (fallback !== "" && value === "") setValue(fallback);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hidden, fallback, value]);
  return hidden;
}

/** رمز مركز التكلفة الافتراضي من تأسيس المنشأة، أو نصّ فارغ قبل الوصول. */
export function useDefaultCostCenter(): string {
  const { transport, config } = useApi();
  const setup = useQuery({
    queryKey: ["setup", "company", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readCompanySetup(transport, { companyId: config.companyId }, signal),
  });
  return setup.data?.defaultCostCenter ?? "";
}

/** اسمُ العضو الحالي كما في سجلّ الأعضاء، أو نصّ فارغ قبل الوصول — لحقول «المعتمِد». */
export function useCurrentMemberName(): string {
  const { transport, config } = useApi();
  const session = useQuery({
    queryKey: ["session", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readSession(transport, signal),
  });
  const members = useQuery({
    queryKey: ["admin", "members", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readMemberships(transport, { companyId: config.companyId }, signal),
  });
  const me = session.data?.userId;
  return members.data?.members.find((member) => member.userId === me)?.displayNameAr ?? "";
}
