/* ═══════════════════════════════════════════════════════════════════════════
   الإفصاح — ما هذه الصفحة، بلا مواربة
   ───────────────────────────────────────────────────────────────────────────
   لا يُغلَق. يُطوى إلى شارةٍ تبقى مرئيةً على كل شاشة، وتُعيده لمسةٌ واحدة.
   وهو خارج الموجّه عمداً: شاشةٌ واحدة تخلو منه تكفي كي يُقرأ ما بعدها نظاماً
   يعمل. والنصّ عربيٌّ لأن العربية هي سجلّ هذا النظام لا ترجمةً فيه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useState, type ReactNode } from "react";
import { IDS } from "./seed";
import "./showcase.css";

/* مفاتيح العرض. **ليست ميزةً في المنتج**: الشاشات تفتح مستنداً بمعرّفه لأنه
   لا باب يسرد المستندات في العقد (وهو قرارٌ مكتوب، لا نقص). ومن يمشي على هذه
   الصفحة لا يعرف معرّفاً واحداً، فتُعرَض هنا — في طبقة العرض لا في الشاشة. */
const KEYS: readonly { readonly label: string; readonly value: string }[] = [
  { label: "عقد إيجار", value: IDS.lease },
  { label: "عقد مشروع", value: IDS.contract },
  { label: "مستخلص", value: IDS.certificate },
  { label: "عقد باطن", value: IDS.subcontract },
  { label: "ضمان", value: IDS.guarantee },
  { label: "موظّف", value: IDS.employee },
  { label: "مسيّر رواتب", value: IDS.payrollRun },
  { label: "قسيمة راتب", value: IDS.payslip },
];

/** الشريط الثابت. */
export function ShowcaseNote(): ReactNode {
  const [folded, setFolded] = useState(false);
  const [keysOpen, setKeysOpen] = useState(false);
  return (
    <aside
      className="showcase-note"
      data-folded={folded ? "true" : "false"}
      data-testid="showcase-note"
      role="note"
      aria-label="إفصاح عن طبيعة هذه الصفحة"
    >
      <span className="showcase-note__mark">عرضُ واجهة</span>
      <p className="showcase-note__text">
        <span className="showcase-note__lead">بياناتٌ ثابتة — لا خادمَ هنا ولا دفترَ حقيقي.</span>{" "}
        الأشكالُ والرفوضُ من العقد المنشور، والأسماءُ والحساباتُ من بذرة العرض ودليلِ الحسابات؛
        وما عداها من الأرقام مُركَّبٌ لهذه الصفحة. لا يُرحَّل هنا قيدٌ، ولا تُوقَّع سلسلة، ولا
        يغادر الصفحةَ بايتٌ واحد.
      </p>
      <button
        type="button"
        className="showcase-note__toggle"
        onClick={() => setKeysOpen((value) => !value)}
        aria-expanded={keysOpen}
        data-testid="showcase-keys-toggle"
      >
        مفاتيح العرض
      </button>
      <button
        type="button"
        className="showcase-note__toggle"
        onClick={() => setFolded((value) => !value)}
        aria-expanded={!folded}
        data-testid="showcase-note-toggle"
      >
        {folded ? "افتح الإفصاح" : "اطوِ"}
      </button>

      {keysOpen ? (
        <div className="showcase-keys" data-testid="showcase-keys">
          <p className="showcase-keys__lead">
            الشاشات تفتح مستنداً <strong>بمعرّفه</strong> — فلا باب يسرد المستندات في العقد، وذلك
            قرارٌ مكتوب لا نقص. وهذه معرّفات هذه الصفحة، انسخ أحدها والصقه في حقل المعرّف.
          </p>
          <ul className="showcase-keys__list">
            {KEYS.map((key) => (
              <li key={key.value}>
                <span className="showcase-keys__label">{key.label}</span>
                <code className="mono" dir="ltr">
                  {key.value}
                </code>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </aside>
  );
}
