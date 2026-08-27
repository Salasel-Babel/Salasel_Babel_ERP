/* المشهد الافتتاحي: الاسم، والوعد، وثلاثة أرقام كلّها من القاعدة المبذورة. */
import type { ReactNode } from "react";
import { Amount, Num } from "../../i18n/react";
import { snapshot, wire } from "../data";

/** بطاقة العنوان. */
export function TitleScene(): ReactNode {
  const t = snapshot.totals;
  return (
    <div className="demo-title demo-fade">
      <h1 className="demo-title__h">سلاسل بابل</h1>
      <p className="demo-title__p">
        نظام محاسبي عربيُّ الأصل للمملكة العربية السعودية — دفترٌ يُضاف إليه ولا يُعدَّل،
        وسلسلة بصمات تُثبت أن الماضي لم يُمسّ.
      </p>
      <div className="demo-title__strip">
        <span className="demo-chip">
          <strong><Num value={t.entryCount} /></strong> قيداً مُرحَّلاً
        </span>
        <span className="demo-chip">
          ميزان متوازن — <strong><Amount value={wire(t.totalDebit)} /></strong> ريالاً
        </span>
        <span className="demo-chip">
          سلسلة البصمات <strong>CHAIN-OK</strong>
        </span>
        <span className="demo-chip">
          دليل حسابات <strong><Num value={t.chartSize} /></strong> حساباً
        </span>
      </div>
      <p className="demo-note" style={{ fontSize: 19, maxWidth: 1000 }}>
        كلّ رقم في هذا العرض مقروء من قاعدة الشركة التجريبية المبذورة عبر محرّك الترحيل نفسه.
        وما لا يكون حقيقياً يحمل وسماً بنفسجياً أعلى الشاشة.
      </p>
    </div>
  );
}
