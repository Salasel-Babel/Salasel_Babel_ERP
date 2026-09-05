using System.Globalization;
using System.Text;
using Babel.SharedKernel;

namespace Babel.Contracts.Parameters;

/// <summary>
/// <b>على أيّ مستوى قُرِّرت القيمة.</b> مستويان لا ثالث لهما.
/// <para>
/// <b>ولماذا مستويان لا واحد:</b> نظامٌ بلا افتراضٍ يُشحن <b>يقف</b> حتى يجيب صاحب
/// المصلحة، ونظامٌ بلا تجاوزٍ للمستأجر يفرض على كل عميلٍ رقمَ غيره. فالافتراض يُشحن
/// كي يعمل النظام، والتجاوز يوجد كي لا يكون الافتراض قدراً.
/// </para>
/// </summary>
public enum ParameterScope
{
    /// <summary>افتراض المنصّة — يُشحن مع المنتج، ولا يحمل اعتماد إنسان.</summary>
    Platform = 1,

    /// <summary>تجاوز المستأجر — أودعه إنسانٌ في نسخة عميلٍ بعينها.</summary>
    Tenant = 2,
}

/// <summary>
/// <b>حالة اعتماد الإصدار — ثلاثية لا ثنائية.</b>
/// <para>
/// <b>ولماذا لا تكفي ثنائية «معتمَد / غير معتمَد»:</b> لأنها تخلط <b>مسؤوليّتين</b>.
/// أن يقول صاحب المنشأة «هذه نسبتي» مسؤوليةٌ تجارية يملكها هو؛ وأن يقول محاسبٌ قانوني
/// «هذه النسبة صحيحة نظاماً» مسؤوليةٌ مهنية يملكها هو وحده. وحقلٌ واحد بقيمتين يجعل
/// الأولى تُقرأ الثانية — فتصير قائمةُ مراجعة المحاسب فارغةً وهي لم تُراجَع بعد.
/// </para>
/// <para>
/// <b>ولا يُشحن شيءٌ بالحالة الثالثة أبداً:</b> توقيعُ المحاسب واقعةٌ تقع في نسخة عميل
/// بعد مراجعة، ولا يملك من يبني المنتج أن يشحنها. والقيد مفروضٌ في المخطّط لا في
/// الانضباط — انظر <c>ck_parameter_version_scope_matches_approval</c>.
/// </para>
/// </summary>
public enum ParameterApproval
{
    /// <summary>افتراضُ منصّة <b>غير مُعتمَد</b> — يعمل به النظام، ويُعرض موسوماً.</summary>
    PlatformDefault = 1,

    /// <summary>اعتمادُ مستأجر — إنسانٌ في المنشأة أودعه باسمه وتاريخه ومصدره.</summary>
    TenantApproved = 2,

    /// <summary>توقيعُ محاسبٍ قانوني — الخطوة الأخيرة على نسخةٍ مكتملة.</summary>
    AuditorSigned = 3,
}

/// <summary>صنف القيمة — وهو ما يقرّر حارسها.</summary>
public enum ParameterValueKind
{
    /// <summary>نسبة، <b>كسراً عشرياً لا مئوية</b>: خمسة عشر بالمئة <c>0.15</c> لا <c>15</c>.</summary>
    Rate = 1,

    /// <summary>مبلغ بعملة المنشأة — لا يحمل عملته هنا، فالعملة على المستند.</summary>
    Money = 2,

    /// <summary>عدد صحيح غير سالب — أيّامٌ أو مرّاتٌ أو أشهر.</summary>
    Count = 3,
}

/// <summary>
/// ما يفعله كلٌّ من هذه الحالات على الشاشة وفي المراجعة — جدولٌ واحد معلَن، على شاكلة
/// <see cref="Babel.Contracts.Capture.FieldProvenanceInfo"/> حرفاً.
/// </summary>
public static class ParameterApprovalInfo
{
    /// <summary>مفتاح المورد الذي تُترجم به تسمية الحالة. مفتاح لا نصّ (‏ADR-0021).</summary>
    /// <param name="approval">الحالة.</param>
    public static string ResourceKeyOf(ParameterApproval approval) => approval switch
    {
        ParameterApproval.PlatformDefault => "core.parameters.approval.platformDefault",
        ParameterApproval.TenantApproved => "core.parameters.approval.tenantApproved",
        ParameterApproval.AuditorSigned => "core.parameters.approval.auditorSigned",
        _ => throw new ArgumentOutOfRangeException(nameof(approval), approval, "حالة اعتماد غير معروفة / unknown approval state"),
    };

    /// <summary>
    /// هل تحمل هذه الحالة <b>اسم إنسانٍ اعتمدها</b>؟
    /// <para>
    /// <b>لا</b> لافتراض المنصّة، و<b>نعم</b> لما عداه. والسبب مكتوبٌ حرفياً في
    /// <c>src/Babel.Hr/Persistence/HrRows.cs</c> عند <c>ApprovedBy</c>: «من اعتمد الصفّ
    /// — إنسان، لا نظام». فافتراضُ المنصّة لا يُكتب في ذلك الحقل، وحالتُه هي تمثيله.
    /// </para>
    /// </summary>
    /// <param name="approval">الحالة.</param>
    public static bool CarriesAHumanApprover(ParameterApproval approval)
        => approval is ParameterApproval.TenantApproved or ParameterApproval.AuditorSigned;

    /// <summary>هل انتهت مراجعة المحاسب على هذه الحالة؟ نعم للموقَّعة وحدها.</summary>
    /// <param name="approval">الحالة.</param>
    public static bool IsSigned(ParameterApproval approval) => approval is ParameterApproval.AuditorSigned;

    /// <summary>الرمز المخزَّن للحالة — <c>snake_case</c> ثابت، وهو ما يُكتب في العمود.</summary>
    /// <param name="approval">الحالة.</param>
    public static string TokenOf(ParameterApproval approval) => approval switch
    {
        ParameterApproval.PlatformDefault => "platform_default",
        ParameterApproval.TenantApproved => "tenant_approved",
        ParameterApproval.AuditorSigned => "auditor_signed",
        _ => throw new ArgumentOutOfRangeException(nameof(approval), approval, "حالة اعتماد غير معروفة / unknown approval state"),
    };

    /// <summary>يقرأ الحالة من رمزها المخزَّن.</summary>
    /// <param name="token">الرمز.</param>
    public static ParameterApproval ApprovalFrom(string token) => token switch
    {
        "platform_default" => ParameterApproval.PlatformDefault,
        "tenant_approved" => ParameterApproval.TenantApproved,
        "auditor_signed" => ParameterApproval.AuditorSigned,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "رمز حالة اعتماد غير معروف / unknown approval token"),
    };

    /// <summary>الرمز المخزَّن للمستوى.</summary>
    /// <param name="scope">المستوى.</param>
    public static string TokenOf(ParameterScope scope) => scope switch
    {
        ParameterScope.Platform => "platform",
        ParameterScope.Tenant => "tenant",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "مستوى غير معروف / unknown scope"),
    };

    /// <summary>الرمز المخزَّن لصنف القيمة.</summary>
    /// <param name="kind">الصنف.</param>
    public static string TokenOf(ParameterValueKind kind) => kind switch
    {
        ParameterValueKind.Rate => "rate",
        ParameterValueKind.Money => "money",
        ParameterValueKind.Count => "count",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "صنف قيمة غير معروف / unknown value kind"),
    };

    /// <summary>يقرأ صنف القيمة من رمزه المخزَّن.</summary>
    /// <param name="token">الرمز.</param>
    public static ParameterValueKind KindFrom(string token) => token switch
    {
        "rate" => ParameterValueKind.Rate,
        "money" => ParameterValueKind.Money,
        "count" => ParameterValueKind.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "رمز صنف قيمة غير معروف / unknown value kind token"),
    };

    /// <summary>يقرأ المستوى من رمزه المخزَّن.</summary>
    /// <param name="token">الرمز.</param>
    public static ParameterScope ScopeFrom(string token) => token switch
    {
        "platform" => ParameterScope.Platform,
        "tenant" => ParameterScope.Tenant,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "رمز مستوى غير معروف / unknown scope token"),
    };
}

/// <summary>
/// <b>لقطةُ إصدارٍ كما استُعمل — وهذه هي التي تُخزَّن على المستند نفسه.</b>
/// <para>
/// <b>لماذا لقطةٌ لا مفتاحٌ أجنبي:</b> <c>deploy/compose.yml</c> يعطي كلَّ وحدةٍ قاعدةَ
/// بياناتٍ منفصلة (‏<c>babel_core</c> · <c>babel_purchasing</c> · …)، ومفتاحٌ أجنبيّ
/// عابرٌ للقواعد <b>غير ممكن</b> في PostgreSQL. فلو حُفظ معرّف الإصدار وحده لاحتاج قارئُ
/// القيد بعد سنتين اتصالاً بقاعدةٍ أخرى — وربما بنسخةٍ منها لم تُحفظ أصلاً. واللقطة
/// تحمل <b>القيم المستعمَلة معها</b>، فالمستند يُقرأ وحده.
/// </para>
/// <para>
/// <b>ووحدة اللقطة هي المجموعة لا القيمة المفردة:</b> نسبةُ المنشأة ونسبةُ الموظف
/// والحدّان <b>يسري بعضها ببعض</b>، ولقطةٌ لرقمٍ واحد تسمح بخليطٍ من إصدارين لم يعتمده
/// أحد. فالمخزَّن ترويسةُ الإصدار وقيمُه كلُّها.
/// </para>
/// <para>
/// <b>وهي في العقود لا في النواة</b> لأن طرفَي الوصلة يستعملانها: النواة تُنتجها،
/// والوحدة المالكة للمستند تخزّنها — ولا تعرف إحداهما الأخرى (القاعدة 3). وهو موضع
/// <see cref="Babel.Contracts.Capture.FieldProvenance"/> نفسه وللسبب نفسه.
/// </para>
/// </summary>
public sealed record ParameterSnapshot
{
    /// <summary>معرّف الإصدار الذي قرأ منه المستند.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>رمز مجموعة المعامِلات — من <c>ParameterSetCatalogue</c>.</summary>
    public required string SetCode { get; init; }

    /// <summary>المستوى الذي جاء منه الإصدار.</summary>
    public required ParameterScope Scope { get; init; }

    /// <summary>تاريخ سريان الإصدار.</summary>
    public required DateOnly EffectiveFrom { get; init; }

    /// <summary>حالة اعتماده <b>لحظة الاستعمال</b> — لا حالته اليوم.</summary>
    public required ParameterApproval Approval { get; init; }

    /// <summary>مرجع المصدر الذي أُخذت منه القيم — نصٌّ يقرؤه مراجع.</summary>
    public required string SourceRef { get; init; }

    /// <summary>القيم المستعمَلة بمفاتيحها، مرتَّبةً ترتيباً ثابتاً.</summary>
    public required IReadOnlyDictionary<string, decimal> Values { get; init; }

    /// <summary>القيمة بمفتاحها، أو <c>null</c> إن لم تكن في اللقطة.</summary>
    /// <param name="key">المفتاح.</param>
    public decimal? Find(string key)
        => Values.TryGetValue(key ?? string.Empty, out decimal value) ? value : null;

    /// <summary>
    /// <b>الشكل القانوني لعمودٍ واحد.</b> ثقافةٌ ثابتة، وترتيبٌ رتيب للمفاتيح، وهروبٌ
    /// معلَن — فبايتاتُ اللقطة نفسها في كل آلة وكل لغة (القاعدة 10).
    /// </summary>
    public string Canonical()
    {
        StringBuilder text = new();
        text.Append("p1|")
            .Append(VersionId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
            .Append(Escape(SetCode)).Append('|')
            .Append(ParameterApprovalInfo.TokenOf(Scope)).Append('|')
            .Append(EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
            .Append(ParameterApprovalInfo.TokenOf(Approval)).Append('|')
            .Append(Escape(SourceRef)).Append('|');

        bool first = true;
        foreach (KeyValuePair<string, decimal> pair in Values.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                text.Append(';');
            }

            first = false;
            text.Append(Escape(pair.Key)).Append('=').Append(pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>يقرأ لقطةً من شكلها القانوني.</summary>
    /// <param name="canonical">النصّ كما خُزِّن.</param>
    /// <exception cref="FormatException">النصّ ليس شكلاً قانونياً لهذه النسخة.</exception>
    public static ParameterSnapshot Parse(string canonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonical);

        string[] parts = canonical.Split('|');

        if (parts.Length != 8 || !string.Equals(parts[0], "p1", StringComparison.Ordinal))
        {
            throw new FormatException("لقطة معامِلات بشكل غير معروف / unrecognised parameter snapshot form");
        }

        Dictionary<string, decimal> values = new(StringComparer.Ordinal);

        if (parts[7].Length > 0)
        {
            foreach (string entry in parts[7].Split(';'))
            {
                int split = entry.IndexOf('=', StringComparison.Ordinal);

                if (split <= 0)
                {
                    throw new FormatException("قيمة بلا مفتاح في لقطة معامِلات / a value without a key in a parameter snapshot");
                }

                values[Unescape(entry[..split])] = decimal.Parse(
                    entry[(split + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture);
            }
        }

        return new ParameterSnapshot
        {
            VersionId = Guid.ParseExact(parts[1], "D"),
            SetCode = Unescape(parts[2]),
            Scope = ParameterApprovalInfo.ScopeFrom(parts[3]),
            EffectiveFrom = DateOnly.ParseExact(parts[4], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Approval = ParameterApprovalInfo.ApprovalFrom(parts[5]),
            SourceRef = Unescape(parts[6]),
            Values = values,
        };
    }

    private static string Escape(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\p", StringComparison.Ordinal)
        .Replace(";", "\\s", StringComparison.Ordinal)
        .Replace("=", "\\e", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal);

    private static string Unescape(string text)
    {
        StringBuilder plain = new(text.Length);

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 >= text.Length)
            {
                plain.Append(text[index]);
                continue;
            }

            index++;
            plain.Append(text[index] switch
            {
                '\\' => '\\',
                'p' => '|',
                's' => ';',
                'e' => '=',
                'n' => '\n',
                'r' => '\r',
                _ => throw new FormatException("هروبٌ غير معروف في لقطة معامِلات / unknown escape in a parameter snapshot"),
            });
        }

        return plain.ToString();
    }
}

/// <summary>
/// <b>منفذ قراءة المعامِل السارِي.</b> تنفّذه النواة، ويقرؤه من يحتاج قيمةً مقرَّرة —
/// بلا أن يعرف جدولاً ولا قاعدة بيانات ولا وحدةً أخرى.
/// </summary>
public interface IParameterSource
{
    /// <summary>
    /// اللقطةُ السارية لمجموعةٍ في تاريخ: تجاوزُ المستأجر إن وُجد، وإلّا افتراضُ
    /// المنصّة، وإلّا <b>رفضٌ مُسمّى</b> — ولا قيمة تُخترع عند الغياب.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="setCode">رمز المجموعة.</param>
    /// <param name="on">التاريخ الذي تُطلب له.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<ParameterSnapshot>> ResolveAsync(
        TenantId tenant, string setCode, DateOnly on, CancellationToken cancellationToken = default);
}

/// <summary>استعمالُ إصدارٍ في مستندٍ مُرحَّل — واقعةٌ تُسجَّل مرّةً ولو تكرّر الترحيل.</summary>
/// <param name="VersionId">الإصدار المُستعمَل.</param>
/// <param name="Module">الوحدة المالكة للمستند.</param>
/// <param name="DocumentType">نوع المستند داخلها.</param>
/// <param name="DocumentId">معرّفه داخلها.</param>
/// <param name="PostedOn">تاريخ الترحيل.</param>
public sealed record ParameterUsage(
    Guid VersionId,
    BabelModule Module,
    string DocumentType,
    Guid DocumentId,
    DateOnly PostedOn);

/// <summary>
/// <b>منفذ تسجيل الاستعمال.</b> تُناديه الوحدة المالكة <b>لحظة الترحيل</b>، فتصير
/// «كلُّ مستندٍ استعمل هذا الإصدار» استعلاماً واحداً في قاعدةٍ واحدة.
/// <para>
/// <b>وهذا لا يُغني عن اللقطة على المستند ولا يُغنيه عنها:</b> اللقطة هي <b>السجلّ</b>
/// الذي يُقرأ منه المستند وحده، وهذا <b>فهرسٌ</b> يُقرأ منه المراجع. ولو ضاع الفهرس
/// لبقيت المستندات مقروءة؛ ولو ضاعت اللقطة لما أعاده شيء.
/// </para>
/// </summary>
public interface IParameterUsageRecorder
{
    /// <summary>
    /// يسجّل الاستعمال. <b>آمنُ التكرار</b>: النداء الثاني على المستند نفسه
    /// بالإصدار نفسه ينجح ولا يكتب صفّاً ثانياً.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="usage">الواقعة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result> RecordAsync(TenantId tenant, ParameterUsage usage, CancellationToken cancellationToken = default);
}
