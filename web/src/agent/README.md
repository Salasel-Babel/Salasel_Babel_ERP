# ورقة السؤال — القرار الحاجز الذي يطلبه الوكيل ولا يراه

المسار: `web/src/agent/`. مرآةُ `web/src/voice/` في الاستقلال: اعتمادُه على التطبيق
أن يكون مرسوماً داخل `LocaleProvider` القائم، وعلى `web/src/ui/` وحدها في المفردات
البصرية — **ولا لغة بصرية ثانية**. والأنماط تُحمَّل من داخل المكوّن (`import "./agent.css"`).

```tsx
import { AgentQuestionSheet } from "../agent";

<AgentQuestionSheet
  sheet={sheet}                       // ما رسمه الخادم من بياناتٍ محلّية
  onAnswer={(a) => post(a)}           // { questionId, optionToken } — لا ثالث لهما
  onCreate={(d) => post(d)}           // مسوّدة الإنشاء، بقيمٍ بمفاتيح حقول العقد
  onDismiss={close}
/>
```

## الحدّ في جملة

الوكيل يقول «هذا الاسم ملتبس، اسأل». **الخادم** يبحث محلّياً ويرسم الورقة، ويختار
الإنسان، ويعود إلى الوكيل **رمزٌ معتم** وحده. فلا يعبر اسمٌ، **ولا يعبر العدد**، ولا
تُعرف حتى واقعةُ الإنشاء: شكل ما يعود واحدٌ في الحالين.

| ما يراه الإنسان | ما يبلغ النموذج |
|---|---|
| الأسماء المرشَّحة كاملةً من سجلّه | لا شيء منها |
| «الثالث من أربعة» يقرؤه قارئ الشاشة (`aria-posinset` / `aria-setsize`) | لا العدد ولا الموضع |
| رمز الطرف تحت الاسم | لا شيء |
| «جديد» وقد فُتحت وسُجّل بها كيان | لا يعلم أن إنشاءً وقع |

والسبب أن `answerOf()` تبني `{ questionId, optionToken }` **ولا تقرأ نصّ الخيار ولا
موضعه**. والرمز موقَّع في الخادم بطولٍ ثابت، فلا الطول يقول شيئاً ولا يُزوَّر ولا يُعدّ.

## ورقةُ الإنشاء تُشتقّ من العقد، ولا تُكتب بيد

`planAgentCreateSheet(kind)` تقرأ `src/api/generated/` — المُولَّدة من
`contracts/openapi/v1.json` والحاملةَ بصمته، و`npm run gen:check` بوّابتُها. فالحقول
وإلزامياتُها وأنماطُها **واقعُ عقدٍ لا قائمةٌ في شاشة**.

| النوع | العملية | النتيجة |
|---|---|---|
| `customer` | `addCustomer` | خمسة حقول: `code` · `creditLimit` · `name.ar` · `name.en` · `paymentTermsDays` — **ولا `vatNumber`**، فالعقد ينصّ أن إرساله يُسقط الطلب كلّه |
| `supplier` | `addSupplier` | الخمسة نفسها ومعها `vatNumber` اختيارياً |
| `employee` | `registerEmployee` | **يُرفض** — فعلُه `register` وليس في `VoiceOperationGuard.PermittedVerbs` |
| `propertyUnit` | `createUnit` | **يُرفض** — مساره تحت `{propertyId}`، والورقة لا تختار عقاراً |
| `inventoryItem` | `addItem` | **يُرفض** — `units` حقلٌ إلزاميٌّ قائمة |
| `project` | `addProject` | **يُرفض** — `nameTranslations` حقلٌ إلزاميٌّ قائمة |

والأربعةُ الأخيرة **مُستنتَجةٌ من العقد لا مكتوبةٌ باليد**: يكفي أن يُنشر غداً فعلٌ مسموح
أو مسارٌ بلا أب أو مخطّطٌ بلا قائمة إلزامية حتى تُرسَم الورقة وحدها.

## الوصولية — شرطٌ لا تحسين

- `role="dialog"` · `aria-modal` · `aria-labelledby` · `aria-describedby`.
- الخيارات `role="radiogroup"` بمؤشّرٍ متجوّل: **الأسهم تنقل التركيز مع `aria-checked`**،
  لا الحالة وحدها — مجموعةٌ يتحرّك فيها الوسم والتركيز ثابت يقرؤها قارئ الشاشة خطأً.
- الأسهم الأفقية **بحسب اتجاه الصفحة**: اليسار يتقدّم بالعربية ويتأخّر بالإنجليزية.
  والاتجاه يُقرأ من الوثيقة لا يُفترض في الشيفرة.
- `Home` · `End` · `Enter` · `Esc`، و`Tab` **محبوسٌ داخل اللوحة**، والتركيز يعود إلى ما فتحها.
- ولا خاصّية فيزيائية واحدة في `agent.css` — يفرضه `scripts/audit.mjs`.

## وسوم الاختبار (`data-testid`)

`agent-question-scrim` · `agent-question-sheet` · `agent-question-title` ·
`agent-question-note` · `agent-question-options` · `agent-option-<i>` ·
`agent-sheet-cancel` · `agent-sheet-fault` · `agent-create-sheet` (ويحمل
`data-operation`) · `agent-create-<path>` · `agent-create-faults` ·
`agent-create-omitted` · `agent-create-refusal` · `agent-create-back` ·
`agent-create-submit`.

## ما لا يفعله هذا المجلّد

لا ينادي باباً، ولا يرحّل، ولا يوقّع رمزاً. يستدعي `onAnswer` و`onCreate` ويترك النداء
لمن ركّبه — والخادم وحده يفتدي الرمز ويعرف غرضه ومستأجره وجلسته. وكلّ ما يُنتجه هذا
المسار **مسوّدة**: التأكيد هنا يعني «أقبل شكل هذه البيانات»، لا «رحّلها».
