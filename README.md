# Babel ERP — نظام سلاسل بابل الذكي

نظام تخطيط موارد مؤسسية (ERP) عربي/إنجليزي، متعدد الشركات والفروع، يغطي:

| الركيزة | الحالة |
|---|---|
| النظام المحاسبي والمالي (النواة) | قيد التحليل |
| نقاط البيع (POS) | قيد التحليل — يحتاج توصيف |
| الموارد البشرية والرواتب | قيد التحليل |
| المقاولات والمشاريع والمستخلصات | موصّف في وثيقة العميل |
| الإدارة العقارية (الإيجارات والملاك) | قيد التحليل — يحتاج توصيف |
| المخازن والمستودعات والمشتريات | موصّف في وثيقة العميل |
| تكامل هيئة الزكاة والضريبة والدخل (ZATCA) | قيد التحليل |

## أين تبدأ

الوثيقة الرئيسية هي **[docs/analysis/00-preliminary-vision.md](docs/analysis/00-preliminary-vision.md)** — الرؤية الأولية العامة ومنهجية تحليل الوضع.

| الملف | المحتوى |
|---|---|
| [00-preliminary-vision.md](docs/analysis/00-preliminary-vision.md) | الرؤية الأولية: كيف نبدأ التحليل، وما هي النواة |
| [01-scope-and-gaps.md](docs/analysis/01-scope-and-gaps.md) | خريطة النطاق والفجوات بين الوثيقة المستلمة والطلب |
| [02-architecture.md](docs/analysis/02-architecture.md) | المعمارية المقترحة والقرارات التقنية المبكرة |
| [03-accounting-core.md](docs/analysis/03-accounting-core.md) | النواة المحاسبية ومصفوفة الترحيل |
| [04-zatca-integration.md](docs/analysis/04-zatca-integration.md) | الفوترة الإلكترونية والالتزام الضريبي |
| [05-roadmap.md](docs/analysis/05-roadmap.md) | خارطة الطريق والمراحل و MVP |
| [06-risks-and-decisions.md](docs/analysis/06-risks-and-decisions.md) | المخاطر والقرارات المطلوبة من المالك |
| [posting-matrix.md](docs/reference/posting-matrix.md) | مصفوفة الترحيل المحاسبي (مسودة أولية) |
| [chart-of-accounts.md](docs/reference/chart-of-accounts.md) | هيكل دليل الحسابات المقترح |

## استراتيجية الفروع (Branches)

النموذج المعتمد هو **Git Flow**:

| الفرع | الغرض |
|---|---|
| `production` | ما يعمل لدى العميل — محمي، لا دفع مباشر |
| `main` | المستقر المُختبَر وبوابة المالك — محمي، لا دفع مباشر |
| `develop` | فرع التكامل اليومي |
| `feature/*` · `claude/*` · `fix/*` | فروع العمل — تتفرع من `develop` وتُدمج فيه |
| `gh-pages` | فرع نشر النماذج على GitHub Pages |

```
feature/<مهمة>  ──PR──▶  develop  ──PR──▶  main  ──إصدار──▶  production
```

انظر [CONTRIBUTING.md](CONTRIBUTING.md).

## المرحلة الحالية

**تحليل ولا يوجد كود بعد.** المخرجات الحالية وثائق تحليل ومعمارية فقط. قرارات المالك المطلوبة قبل كتابة أي كود موجودة في
[06-risks-and-decisions.md](docs/analysis/06-risks-and-decisions.md).
