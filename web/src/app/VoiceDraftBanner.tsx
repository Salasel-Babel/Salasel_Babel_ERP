/* ═══════════════════════════════════════════════════════════════════════════
   المسوّدة المنطوقة حين تهبط  ·  The spoken draft, where it lands
   ───────────────────────────────────────────────────────────────────────────
   يُنطَق الأمر في لوحة الصوت، فتُودَع المسوّدة ويُنتقل بالمستخدم إلى شاشة
   مستندها. **وهذه اللوحة هي ما يراه هناك**: اسمُ المستند، والعملية المنشورة
   التي ستُنشئه، وكلُّ قيمةٍ قيلت **بمصدرها**.

   ⚠ **ولماذا في الهيكل لا في كل شاشة:** الهبوط سلوكٌ عابرٌ للشاشات — يقع في
   ثلاث عشرة شاشة اليوم وفي غيرها غداً. ولوحةٌ تُنسخ ثلاث عشرة مرّة تُنسى في
   الرابعة عشرة، فيصل المستخدم إلى شاشةٍ **صامتة** بعد أن قال أمره كاملاً.

   ⚠ **ولا زرّ ترحيلٍ هنا.** الترحيل زرُّ الشاشة نفسها: فعلٌ بصريّ يدويّ على
   مستندٍ قُرئ. وهذه اللوحة تقول **ما وصل** لا أكثر.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useSyncExternalStore, type ReactNode } from "react";
import { useT } from "../i18n/react";
import { dropVoiceDraft, peekVoiceDraft, subscribeVoiceDraft } from "../voice";

/** لوحةُ المسوّدة الواصلة، أو لا شيء. */
export function VoiceDraftBanner(): ReactNode {
  const { t } = useT();
  const draft = useSyncExternalStore(subscribeVoiceDraft, peekVoiceDraft, () => null);

  if (draft === null) return null;

  return (
    <section
      className="voice-landed"
      data-testid="voice-draft-landed"
      data-intent={draft.intentId}
      data-operation={draft.operationId}
      role="status"
      aria-live="polite"
    >
      <h2 className="voice-landed__title">
        {t("app.voiceDraft.title", { name: draft.nameAr })}
      </h2>
      <p className="voice-landed__note">{t("app.voiceDraft.note")}</p>

      <dl className="voice-landed__fields">
        {draft.fields.map((field) => (
          <div key={field.name} className="voice-landed__field" data-provenance={field.provenance}>
            <dt>{field.nameAr}</dt>
            <dd data-testid={"voice-landed-value-" + field.name}>
              {field.text + (field.unit ? " " + field.unit : "")}
            </dd>
          </div>
        ))}
      </dl>

      <button
        type="button"
        className="btn btn-sm"
        data-testid="voice-draft-dismiss"
        onClick={dropVoiceDraft}
      >
        {t("app.voiceDraft.dismiss")}
      </button>
    </section>
  );
}
