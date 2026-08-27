/* ربط المتجر بـReact — خطّاف واحد لا أكثر. */
import { useSyncExternalStore } from "react";
import { snapshotState, subscribe, type DemoState } from "./store";

/** الحالة الحالية للعرض. */
export function useDemo(): DemoState {
  return useSyncExternalStore(subscribe, snapshotState, snapshotState);
}

/**
 * يقرأ مفتاحاً من حقيبة البيانات المحقونة بنوعٍ مُدّعى.
 * @param state الحالة.
 * @param key المفتاح.
 */
export function bagOf<T>(state: DemoState, key: string): T | null {
  const value = state.bag[key];
  return value === undefined ? null : (value as T);
}
