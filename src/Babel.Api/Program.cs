using Babel.Compliance;
using Babel.Core;
using Babel.Inventory;
using Babel.Ledger;
using Babel.Purchasing;
using Babel.Sales;

// ─────────────────────────────────────────────────────────────────────────────
// الجذر التركيبي لـ«سلاسل بابل».
//
// موجة الهيكل: لا منطق أعمال، ولا نقاط نهاية للأعمال، ولا هجرات.
// ما يوجد هنا هو ترتيب التركيب نفسه، وهو ما يصعب تغييره لاحقاً.
//
// ملاحظتان تشغيليتان مثبَّتتان الآن ليُبنى عليهما في موجة الرسائل
// (وثيقة المعمارية §2.2 · spikes/relational-stack/VERDICT.md §5):
//
//   1. Wolverine يُهيَّأ بالتوليد الساكن للشيفرة:
//        opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
//      مع تشغيل `dotnet run -- codegen write` في خطوة بناء.
//
//   2. WolverineFx.RuntimeCompilation ممنوعة في الإنتاج — تجرّ Roslyn إلى العملية.
//      المنع مفروض ببناء: tests/Babel.ArchitectureTests/Rule08_NoRuntimeCompilationInProduction.cs
//      يفحص كل csproj وكل Directory.Packages.props ويُفشل البناء على أي ذكر لها.
// ─────────────────────────────────────────────────────────────────────────────

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// النواة أولاً: الاستحقاق وقياس الاستخدام والتدقيق. إلزامية دائماً.
builder.Services.AddBabelCore();

// الدفتر: يسجّل IPostingService. أنواعه الداخلية (LedgerDbContext, AccountCode) لا تُرى هنا.
builder.Services.AddBabelLedger();

// الوحدات الأفقية: خدمات تطبيق فقط. لا وصول إلى جداول بعضها ولا إلى جداول الدفتر.
builder.Services.AddBabelSales();
builder.Services.AddBabelPurchasing();
builder.Services.AddBabelCompliance();
builder.Services.AddBabelInventory();

WebApplication app = builder.Build();

app.MapGet("/health", static () => Results.Ok(new { status = "ok" }));

await app.RunAsync().ConfigureAwait(false);

/// <summary>نقطة الدخول. مُعلنة ليتمكّن اختبار التكامل من الإشارة إليها لاحقاً.</summary>
public partial class Program;
