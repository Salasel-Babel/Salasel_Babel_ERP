using System.Globalization;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// التبعية البنيوية الوحيدة في هذا المستودع على امتداد PostgreSQL — تُفحَص
/// <b>قبل</b> أن يُنشر مخطّطٌ واحد، وتُرفض بصوتها لا بصمتها.
/// <para>
/// <b>ما الذي يحميه <c>btree_gist</c>:</b> قيد الاستبعاد الزمني
/// <c>ex_realestate_lease_term_does_not_overlap</c> على <c>realestate.lease_contract</c> —
/// وهو ما يمنع أن تُؤجَّر وحدةٌ واحدة بعقدين ساريين متداخلي المدّة. و«مدّة واحدة» شرطُ
/// <b>تقاطع مدى</b> لا شرط تساوٍ، فلا يعبّر عنه فهرس فريد مهما اتّسع
/// (‏<see href="https://www.postgresql.org/docs/16/btree-gist.html">btree_gist</see> ·
/// ADR-0052 §7).
/// </para>
/// <para>
/// <b>ولماذا الرفض لا التدرّج:</b> نشرٌ يمضي بلا القيد يقبل عقدين متداخلين على وحدة
/// واحدة، ولا يعلم بذلك أحدٌ حتى يقع نزاعٌ بين مستأجرين — أي بعد شهور، وعلى بيانات
/// حقيقية. ونشرٌ يتوقّف الآن يُقرأ في السطر الأول من سجلّ الترحيل. <b>ولا بديل تطبيقي
/// يُخترع هنا</b>: فحصٌ في الخدمة يقرأ ثم يكتب، وبين القراءة والكتابة يمرّ نداءٌ آخر —
/// فيجتاز اثنان الفحص معاً. واختيار البديل قرارُ مالك مفتوح صراحةً في ADR-0052
/// وفي <c>docs/evidence/verification-debt.md §9.1.2</c>، وحسمُه هنا انتحالٌ لذلك القرار.
/// </para>
/// </summary>
internal static class RealEstateExtension
{
    /// <summary>اسم الامتداد — مكتوبٌ مرّة واحدة، ويظهر في كل رسالة رفض.</summary>
    public const string Name = "btree_gist";

    /// <summary>اسم القيد الذي يحميه — يظهر في رسالة الرفض كي يُعرَف الثمن لا الاسم وحده.</summary>
    public const string ProtectedConstraint = "ex_realestate_lease_term_does_not_overlap";

    /// <summary>
    /// يتحقّق أن الامتداد <b>متاح ومركَّب</b> في قاعدة العقارات، ويرمي برسالة مسمّاة إن لم يكن.
    /// </summary>
    /// <param name="settings">الإعدادات — يُستعمل اتصال المالك وحده.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task EnsureAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("فحص تبعية العقارات البنيوية / checking the real-estate structural dependency");

        await using NpgsqlConnection owner = new(settings.RealEstateOwner.ConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        string? available = await ScalarAsync(
            owner,
            "select default_version from pg_available_extensions where name = 'btree_gist'",
            cancellationToken).ConfigureAwait(false);

        if (available is null)
        {
            throw new InvalidOperationException(Refusal(
                settings,
                "الامتداد غير معروض أصلاً في هذه النسخة من PostgreSQL: لا صفّ له في "
                + "pg_available_extensions، أي أن ملفّات الامتداد غير مثبَّتة على الخادم.",
                "ثبِّت حزمة الإضافات (‏postgresql-contrib) على خادم القاعدة، أو استعمل صورة "
                + "postgres الرسمية التي تحملها أصلاً، ثم أعِد النشر."));
        }

        string? installed = await ScalarAsync(
            owner, "select extversion from pg_extension where extname = 'btree_gist'", cancellationToken)
            .ConfigureAwait(false);

        if (installed is null)
        {
            try
            {
                await using NpgsqlCommand create = new("create extension if not exists btree_gist", owner);
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException failure)
            {
                throw new InvalidOperationException(Refusal(
                    settings,
                    "‏create extension رُفض برمز PostgreSQL "
                    + failure.SqlState + " ورسالته: " + failure.MessageText,
                    "امنح دور المالك في هذا الاتصال حقّ تركيب الامتدادات الموثوقة، أو ركِّب "
                    + "الامتداد مرّةً بيد مسؤول القاعدة: create extension btree_gist; ثم أعِد النشر. "
                    + "وإن كانت بيئة الاستضافة تمنعه نهائياً فالقرار يعود إلى المالك — وهو "
                    + "بندٌ مفتوح بنصّه في ADR-0052 §7 وفي docs/evidence/verification-debt.md §9.1.2، "
                    + "ولا يُحسَم بديلُه في شيفرة النشر."),
                    failure);
            }

            installed = await ScalarAsync(
                owner, "select extversion from pg_extension where extname = 'btree_gist'", cancellationToken)
                .ConfigureAwait(false);
        }

        if (installed is null)
        {
            throw new InvalidOperationException(Refusal(
                settings,
                "‏create extension مضى بلا خطأ ولم يظهر صفٌّ في pg_extension بعده.",
                "افحص القاعدة بيد مسؤولها: \\dx في psql على قاعدة العقارات."));
        }

        string? server = await ScalarAsync(owner, "show server_version", cancellationToken).ConfigureAwait(false);

        Say.Detail(
            "‏" + Name + " مركَّب في «" + settings.RealEstateDatabase + "» — النسخة " + installed
            + " (المتاحة " + available + ") على PostgreSQL " + (server ?? "?"));
        Say.Detail("وهو ما يجعل القيد «" + ProtectedConstraint + "» ممكناً — مدّةٌ سارية واحدة لكل وحدة.");
    }

    /// <summary>
    /// يقرأ القيد من <c>pg_constraint</c> بعد نشر المخطّط — <b>من القاعدة نفسها لا من الملفّ</b>.
    /// <para>نصٌّ في المستودع لا يُثبت قيداً في قاعدة (‏فخ-129).</para>
    /// </summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task AssertConstraintAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using NpgsqlConnection owner = new(settings.RealEstateOwner.ConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        string? definition = await ScalarAsync(
            owner,
            "select pg_get_constraintdef(oid) from pg_constraint where conname = '" + ProtectedConstraint + "'",
            cancellationToken).ConfigureAwait(false);

        Say.Require(
            definition is not null,
            "قيد الاستبعاد الزمني قائمٌ في القاعدة المنشورة",
            definition ?? "لا صفّ باسم " + ProtectedConstraint + " في pg_constraint — المخطّط منشور بلا حارسه");
    }

    private static async Task<string?> ScalarAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string Refusal(Settings settings, string cause, string remedy)
        => "\n"
        + "✘ توقّف الترحيل: امتداد PostgreSQL «" + Name + "» غير متاح في قاعدة العقارات «"
        + settings.RealEstateDatabase + "».\n"
        + "  السبب  : " + cause + "\n"
        + "  الثمن  : بدونه يسقط القيد «" + ProtectedConstraint + "» على "
        + "realestate.lease_contract، وهو وحده ما يمنع تأجير الوحدة الواحدة بعقدين ساريين "
        + "متداخلي المدّة. ولا يُعوَّض بفهرس فريد: «مدّة واحدة» شرطُ تقاطع مدى لا تساوٍ.\n"
        + "  العلاج : " + remedy + "\n"
        + "  ولماذا يتوقّف النشر بدل أن يمضي: نشرٌ يمضي بلا هذا القيد يقبل تأجيراً مزدوجاً "
        + "لا يظهر إلا في نزاعٍ بين مستأجرين بعد شهور. والرفض الآن أرخص من الاكتشاف هناك.";
}
