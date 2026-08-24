# عرض الشريحة الرأسية — الوثيقة التقنية
# Vertical slice demo — technical README

> للجولة الموجّهة لغير التقنيين انظر [DEMO.md](DEMO.md).
> For the stakeholder walkthrough see [DEMO.md](DEMO.md).

تطبيق واحد على `net10.0` يجمع ما أثبته [`spikes/relational-stack`](../../spikes/relational-stack)
في شريحة رأسية قابلة للنقر: دليل حسابات، شاشة قيد يومية، ميزان مراجعة، إجراءات خطرة،
وتحقّق من سلامة السلسلة.

## 1. التشغيل بأمر واحد

```bash
./demo.sh                 # يهيّئ + يبذر + يشغّل، ويطبع الرابط
./demo.sh --setup-only    # تهيئة وبذر فقط، بلا خادم
```

الرابط: `http://localhost:5099/` (غيّره بـ `BABEL_DEMO_PORT`).

**الحزمة:** ASP.NET Core minimal API على `net10.0` · EF Core 10.0.11 ·
Npgsql 10.0.3 · PostgreSQL 16. **بلا Marten** (انظر
[`spikes/relational-stack/VERDICT.md`](../../spikes/relational-stack/VERDICT.md)).

## 2. الاتصال بقاعدة البيانات — لا كلمات مرور في المستودع

**لا توجد أي كلمة مرور في هذا المستودع.** يقرأ التطبيق متغيّرَي بيئة، ولهما قيمة
افتراضية محلية بلا كلمة مرور:

| المتغيّر | الدور | الافتراضي |
|---|---|---|
| `BABEL_DEMO_ADMIN_DB` | مالك المخطط: DDL، الأدوار، الصلاحيات، بذر دليل الحسابات، وهو «العابث» في الخطوة ٦ | `Host=127.0.0.1;Port=5432;Database=babel_demo;Username=postgres` |
| `BABEL_DEMO_APP_DB` | دور التطبيق الأقل امتيازاً: `INSERT` و`SELECT` على الدفتر فقط | `Host=127.0.0.1;Port=5432;Database=babel_demo;Username=babel_demo_app` |
| `BABEL_DEMO_PORT` | منفذ الخادم | `5099` |

لكلمة مرور محلية:

```bash
export BABEL_DEMO_ADMIN_DB="Host=127.0.0.1;Port=5432;Database=babel_demo;Username=postgres;Password=<كلمة-مرور-محلية>"
export BABEL_DEMO_APP_DB="Host=127.0.0.1;Port=5432;Database=babel_demo;Username=babel_demo_app;Password=<كلمة-مرور-محلية>"
```

**للتطوير المحلي فقط** يمكن السماح بالاتصال بلا كلمة مرور عبر `pg_hba.conf`
(لا تفعل هذا على خادم مشترك أو إنتاجي):

```
host    all    all    127.0.0.1/32    trust
```

## 3. الحصانة (idempotency)

`demo.sh` يُنشئ قاعدة البيانات `babel_demo` والدور `babel_demo_app` إن لم يكونا
موجودين، ثم **يحذف ويعيد إنشاء** المخططين `ledger` و`demo` في كل تشغيلة، ثم يبذر
٤١ حساباً وثلاثة قيود افتتاحية. تشغيله مرّتين يعطي البنية والبيانات نفسها بالضبط —
عدا `posted_at` (وقت الترحيل الفعلي) وما يترتّب عليه من قيم بصمة، وهو المطلوب: البصمة
تشمل الوقت.

## 4. خريطة الملفات

| الملف | الدور |
|---|---|
| `Program.cs` | نقاط النهاية (minimal API) + تشغيل التهيئة + تقديم `wwwroot` |
| `Db/Bootstrap.cs` | كل الـDDL: الجداول، القيد المؤجّل، الصلاحيات. يُنفَّذ بحساب المالك وحده |
| `Db/Model.cs` | كيانات EF Core وربطها بالأعمدة، بما فيها `numeric(19,4)` |
| `Db/PostingService.cs` | **مسار الكتابة الوحيد**: العدّاد، البصمة، الإدراج، إسقاط الأرصدة، `COMMIT` |
| `Db/LedgerQueries.cs` | قراءات: الدليل، القيود، ميزان المراجعة، **تمريرة التحقق من السلسلة** |
| `Db/DangerOps.cs` | محاولات `UPDATE`/`DELETE` بدور التطبيق، والعبث بحساب المالك، والاستعادة، وحاشية U+200F |
| `Db/ChartOfAccounts.cs` | ٤١ حساباً، لكلٍّ `name_ar` و`name_en` |
| `Db/Seed.cs` | ثلاثة قيود افتتاحية تُرحَّل عبر `PostingService` نفسه |
| `Support/Canonical.cs` | **منسوخ حرفياً** من `spikes/relational-stack/Support/Canonical.cs` (اسم النطاق فقط اختلف) |
| `Support/Money.cs` | `decimal` ⇄ نصّ بمقياس ثابت، ومحوّل JSON الذي يمنع مرور المال عبر `double` |
| `wwwroot/index.html` | الواجهة كاملة، مبنية على شاشة القيد المعتمدة في `docs/prototypes/journal-entry` |

## 5. المخطط

```
ledger.account          (account_code pk, parent_code, name_ar, name_en, account_type, normal_side, is_postable)
ledger.entry_counter    (book_id pk, next_no, next_seq)          -- عدّاد بلا فجوات، ليس sequence
ledger.journal_entry    (entry_id pk, book_id, entry_no, chain_seq, entry_date, memo, memo_ar,
                         posted_at, actor, prev_hash, entry_hash)   -- INSERT + SELECT فقط
ledger.journal_line     (line_id pk, entry_id fk, line_no, account_code fk,
                         description, debit numeric(19,4), credit numeric(19,4))  -- INSERT + SELECT فقط
ledger.account_balance  (book_id, period, account_code) pk, debit, credit, updated_at  -- إسقاط، قابل لإعادة البناء
demo.tamper_log         (سجل العبث والاستعادة — يخصّ العرض وحده، بحساب المالك)
```

قيدان مؤجّلان (`DEFERRABLE INITIALLY DEFERRED`) على `journal_entry` و`journal_line`
يستدعيان `ledger.assert_entry_balanced()` عند `COMMIT`، فترفض قاعدة البيانات أي قيد
غير متوازن أو ذي سطر واحد مهما كان مسار الكتابة.

## 6. القواعد المعمارية المطبَّقة هنا

مأخوذة من [`docs/analysis/02-architecture.md`](../../docs/analysis/02-architecture.md) §6:

1. **صفر ذهاب وإياب مع العميل داخل معاملة تحمل قفلاً.** الترحيل نداء خادم واحد
   (`POST /api/entries`) يفتح المعاملة ويغلقها داخله.
2. **تحديث الأرصدة عبارة واحدة بالضبط، صفوفها مرتّبة تصاعدياً بالحساب** — ترتيب
   أخذ الأقفال هو ترتيب الصفوف، والصفوف غير المرتّبة تعني جموداً تحت التزامن.
3. **`INSERT … ON CONFLICT DO UPDATE` دائماً، ولا `UPDATE` مجرّد، وكل خطوة تؤكّد عدد
   صفوفها** (`BALANCE_ROWCOUNT_MISMATCH` و`COUNTER_ROWCOUNT_MISMATCH` يُفشلان المعاملة).
4. **المال `decimal` ⇄ `NUMERIC(19,4)`** من الشاشة إلى القرص. في JSON نصّ بمقياس ثابت
   (`MoneyJsonConverter`)، وفي المتصفح حساب صحيح بـ`BigInt` بمقياس ١/١٠٠٠٠ — لا
   `parseFloat` على أي مبلغ.

## 7. نقاط النهاية

| الطريقة | المسار | الوظيفة |
|---|---|---|
| `GET`  | `/api/meta` | الإصدارات، الاتصالات، وجدول الصلاحيات من `information_schema` |
| `GET`  | `/api/accounts` | دليل الحسابات |
| `GET`  | `/api/entries` | القيود المرحَّلة بسطورها |
| `GET`  | `/api/trial-balance?period=` | ميزان المراجعة من جدول الإسقاط |
| `POST` | `/api/entries` | **الترحيل** — نداء واحد، معاملة واحدة |
| `POST` | `/api/entries/{no}/reverse` | قيد عكسي (التصحيح الوحيد المتاح) |
| `POST` | `/api/danger/update` · `/api/danger/delete` | محاولة تعديل/حذف بدور التطبيق ← `42501` |
| `POST` | `/api/danger/tamper` · `/api/danger/restore` | العبث بحساب المالك، والاستعادة |
| `GET`  | `/api/verify` | إعادة بناء السلسلة وتسمية أول تسلسل مختلف |
| `GET`  | `/api/bidi?text=` | حاشية U+200F |
| `POST` | `/api/reset` | حذف المخطط وإعادة بذره |

## 8. ما لا يفعله هذا العرض

انظر قسم **«حدود هذا العرض»** في [DEMO.md](DEMO.md): لا مصادقة، ولا تعدد مستأجرين،
ولا ZATCA، وبيانات وهمية بالكامل. هذا إثبات معماري لا منتج.
