# الحكم: هل يستحق Marten مكانه بعد جعل دفتر الأستاذ علائقياً؟
# Verdict: with the ledger relational, does Marten still earn its place?

**التوصية: أسقِط Marten. أبقِ Wolverine وEF Core 10 وPostgreSQL خالصة.**
**Recommendation: DROP Marten. Keep Wolverine, EF Core 10 and plain PostgreSQL.**

هذا الحكم مبني على 27 إثباتاً نفّذها هذا الاختبار فعلياً على .NET 10.0.11 وPostgreSQL 16.13،
لا على تفضيل معماري. الأمر الحاسم كان (أ): **صندوق Wolverine الصادر المعاملاتي الدائم لا
يحتاج Marten على الإطلاق** — لا في شجرة الحزم (89 حزمة، صفر منها Marten) ولا في زمن التشغيل.
لو كان يحتاجه لانقلبت التوصية كلياً.

This verdict rests on 27 proofs actually executed against .NET 10.0.11 and PostgreSQL 16.13,
not on architectural taste. The decisive item was (A): **Wolverine's durable transactional
outbox does not need Marten at all** — not in the 89-package dependency graph (zero Marten
packages) and not at runtime. Had it needed Marten, the recommendation would have flipped.

---

## 1. ما الذي يقدّمه Marten ولم يُعِده هذا الاختبار
## 1. What Marten uniquely provides that this proof did NOT replicate

أرتّبها بصدق من الأهم إلى الأقل بالنسبة لهذا المشروع تحديداً:

| # | القدرة | هل نحتاجها فعلاً هنا؟ |
|---|---|---|
| 1 | **نقاط تفتيش الإسقاطات** (`mt_event_progression`): علامة مائية لكل إسقاط تسمح باستئنافه من حيث توقّف بالضبط | **هذه هي الفجوة الحقيقية الوحيدة.** بديلها جدول `last_event_seen` نكتبه بأنفسنا؛ عمل بسيط لكنه ليس صفراً، ومن السهل إخفاق ترتيبه (معرّف أصغر يُثبَّت بعد معرّف أكبر) |
| 2 | **خفي الإسقاطات غير المتزامن** بالتجميع والضغط العكسي وسياسات الأخطاء وتقسيم الشرائح | إسقاطات «سرد العمليات» عندنا فورية وضئيلة (عمود حالة واحد + عرض `DISTINCT ON`). ولا ننسى أن **افتراضاته الصامتة هي سبب هذه المراجعة أصلاً** |
| 3 | **أدوات إعادة بناء الإسقاطات** بتقدّم مرئي وإعادة بناء أثناء التشغيل | بديلها المُثبَت أعلاه `REFRESH MATERIALIZED VIEW`. كافٍ لملايين الصفوف، أضعف فعلاً عند مئات الملايين |
| 4 | **تنسيق تعدد النسخ** (انتخاب قائد للخفي) | Wolverine موجود أصلاً ويفعل انتخاب القائد وتوزيع الوكلاء (جداول `wolverine_nodes` و`wolverine_node_assignments` الظاهرة في (أ)) |
| 5 | **واجهة `Patch`**: تعديل جزئي لمستند دون تحميله | فجوة حقيقية صغيرة: وجدنا أن EF Core 10 + Npgsql **يعيد كتابة عمود jsonb كاملاً**. لكن `jsonb_set` الخام (مُثبَت في D3) يغطيها بسطر SQL واحد |
| 6 | **بحث نصّي كامل، حذف ناعم، تسلسل هرمي للمستندات، فهارس محسوبة، استعلامات مُصرَّفة** فوق المستندات | لا شيء منها في مسار العمل الحالي؛ وPostgreSQL يقدّم `tsvector` مباشرة عند الحاجة |
| 7 | **تجميع حيّ للتيّار** (`AggregateStreamAsync`) وتحكم تفاؤلي بإصدار التيّار | التحكم التفاؤلي مُغطّى بقيد `unique (stream_id, stream_seq)`؛ التجميع الحيّ سطران في C# لتيّارات من 2–20 حدثاً |
| 8 | **أرشفة الأحداث وضغط التيّارات** | غير مطلوبة، وهي بالضبط ما يجبر Marten على إصدار `UPDATE`/`DELETE` على `mt_events` |

**الأمانة تقتضي القول:** البنود 1 و3 و5 قدرات حقيقية سنعيد بناء نسخ أبسط منها بأنفسنا.
هذا ليس مجانياً. لكنه محدود وقابل للقياس، وليس نظاماً فرعياً كاملاً.

**In fairness:** items 1, 3 and 5 are real capabilities we will re-implement in simpler form.
That is not free. It is, however, bounded and measurable — not a subsystem.

---

## 2. تكاليف إبقاء Marten / the cost of keeping Marten

1. **نموذج استمرارية ثانٍ.** بما أن دفتر الأستاذ علائقي، أصبح EF Core إلزامياً. إبقاء
   Marten يعني نموذجَي بيانات، وقصّتَي ترحيل مخطط، وطريقتَي استعلام، ومالكَي معاملة،
   وطريقتَي تعدد مستأجرين، في فريق واحد. الاختبار (أ5) يثبت أن Wolverine يتعامل مع
   `DbContext` كمالك معاملة من الدرجة الأولى، فلا يوجد سبب **رسائلي** لإبقاء Marten.

2. **سوق التوظيف.** خبرة EF Core متوفّرة بغزارة في السوق السعودي والعربي؛ خبرة Marten
   نادرة. لنظام محاسبي عمره عشر سنوات مع دوران موظفين، هذا عامل تشغيلي لا تجميلي.

3. **الافتراضات الصامتة.** خفي الإسقاطات غير المتزامن يفترض
   `SkipApplyErrors = SkipSerializationErrors = SkipUnknownEvents = true`، أي أن حدث
   دفتر أستاذ يفشل تطبيقه يُرحَّل إلى الرسائل الميتة **بصمت**. هذا سلوك افتراضي غير مقبول
   في نظام محاسبي، ويمكن تغييره — لكن كونه الافتراضي يعني أن أي مطوّر جديد سيصيبه.

4. **الحصانة غير قابلة للتحقيق على `mt_events`.** Marten نفسه يصدر `UPDATE`/`DELETE`
   على `mt_events` (الأرشفة، الإخفاء، شواهد الحذف)، فلا يمكن سحب هاتين الصلاحيتين أبداً.
   الاختبار (ب2)/(ب3)/(ج3) يُظهر البديل: `REVOKE UPDATE, DELETE` صريح، ورفض من PostgreSQL
   نفسه برمز `42501` عبر EF Core وعبر SQL الخام على السواء. **هذا الفارق هو جوهر القضية
   بالنسبة لجهة تدقيق أو لهيئة الزكاة والضريبة والجمارك.**

5. **`FlatTableProjection` لا يصل إلى بيانات الحدث الوصفية** (الزمن، معرّف الارتباط،
   الرؤوس)، وهي بالضبط الحقول التي يريدها جدول تدقيق مسطّح.

6. **سطح اعتماديات أكبر** ودورة إصدار إضافية.

**تحفّظ صريح على حجّتنا:** إسقاط Marten **لا يُخرجنا** من منظومة JasperFx. فـWolverine
يعتمد على `JasperFx` و`Weasel` من الفريق نفسه وبدورة الإصدار نفسها. من يبقي Wolverine لا
يستطيع أن يزعم أنه تخلّص من مخاطرة المورّد الواحد؛ هو فقط قلّل سطحها.

**An honest caveat against our own argument:** dropping Marten does **not** remove the
JasperFx ecosystem. Wolverine depends on `JasperFx` and `Weasel`, same team, same release
cadence. Keeping Wolverine means the single-vendor risk is reduced, not eliminated.

---

## 3. القرار / the decision

**أسقِط Marten. لا دور ضيّق له في هذه الحزمة.**

نظرنا في دورين ضيّقين محتملين وأسقطناهما بناءً على الإثباتات:

- **مخزن مستندات للإعدادات ونماذج المستأجرين؟** الإثبات (د) يُظهر أن EF Core 10 + Npgsql
  يربط رسم POCO كاملاً (بما فيه القوائم المتداخلة) بعمود `jsonb` واحد عبر `ToJson()`،
  ويستعلم داخل قيمة متداخلة عبر فهرس تعبيري، ويحدّث قيمة متداخلة. إضافة نموذج استمرارية
  ثانٍ من أجل واجهة `Patch` وحدها ليست مقايضة عادلة؛ `jsonb_set` يكفي.
- **مخزن أحداث لسرد العمليات؟** الإثبات (ج) يُظهر جدولاً علائقياً واحداً يفعل ما نحتاجه
  **ويضيف قدرة لا يملكها Marten**: إمكانية سحب `UPDATE` و`DELETE` فعلياً. بالنسبة لسجل
  الاعتمادات وإرسال فواتير هيئة الزكاة والضريبة والجمارك، هذه ميزة وليست تنازلاً.

**Drop Marten. There is no narrow role left for it in this stack.**

---

## 4. ما الذي يغيّر هذا الحكم / what would change my answer

سأعود عن التوصية إذا تحقّق أيٌّ من هذه — وهي معايير قابلة للقياس، لا انطباعات:

1. **ظهور ثلاثة إسقاطات غير متزامنة ثقيلة أو أكثر** (لا مجرد عمود حالة)، أو تيّارات
   تتجاوز ~100,000 حدث لكل تيّار. عندها تصبح نقاط التفتيش والخفي وإعادة البناء نظاماً
   فرعياً حقيقياً نبنيه، وMarten يصبح أرخص من كتابته.
2. **اعتماد تعدّد المصادر (event sourcing) نموذجاً أساسياً** لنظام فرعي كبير — المخزون
   أو التصنيع مثلاً — بدل كونه سجلاً سردياً.
3. **إضافة Marten وضع «إضافة فقط»** لا يُصدر فيه `UPDATE`/`DELETE` على `mt_events` ويسمح
   بسحب الصلاحيتين، **مع** تحويل افتراضات `SkipApplyErrors`/`SkipSerializationErrors`/
   `SkipUnknownEvents` إلى الفشل الصريح. هاتان النقطتان هما أساس القضية ضده؛ زوالهما يقلب
   الميزان.
4. **انقلاب سوق التوظيف**: إن تبيّن أن الفريق الفعلي يملك خبرة Marten عميقة أصلاً.
5. **قرار قاعدة بيانات لكل مستأجر** يجعل إدارة Marten لقواعد المستأجرين حاسمة — لكن
   يجب أولاً تقييم `AddDbContextWithWolverineManagedMultiTenancy` و
   `AddDbContextWithWolverineManagedConjoinedTenancy` في `WolverineFx.EntityFrameworkCore`
   6.29.2، فهما موجودتان ولم نختبرهما هنا.
6. **إخفاق قياس الإنتاجية على أجهزة حقيقية**: أرقامنا (285 قيد/ث لكل دفتر، ~1266 قيد/ث
   بعدّاد لكل دفتر) مأخوذة على 4 vCPU. إن لم تتحسّن على عتاد الإنتاج فالمشكلة في العدّاد
   بلا فجوات لا في Marten، وتغيير مخزن المستندات لن ينقذنا.

---

## 5. ماذا نفعل غداً / what to do next

1. اعتمد `WolverineFx` + `WolverineFx.Postgresql` + `WolverineFx.EntityFrameworkCore`
   6.29.2 مع EF Core 10.0.11 وNpgsql 10.0.3.
2. في الإنتاج: `dotnet run -- codegen write` مع
   `opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static` لإسقاط `WolverineFx.RuntimeCompilation`
   ومعها Roslyn.
3. اجعل دور التطبيق `INSERT` + `SELECT` فقط على `journal_entry` و`journal_line` و
   `process_event`، مع `REVOKE UPDATE, DELETE, TRUNCATE` صريحة، وضع الترحيلات تحت دور مالك منفصل.
4. اجعل العدّاد بلا فجوات لكل (مستأجر × دفتر) لا عاماً.
5. اقصّ كل الأوقات إلى الميكروثانية قبل التجزئة (`Canonical.PgInstant`)، واعتمد المقياس 4
   شكلاً قانونياً للمبالغ، وارفض محارف التحكم الاتجاهية عند الإدخال.
6. انشر بصمة رأس السلسلة يومياً إلى العميل وإلى شاهد خارجي؛ هذا ما يحوّل السلسلة من
   كشف عبث إلى منع عبث.
7. اكتب جدول نقاط تفتيش صغيراً (`projection_checkpoint(name, last_event_id, updated_at)`)
   قبل أول إسقاط غير متزامن، لا بعده.
