# الإدخال الصوتي العربي — مكوّن مستقلّ

المسار: `web/src/voice/`. لا يعتمد على `screens/` ولا على `demo/` ولا على عميل الواجهة
المولَّد. اعتمادُه الوحيد على التطبيق: أن يكون مرسوماً داخل `LocaleProvider` القائم.

```tsx
import { VoiceCapture } from "../voice";

<VoiceCapture today="2026-08-26" onCommit={(intent, transcript) => …} />
```

الأنماط تُحمَّل من داخل المكوّن (`import "./voice.css"`) — لا سطر يُضاف في `main.tsx`.

## الخصائص

| الخاصية | النوع | الافتراضي | ماذا تفعل |
|---|---|---|---|
| `lang` | `string` | `"ar-SA"` | لغة التفريغ في المتصفّح |
| `today` | `string` (‏ISO) | — | تاريخ اليوم. **يُحقن** كي يكون السلوك حتمياً في اختبار أو تسجيل. بدونه لا يُملأ حقل التاريخ إطلاقاً |
| `statutoryTaxRate` | `string` | `"0.15"` | النسبة حين لا تُنطق. **نصّ لا رقم** — لا عائمة في المال |
| `fields` | `readonly VoiceField[]` | `INVOICE_FIELDS` | الحقول المعروضة. `key` هو مفتاح الحقل في مسوّدة الخادم حرفياً |
| `resolveIntent` | `(t: string) => Promise<SpokenIntent>` | — | قراءة النيّة بنموذج. **اختياري تماماً**: غيابه يعني عملاً بلا شبكة وبلا مفتاح |
| `onChange` | `(intent, transcript) => void` | — | يُستدعى عند **كل** تغيّر، بما فيه النتائج الأوّلية أثناء الكلام |
| `onCommit` | `(intent, transcript) => void` | — | مرّة واحدة عند إفلات الزرّ، بالنصّ النهائي |
| `onUnavailable` | `(reason) => void` | — | حين يتعذّر التفريغ، بسببٍ مُسمّى |
| `allowManualTranscript` | `boolean` | `true` | إتاحة إدخال التفريغ نصّاً حين يتعذّر الصوت |
| `simulatedTranscript` | `string` | — | نصّ يُحقن بدل الميكروفون. **يُوسَم على الشاشة وسماً ظاهراً** |

## ما يُصدره

```ts
interface SpokenValue {
  field: string;          // seller_name · invoice_number · gross_total · tax_rate · issued_on · suggested_event
  text: string;           // نصّ دائماً — المال نصّ
  provenance: "spoken" | "inferred" | "defaulted" | "typed" | "read" | "attested";
  confidence?: number;
  heard?: string;         // المقطع من الكلام الذي أنتج القيمة
}

interface SpokenIntent { values: readonly SpokenValue[]; faults: readonly string[]; }
```

`faults` رموزٌ مُسمّاة بنفس رموز `VoiceErrors` في الخادم — تُعرَض ولا تُبتلع.

## معالم الاختبار (‏`data-testid`)

`voice-capture` · `voice-hold` · `voice-transcript` · `voice-simulated` ·
`voice-unavailable` · `voice-manual-input` · `voice-manual-apply` · `voice-faults` ·
`voice-not-a-fact` · و لكل حقل: `voice-field-<key>` (ويحمل `data-provenance`) و
`voice-value-<key>` و `voice-source-<key>`.

## السلوك حين لا يعمل شيء

| الحال | ما يظهر | ما يُستدعى |
|---|---|---|
| المتصفّح بلا الواجهة (فَيَرفُكس، ويب-فيو قديم) | رسالة `unsupported` + الزرّ معطَّل + حقل نصّ بديل | `onUnavailable("unsupported")` |
| الصفحة ليست ‏HTTPS ولا `localhost` | رسالة `insecure` | `onUnavailable("insecure")` |
| رُفض إذن الميكروفون | رسالة `denied` | `onUnavailable("denied")` |
| **لا مدخل صوتي — وهي حال المتصفّح بلا رأس المقيسة** | رسالة `noAudio` + البديل | `onUnavailable("no-audio")` |
| المتصفّح لا يبلغ خدمة التعرّف | رسالة `network` | `onUnavailable("network")` |
| `resolveIntent` رمى أو فشل | تبقى قراءة القارئ الحتمي على الشاشة، ويُستدعى `onCommit` بها | — |

**كل مسار بديل يُوسَم على الشاشة** بـ«مُحاكاة: هذا النصّ حُقن ولم يُستعمل ميكروفون»
(‏`voice-simulated`، بدور `status`). ولا مسار يملأ حقلاً بلا وسم.

## ما لا يفعله المكوّن

لا يرحّل، ولا يحفظ، ولا ينادي خادماً من تلقاء نفسه. يملأ **مسوّدة** يؤكّدها إنسان حقلاً
حقلاً قبل أن يصير أي شيء قيداً (‏ADR-0024). والامتلاء في المتصفّح **معاينة**: الخادم
يعيد اشتقاق القيم من التفريغ النهائي وهو المرجع.

> **ملاحظة على مركز التكلفة:** بعد إغلاق نافذة العقد صار `PostingScope.CostCenterId` غير
> قابل للغياب. وهذا المكوّن **لا يرحّل**، فلا يحمل مركز تكلفة ولا يخترعه؛ ومَن يُرقّي
> المسوّدة إلى مستند حقيقي هو مَن يحلّه عبر `ICostCenterResolver`.

---

## لوحة الأمر المنطوق — الأقسام الخمسة

`VoiceCapture` أعلاه يخدم تدفّقاً واحداً (فاتورة مورد) وواجهتُه مستقرّة كما هي.
وبجانبه `VoiceConsole`: لوحةٌ تحمل **الأقسام الخمسة**، ونيّاتُها تصل من مرآةٍ محروسة
لسجلّ الخادم — لا من قائمةٍ مكتوبة في الواجهة.

```tsx
import { VoiceConsole } from "../voice";

<VoiceConsole
  caller={{ companyId, companyNameAr, permittedIntentIds }}
  today="2026-08-31"
  statutoryTaxRate="0.15"
  onDispatch={(command) => /* شاشة القسم تُنفّذ */ undefined}
/>
```

| الملفّ | ما فيه |
|---|---|
| `catalogue.ts` | **مرآة السجلّ** — عشرون نيّةً بأقسامها وشرائحها وعبارات إطلاقها. يقرؤها حارسٌ في الخادم ويطابقها بما بنته الوحدات، فالانحراف يُحمِّر بوّابةً لا شاشة |
| `command.ts` | القارئ الحتمي والبوابة — نظير `SpokenCommandReader` و`VoiceConfirmationGate`، ويقرأ معهما ملفّ متجهاتٍ **واحداً** |
| `speak.ts` | النُّطق — **زينةٌ لا شرط**: متصفّحٌ بلا `speechSynthesis` يبقى قادراً على إتمام العملية كاملة |
| `VoiceConsole.tsx` | اللوحة: الأقسام، وما يمكن أن يُقال في كلٍّ منها، والقراءة المرتدّة، وزرّا التأكيد والإلغاء |

**والقاعدة التي تحكمها كلّها:** ما يكتب في الدفتر، أو يحرّك مخزوناً، أو يصرف لإنسان،
أو يوقّع عقداً — يُقرأ على قائله ثم يُؤكَّد صراحةً. والاستعلام وحده يمضي بلا تأكيد.

**والملخّص المرتدّ نصٌّ واحد يُعرض ويُنطَق معاً**: من لا يسمع يقرؤه، ومن لا يرى يسمعه
ويُعلَن له بـ`aria-live`. ونصّان ينحرفان فيؤكّد كلٌّ منهما ما لم يؤكّده الآخر.

الخطة كاملةً — بما فيها ما **لم** يُعطَ صوتاً وسببه — في
[`docs/voice/خطة-الصوت-للأقسام-الخمسة.md`](../../../docs/voice/خطة-الصوت-للأقسام-الخمسة.md).
