using System.Globalization;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Babel.Ledger.Posting;

/// <summary>
/// المسار التشخيصي لرفض طبقة التخزين — <b>مفصولاً عن الرسالة المعروضة</b>.
/// <para>
/// <b>لماذا الفصل:</b> <c>PostgresException.MessageText</c> يحمل اسم القيد، واسم الجدول،
/// وأحياناً قيمة الصفّ المخالف. والخطأ المجالي عقدٌ <b>يُعاد إلى كل مستدعٍ</b> — معالج
/// رسالة، ومهمة مجدولة، وتقرير — لا إلى سطح HTTP وحده. فحجب النصّ عند حدّ HTTP دفاعٌ في
/// العمق صحيح، لكنه لا يحمي شيئاً وراء ذلك الحدّ.
/// </para>
/// <para>
/// <b>ولماذا لا يُحذف النصّ:</b> حذفه يستبدل عيباً بعيب. المشغّل يحتاج اسم القيد ليعرف
/// أي طبقة رفضت ولماذا، وبدونه يصير رفضٌ مشروع من قاعدة البيانات عطلاً لا يُشخَّص.
/// فالنصّ يذهب إلى <b>سجلّ مبنيَ الحقول</b> تحت <b>معرّف تشخيص</b>، والمعرّف — وحده —
/// هو ما يعبر في الرسالة المعروضة. فمن يملك السجلّ يربط، ومن يملك الرسالة لا يتعلّم عن
/// المخطّط شيئاً.
/// </para>
/// </summary>
internal static class PostingDiagnostics
{
    /// <summary>معرّف تشخيص جديد بصيغة ثابتة لا تقرأ ثقافة.</summary>
    public static string NewId() => Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);

    /// <summary>
    /// يسجّل رفض طبقة التخزين بكل ما يحتاجه المشغّل، ولا يعيد منه حرفاً.
    /// </summary>
    /// <param name="logger">سجلّ الخادم.</param>
    /// <param name="diagnosticId">معرّف التشخيص الذي يظهر — وحده — في الرسالة المعروضة.</param>
    /// <param name="plan">القيد كما خُطِّط، لتحديد المستند المعنيّ.</param>
    /// <param name="exception">الاستثناء كما وصل من PostgreSQL.</param>
    public static void DatabaseRefused(
        ILogger logger,
        string diagnosticId,
        PostingPlan plan,
        PostgresException exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exception);

        // الحقول مسمّاة لا مُدمَجة في نصّ: سجلّ مبنيُ الحقول يُستعلَم، والنصّ المدموج يُقرأ بالعين.
        logger.LogError(
            exception,
            "رفضت قاعدة البيانات الترحيل. معرّف التشخيص {DiagnosticId} · SQLSTATE {SqlState} · "
            + "القيد {ConstraintName} · المخطّط {SchemaName} · الجدول {TableName} · العمود {ColumnName} · "
            + "الشركة {CompanyId} · الدفتر {BookId} · الحدث {EventCode} · المستند {SourceDocType}/{SourceDocId} · "
            + "مفتاح الحصانة {IdempotencyKey} · النصّ {MessageText} · التفصيل {Detail}",
            diagnosticId,
            exception.SqlState,
            exception.ConstraintName ?? "-",
            exception.SchemaName ?? "-",
            exception.TableName ?? "-",
            exception.ColumnName ?? "-",
            plan.CompanyId,
            plan.BookId,
            plan.EventCode,
            plan.SourceDocType,
            plan.SourceDocId,
            plan.IdempotencyKey,
            exception.MessageText,
            exception.Detail ?? "-");
    }
}
