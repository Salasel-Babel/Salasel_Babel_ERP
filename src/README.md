# هيكل «سلاسل بابل» — خريطة الوحدات والحدود المفروضة

> **ما هذه الوثيقة:** دليل من يكتب كوداً في هذا المستودع. تشرح **أين يوضع كل شيء**، و**ما الذي
> يمنعه البناء**، و**كيف تُضاف وحدة جديدة دون كسر شيء**.
>
> **ما ليست هذه الوثيقة:** ليست توصيات ولا أسلوباً مفضّلاً. كل قاعدة هنا **مفروضة باختبار
> يُفشل البناء**، ولها ملف باسمها تحت `tests/Babel.ArchitectureTests/`. القاعدة التي لا اختبار
> لها ليست قاعدة — وقد ذُكرت صراحةً في §6 «ما لم نستطع فرضه بنيوياً».

---

## 1. لماذا هيكل قبل منطق

هذا المستودع في موجة **الهيكل**: لا منطق محاسبي، ولا هجرات، ولا واجهة. القيمة كلها في
**ما صار مستحيلاً**.

السبب أن ثلاثة أشياء في هذا المنتج **تكلفتها اليوم قريبة من الصفر، وتكلفتها بعد سنة إعادة كتابة**:

| البند | لماذا يستحيل تأجيله |
|---|---|
| **حدّ الدفتر** | وحدة كتبت في `ledger.journal_line` مرة واحدة، صار لها مسار كتابة ثانٍ لا يمرّ بالتوازن ولا بالفترة ولا بسلسلة التجزئة. سحب ذلك بعد بيانات إنتاج يعني هجرة بيانات، لا إعادة هيكلة |
| **حالة `ReadOnly`** | تمسّ **كل** مسار كتابة و**كل** شاشة و**كل** تقرير في **كل** وحدة. يجب أن توجد قبل أول عميل يدفع، لا بعد أول عميل يتوقف عن الدفع ([02-architecture §17 م-7](../docs/analysis/02-architecture.md)) |
| **التقاط الاستخدام** | التسعير بالوحدة **وبالمستخدم**. ما لم يُكتب اليوم لا يوجد استعلام يستخرجه غداً |

---

## 2. خريطة الوحدات

```
                        ┌──────────────────────────────────────────┐
                        │              Babel.Api                   │  الجذر التركيبي
                        │   يعرف الجميع · لا يعرفه أحد             │  وسطح HTTP
                        └───────────────────┬──────────────────────┘
                                            │
   ┌────────────────────────────────────────┴─────────────────────────────────────┐
   │  الوحدات الأفقية — لا تعتمد على بعضها، ولا على الدفتر                        │
   │                                                                              │
   │  Sales · Purchasing · Compliance   [إلزامية]                                 │
   │  Inventory · Pos · Hr · Projects · RealEstate · Assets · Portals · Ai [اختيارية] │
   └────────────────────────────────────────┬─────────────────────────────────────┘
                                            │  عبر IPostingService والأحداث فقط
                        ┌───────────────────┴──────────────────────┐
                        │              Babel.Ledger                │  [إلزامي دائماً]
                        │  دليل الحسابات · القيود · الفترات ·      │  الجهة الوحيدة
                        │  العملات · محرك الترحيل · سلسلة التجزئة  │  التي تكتب قيداً
                        └───────────────────┬──────────────────────┘
                        ┌───────────────────┴──────────────────────┐
                        │              Babel.Core                  │  [إلزامي دائماً]
                        │  الهوية · الصلاحيات · الشجرة التنظيمية · │
                        │  المستندات · سير العمل · التدقيق ·       │
                        │  الإشعارات · تعدد المستأجرين ·           │
                        │  **الاستحقاق** · **قياس الاستخدام**      │
                        └───────────────────┬──────────────────────┘
                        ┌───────────────────┴──────────────────────┐
                        │            Babel.Contracts               │
                        │  عقد الترحيل (PostingRequest/Line) ·     │
                        │  عقود أحداث الأعمال — لا منطق            │
                        └───────────────────┬──────────────────────┘
                        ┌───────────────────┴──────────────────────┐
                        │           Babel.SharedKernel             │
                        │  Money · TenantId · PeriodId ·           │
                        │  LocalizedName · Result — أنواع قيمة فقط │
                        └──────────────────────────────────────────┘
```

**الاتجاه دائماً إلى الأسفل.** السهم الوحيد الصاعد في المنتج كله هو `Babel.Api` → الجميع.

| المشروع | ما فيه | الاستحقاق |
|---|---|---|
| `Babel.SharedKernel` | أنواع قيمة فقط: `Money` بـ`decimal`، `TenantId`، `PeriodId`، `BillingPeriod`، `LocalizedName` (`name_ar` + `name_en`)، `Result`/`Result<T>`/`Error`، `IdempotencyKey`، `BabelModule` | — |
| `Babel.Contracts` | `IPostingService` · `PostingRequest` · `PostingLine` · `PostingRole` · عقود أحداث الأعمال. **لا منطق محاسبي** | — |
| `Babel.Core` | الهوية · الصلاحيات · الشجرة التنظيمية · المستندات · سير العمل · التدقيق · الإشعارات · تعدد المستأجرين · **الاستحقاق** · **قياس الاستخدام** | **إلزامي — لا يكون اختيارياً أبداً** |
| `Babel.Ledger` | دليل الحسابات · القيود · الفترات · العملات · محرك الترحيل · سلسلة التجزئة · التقارير المالية | **إلزامي — لا يكون اختيارياً أبداً** |
| `Babel.Sales` | العملاء · عروض الأسعار · الفواتير · سندات القبض | إلزامي مع الدفتر |
| `Babel.Purchasing` | الموردون · أوامر الشراء · الفواتير · سندات الصرف | إلزامي مع الدفتر |
| `Babel.Compliance` | الفوترة الإلكترونية · هيئة الزكاة والضريبة والجمارك | **إلزامي في السوق السعودي** |
| `Babel.Inventory` | الأصناف · المستودعات · الحركات · التكلفة | اختياري |
| `Babel.Pos` | نقاط البيع · الورديات · المزامنة دون اتصال | اختياري — **يتطلب المخزون** |
| `Babel.Hr` | الموظفون · الحضور · الإجازات · الرواتب | اختياري |
| `Babel.Projects` | المشاريع · جداول الكميات · العقود · المستخلصات | اختياري — **يتطلب المخزون** |
| `Babel.RealEstate` | العقارات · الوحدات · الملاك · عقود الإيجار | اختياري |
| `Babel.Assets` | الأصول الثابتة · المعدات · الإهلاك | اختياري |
| `Babel.Portals` | بوابات العميل والمورد والمقاول من الباطن | اختياري |
| `Babel.Ai` | OCR · الاقتراحات · التنبؤات — **مساعد فقط، لا صلاحية اعتماد** | اختياري |
| `Babel.Api` | الجذر التركيبي وسطح HTTP | — |

### مشاريع مساندة — ليست وحدات منتج

هذه المشاريع لا تحمل بطاقة وحدة، ولا مدخل في `BabelModule`، ولا استحقاقاً. وهي **ليست
وحدات أفقية**: الوحدة الأفقية يُمنع عليها الاعتماد على أخواتها، وهذه يُعتمد عليها بقصد.
مذكورة في `ModuleMap.Supporting`، ومراجعها المسموح بها مُعلنة هناك مثل غيرها (القاعدة 3).

| المشروع | ما فيه | مراجعه المسموح بها |
|---|---|---|
| `Babel.Canonicalization` | الشكل القانوني للبايتات المُجزَّأة · سلسلة التجزئة · سياسة النص العربي | **لا شيء** — ولا حزمة خارجية واحدة |
| `Babel.Compliance.Abstractions` | عقد حدّ الفوترة الإلكترونية: أنواع فقط، بلا EF ولا Wolverine ولا Npgsql ولا HTTP | **لا شيء** — مفروض باختبار في `BoundaryCostTests` |
| `Babel.Compliance.FakeProvider` | مزوّد وهمي كامل التنفيذ للاختبار: بلا شبكة وبلا اعتمادات وبلا شهادات في المستودع | `Babel.Compliance.Abstractions` |
| `Babel.Compliance.Wolverine` | محوّل الصندوق الصادر الدائم، معزول كي يبقى تشغيل الالتزام ممكناً بلا Wolverine | `Babel.Compliance` · `Babel.Compliance.Abstractions` |

و`Babel.Compliance` وحدها من الوحدات الأفقية لها مرجع زائد معلن: `Babel.Compliance.Abstractions`.
الغرض أن يبقى اسم المزوّد خارج الوحدة نفسها؛ حذف المرجع يعني عودة اسم المزوّد إلى قلب التنسيق.

---

## 3. القواعد المفروضة — كل واحدة تُفشل البناء

الأداة: **NetArchTest.Rules** فوق **xunit.v3**، ومعها فحص مباشر لملفات المشاريع بالانعكاس.

> **لماذا NetArchTest لا محلّل Roslyn:** المحلّل يفحص **شيفرة مصدر مشروع واحد وهي تُصرَّف**؛
> وأغلب قواعدنا **علاقات بين تجميعات** («هل تعتمد المبيعات على الدفتر؟»، «هل هذا النوع مكشوف
> خارج وحدته؟»)، وهي معلومات لا يملكها المحلّل وقت تصريف المشروع الواحد. زد على ذلك أن
> النتيجة نفسها — بناء فاشل في التكامل المستمر — تتحقق بالاثنين، بينما تكلفة صيانة مشروع
> محلّلات وتوزيعه أعلى بمراتب.
>
> **وثمن هذا الاختيار مذكور في §6:** الخطأ يظهر عند `dotnet test` لا عند الكتابة في المحرّر.

ولأن فحص IL وحده **لا يرى مرجع مشروع لم يُستعمل بعد**، تُقرأ ملفات `csproj` نفسها. المرجع
الخاطئ يُمنع يوم كُتب، لا يوم استُعمل.

### القاعدة 1 · لا وحدة تكتب في دفتر الأستاذ
`Rule01_NoModuleWritesToTheLedger.cs` — **أهم ثابت في المشروع.**

الترحيل عبر `IPostingService.PostAsync(PostingRequest)` حصراً. ثلاث طبقات:

1. **لا مرجع مشروع** من أي وحدة أفقية إلى `Babel.Ledger`. لا يوجد ما يُستدعى أصلاً.
2. **أنواع الاستمرارية `internal`**: `LedgerDbContext`، `JournalEntryRow`، `JournalLineRow`، `AccountRow`.
3. **صلاحيات PostgreSQL**: الدور التطبيقي `INSERT` + `SELECT` فقط، مع `REVOKE UPDATE, DELETE, TRUNCATE`،
   والهجرات بدور مالك منفصل ([02-architecture §3.2](../docs/analysis/02-architecture.md) — مقيس، رمز الرفض `42501`).
   **هذه الطبقة تُنفَّذ في موجة الهجرات** ولم تُنفَّذ بعد؛ الطبقتان الأوليان مفروضتان اليوم.

**إثبات الالتقاط** — أُضيف `<ProjectReference Include="../Babel.Ledger/…" />` إلى `Babel.Sales.csproj`:

```
failed Rule01_NoModuleWritesToTheLedger.NoProjectOtherThanTheCompositionRootReferencesTheLedger
  src/Babel.Sales/Babel.Sales.csproj يشير إلى Babel.Ledger
```

وحتى مع وجود المرجع، الكتابة نفسها **لا تُصرَّف**:

```
src/Babel.Sales/Violation.cs(7,49): error CS0122: 'LedgerDbContext' is inaccessible due to its protection level
```

### القاعدة 2 · الوحدة لا تستطيع تسمية حساب
`Rule02_ModulesCannotNameAnAccount.cs`

الوحدة تصف **حدثاً تجارياً** بـ`PostingRole` (صافٍ، ضريبة مخرجات، محتجز…)؛ **مصفوفة الترحيل**
داخل الدفتر هي التي تحوّل الدور إلى رقم حساب. `AccountCode` نوع `internal` في `Babel.Ledger`،
و`PostingLine` **لا يحمل حقل حساب إطلاقاً**.

الفائدة العملية: تعديل قاعدة ترحيل يصبح **تعديل صف في جدول**، لا تعديل كود في وحدة المبيعات
ونشر إصدار ([03-accounting-core §4](../docs/analysis/03-accounting-core.md)).

**إثبات الالتقاط** — أُضيف `public string? AccountCode { get; init; }` إلى `PostingLine`:

```
failed Rule02_ModulesCannotNameAnAccount.ThePostingContractExposesNoAccountShapedMember
  Babel.Contracts.Posting.PostingLine.AccountCode
failed Rule02_ModulesCannotNameAnAccount.APostingLineCarriesARoleAndNotAnAccount
```

### القاعدة 3 · الاعتماد دائماً إلى الأسفل
`Rule03_DependencyDirectionIsAlwaysDownward.cs`

`Core` و`Ledger` لا تعتمدان على أي وحدة أعلى منهما — أبداً. والوحدات الأفقية لا تستدعي بعضها
مباشرة، بل عبر `Babel.Contracts` أو الأحداث. الخريطة معلنة في
`tests/Babel.ArchitectureTests/Support/ModuleMap.cs`، وما ليس فيها ممنوع.

**إثبات الالتقاط** — `Babel.Sales` → `Babel.Inventory`:

```
failed Rule03_DependencyDirectionIsAlwaysDownward.EveryProjectReferenceIsDeclaredAllowed
  src/Babel.Sales/Babel.Sales.csproj → Babel.Inventory (غير مسموح)
```

### القاعدة 4 · المال `decimal`، لا `float` ولا `double`
`Rule04_MoneyIsDecimal.cs` — CONTRIBUTING §3 بند 2.

يُفحص **كل** نوع وكل عضو في كل تجميعة. إذا مسّ اسم النوع أو فضاء أسمائه أو اسم العضو إحدى
الكلمات — `money · amount · balance · price · rate · tax · total · currency · cost` — وجب ألا
يكون نوعه `float` ولا `double` ولا `Half`.

المطابقة **بالكلمة لا بالنص الخام**: `ExchangeRate` يُلتقط، و`Corporate` لا يُلتقط.

> **لماذا اختبار وليس مراجعة:** خطأ الفاصلة العائمة **لا يظهر في الاختبارات**. تمرّ فاتورة،
> وتمرّ مئة، ثم لا يطابق ميزان المراجعة بهللة واحدة بعد ستة أشهر — ولا يدلّ شيء على الموضع.

**إثبات الالتقاط** — `public double DefaultTaxRate { get; set; }` على `CustomerRow`:

```
failed Rule04_MoneyIsDecimal.NoBinaryFloatingPointAnywhereNearMoney
  Babel.Sales.Persistence.CustomerRow.خاصية DefaultTaxRate : Double
```

### القاعدة 5 · كل وحدة تملك جداولها
`Rule05_EveryModuleOwnsItsTables.cs`

كل `DbContext` **`internal`** وفي فضاء `<الوحدة>.Persistence`. كل كيان **`internal`**. لا كيان
يشير إلى كيان وحدة أخرى، ولا `DbSet<T>` يربط كياناً من وحدة أخرى. القراءة العابرة عبر واجهات
معلنة، لا `JOIN`.

> مفتاح خارجي واحد من `sales.invoice` إلى `inventory.item` يبدو مريحاً يوم كتابته، ثم يصير هو
> السبب في أن ترقية المخزون توقف المبيعات، وأن أرشفة سنة مالية تفشل.

**إثبات الالتقاط** — `internal sealed class CustomerRow` → `public`:

```
failed Rule05_EveryModuleOwnsItsTables.NoEntityTypeIsVisibleOutsideItsModule
  Babel.Sales.Persistence.CustomerRow
```

### القاعدة 6 · لا شيء يتجاوز الاستحقاق
`Rule06_NothingBypassesEntitlement.cs`

كل دالة عامة على كل نوع يحمل `IApplicationService` **يجب** أن تحمل
`[RequiresEntitlement(BabelModule.X, EntitlementAccess.Y)]` — عليها أو على نوعها. ويُفحص أيضاً
أن السمة تعلن **وحدتها هي**، لا وحدة أخرى.

> الإنفاذ عند **حدّ الخدمة** لا عند الواجهة: إخفاء عنصر من القائمة لا يمنع نداء HTTP.

**إثبات الالتقاط** — أُضيفت `VoidInvoiceAsync` عامة بلا سمة:

```
failed Rule06_NothingBypassesEntitlement.EveryPublicEntryPointDeclaresItsEntitlementRequirement
  نقاط دخول عامة بلا [RequiresEntitlement] — أي بلا إنفاذ استحقاق:
  Babel.Sales.Application.SalesInvoiceService.VoidInvoiceAsync
```

### القاعدة 7 · النواة المشتركة والعقود بلا منطق أعمال
`Rule07_SharedKernelAndContractsArePure.cs`

`Babel.SharedKernel` و`Babel.Contracts`: **صفر `PackageReference`**، ولا اعتماد على EF Core أو
Npgsql أو Wolverine أو حقن الاعتماديات أو ASP.NET، ولا نوع ملموس باسم يشبه الخدمات
(`*Service`, `*Repository`, `*Manager`, `*Store`, `*Engine`, `*Handler`, …) — **الواجهات
مستثناة**، فـ`IPostingService` عقدٌ لا تنفيذ. ولا خاصية عامة قابلة للتعديل بعد الإنشاء.

> هاتان التجميعتان يعتمد عليهما **كل شيء**. أي منطق يدخلهما يصبح فوراً منطقاً لا يمكن تغييره
> دون تغيير الجميع. وأول ما يدخل عادةً هو «مجرد دالة مساعدة صغيرة».

**إثبات الالتقاط** — حزمة EF Core في `Babel.Contracts.csproj`، وصنف خدمة في `Babel.SharedKernel`:

```
failed Rule07_SharedKernelAndContractsArePure.NeitherProjectDeclaresAnyPackageReference
  src/Babel.Contracts/Babel.Contracts.csproj يعلن حزماً: Microsoft.EntityFrameworkCore

failed Rule07_SharedKernelAndContractsArePure.NoConcreteServiceLikeTypeExists
  أنواع تشبه الخدمات في النواة المشتركة أو العقود — الواجهات وحدها مسموحة:
  Babel.SharedKernel.MoneyConversionService
```

### القاعدة 8 · لا `WolverineFx.RuntimeCompilation` في المنتج
`Rule08_NoRuntimeCompilationInProduction.cs`

الحزمة تجرّ Roslyn إلى عملية الإنتاج ([02-architecture §2.2](../docs/analysis/02-architecture.md) — مقيس
من شجرة الحزم). البديل: التوليد الساكن، `dotnet run -- codegen write` مع `TypeLoadMode.Static`.
تفحص القاعدة كل `csproj` و`Directory.Packages.props`. وتتحقق أيضاً من أن إدارة الإصدارات
المركزية فعّالة وأن لا إصدار حزمة مكتوباً داخل `csproj`.

`spikes/` خارج النطاق عمداً: تجارب لا منتج، وإحداها تستعمل الحزمة فعلاً.

**إثبات الالتقاط:**

```
failed Rule08_NoRuntimeCompilationInProduction.TheCentralPackageFileDoesNotPinIt
  WolverineFx.RuntimeCompilation مثبَّتة مركزياً — الخطوة الأولى نحو دخولها الإنتاج.
```

### القاعدة 9 · الحل يطابق خريطة الوحدات
`Rule09_TheSolutionMatchesTheModuleMap.cs`

كل عضو في `BabelModule` له مشروع `src/` ومشروع `tests/` وبطاقة `<Module>ModuleInfo`؛
و**كل** `*.csproj` على القرص — أياً كان مجلده — موجود في `Babel.slnx`، وإلا لم يبنه التكامل
المستمر ولم تفحصه أي قاعدة. المُعفى الوحيد `spikes/`، مكتوباً بالاسم في القاعدة مع سببه:
تجارب لا منتج، وإحداها تستعمل `WolverineFx.RuntimeCompilation` التي تمنعها القاعدة 8.

> **تصحيح تاريخي.** كانت هذه الفقرة تَعِد بهذا وتفعل أقلّ منه: القاعدة كانت تقرأ
> `RepositoryLayout.Projects`، ونطاقه قائمة مجلدات ثابتة `{src, tests}`. فمشروع تحت
> `tools/` أو `demo/` لم يكن «على القرص» بنظرها، ومرّت خضراء على ثلاثة مشاريع لا يبنيها
> شيء لشهور. الاكتشاف الآن **بالبحث في شجرة الملفات لا بقائمة مجلدات**، ومعه اختبار
> يُثبت أن الإعفاء ما زال واحداً. القصة كاملةً في
> [`docs/evidence/traps.md` فخ-41](../docs/evidence/traps.md).

**إثبات الالتقاط ١** — أُنشئ `src/Babel.Warehousing` ولم يُضف إلى الحل:

```
failed Rule09_TheSolutionMatchesTheModuleMap.EveryProjectOnDiskIsInTheSolution
  مشاريع خارج ملف الحل — لن يبنيها التكامل المستمر:
  src/Babel.Warehousing/Babel.Warehousing.csproj
failed Rule03_DependencyDirectionIsAlwaysDownward.EveryProjectReferenceIsDeclaredAllowed
  src/Babel.Warehousing/Babel.Warehousing.csproj: مشروع غير مذكور في ModuleMap
```

**إثبات الالتقاط ٢ — خارج `src/`، وهو ما كان يفوت** — وُضع `apps/rot-canary/RotCanary.csproj`
وشُغِّلت اختبارات المعمارية. بنسخة `develop` من القاعدة مرّت **47/47 خضراء**؛ وبالنسخة
الحالية:

```
failed Rule09_TheSolutionMatchesTheModuleMap.EveryProjectOnDiskIsInTheSolution
  مشاريع على القرص وخارج Babel.slnx — لا يبنيها شيء، فمحلّلاتها واختباراتها وأخطاء
  ترجمتها غير مرئية والتكامل المستمر أخضر (traps.md — فخ-41):
  apps/rot-canary/RotCanary.csproj
failed Rule09_TheSolutionMatchesTheModuleMap.TheOnlyFolderOutsideTheSolutionIsSpikes
  مجلدات خارج الحل غير المُعفى الوحيد (spikes/):
  apps/
  spikes/
```

### حارس إضافي: كل قاعدة تُثبت أنها ليست فارغة

في كل ملف قاعدة اختبار اسمه `TheRuleIsNotVacuous`. السبب مباشر: قاعدة تفحص مجموعة فارغة
**تمرّ دائماً**. لو حُذف مشروع، أو فشل تحميل تجميعة، أو أُعيدت تسمية فضاء أسماء، تصير كل
القواعد خضراء وهي لا تفحص شيئاً. هذا الحارس هو ما يمنع «إنفاذاً» وهمياً.

---

## 4. الاستحقاق وقياس الاستخدام

### الحالات الثلاث

| الحالة | المعنى | القراءة | الكتابة |
|---|---|---|---|
| `NotEntitled` | لم تُشترَ قط — لا تظهر أصلاً | ✗ | ✗ |
| `Entitled` | مشتراة وفاعلة | ✓ | ✓ |
| `ReadOnly` | اشتُريت ثم انقضى الاشتراك | ✓ **كاملة، بما فيها التقارير** | ✗ |

> **لا يوجد «إلغاء تثبيت وحدة».** وحدة رحّلت قيوداً غير قابلة للتعديل لا يمكن إزالتها: القيود
> في الدفتر، وفي ميزان المراجعة، وفي الإقرار المُقدَّم، وفي سلسلة التجزئة. إزالتها إتلاف للدفتر.
> البديل هو الأرشفة + `ReadOnly`.

### رسم الاعتماد — ومجموعة الاستحقاق غير المتسقة تُرفض

`Babel.Core/Entitlement/ModuleDependencyGraph.cs`:

| الوحدة | تتطلب |
|---|---|
| `Ledger` | `Core` |
| `Sales` · `Purchasing` · `Inventory` · `Hr` · `RealEstate` · `Assets` · `Portals` | `Core` · `Ledger` |
| `Compliance` | `Core` · `Ledger` · `Sales` |
| `Pos` | `Core` · `Ledger` · `Sales` · **`Inventory`** |
| `Projects` | `Core` · `Ledger` · **`Inventory`** |
| `Ai` | `Core` |

قاعدة التحقق: **قدرة الوحدة لا تتجاوز قدرة ما تعتمد عليه.** نقاط بيع `Entitled` فوق مخزون
`ReadOnly` تعني بيعاً لا يستطيع أن ينقص رصيداً — فتُرفض المجموعة **كاملة**، ولا تُصحَّح ضمنياً:
التصحيح الضمني يجعل العميل يظن أنه اشترى ما لم يشتره.

والوحدات الإلزامية (`Core`, `Ledger`, `Sales`, `Purchasing`, `Compliance`) لا تُطفأ إطلاقاً.

### الواجهات

```csharp
// الاستحقاق — Babel.Core/Entitlement
public interface IEntitlementService
{
    ValueTask<EntitlementSet>          GetAsync(TenantId tenant, CancellationToken ct = default);
    ValueTask<EntitlementState>        GetStateAsync(TenantId tenant, BabelModule module, CancellationToken ct = default);
    ValueTask<Result<EntitlementSet>>  ApplyAsync(EntitlementChangeRequest request, CancellationToken ct = default);
}

public interface IEntitlementEnforcer
{
    ValueTask<Result> EnsureAsync(TenantId tenant, UserId actor, BabelModule module,
                                  EntitlementAccess access, string operation, CancellationToken ct = default);
}

// قياس الاستخدام — Babel.Core/Metering — محورا التسعير كلاهما
public interface IUsageMeter
{
    ValueTask RecordModuleUsageAsync(ModuleUsageEvent usage, CancellationToken ct = default);
    ValueTask RecordUserActivityAsync(UserActivityEvent activity, CancellationToken ct = default);
}

public interface IUsageReader
{
    ValueTask<IReadOnlyDictionary<BabelModule, long>> GetModuleUsageAsync(TenantId t, BillingPeriod p, CancellationToken ct = default);
    ValueTask<IReadOnlyCollection<UserId>>            GetActiveUsersAsync(TenantId t, BillingPeriod p, CancellationToken ct = default);
}

public interface IUsageStore   // الحدّ الذي يصير جدولاً مقسَّماً بالفترة لاحقاً
{
    ValueTask AppendModuleUsageAsync(IReadOnlyList<ModuleUsageEvent> batch, CancellationToken ct = default);
    ValueTask AppendUserActivityAsync(IReadOnlyList<UserActivityEvent> batch, CancellationToken ct = default);
}
```

**الإنفاذ والقياس في مكان واحد عمداً** (`EntitlementEnforcer`): لو كان القياس مساراً منفصلاً
لنُسي في نصف نقاط الدخول، ولاكتُشف النقص عند أول فاتورة اشتراك — بعد فوات أوان الالتقاط.
وبهذا الدمج **لا يمكن أن يمرّ استدعاء مستحَق دون أن يُقاس على المحورين**. والنداء المرفوض
**لا يُقاس**: لا يُفوتَر العميل على نداء رُفض.

**كل تغيير استحقاق يُكتب في سجل التدقيق** بمن ومتى ومن أي حالة إلى أي حالة وبأي سبب.
مغطّى في `tests/Babel.Core.Tests/EntitlementTests.cs`.

التنفيذات في هذه الموجة **في الذاكرة**: `InMemoryEntitlementService` · `InMemoryUsageStore` ·
`InMemoryAuditLog`. لا قاعدة بيانات بعد — المطلوب الآن أن يوجد **الحدّ**، لأن الحدّ هو ما
يصعب إضافته لاحقاً، لا الجدول.

---

## 5. كيف تُضاف وحدة جديدة — القائمة الكاملة

القواعد تجعل الخطوة الناقصة **بناءً فاشلاً**، لا خطأً صامتاً. الترتيب هنا هو ترتيب الفشل.

1. **أضِف العضو إلى `BabelModule`** في `src/Babel.SharedKernel/BabelModule.cs`.
   ← بدونها لا يوجد للوحدة هوية في الاستحقاق ولا في القياس ولا في التدقيق.

2. **أضِف اعتماداتها إلى `ModuleDependencyGraph`** في `src/Babel.Core/Entitlement/`.
   ← نسيانها يرمي `ArgumentOutOfRangeException` عند أول تحقق اتساق.
   وإن كانت إلزامية، أضِفها إلى `Mandatory` — وهذا **قرار تجاري** يُوقَّع، لا خيار مطوّر.

3. **أنشئ `src/Babel.<Module>/`** بمرجعَي `Babel.SharedKernel` و`Babel.Contracts` و`Babel.Core`.
   **ولا شيء غيرها.** المرجع إلى `Babel.Ledger` أو إلى وحدة أفقية أخرى يُفشل Rule01/Rule03.

4. **أضِف `<Module>ModuleInfo`** — بطاقة الوحدة باسمها ثنائي اللغة. Rule09 يتحقق منها.

5. **خدمات التطبيق**: كل خدمة تحمل `IApplicationService`، وكل دالة عامة فيها تحمل
   `[RequiresEntitlement(BabelModule.<Module>, EntitlementAccess.Read|Write)]`، **وتستدعي
   `IEntitlementEnforcer.EnsureAsync` قبل أي عمل**. Rule06 يتحقق من الثلاثة.

6. **الاستمرارية إن وُجدت**: `<Module>DbContext` و كيانات **`internal`** في فضاء
   `Babel.<Module>.Persistence`، بلا أي إشارة إلى كيان وحدة أخرى. Rule05.

7. **الترحيل**: عبر `IPostingService` وحده، بـ`PostingRole` لا برقم حساب، ومع `IdempotencyKey`.

8. **التخاطب مع وحدة أخرى**: عقد حدث في `Babel.Contracts/Events/`. **لا مرجع مباشر إطلاقاً.**

9. **أنشئ `tests/Babel.<Module>.Tests/`.** Rule09 يتحقق من وجوده.

10. **أضِف السطر إلى `ModuleMap.AllowedProjectReferences`** في مشروع اختبارات المعمارية.
    ← هذه الخطوة **مقصودة**: توسيع خريطة الاعتماد قرار معماري يمرّ بمراجعة، لا أثر جانبي
    لإضافة مجلد.

11. **أضِف المشروعين إلى `Babel.slnx`**، وسجّل الوحدة في `Babel.Api/Program.cs` عبر
    `AddBabel<Module>()`.

12. **حدّث هذه الوثيقة** وجدول الوحدات في [02-architecture §13](../docs/analysis/02-architecture.md).

ثم: `dotnet test --solution Babel.slnx`. البناء الأخضر يعني أن الوحدة داخل الحدود.

---

## 6. ما لم نستطع فرضه بنيوياً — ويجب أن يُقال

> قاعدة غير قابلة للفرض **موثَّقة** أنفع من قاعدة أُسقطت بصمت. هذه هي القائمة كاملة.

| # | ما ليس مفروضاً | لماذا | ما يعوّضه |
|---|---|---|---|
| ح-1 | **`REVOKE UPDATE, DELETE` على جداول الدفتر** — الطبقة الثالثة من القاعدة 1 | خاصية قاعدة بيانات، لا خاصية شيفرة. لا توجد بعد هجرات ولا أدوار | يُنفَّذ في موجة الهجرات، ويُختبر بمحاولة `UPDATE` تتوقّع الرمز `42501` كما في [spikes/relational-stack](../spikes/relational-stack/) |
| ح-2 | **الاستدعاء الفعلي لـ`IEntitlementEnforcer`** داخل كل نقطة دخول | القاعدة تتحقق من **وجود السمة** و**حقن المنفِّذ** في التجميعة؛ لا تتحقق من أن جسم الدالة استدعاه فعلاً. إثبات ذلك يحتاج تحليل تدفق داخل أجسام الدوال | القاعدة تُضيّق الفجوة إلى «كتب السمة ولم يستدعِ» — وهو ما تلتقطه مراجعة الـPR واختبار سلوكي لكل خدمة. رفعُه إلى فرض بنيوي يحتاج محلّل Roslyn أو اعتراضاً في زمن التشغيل، وكلاهما موجة لاحقة |
| ح-3 | **`JOIN` عابر للوحدات في SQL خام** | القاعدة 5 تفرض حدود **الأنواع**؛ نص SQL خام يمكنه ذكر أي جدول | مخططات منفصلة لكل وحدة (`core`, `ledger`, `sales`) + دور قاعدة بيانات لكل وحدة عند الهجرات. حتى ذلك الحين: مراجعة الـPR |
| ح-4 | **تسمية حساب داخل نص** (`"1210100"` كسلسلة نصية في وحدة) | لا يوجد نوع يُفحص. النوع `AccountCode` محمي، والنص لا | مصفوفة الترحيل بيانات لا كود، فلا داعي أصلاً؛ ومراجعة الـPR لكل ثابت نصي رقمي |
| ح-5 | **التوحيد القياسي ومتجهات التجزئة الذهبية** ([02-architecture §8](../docs/analysis/02-architecture.md)) | لا يوجد ترحيل ولا سلسلة بعد | تُكتب **قبل** أول قيد، مع إعادة تحقق من السلسلة كاملة في التكامل المستمر. هذه أعلى بنود موجة الدفتر أولوية |
| ح-6 | **«العمود الموقَّع منفصل عن عمود البحث المطبَّع»** (§8.3 ع-4) | لا أعمدة بعد | قاعدة مخطط تُفرض عند أول هجرة تحمل نصاً عربياً |
| ح-7 | **الاستحقاق في الواجهة** | لا واجهة بعد | الإنفاذ عند حدّ الخدمة يكفي للأمان؛ الواجهة شأن تجربة استخدام. **إخفاء عنصر من القائمة ليس إنفاذاً وحده** |
| ح-8 | **رد فعل فوري في المحرّر** على مخالفة حدّ | النتيجة اختبار لا محلّل — انظر §3 | الخطأ يظهر عند `dotnet test` وفي التكامل المستمر قبل الدمج. الثمن: دورة تغذية راجعة أبطأ من محلّل Roslyn |

---

## 7. الإعدادات الملزمة

| الملف | ما يفرضه |
|---|---|
| `Directory.Build.props` | `TreatWarningsAsErrors` · `Nullable=enable` · `LangVersion=latest` · `ImplicitUsings=enable` · محلّلات .NET بوضع `Recommended` · `EnforceCodeStyleInBuild` · بناء حتمي مع `ContinuousIntegrationBuild` و`PathMap` في التكامل المستمر · `InvariantGlobalization=false` (العربية لغة أولى) |
| `Directory.Packages.props` | إدارة إصدارات مركزية + تثبيت تعدّي. **مصدر الحقيقة الوحيد للإصدارات** |
| `global.json` | تثبيت جيل الـSDK + مشغّل الاختبارات `Microsoft.Testing.Platform` (مطلوب على .NET 10) |
| `.editorconfig` | اصطلاحات C#: فضاء أسماء بصيغة الملف · `using` خارج فضاء الأسماء · حقول خاصة `_camelCase` · `IDE0005` و`IDE0161` بمستوى تحذير أي **خطأ بناء** |
| `.github/workflows/ci.yml` | استرجاع · بناء بالتحذير خطأً · **اختبارات المعمارية أولاً ومنفصلة** · كل الاختبارات · فحص أسرار · تسجيل شجرة الحزم المحلولة كمصنوع |

قراران معلنان في `.editorconfig`، لا إسكات عابر:
`CA1716` (`Error` كلمة محجوزة في VB — التشغيل البيني مع VB ليس هدفاً) و
`CA1000` (`Result<T>.Success/Failure` أعضاء ساكنة على نوع عام — هذا شكل نمط النتيجة).

---

## 8. تشغيل محلي

```bash
dotnet build Babel.slnx                                                      # التحذير خطأ
dotnet test  --solution Babel.slnx                                           # كل الاختبارات
dotnet test  --project tests/Babel.ArchitectureTests/Babel.ArchitectureTests.csproj   # الحدود وحدها
```

---

## 9. مراجع

- [docs/analysis/02-architecture.md](../docs/analysis/02-architecture.md) — الحزمة المقيسة · القواعد الأربع · التوحيد القياسي · تعدد المستأجرين
- [docs/analysis/03-accounting-core.md](../docs/analysis/03-accounting-core.md) — الكيانات · مصفوفة الترحيل · الدفاتر المساعدة
- [docs/analysis/06-risks-and-decisions.md](../docs/analysis/06-risks-and-decisions.md) — ق-16 التحزيم ونموذج الاستحقاق
- [spikes/relational-stack/VERDICT.md](../spikes/relational-stack/VERDICT.md) — لماذا لا Marten
- [CONTRIBUTING.md](../CONTRIBUTING.md) — الفروع · رسائل الالتزام · قواعد الكود المحاسبي
