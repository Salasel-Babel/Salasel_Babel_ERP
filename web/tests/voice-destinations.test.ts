/* ═══════════════════════════════════════════════════════════════════════════
   أين تهبط المسوّدة المنطوقة — مقيساً على الموجّه القائم، لا على جدولٍ يُصدَّق.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ جدولُ الوجهات نصٌّ يُكتب بيد، والمسارات تُسجَّل في مكانٍ آخر. ومسارٌ يُعاد
   تسميته في `router.tsx` يجعل الجدول يشير إلى لا شيء — **بصمت**، لأن الوجهة
   الغائبة سلوكٌ مشروع في هذه الطبقة (شاشةٌ لم تهبط بعد). فيُقاس هنا الاتجاهان:
   لا مسار في الجدول إلا وهو مسجَّل، **ولا قسمٌ بُنيت شاشاتُه ونيّاتُه بلا وجهة**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { createAppRouter } from "../src/app/router";
import { VOICE_DESTINATIONS, destinationOf, registeredPaths } from "../src/app/voice-destinations";
import { VOICE_INTENTS } from "../src/voice";

const paths = registeredPaths(createAppRouter({ memory: true }));

describe("وجهات المسوّدة المنطوقة", () => {
  it("قراءةُ مسارات الموجّه ليست ضامرة", () => {
    /* حارس لا فراغ: قائمةٌ فارغة تجعل كل ما تحته يمرّ على «لا شاشة هبطت». */
    expect(paths.length).toBeGreaterThanOrEqual(10);
    expect(paths).toContain("/voice");
  });

  it("كل مسارٍ في جدول الوجهات مسجَّل فعلاً في الموجّه", () => {
    const declared = [...new Set(Object.values(VOICE_DESTINATIONS))];
    expect(declared.length).toBeGreaterThanOrEqual(10);

    for (const path of declared) {
      expect(paths, path + " في الجدول وليس في الموجّه").toContain(path);
    }
  });

  it("كل نيّةٍ في الأقسام الأربعة المبنيّة تهبط على شاشة", () => {
    const built = ["Contracting", "HumanResources", "Inventory", "RealEstate"];
    let landed = 0;

    for (const intent of VOICE_INTENTS) {
      if (!built.includes(intent.section)) continue;
      /* النيّة التي تنتظر قراراً لا تُنفَّذ أصلاً، فلا وجهة لها. */
      if (intent.status === "AwaitingOwnerDecision") continue;

      const to = destinationOf(intent.id, paths);
      expect(to, intent.id + " بلا وجهة").not.toBeNull();
      landed++;
    }

    expect(landed).toBeGreaterThanOrEqual(29);
  });

  /* كان هذا الحارس يقول «قسم المحاسبة بلا وجهةٍ واحدة» — وكان صادقاً يوم
     كُتب: لم تكن لمستندات المحاسبة شاشة في أي فرع (خطة الصوت §13.4). وقد
     هبطت سبعُ شاشاتٍ للدورة، فصار الحارس **يقيس التوزيع لا الغياب**: أيُّ
     نيّةٍ لها وجهة، وأيُّها لا — وكلتاهما مُسمّاة بالاسم. وعدُّ الطرفين مثبَّت
     فلا تُضاف وجهةٌ ولا تُسقَط بصمت. */

  /** النيّات المحاسبية التي **لم تُبنَ لمستنداتها شاشة** — وأبوابها منشورة. */
  const ACCOUNTING_WITHOUT_SCREEN = [
    "accounting.credit_note.draft",
    "accounting.payables_aging.query",
    "accounting.purchase_return.draft",
    "accounting.stock_bill.capture",
  ];

  it("كل نيّة محاسبية بُنيت شاشةُ مستندها تهبط عليها", () => {
    const accounting = VOICE_INTENTS.filter((intent) => intent.section === "Accounting");
    expect(accounting.length).toBeGreaterThanOrEqual(13);

    const landed = [];
    const absent = [];
    for (const intent of accounting) {
      const to = destinationOf(intent.id, paths);
      if (to === null) absent.push(intent.id);
      else landed.push(intent.id);
    }

    /* والغياب **مُعلَن لا مسكوتٌ عنه**: هذه الأربع بأعيانها، لا عدداً مبهماً. */
    expect(absent.sort()).toEqual(ACCOUNTING_WITHOUT_SCREEN);
    expect(landed.length).toBe(accounting.length - ACCOUNTING_WITHOUT_SCREEN.length);
  });

  it("دورة المستند تهبط على شاشتها بعينها، لا على شاشةٍ تشبه اسمها", () => {
    /* والوجهة تتبع `operationId` المنشور: رصيدُ عميلٍ عمليتُه readReceivablesAging،
       فوجهتُه شاشةُ ذلك التقرير. */
    expect(destinationOf("accounting.sales_invoice.draft", paths)).toBe("/sales/invoice");
    expect(destinationOf("accounting.customer_receipt.record", paths)).toBe("/sales/receipt");
    expect(destinationOf("accounting.receivables_aging.query", paths)).toBe("/sales/receivables");
    expect(destinationOf("accounting.customer_balance.query", paths)).toBe("/sales/receivables");
    expect(destinationOf("accounting.purchase_order.draft", paths)).toBe("/purchasing/order");
    expect(destinationOf("accounting.goods_receipt.draft", paths)).toBe("/purchasing/goods-receipt");
    expect(destinationOf("accounting.supplier_bill.capture", paths)).toBe("/purchasing/bill");
    expect(destinationOf("accounting.supplier_payment.record", paths)).toBe("/purchasing/payment");
    expect(destinationOf("accounting.journal_entry.draft", paths)).toBe("/voucher");
  });

  it("الفاتورة المخزنية لا تُخلَط بفاتورة المصروف", () => {
    /* بابان منشوران لمستندين مختلفين: draftStockBill وdraftExpenseBill.
       وشاشةُ المصروف قائمة، والمخزنية لا — فالنيّة تبقى بلا وجهة ولا تُصرَف
       إلى شاشةٍ لا تكتب مستندها. */
    expect(destinationOf("accounting.stock_bill.capture", paths)).toBeNull();
    expect(destinationOf("accounting.supplier_bill.capture", paths)).toBe("/purchasing/bill");
  });

  it("مسارٌ غير مسجَّل لا يُقفَز إليه ولو كان في الجدول", () => {
    expect(destinationOf("hr.payroll_run.draft", ["/voice"])).toBeNull();
    expect(destinationOf("hr.payroll_run.draft", paths)).toBe("/hr/payroll");
  });
});
