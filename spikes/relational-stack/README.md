# اختبار استكشافي: هل يستحق Marten مكانه بعد جعل دفتر الأستاذ علائقياً؟
# Spike: with the ledger relational, does Marten still earn its place?

> **الخلاصة / bottom line:** كل الإثباتات (أ–هـ) نجحت **بدون Marten إطلاقاً**.
> صندوق Wolverine الصادر المعاملاتي الدائم لا يحتاج Marten، وEF Core 10 قادر على
> دفتر أستاذ يُضاف إليه فقط، وسجل أحداث علائقي، ومستندات JSONB لكل مستأجر.
>
> Every proof (A–E) passes **with no Marten in the dependency graph at all**.
> Wolverine's durable transactional outbox does not need Marten; EF Core 10 can own
> an append-only ledger, a relational event log, and per-tenant JSONB documents.

---

## 1. التشغيل بأمر واحد / one command

```bash
./run.sh
```

يطبع جدول PASS/FAIL للإثباتات (أ) إلى (هـ)، وجدول الإصدارات، وأرقام الإنتاجية،
ويُنهي العملية برمز خروج `0` فقط عند نجاح كل الإثباتات.

Prints the PASS/FAIL table for (A)–(E), the resolved versions table and the write
throughput numbers, and exits `0` only when every proof passes.

خيارات / options:

```bash
./run.sh --only=A          # إثبات واحد فقط / a single section
./run.sh --only=AB         # عدّة أقسام / several sections
./run.sh --no-bench        # تخطّي قياس الإنتاجية / skip the throughput run
./versions.sh              # جدول الحزم المحلولة فعلياً / the resolved-package table
```

مدة التشغيل الكاملة نحو **دقيقة إلى دقيقتين** (منها ~20 ثانية انتظار مقصود في
إثبات التراجع، و~13 ثانية تحميل 500,000 صف اختباري).
Full run takes roughly **1–2 minutes**, of which ~20 s is the deliberate wait in the
rollback proof and ~13 s is loading 500,000 synthetic rows.

## 2. المتطلبات وإعداد قاعدة البيانات (للتطوير المحلي فقط)
## 2. Prerequisites and dev-only database setup

- .NET SDK 10.0.x
- PostgreSQL 14+ (طُوِّر واختُبِر على 16.13 / developed and verified on 16.13)

**لا توجد أي كلمة مرور في هذا المستودع.**
**No password is stored anywhere in this repository.**

يقرأ الاختبار متغيّرَي بيئة، ولهما قيمة افتراضية محلية بلا كلمة مرور:
The spike reads two environment variables, each with a password-less local default:

| متغيّر / variable | الدور / role | الافتراضي / default |
|---|---|---|
| `BABEL_RELSPIKE_ADMIN_DB` | مالك المخطط: ينشئ الجداول والأدوار والصلاحيات، ويقوم بدور «العميل الذي يعبث بالبيانات» في (هـ) | `Host=127.0.0.1;Port=5432;Database=babel_relspike;Username=postgres` |
| `BABEL_RELSPIKE_APP_DB` | دور التطبيق الأقل امتيازاً: `INSERT` و`SELECT` فقط | `Host=127.0.0.1;Port=5432;Database=babel_relspike;Username=babel_ledger_app` |

```bash
export BABEL_RELSPIKE_ADMIN_DB="Host=127.0.0.1;Port=5432;Database=babel_relspike;Username=postgres;Password=<your-local-dev-password>"
export BABEL_RELSPIKE_APP_DB="Host=127.0.0.1;Port=5432;Database=babel_relspike;Username=babel_ledger_app;Password=<your-local-dev-password>"
```

الاختبار ينشئ قاعدة البيانات `babel_relspike` والدور `babel_ledger_app` تلقائياً إن
لم يكونا موجودين، ثم **يحذف ويعيد إنشاء** المخططين `ledger` و`app` في كل تشغيلة.
استخدم قاعدة بيانات مخصّصة للتجارب.

The spike creates the `babel_relspike` database and the `babel_ledger_app` role if
they are missing, then **drops and recreates** the `ledger` and `app` schemas on every
run. Use a throwaway database.

للتطوير المحلي فقط يمكن السماح بالاتصال بلا كلمة مرور عبر `pg_hba.conf`
(لا تفعل هذا على خادم مشترك أو إنتاجي):
For local development only you may allow password-less loopback connections in
`pg_hba.conf` (never on a shared or production server):

```
host    all    all    127.0.0.1/32    trust
```

ثم / then: `sudo pg_ctlcluster 16 main reload`

---

## 3. ما الذي أُثبت / what was proven

| # | الإثبات / proof | النتيجة |
|---|---|---|
| **A1** | مخزن رسائل Wolverine الدائم في PostgreSQL موجود ويعمل بدون Marten | PASS |
| **A2** | صف الظرف الصادر يُكتب **داخل** معاملة العمل نفسها (مرئي على الاتصال نفسه قبل الـcommit، غير مرئي لغيره) | PASS |
| **A3** | رسالة نُشرت داخل معاملة مُثبَّتة **تُسلَّم** | PASS |
| **A4** | رسالة نُشرت داخل معاملة متراجعة **لا تُسلَّم أبداً** (انتظار 20 ثانية، وجدول الظروف الصادرة نظيف) | PASS |
| **A5** | `DbContext` في EF Core 10 مالك معاملة من الدرجة الأولى للصندوق الصادر | PASS |
| **A6** | معاملة يرفضها PostgreSQL **عند الـCOMMIT**: تختفي صفوف العمل والرسالة معاً | PASS |
| **B0** | دور التطبيق ليس superuser وليس مالكاً: `INSERT` و`SELECT` فقط | PASS |
| **B1** | قيد متوازن يُدرج عبر EF Core بدور التطبيق | PASS |
| **B2** | `UPDATE` على سطر مُرحَّل يرفضه PostgreSQL نفسه (42501) | PASS |
| **B3** | `DELETE` و`TRUNCATE` مرفوضان كذلك (42501) | PASS |
| **B4** | مشغّل `DEFERRABLE INITIALLY DEFERRED` يرفض فرقاً قدره 0.0001 عند الـCOMMIT، مهما كان مسار الشيفرة | PASS |
| **B5** | `decimal` يدور ذهاباً وإياباً عبر EF Core بلا فقدان قيمة (مع ملاحظة صريحة حول المقياس) | PASS |
| **C1** | EF Core 10 يكتب ويقرأ حمولة JSONB متعددة الأشكال | PASS |
| **C2** | استعلام EF Core داخل JSONB يخدمه فهرس GIN | PASS |
| **C3** | سجل الأحداث يُضاف إليه فقط: `UPDATE`/`DELETE` مسحوبتان | PASS |
| **C4** | إعادة بناء الحالة الحالية من السجل (في C# وفي SQL) | PASS |
| **D1** | EF Core 10 يربط رسم POCO كاملاً بعمود jsonb واحد عبر `ToJson()` | PASS |
| **D2** | استعلام EF Core على قيمة **متداخلة** يستخدم فهرس تعبيري | PASS |
| **D3** | تحديث قيمة متداخلة، عبر EF Core وعبر `jsonb_set` على الخادم | PASS |
| **D4** | مستأجران بشكلَي مستند مختلفين، بلا أي ترحيل مخطط | PASS |
| **E1** | `SEQUENCE` في PostgreSQL يُهدر أرقاماً عند التراجع (ولهذا لا نستخدمها) | PASS |
| **E2** | عدّاد `SELECT ... FOR UPDATE` بلا فجوات تحت 8 كتّاب متزامنين | PASS |
| **E3** | التوحيد القياسي: مبالغ بمقياس ثابت ولغة ثابتة، أوقات UTC، نص عربي بـNFC | PASS |
| **E4** | محرف U+200F غير مرئي داخل النص العربي **يغيّر البصمة** | PASS |
| **E5** | سلسلة SHA-256 تُتحقَّق من أولها إلى آخرها | PASS |
| **E6** | عبث مباشر بـSQL من **مالك الجدول** يُكتشَف ويُسمّى أول تسلسل منحرف | PASS |
| **E7** | حتى لو أعاد العابث حساب `entry_hash`، تنكسر السلسلة عند القيد التالي | PASS |

### 3.1 (A) هو البند الحاسم / (A) is the decisive item

**صندوق Wolverine الصادر الدائم لا يحتاج Marten.** الحزمة `WolverineFx.Postgresql`
تعتمد فقط على `WolverineFx.RDBMS` و`Weasel.Postgresql`، ولا تعتمد على Marten لا
مباشرة ولا بشكل غير مباشر. الشجرة كاملة (89 حزمة) لا تحتوي على أي حزمة باسم Marten،
والاختبار يتحقق وقت التشغيل من عدم تحميل أي تجميعة باسم `Marten*`.

**Wolverine's durable outbox does not require Marten.** `WolverineFx.Postgresql`
depends only on `WolverineFx.RDBMS` and `Weasel.Postgresql`. The full 89-package graph
contains no Marten package, and the spike asserts at runtime that no `Marten*` assembly
is loaded.

جداول المخزن التي أنشأتها `WolverineFx.Postgresql` في PostgreSQL:
`wolverine_incoming_envelopes`, `wolverine_outgoing_envelopes`, `wolverine_dead_letters`,
`wolverine_nodes`, `wolverine_node_assignments`, `wolverine_node_records`,
`wolverine_control_queue`, `wolverine_agent_restrictions`.

**مصيدة يجب توثيقها / a gotcha worth writing down:**
`IDbContextOutbox<T>.SaveChangesAndFlushMessagesAsync()` **يُثبِّت** المعاملة القائمة
لـEF Core ثم يُفرغ الرسائل. إن أردت التحكم اليدوي بالتراجع فاستخدم المسار الخام
`MessageContext.EnlistInOutboxAsync(new DatabaseEnvelopeTransaction(store, tx))` كما في
(A2)/(A4).

---

## 4. جدول الإصدارات / versions table

مُولَّد من `obj/project.assets.json` عبر `./versions.sh`. العمود الأخير هو إطار العمل
الذي أُخذت منه تجميعات الحزمة فعلياً — أي دليل حقيقي على وجود `net10.0` أصلي.

Generated from `obj/project.assets.json` by `./versions.sh`. The last column is the TFM
whose assemblies were actually selected — the real evidence of native `net10.0` support.

### الحزم المُعلَنة مباشرة / directly referenced

| PACKAGE | VERSION | ASSETS TAKEN FROM | native net10.0 |
|---|---|---|---|
| WolverineFx | 6.29.2 | `lib/net10.0` | yes |
| WolverineFx.Postgresql | 6.29.2 | `lib/net10.0` | yes |
| WolverineFx.EntityFrameworkCore | 6.29.2 | `lib/net10.0` | yes |
| WolverineFx.RuntimeCompilation | 6.29.2 | `lib/net10.0` | yes |
| Microsoft.EntityFrameworkCore | 10.0.11 | `lib/net10.0` | yes |
| Microsoft.EntityFrameworkCore.Relational | 10.0.11 | `lib/net10.0` | yes |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | `lib/net10.0` | yes |
| Npgsql | 10.0.3 | `lib/net10.0` | yes |
| Microsoft.Extensions.Hosting | 10.0.0 | `lib/net10.0` | yes |

### الحزم غير المباشرة المهمة / notable transitives

| PACKAGE | VERSION | ASSETS TAKEN FROM | native net10.0 |
|---|---|---|---|
| WolverineFx.RDBMS | 6.29.2 | `lib/net10.0` | yes |
| Weasel.Postgresql | 9.25.1 | `lib/net10.0` | yes |
| Weasel.EntityFrameworkCore | 9.25.1 | `lib/net10.0` | yes |
| Weasel.Core | 9.25.1 | `lib/net10.0` | yes |
| JasperFx | 2.53.0 | `lib/net10.0` | yes |
| JasperFx.Events | 2.53.0 | `lib/net10.0` | yes |
| JasperFx.RuntimeCompiler | 5.0.0 | `lib/net10.0` | yes |
| Microsoft.EntityFrameworkCore.Abstractions | 10.0.11 | `lib/net10.0` | yes |
| Microsoft.EntityFrameworkCore.Design | 10.0.2 | `lib/net10.0` | yes |
| Microsoft.Extensions.* (all) | 10.0.0 / 10.0.11 | `lib/net10.0` | yes |
| Spectre.Console | 0.55.0 | `lib/net10.0` | yes |
| Microsoft.CodeAnalysis.* (Roslyn) | 5.0.0 | `lib/net9.0` | no — net9.0 |
| Polly.Core | 8.6.5 | `lib/net8.0` | no — net8.0 |
| DistributedLock.Postgres / .Core | 1.3.0 / 1.0.8 | `lib/net8.0` | no — net8.0 |
| Npgsql.NetTopologySuite | 9.0.4 | `lib/net6.0` | no — net6.0 |
| NetTopologySuite | 2.5.0 | `lib/netstandard2.0` | no — netstandard2.0 |
| Newtonsoft.Json | 13.0.3 | `lib/net6.0` | no — net6.0 |
| NewId | 4.0.1 | `lib/net6.0` | no — net6.0 |

**أمانةً:** كل حزم Wolverine وEF Core وNpgsql وWeasel وJasperFx تشحن تجميعات `net10.0`
أصلية. بعض الحزم غير المباشرة (Roslyn، Polly، NetTopologySuite، Newtonsoft.Json) ما
تزال `net8.0`/`net9.0`/`netstandard2.0`؛ هذا طبيعي ومدعوم تماماً على .NET 10، لكنه ليس
«net10.0 أصلي».

**Honestly:** every Wolverine, EF Core, Npgsql, Weasel and JasperFx package ships native
`net10.0` assemblies. Some transitives (Roslyn, Polly, NetTopologySuite, Newtonsoft.Json)
are still `net8.0`/`net9.0`/`netstandard2.0`. That is normal and fully supported on
.NET 10, but it is not "native net10.0".

**ملاحظة تشغيلية:** `WolverineFx.RuntimeCompilation` هي التي تجرّ Roslyn وSpectre.Console.
في الإنتاج استخدم `dotnet run -- codegen write` مع
`opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static` وأسقط الحزمة بالكامل.

---

## 5. أرقام الإنتاجية / write throughput

قيد واحد = رأس + 3 أسطر + `COMMIT` واحد، مع المشغّل المؤجّل والعدّاد بلا فجوات
وسلسلة SHA-256 كلها فعّالة.

One entry = 1 header + 3 lines + one `COMMIT`, with the deferred trigger, the gapless
counter and the SHA-256 chain all live.

| CONFIGURATION | WRITERS | ENTRIES | SECONDS | ENTRIES/S | p50 ms | p95 ms | max ms |
|---|---|---|---|---|---|---|---|
| chain + shared counter | 1 | 200 | 1.26 | 159 | 5.79 | 8.47 | 50.5 |
| chain + shared counter | 8 | 400 | 1.41 | 285 | 27.1 | 42.1 | 62.4 |
| chain + shared counter | 32 | 512 | 1.83 | 280 | 67.7 | 306.8 | 670.1 |
| chain + counter per book | 8 | 400 | 0.37 | 1081 | 6.97 | 11.7 | 17.4 |
| chain + counter per book | 32 | 512 | 0.40 | 1266 | 21.9 | 44.2 | 54.6 |
| no chain, no counter | 8 | 400 | 0.28 | 1417 | 5.33 | 8.07 | 12.0 |
| no chain, no counter | 32 | 512 | 0.27 | 1867 | 15.7 | 25.1 | 34.9 |

### كيف تُقرأ هذه الأرقام / how to read them

- **العدّاد بلا فجوات هو نقطة الاختناق، لا التجزئة (hashing).** دفتر واحد = صف عدّاد
  واحد = `SELECT ... FOR UPDATE`، فيتسلسل الكتّاب عمداً: ~285 قيد/ث عند 8 كتّاب مقابل
  ~1081 قيد/ث حين يكون لكل دفتر عدّاده. زيادة الكتّاب من 8 إلى 32 على دفتر واحد **لا
  تزيد الإنتاجية** (280 مقابل 285) بل تضاعف زمن الاستجابة فقط.
  The gapless counter, not SHA-256, is the bottleneck. Going from 8 to 32 writers on one
  book does not raise throughput at all — it only inflates latency.
- **تكلفة السلسلة نفسها متواضعة:** 1081 مقابل 1417 قيد/ث (‏~24%)، وأغلبها ذهاب وإياب
  إضافي إلى قاعدة البيانات (قراءة العدّاد، قراءة البصمة السابقة)، لا حساب SHA-256.
- **الأثر العملي:** اجعل العدّاد لكل (مستأجر × دفتر). عندها التسلسل يقع داخل دفتر
  المستأجر الواحد فقط، وهو ما يريده المدقّق أصلاً.

### تحفّظات صريحة / honest caveats

- 4 vCPU، 16 GB، جهاز تطوير مشترك؛ التطبيق وPostgreSQL على المضيف نفسه.
- `synchronous_commit = on`، `fsync = on`، `full_page_writes = on`،
  `shared_buffers = 16384 × 8kB (128 MB)`، `wal_compression = off`،
  `data_checksums = off`، `max_connections = 100`. كل قيد يدفع ثمن fsync واحد على الأقل.
- بلا connection pooler، بلا تجميع دفعات، بلا ضبط WAL، ذاكرات باردة.
- **اعتبرها أرقاماً نسبية لا مطلقة.** القيمة هنا في المقارنة بين الصفوف السبعة.

---

## 6. النتائج الصريحة التي كلّفتنا وقتاً / honest findings the spike cost us

هذه أخطاء حقيقية وقعت أثناء بناء هذا الاختبار، ووُثِّقت هنا حتى لا يقع فيها الفريق:

1. **دقة الوقت.** `timestamptz` في PostgreSQL يخزّن **بالميكروثانية**، بينما
   `DateTime` في .NET يخزّن بـ100 نانوثانية. البصمة المحسوبة قبل الحفظ لا تطابق أبداً
   البصمة المُعاد حسابها بعد القراءة. الحل: `Canonical.PgInstant()` يقصّ إلى الميكروثانية
   **قبل** التجزئة والحفظ.
   PostgreSQL `timestamptz` keeps microseconds; .NET `DateTime` keeps 100-ns ticks. Hash
   the untruncated value and the chain can never re-verify. Truncate before hashing.
2. **مقياس `numeric(19,4)` ثابت.** إدخال `100.00m` يعود `100.0000m`: القيمة متطابقة
   تماماً لكن `decimal.GetBits()` يختلف (بايت المقياس 2 مقابل 4). اعتمد المقياس 4 شكلاً
   قانونياً في نطاق النموذج، وإلا ستُبلّغ مقارنات البتات عن فروق وهمية.
3. **`jsonb` يعيد ترتيب المفاتيح.** لذا لا يأتي مميّز النوع `$type` أولاً، ويحتاج
   `System.Text.Json` إلى `AllowOutOfOrderMetadataProperties = true` (متاح منذ net9.0).
4. **EF Core 10 + Npgsql يعيد كتابة عمود jsonb كاملاً** عند تعديل قيمة متداخلة
   (`SET settings = @p0`)، ولا يُصدر `jsonb_set` جزئياً. للمستندات الكبيرة كثيرة التعديل
   استخدم `ExecuteUpdate` أو `jsonb_set` مباشرة.
5. **فهرس GIN يخزّن الإدخالات الجديدة في قائمة معلّقة (fastupdate).** بعد `COPY` كبير
   يبالغ المخطِّط في تكلفة الفهرس ويختار مسحاً تسلسلياً؛ `VACUUM ANALYZE` يُفرغ القائمة
   ويصلح الخطة.
6. **`Policies.UseDurableLocalQueues()` ضرورية**، وإلا كانت الطوابير المحلية في الذاكرة
   ولم يكن هناك صندوق صادر أصلاً.
7. **محارف التحكم الاتجاهية.** `U+200F` غير مرئي لكنه يغيّر البصمة. **التوصية: ارفضه عند
   الإدخال، لا تُزِله عند التجزئة** — لأن إزالته عند التجزئة تفتح ثغرة تسمح بإضافة أو حذف
   محارف غير مرئية دون كسر السلسلة.

---

## 7. الحكم / the verdict

مفصّل في [`VERDICT.md`](VERDICT.md).
The full recommendation is in [`VERDICT.md`](VERDICT.md).

---

## 8. خريطة الملفات / file map

| الملف | المحتوى |
|---|---|
| `Program.cs` | تركيب المضيف: Wolverine + PostgreSQL + EF Core 10، وتشغيل الإثباتات |
| `Db/Bootstrap.cs` | كل تعريفات المخطط: الجداول، المشغّل المؤجّل، الفهارس، ومنح/سحب الصلاحيات |
| `Db/Model.cs` | نموذج EF Core 10 (`numeric(19,4)`، `jsonb`، `ToJson()`) |
| `Db/Ledger.cs` | مسار الكتابة الوحيد للدفتر + مُتحقِّق السلسلة |
| `Db/ProcessEvents.cs` | أنواع حمولات «سرد العمليات» متعددة الأشكال |
| `Db/TenantSettings.cs` | مستند إعدادات المستأجر المربوط بـ`ToJson()` |
| `Support/Canonical.cs` | التوحيد القياسي وSHA-256 (المقياس الثابت، UTC، NFC، محارف bidi) |
| `Support/SqlCapture.cs` | التقاط SQL الذي يولّده EF Core لإعادة تشغيله تحت `EXPLAIN` |
| `Support/MartenGap.cs` | جرد صريح لما يقدّمه Marten ولا يقدّمه هذا الحل |
| `Proofs/ProofA_Outbox.cs` … `ProofE_HashChain.cs` | الإثباتات |
| `Proofs/Benchmark.cs` | قياس الإنتاجية |
