/* ═══════════════════════════════════════════════════════════════════════════
   شاشة الأمر المنطوق — الصوت مدخلٌ إلى النظام لا ميزةٌ في شاشة.
   ───────────────────────────────────────────────────────────────────────────
   الأقسام الخمسة كلّها هنا، ونيّاتُها تصل من سجلٍّ **تُسهم فيه الوحدات** لا من
   قائمةٍ مكتوبة في هذا الملفّ.

   **والمنشأة تُقرأ من الجلسة لا من نصٍّ محفوظ**: من يتكلّم في منشأة وهو يظنّ
   أنه في أخرى يُرحّل في الخطأ بلا أن تقول له الشاشة شيئاً. ولذلك تُحقن المنشأة
   واسمها في اللوحة، وتُرفض أي جملةٍ تسمّي شركةً غيرها.

   **والصلاحيات تُضيَّق هنا لا في اللوحة**: المسار المنطوق مدخلٌ آخر إلى
   الاستحقاق نفسه، لا بابٌ أوسع منه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readSession } from "../../api/generated/client";
import { useApi } from "../../app/api-context";
import { useLocale, useT } from "../../i18n/react";
import { resolveTranslatedName } from "../../app/translated-name";
import { VOICE_INTENTS, VoiceConsole, type VoiceDispatch } from "../../voice";

/** الشاشة. */
export function VoiceScreen(): ReactNode {
  const { t } = useT();
  const { locale } = useLocale();
  const { transport, config } = useApi();
  const [last, setLast] = useState<VoiceDispatch | null>(null);

  const session = useQuery({
    queryKey: ["session", config.baseUrl, config.token],
    enabled: config.token !== "",
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: ({ signal }) => readSession(transport, signal),
  });

  const chosen = session.data?.companies.find((c) => c.companyId === config.companyId) ?? null;
  const companyNameAr = chosen?.nameAr
    ? (resolveTranslatedName(chosen.nameAr, chosen.nameTranslations, "ar")?.text ?? chosen.nameAr)
    : "";

  /* اسم المنشأة للعرض يتبع اللغة؛ والمقارنة الصوتية تقع على الاسم العربي —
     وهو السجلّ لا ترجمته (ADR-0021). */
  const shown = chosen?.nameAr
    ? (resolveTranslatedName(chosen.nameAr, chosen.nameTranslations, locale)?.text ?? chosen.nameAr)
    : "";

  const caller = useMemo(
    () => ({
      companyId: config.companyId,
      companyNameAr,
      /* حتى تصل خريطةُ الصلاحيات إلى الواجهة، يقتصر المسموح على ما تعرفه
         الجلسة الحالية من نيّات — والخادم يفحص الاستحقاق مرّةً أخرى عند
         التنفيذ، فلا تكون هذه الطبقة هي الحارس الوحيد. */
      permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
    }),
    [companyNameAr, config.companyId]
  );

  /* تاريخ اليوم يُقرأ **هنا** مرّةً ويُحقن في اللوحة: قارئٌ يسأل الساعة بنفسه
     يعطي نتيجةً مختلفة كل يوم، فلا يُعاد تشغيل عطلٍ وقع أمس. */
  const today = useMemo(() => new Date().toISOString().slice(0, 10), []);

  return (
    <div className="page" data-testid="voice-screen">
      <h1 className="page__title">{t("screen.voice.console.title")}</h1>
      <p className="muted" data-testid="voice-screen-company">
        {shown}
      </p>

      <VoiceConsole caller={caller} today={today} statutoryTaxRate="0.15" onDispatch={setLast} />

      {last ? (
        <p className="muted" data-testid="voice-screen-dispatch" data-intent={last.intent.id}>
          {last.intent.nameAr}
        </p>
      ) : null}
    </div>
  );
}
