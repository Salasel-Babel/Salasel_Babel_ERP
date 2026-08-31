/* ═══════════════════════════════════════════════════════════════════════════
   ما اختاره المستخدم في المقاولات — مخزنٌ واحد تقرؤه الشاشات الأربع
   The contracting selection — one store the four screens read
   ───────────────────────────────────────────────────────────────────────────
   السجلّ يختار عقداً، والمستخلص يُبنى على ذلك العقد نفسه، والمحتجزات تُقرأ
   تحته. ولو حملت كل شاشةٍ اختيارها وحدها لكان على المحاسب أن يختار العقد
   ثلاث مرّات — ولو **اشتقّت** الشاشة الثانية العقد من عندها لانحرفت عن
   الأولى عند أول تعديل.

   ولا معرّف هنا يُكتب بيد ولا يُخترَع: كل قيمة في هذا المخزن جاءت من جوابٍ
   للخادم (listProjects أو readSubcontract) أو من حقلٍ ألصقه المستخدم ثم
   قُرئ من الخادم فعلاً.

   **وما لا يفعله هذا المخزن:** لا يحفظ في المتصفّح ولا يدخل الرابط. فإعادةُ
   تحميل الصفحة تُفرّغه، والشاشة تعود إلى حالة «لم يُختَر عقد» المصمَّمة —
   لا إلى شاشةٍ تدّعي عقداً لا تملكه. (ودَينٌ مُعلَن: الرابط العميق يحتاج
   وسائط بحثٍ على المسار، وهي إضافةٌ لاحقة لا تغيير سلوك.)
   ═══════════════════════════════════════════════════════════════════════════ */
import { useSyncExternalStore } from "react";

/** ما اختير، بمعرّفاته كما ردّها الخادم. */
export interface ContractingSelection {
  /** معرّف المشروع. */
  readonly projectId: string;
  /** رمز المشروع — وهو ما يدخل بُعد القيد، ويُعرض ولا يُرسَل. */
  readonly projectCode: string;
  /** معرّف عقد العميل. */
  readonly contractId: string;
  /** رقم العقد كما يقرؤه المحاسب. */
  readonly contractNumber: string;
  /** معرّف عقد الباطن. */
  readonly subcontractId: string;
  /** رقم عقد الباطن. */
  readonly subcontractNumber: string;
}

const EMPTY: ContractingSelection = {
  projectId: "",
  projectCode: "",
  contractId: "",
  contractNumber: "",
  subcontractId: "",
  subcontractNumber: "",
};

let current: ContractingSelection = EMPTY;
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

/**
 * يبدّل ما اختير. الحقول المذكورة وحدها تتغيّر.
 * @param patch ما تغيّر.
 */
export function selectContracting(patch: Partial<ContractingSelection>): void {
  const next = { ...current, ...patch };
  const same = (Object.keys(next) as (keyof ContractingSelection)[]).every(
    (key) => next[key] === current[key]
  );
  if (same) return;
  current = next;
  emit();
}

/** يُفرّغ الاختيار — تُستعمله الاختبارات كي لا تتسرّب حالةٌ بين حالتين. */
export function resetContractingSelection(): void {
  current = EMPTY;
  emit();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function snapshot(): ContractingSelection {
  return current;
}

/** ما اختير الآن. */
export function useContractingSelection(): ContractingSelection {
  return useSyncExternalStore(subscribe, snapshot, snapshot);
}
