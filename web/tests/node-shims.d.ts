/* أدنى ما يلزم من تعريفات Node لاختبارٍ يقرأ ملفّ متجهات من جذر المستودع.
   وُضعت هنا لا في tsconfig.app.json عمداً: إتاحة أنواع Node لشيفرة الواجهة كلها
   تفتح باباً لاستدعاء fs من مكوّن، وهو ما لا يُكتشف إلا وقت التشغيل في المتصفّح. */
declare module "node:fs" {
  export function readFileSync(path: string, encoding: string): string;
  /* يُستعمل في حرّاس المصدر: «لا مبلغ يمرّ بـNumber في هذا المجلّد» تفحص
     **كل** ملفّاته لا قائمةً مكتوبة بيد — وقائمةٌ مكتوبة بيد تعمي عن الملفّ
     الذي يُضاف غداً. */
  export function readdirSync(path: string): string[];
}

declare module "node:path" {
  const path: { resolve(...segments: string[]): string };
  export default path;
}

declare const process: { cwd(): string };
