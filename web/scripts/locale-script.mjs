/* ═══════════════════════════════════════════════════════════════════════════
   خطّ اللغة والترميز — قواعد تُشتقّ ولا تُعدَّد
   Script and encoding — derived predicates, never an enumerated table
   ───────────────────────────────────────────────────────────────────────────
   ‏وحدةٌ خالصة بلا مدخلات ولا مخرجات: يستوردها `scripts/audit.mjs` (الفحص ١٠)
   ويستوردها `tests/locale-script.test.ts`. فالكاشف واحدٌ في الموضعين، ولا
   ينحرف نسختان منه.

   ‏**لماذا لا جدول محارف مسموحة.** ثلاثة علاجات في هذا المستودع شُحنت قوائم،
   ‏وهُزمت كلُّها ببندٍ لم يُكتب — مسجَّل في
   ‏`docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy`.
   ‏فكل قاعدة هنا إمّا **فكُّ ترميزٍ يُجرى فعلاً** (لا مطابقةُ نمطٍ على شكل
   المحارف)، وإمّا **خاصّيةٌ تُشتقّ من رمز اللغة نفسه** عبر `Intl` وخصائص
   يونيكود. ولا يظهر في هذا الملفّ حرفٌ واحد من حروف أي لغة.
   ═══════════════════════════════════════════════════════════════════════════ */

/**
 * ‏خطّ اللغة — مشتقٌّ من رمزها عبر `Intl.Locale.maximize()`، أي من بيانات CLDR
 * في زمن التشغيل لا من جدولٍ في هذا المستودع. لغةٌ خامسة تعمل بلا تعديل هنا.
 * The locale's script, taken from CLDR via Intl — not from a table we keep.
 */
export function scriptOf(code) {
  const script = new Intl.Locale(code).maximize().script;
  if (!script) {
    throw new Error("لا خطّ معروف للغة «" + code + "» — لا يُخمَّن ولا يُفترض.");
  }
  return script;
}

/**
 * ‏مُطابِقُ الخطّ. يُبنى من اسم الخطّ نفسه بخاصية يونيكود `Script_Extensions`،
 * فيرمي `SyntaxError` على اسمٍ لا تعرفه المنصّة — وهو الفشل الصاخب المطلوب:
 * خطٌّ لا يُفهم يوقف الفحص ولا يمرّ صامتاً.
 * Built from the script name itself; an unknown script throws rather than passing.
 */
export function scriptMatcher(script) {
  return new RegExp("\\p{Script_Extensions=" + script + "}", "u");
}

/**
 * ‏هل هذا الخطّ **دليلٌ على نثر لغته؟** السؤال يُجاب بيونيكود لا برأي: خطُّ
 * المعرّفات الآلية هو الخطّ الذي يحوي الحرف اللاتيني `A`، وكلُّ مستودع مليء
 * برموزٍ منه (`PDF`، `SAR`، `BANK-0001`) داخل نصوصٍ ليست بلغته. فوجود حرفٍ
 * من ذلك الخطّ لا يُثبت شيئاً، ووجود حرفٍ من خطٍّ لا يحوي `A` يُثبت نثراً.
 *
 * Is the script diagnostic of its language's prose? Answered from Unicode:
 * the script of machine identifiers is the one containing the ASCII letter A,
 * and its letters appear inside every locale. A script without A is diagnostic.
 */
export function isDiagnostic(script) {
  return !scriptMatcher(script).test("A");
}

/**
 * ‏شهود لغةٍ ما: **كل** لغةٍ أخرى خطُّها دليل. فإن كان نصُّ الشاهد نثراً بخطّه،
 * فالمفتاح مفتاحُ نثرٍ لا رمزٍ آلي — ويجب أن يكون نصُّ هذه اللغة نثراً بخطّها هي.
 * ولا تُستثنى لغة المصدر: للعربية شاهدان كما لغيرها.
 *
 * ‏**ولا يُشترط اختلاف الخطّ.** الأردية والعربية خطُّهما واحد، ومع ذلك تشهد
 * كلٌّ منهما للأخرى شهادةً صحيحة: «نصُّ العربية هنا نثرٌ عربيّ الخطّ» يوجب أن
 * يكون نصُّ الأردية عربيَّ الخطّ أيضاً — وهو ما يلتقط قيمةً أرديةً مشوَّهة حتى لو
 * كانت الهندية مشوَّهةً معها في الإنزال نفسه. واشتراطُ اختلاف الخطّ كان يجعل
 * للأردية شاهداً واحداً هو الهندية، فيسقط الشاهدان معاً بعطلٍ واحد. **مقيس:**
 * بالاشتراط، حقنُ التشويه في `hi` و`ur` معاً يُخفي `ur` عن هذه القاعدة.
 *
 * Witnesses: every other locale whose script is diagnostic — sharing a script
 * does not disqualify a witness, and requiring difference left Urdu with a
 * single witness that one landing could break alongside it.
 */
export function witnessesOf(code, codes) {
  return codes.filter((other) => other !== code && isDiagnostic(scriptOf(other)));
}

/** المحارف البنيوية: معاملُ استبدالٍ، ووسمٌ، وكيانُ HTML — ليست نثراً ولا تُقاس. */
const STRUCTURAL = [/\{[^{}]*\}/g, /<[^<>]*>/g, /&[a-zA-Z][a-zA-Z0-9]*;/g, /&#[0-9]+;/g];

/** ينزع ما ليس نثراً قبل القياس. Strips the non-prose scaffolding. */
export function prose(text) {
  let out = String(text);
  for (const rx of STRUCTURAL) out = out.replace(rx, " ");
  return out;
}

const LETTER = /\p{L}/u;

/** إحصاء الحروف: كم منها بخطّ اللغة، وكم بغيره. Letter census by script. */
export function census(text, code) {
  const own = scriptMatcher(scriptOf(code));
  let inScript = 0;
  let foreign = 0;
  for (const ch of prose(text)) {
    if (!LETTER.test(ch)) continue;
    if (own.test(ch)) inScript++;
    else foreign++;
  }
  return { letters: inScript + foreign, inScript, foreign };
}

/** أفي النصّ حرفٌ واحد بخطّ لغته؟ Does the value carry one letter of its own script? */
export function hasOwnScript(text, code) {
  return census(text, code).inScript > 0;
}

/** عدد كلمات النثر — كلمةٌ ما فيها حرف. Prose word count. */
export function proseWords(text) {
  return prose(text).split(/\s+/u).filter((w) => LETTER.test(w)).length;
}

/* ═════════════════════════ التشويه · mojibake ═════════════════════════════
   ‏نصٌّ رُمِّز UTF-8 ثم قُرئ Latin-1 يبقى **بايتاته الأصلية سليمة**، وكل ما
   تغيّر أن كل بايت صار محرفاً. فالكشف ليس تعرّفاً على أشكال المحارف — وهو
   ما يُهزَم بأول شكلٍ لم يُكتب — بل **إعادة فكّ الترميز فعلاً**: تُقرأ رموز
   المقطع بايتات، وتُفكّ UTF-8 **بالوضع الصارم**؛ فإن نجح الفكّ وأعطى نصّاً
   مختلفاً، فالمقطع بايتاتُ نصٍّ آخر لا نصٌّ.

   ‏وهذا يُصيب أي لغةٍ مصدرٍ كانت — عربيةً أو ديفاناغارية أو يونانية — بلا
   ذكر أيٍّ منها، ويُصيب الجزء المشوَّه من قيمةٍ سليمة بقيّتها.

   ‏ولا يُنذَر خطأً: نصٌّ ASCII محضٌ يفكّ إلى نفسه فلا اختلاف؛ ومحارف مثل
   ‏`·` و`«` و`»` بايتاتُها تتمّاتٌ بلا رأس فيُخفق الفكّ الصارم؛ وأي محرف فوق
   ‏U+00FF لا يكون بايتاً أصلاً فيقطع المقطع.

   The detector decodes rather than recognises: a Latin-1-only run whose own
   code points, read as bytes, are valid UTF-8 that yields different text was
   never text — it is another text's bytes. No character shapes are listed.
   ═══════════════════════════════════════════════════════════════════════ */

const STRICT_UTF8 = new TextDecoder("utf-8", { fatal: true });

/** يقلب نصّاً سليماً إلى تشويهه — آليّةُ العطل نفسها، تُستعمل شاهداً إيجابياً. */
export function mangle(text) {
  const bytes = new TextEncoder().encode(text);
  let out = "";
  for (const b of bytes) out += String.fromCharCode(b);
  return out;
}

/**
 * مقاطع التشويه في نصّ. لكلٍّ `{ run, decoded }` — المقطع كما هو، وما يعنيه فعلاً.
 * Mojibake runs, each with what the bytes actually say.
 */
export function mojibakeRuns(text) {
  const hits = [];
  let run = "";
  const flush = () => {
    if (run.length >= 2 && /[\u0080-\u00ff]/u.test(run)) {
      const bytes = new Uint8Array(run.length);
      for (let i = 0; i < run.length; i++) bytes[i] = run.charCodeAt(i);
      try {
        const decoded = STRICT_UTF8.decode(bytes);
        if (decoded !== run) hits.push({ run, decoded });
      } catch {
        /* ليست UTF-8 صالحة — فليست بايتات نصٍّ آخر. */
      }
    }
    run = "";
  };
  for (const ch of String(text)) {
    if (ch.codePointAt(0) <= 0xff) run += ch;
    else flush();
  }
  flush();
  return hits;
}

/** كل النصوص داخل قيمة مفتاح: نصٌّ مفرد، أو كيس جمع، أو مصفوفة. */
export function valueTexts(value) {
  if (typeof value === "string") return [value];
  if (Array.isArray(value)) return value.filter((v) => typeof v === "string");
  if (value && typeof value === "object") return Object.values(value).filter((v) => typeof v === "string");
  return [];
}
