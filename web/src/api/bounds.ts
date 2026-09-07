import { SCHEMAS } from "./generated/runtime-schema";

/**
 * حدودُ النصّ المكتوب **من العقد لا من الشاشة**.
 *
 * كانت شاشتان تكتبان `const MINIMUM_REASON = 8;` بيدهما — نسخةً ثالثة من رقم الخادم
 * تفترق عنه يوم يتغيّر. والآن يُعلن العقد `minLength` ويحمله المولّد إلى `SCHEMAS`،
 * وهذه الدالّة تقرؤه. **ولا ارتدادَ رقمياً هنا عمداً**: عقدٌ بلا حدٍّ مُعلَن خطأُ توليدٍ
 * يُقال عند التحميل، لا ثمانيةٌ تُخترَع (ADR-0091).
 */
export function minLengthOf(schema: string, field: string): number {
  const shape = SCHEMAS[schema]?.fields[field];
  if (shape === undefined || shape.mn === undefined) {
    throw new Error(
      `العقد لا يُعلن minLength للحقل ${schema}.${field} — أعِد توليد العميل أو أعلن الحدّ في المُصدِر. / ` +
        `The contract declares no minLength for ${schema}.${field}; regenerate the client or declare the bound in the emitter.`
    );
  }
  return shape.mn;
}

/** أقصى طول النصّ كما يعلنه العقد — بالضمانة نفسها. */
export function maxLengthOf(schema: string, field: string): number {
  const shape = SCHEMAS[schema]?.fields[field];
  if (shape === undefined || shape.mx === undefined) {
    throw new Error(
      `العقد لا يُعلن maxLength للحقل ${schema}.${field}. / The contract declares no maxLength for ${schema}.${field}.`
    );
  }
  return shape.mx;
}
