/* أدنى ما يلزم من تعريفات Node لاختبارٍ يقرأ ملفّ متجهات من جذر المستودع.
   وُضعت هنا لا في tsconfig.app.json عمداً: إتاحة أنواع Node لشيفرة الواجهة كلها
   تفتح باباً لاستدعاء fs من مكوّن، وهو ما لا يُكتشف إلا وقت التشغيل في المتصفّح. */
declare module "node:fs" {
  export function readFileSync(path: string, encoding: string): string;
}

declare module "node:path" {
  const path: { resolve(...segments: string[]): string };
  export default path;
}

declare const process: { cwd(): string };
