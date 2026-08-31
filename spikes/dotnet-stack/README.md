# اختبار استكشافي: PostgreSQL + Marten + Wolverine على .NET 10
# Spike: PostgreSQL + Marten + Wolverine on .NET 10

الهدف: إثبات أن حزمة `.NET 10 + PostgreSQL + Marten + Wolverine` صالحة لبناء
نظام محاسبي متعدد المستأجرين، وبالأخص أن **القيم المالية لا تفقد دقتها**.

Goal: prove the `.NET 10 + PostgreSQL + Marten + Wolverine` stack is viable for a
multi-tenant accounting system — above all that **monetary values never lose precision**.

## التشغيل / Running

```bash
./run.sh          # أو / or:  dotnet run
```

يطبع الاختبار جدول PASS/FAIL ويُنهي العملية برمز خروج `0` عند نجاح كل الإثباتات.

The spike prints a PASS/FAIL table and exits `0` only when every proof passes.

لتشغيل واجهة HTTP بدلاً من الاختبارات / to run the HTTP host instead of the proofs:

```bash
dotnet run --no-launch-profile -- --serve
curl http://127.0.0.1:5000/spike/health     # WolverineFx.Http endpoint
```

## المتطلبات / Prerequisites

- .NET SDK 10.0.x
- PostgreSQL 14+ مع قاعدة بيانات باسم `babel_spike`

```bash
sudo -u postgres createdb babel_spike
```

## الاتصال بقاعدة البيانات / Database connection

**لا توجد أي كلمة مرور مخزّنة في هذا المستودع.**
**No password is stored anywhere in this repository.**

يقرأ الاختبار متغيّر البيئة `BABEL_SPIKE_DB`؛ وإن لم يكن مضبوطاً فسيستخدم
اتصالاً محلياً بلا كلمة مرور.

The spike reads the `BABEL_SPIKE_DB` environment variable and otherwise falls back
to a local, password-less connection:

```
Host=127.0.0.1;Port=5432;Database=babel_spike;Username=postgres
```

اضبط بيانات الاعتماد الخاصة بك عبر البيئة فقط / set your own credentials via the
environment only — never commit them:

```bash
export BABEL_SPIKE_DB="Host=127.0.0.1;Port=5432;Database=babel_spike;Username=postgres;Password=<your-local-dev-password>"
```

للتطوير المحلي فقط، يمكن السماح بالاتصال بلا كلمة مرور عبر `pg_hba.conf`
(لا تفعل هذا على خادم مشترك أو إنتاجي):

For local development only, you may allow password-less loopback connections in
`pg_hba.conf` (never do this on a shared or production server):

```
host    all    all    127.0.0.1/32    trust
```

ثم / then: `sudo pg_ctlcluster <ver> main reload`

## ما الذي يُثبته الاختبار / What is proven

| # | الإثبات / Proof |
|---|---|
| (a) | دقة `decimal` عبر تخزين Marten في JSONB — بما فيها `0.0001` و `99999999999999.9999` |
| (a2)| صحّة وسرعة تجميع المبالغ من JSONB مقابل عمود `numeric(19,4)` |
| (b) | قيد يومية متوازن يُقبل، وغير المتوازن يُرفض |
| (c) | مخزن الأحداث + إعادة بناء الإسقاط (projection) من الأحداث |
| (d) | صندوق Wolverine الصادر المعاملاتي: الرسالة تُسلَّم عند الـ commit ولا تُسلَّم عند الـ rollback |
| (e) | تعدد المستأجرين المقترن (`tenant_id`) — عزل `acme` عن `globex` |
| (e2)| طبقة دفاع ثانية عبر Row Level Security فوق جداول Marten |

## ملاحظات / Notes

- الاختبار يُنشئ ويُسقط جداول داخل مخططي `babel_spike` و `babel_rls`، ويُنشئ
  دور PostgreSQL باسم `babel_rls_app` لاختبار RLS. استخدم قاعدة بيانات مخصّصة للتجارب.
- The spike creates and drops tables in the `babel_spike` and `babel_rls` schemas and
  creates a PostgreSQL role `babel_rls_app` to test RLS. Use a throwaway database.
- إثبات (a2) يُدرج مليون سطر ويستغرق نحو 20–40 ثانية.
- Proof (a2) inserts 1,000,000 rows and takes roughly 20–40 seconds.
