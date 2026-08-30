using System.Security.Cryptography;
using Babel.Hr.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// بناءُ حالةٍ صالحة بأقلّ ما يلزم — <b>وكل رقم فيها مختلقٌ معلَن</b>.
/// <para>
/// ولا نسبة نظامية ولا سقف أجر خاضع ولا معادلة مكافأة في هذا الملفّ ولا في أي ملفّ من
/// هذه المجموعة: القيم النظامية <b>غير متحقَّق منها</b> (البند م-14)، ورقمٌ يُكتب في
/// تجهيز اختبار يُنسخ إلى إنتاج بعد شهرين — وقد وقع هذا في هذا المستودع من قبل.
/// </para>
/// </summary>
internal static class Scenario
{
    /// <summary>نسبة المنشأة في اختبارات هذه المجموعة — <b>قيمة مختلقة، لا نظامية</b>.</summary>
    public const decimal TestEmployerRate = 0.10000000m;

    /// <summary>نسبة الموظف في اختبارات هذه المجموعة — <b>قيمة مختلقة، لا نظامية</b>.</summary>
    public const decimal TestEmployeeRate = 0.05000000m;

    /// <summary>
    /// لاحقةٌ فريدة لكل اختبار، فلا يتنازع اثنان رقم مستندٍ واحد.
    /// <para>
    /// <b>ومن عشوائيةٍ معمّاة لا من <c>Guid.CreateVersion7</c>:</b> الإصدار السابع
    /// يبدأ بطابع زمني بدقّة المِلّي ثانية، فأوّلُ ثمانية محارف منه <b>واحدة</b> لكل ما
    /// يُنشأ في النافذة نفسها. وقد قيس ذلك هنا: ثلاثة اختبارات في التجميعة نفسها أخذت
    /// اللاحقة عينها فتصادمت أرقام مستنداتها.
    /// </para>
    /// </summary>
    public static string Suffix() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    /// <summary>يسجّل موظفاً بهوية وآيبان مختلقين ومميّزين، ثم يُسند له أجراً.</summary>
    /// <param name="harness">التركيبة.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="classCode">تصنيف الاشتراك.</param>
    /// <param name="componentCode">رمز مكوّن الأجر.</param>
    /// <param name="nationalId">رقم هوية مختلق — يُبحث عنه لاحقاً في الدفتر.</param>
    /// <param name="iban">آيبان مختلق — يُبحث عنه لاحقاً في الدفتر.</param>
    /// <param name="nameAr">الاسم العربي — يُبحث عنه لاحقاً في الدفتر.</param>
    /// <param name="wage">الأجر.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public static async Task<EmployeeView> EmployeeAsync(
        Harness harness,
        TenantId tenant,
        string classCode,
        string componentCode,
        string nationalId,
        string iban,
        string nameAr,
        decimal wage,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(harness);

        Result<EmployeeView> employee = await harness.Employees
            .RegisterAsync(
                tenant,
                Harness.Actor,
                new EmployeeDraft(
                    new TranslatedName(nameAr),
                    classCode,
                    CostCenterId: string.Empty,
                    HiredOn: new DateOnly(2025, 1, 1),
                    new EmployeeIdentityDraft(nationalId, iban, new DateOnly(1990, 1, 1))),
                token)
            .ConfigureAwait(false);

        Assert.True(employee.IsSuccess, Harness.Reason(employee));

        Result<PayElementView> element = await harness.Employees
            .AddPayElementAsync(
                tenant,
                Harness.Actor,
                employee.Value.Id,
                new PayElementDraft(componentCode, new DateOnly(2026, 1, 1), Money.Of(wage, Harness.Currency)),
                token)
            .ConfigureAwait(false);

        Assert.True(element.IsSuccess, Harness.Reason(element));
        return employee.Value;
    }

    /// <summary>يعرّف مكوّن أجرٍ أساسياً يدخل الوعاءين.</summary>
    /// <param name="harness">التركيبة.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="suffix">اللاحقة الفريدة.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public static async Task<string> BasicComponentAsync(
        Harness harness, TenantId tenant, string suffix, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(harness);

        Result<PayComponentView> component = await harness.Employees
            .AddPayComponentAsync(
                tenant,
                Harness.Actor,
                new PayComponentDraft(
                    "basic-" + suffix, new TranslatedName("الراتب الأساسي"), "earning", true, true),
                token)
            .ConfigureAwait(false);

        Assert.True(component.IsSuccess, Harness.Reason(component));
        return component.Value.Code;
    }

    /// <summary>هل يظهر هذا النصّ في أي حقلٍ نصّي داخل قيود الدفتر أو سطورها؟</summary>
    /// <remarks>
    /// والحقول المفحوصة هي بالضبط ما يدخل <b>البايتات المُجزَّأة</b> في الشكل القانوني
    /// v2: البيان بلغتيه، ووصف السطر بلغتيه، والعمود <c>description_ar_search</c>
    /// المفهرس نصّياً، ومعرّف الطرف في الدفتر المساعد، ومعرّف المستند المصدر.
    /// </remarks>
    /// <param name="needle">النصّ المبحوث عنه.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public static async Task<bool> LedgerMentionsAsync(string needle, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select exists (
                select 1 from ledger.journal_entry
                 where coalesce(memo, '') like $1
                    or coalesce(memo_ar, '') like $1
                    or coalesce(memo_ar_search, '') like $1
                    or coalesce(source_doc_id, '') like $1
                    or coalesce(actor, '') like $1
            ) or exists (
                select 1 from ledger.journal_line
                 where coalesce(description, '') like $1
                    or coalesce(description_ar, '') like $1
                    or coalesce(description_ar_search, '') like $1
                    or coalesce(subledger_party_id, '') like $1
            )
            """, connection);

        command.Parameters.AddWithValue("%" + needle + "%");
        return (bool)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!;
    }
}
