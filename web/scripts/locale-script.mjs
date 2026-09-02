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

/**
 * ‏**إحصاء الحروف بثلاث خانات**: بخطّ اللغة، وبخطٍّ أجنبيّ، و**آليّ**.
 *
 * ‏والخانة الثالثة ليست تلطيفاً: «‏PDF» و«‏LTR» ليستا نثرَ لغةٍ أخرى بل رمزاً
 * آلياً، والمستودع يقول ذلك في موضعٍ آخر أصلاً — `foreignRuns` تشترط
 * `>= 0x80`، و`isDiagnostic` تُخرج اللاتينية من الشهادة لأنها خطّ المعرّفات.
 * فلمّا صار الحكم بالأغلبية ظهر أن الخانتين لا تكفيان: «ملف PDF» ثلاثةُ حروفٍ
 * عربية وثلاثةٌ لاتينية، فتعادلٌ يُسقط قيمةً عربيةً سليمة. والحلّ ليس عتبةً
 * أرحم بل **مفهوماً واحداً للأجنبيّ** في كل مواضع هذا الملفّ.
 *
 * Three buckets, not two: own-script, foreign prose, and machine (ASCII) —
 * the last is what `foreignRuns` already excluded and what makes Latin the
 * identifier script everywhere else in this guard.
 */
export function census(text, code) {
  const own = scriptMatcher(scriptOf(code));
  let inScript = 0;
  let foreign = 0;
  let machine = 0;
  for (const ch of prose(text)) {
    if (!LETTER.test(ch)) continue;
    if (own.test(ch)) inScript++;
    else if (ch.codePointAt(0) < 0x80) machine++;
    else foreign++;
  }
  return { letters: inScript + foreign + machine, inScript, foreign, machine };
}

/**
 * ‏**أهذه القيمة مكتوبةٌ بخطّ لغتها؟** — بالأغلبية، لا بحرفٍ واحد.
 *
 * ‏**العطل الذي أُغلق هنا:** كانت القاعدة `census(text, code).inScript > 0`،
 * أي أن **حرفاً واحداً** يُرخّص للقيمة كلَّها. مقيس: بإلصاق حرفٍ ديفاناغاريٍّ
 * واحد بنثرٍ عربيّ في قيمةٍ هندية تصير الحصيلة `letters=83 · inScript=1 ·
 * foreign=82` ويخرج `audit.mjs` بالرمز **صفر**. والصورة الواقعية — أن تُترجَم
 * الكلمة الأولى وحدها ويبقى الباقي عربياً — تمرّ كذلك. أي أن القاعدة **حسبت
 * العدد ثم رمته**.
 *
 * ‏**والأغلبية ليست عتبةً مختارة**: هي السؤال نفسه معكوساً — «بأيّ خطٍّ كُتبت
 * هذه القيمة؟» يُجاب بأكثر حروفها، لا بأقلّها. ولا رقمَ يُعايَر: `inScript >
 * foreign` وحدها، والتعادلُ يسقط لأن قيمةً نصفُها بخطٍّ آخر ليست مكتوبةً بخطّ
 * لغتها بأي معنى مفيد.
 *
 * The value must be *written in* its locale's script — decided by the majority
 * of its letters, not by the existence of one.
 */
export function hasOwnScript(text, code) {
  const { inScript, foreign } = census(text, code);
  return inScript > foreign;
}

/**
 * ‏أفي النصّ حرفٌ واحد بخطّ لغته؟ — القاعدة الضعيفة، تبقى **مسمّاةً** لأنها
 * تصلح لغرضٍ واحد: انتقاءُ عيّنةٍ للشواهد. ولا تصلح حكماً، وهذا سبب اسمها.
 */
export function carriesOwnScript(text, code) {
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

/**
 * ‏يقلب نصّاً إلى تشويهه تحت ترميزٍ أحادي البايت مسمّى — للشواهد وحدها، لا للكشف.
 * وينبّه أن `TextDecoder("latin1")` في معيار الترميز **اسمٌ لـwindows-1252** لا
 * لـLatin-1 الحقيقية؛ ولذلك `mangle` أعلاه تبني البايتات بيدها ولا تستعمله.
 */
export function mangleUnder(label, text) {
  return new TextDecoder(label).decode(new TextEncoder().encode(text));
}

/* ══════════ الحروف الأجنبية غير الآلية · non-machine foreign letters ═══════
   ‏القاعدة (أ) تفكّ الترميز، فتُصيب التشويه الذي بقيت بايتاته ≤ U+00FF. وما
   قُرئ بترميزٍ يرفع بعض البايتات فوق ذلك — `koi8-r`، `macintosh`،
   `iso-8859-7` — ينكسر مقطعُه فيفلت منها، وكذلك بايتات UTF-16. **مقيس.**

   ‏فالقاعدة الثانية تنظر إلى الحرف لا إلى البايت، وتقسم حروف أي قيمة إلى
   ثلاثة أقسام مغلقة **وما لا يُصنَّف يسقط**:
     ١ · حرفٌ بخطّ لغته — سليم.
     ٢ · حرفٌ رمزُه < U+0080 — أبجدية المعرّفات: `PDF`, `SAR`, `BANK-0001`,
          `INV-2026-0587`. وهي ASCII **بحكم العقد المنشور** لا بحكم الذوق.
     ٣ · ما بقي: حرفٌ أجنبيّ غير آليّ. وهو **مسموحٌ بشرطٍ واحد**: أن يظهر
          المقطع نفسه حرفاً بحرف في قيمة المفتاح نفسه **في لغةٍ أخرى**. فالرمز
          المشترك يُكتب في اللغات (‏`Delta` اليونانية في الأربع، والكلمتان
          اللتان ينطقهما المستخدم داخل النصّ الهنديّ)؛ أما التشويه فلا يُصادف
          مطلقاً أن يُنسخ حرفاً بحرف في ملفّ لغةٍ ثانية.

   ‏ولأن هذا **إذنٌ مغلق** لا **منعٌ مفتوح**، فأيّ ترميزٍ لم يخطر لأحد يسقط فيه
   تلقائياً: لا يحتاج الحارس أن يعرف الترميز ليرفض ناتجه.
   ══════════════════════════════════════════════════════════════════════════ */

/** مقاطع الحروف الأجنبية غير الآلية في نصّ. */
export function foreignRuns(text, code) {
  const own = scriptMatcher(scriptOf(code));
  const runs = [];
  let run = "";
  for (const ch of prose(text)) {
    const foreign = LETTER.test(ch) && ch.codePointAt(0) >= 0x80 && !own.test(ch);
    if (foreign) run += ch;
    else {
      if (run) runs.push(run);
      run = "";
    }
  }
  if (run) runs.push(run);
  return runs;
}

/**
 * ‏**هل يُصدِّق هذا المقطعَ الأجنبيَّ نصٌّ من لغةٍ أخرى؟**
 *
 * ‏**العطل الذي أُغلق هنا:** كان التصديق «ظهر المقطع تحت المفتاح نفسه في لغةٍ
 * أخرى». وقيمةٌ **لم تُترجَم** هي نصُّ العربية حرفاً بحرف — فمقاطعها العربية
 * تظهر في `ar.web.ts` تحت المفتاح نفسه **دائماً**، فيُصدَّق كلُّ مقطعٍ فيها.
 * أي أن قاعدة الإذن كانت تُصدِّق العطلَ نفسه الذي وُضعت له.
 *
 * ‏**والقاعدة الصحيحة:** لا يُصدِّق المقطعَ إلّا من هو **أجنبيٌّ عنده أيضاً**.
 * فاسمُ علامةٍ لاتينيٌّ في قيمةٍ هندية يظهر في العربية وهو أجنبيٌّ فيها ⇒
 * مُصدَّق؛ ومقطعٌ عربيٌّ في قيمةٍ هندية يظهر في العربية وهو **خطّها** ⇒ غير
 * مُصدَّق، وهو بالضبط النصّ غير المترجَم.
 */
export function corroborates(run, otherCode, otherTexts) {
  return otherTexts.some((t) => t.includes(run)) && foreignRuns(run, otherCode).length > 0;
}

/* ══════════ محارف لا تُرسم ولا تعني · characters that render nothing ═══════
   ‏محارف التحكّم C0/C1، والبدائل، والاستعمال الخاصّ، وغير المخصَّص. لا واحد
   منها يظهر في نصٍّ كتبه إنسان، وكلُّها تظهر في بايتاتٍ قُرئت بترميزٍ خطأ —
   وبايتات UTF-16 تحديداً تُنتج محرف تحكّم لكل محرف نصّي. والفئة من يونيكود لا
   من قائمة، والفحص ٨ يمسح المصدر كلَّه بنطاقاتٍ **أخرى** (علامات الاتجاه
   والصفر العرض) فلا يتقاطع معها.
   ══════════════════════════════════════════════════════════════════════════ */
export const JUNK_RE = /[\p{Cc}\p{Cn}\p{Co}\p{Cs}]/u;

/** محارف الحشو في نصّ، مع مواضعها. */
export function junkChars(text) {
  const found = [];
  let at = 0;
  for (const ch of String(text)) {
    if (JUNK_RE.test(ch)) found.push({ ch, at, code: ch.codePointAt(0) });
    at += ch.length;
  }
  return found;
}

/** كل النصوص داخل قيمة مفتاح: نصٌّ مفرد، أو كيس جمع، أو مصفوفة. */
export function valueTexts(value) {
  if (typeof value === "string") return [value];
  if (Array.isArray(value)) return value.filter((v) => typeof v === "string");
  if (value && typeof value === "object") return Object.values(value).filter((v) => typeof v === "string");
  return [];
}
