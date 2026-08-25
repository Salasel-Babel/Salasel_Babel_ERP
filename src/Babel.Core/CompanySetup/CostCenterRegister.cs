using System.Collections.Immutable;
using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// <b>سجلّ مراكز التكلفة — غير فارغ بحكم بنائه، وله افتراضيٌّ عامل دائماً.</b>
/// <para>
/// قرار المالك: «مراكز التكلفة يجب أن توجد في المحاسبة؛ ولأننا لا نقبل عموداً فارغاً،
/// يسأل إعداد المنشأة عنها — فإن كان الجواب لا، صار اسم المنشأة هو مركز التكلفة
/// الافتراضي؛ وإن كان متعدّداً، فاسم أول مركز مُدخَل إلزامي.» والثابتة المترتّبة عليه:
/// <b>لكل منشأة مركز تكلفة واحد على الأقل، ولا يكون <c>CostCenterId</c> فارغاً في أي موضع.</b>
/// </para>
/// <para>
/// <b>ولماذا هذا الشكل بالذات:</b> الثابتة هنا ليست فحصاً يؤدّيه مستدعٍ منضبط، بل شرطُ
/// وجودٍ للنوع:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>لا مُنشئ عام.</b> الطريق الوحيد إلى قيمة هو <see cref="Open"/> — وهي تأخذ مركزاً
///     واحداً على الأقل — ثم عملياتٌ تُرجع سجلّاً جديداً ولا تُرجع لا شيء.
///   </description></item>
///   <item><description>
///     <b>المُنشئ الخاص نفسه يرمي</b> على مصفوفة فارغة، أو على افتراضيٍّ غير موجود، أو على
///     افتراضيٍّ موقوف، أو على رمز مكرَّر. وهذا آخر حاجز، ويُختبَر بالانعكاس صراحةً حتى
///     لا يمرّ فراغاً.
///   </description></item>
///   <item><description>
///     <b>لا عملية حذف إطلاقاً.</b> غيابها بنيوي لا اتفاقي — كغياب فعل الحذف من سطح الدفتر
///     (ADR-0002 · ADR-0003). والمركز الذي يُراد إخراجه من الاستعمال <b>يُوقَف</b>
///     (ADR-0006)، فيبقى تاريخه المُرحَّل مقروءاً ومُبوَّباً إلى الأبد.
///   </description></item>
/// </list>
/// </summary>
public sealed class CostCenterRegister
{
    private readonly ImmutableArray<CostCenter> _centers;
    private readonly CostCenterCode _default;

    private CostCenterRegister(ImmutableArray<CostCenter> centers, CostCenterCode defaultCode)
    {
        // ── الحاجز الأخير: يرمي، ولا يُصلح، ولا يصمت ────────────────────────────
        // بلوغُ أيٍّ من هذه الحالات خللٌ برمجي لا فشلٌ متوقّع، فهو استثناء لا Result.
        if (centers.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(
                "سجلّ مراكز تكلفة فارغ — والمنشأة لا تخلو من مركز تكلفة أبداً. / "
                + "An empty cost-centre register — a company is never without a cost centre.");
        }

        if (centers.Select(static center => center.Code.Value).Distinct(StringComparer.Ordinal).Count() != centers.Length)
        {
            throw new InvalidOperationException(
                "رمز مركز تكلفة مكرَّر داخل السجلّ. / A repeated cost-centre code inside the register.");
        }

        CostCenter? head = centers.FirstOrDefault(center => center.Code == defaultCode);

        if (head is null)
        {
            throw new InvalidOperationException(
                $"المركز الافتراضي «{defaultCode}» ليس في السجلّ. / The default centre '{defaultCode}' is not in the register.");
        }

        if (!head.IsActive)
        {
            throw new InvalidOperationException(
                $"المركز الافتراضي «{defaultCode}» موقوف. / The default centre '{defaultCode}' is suspended.");
        }

        _centers = centers;
        _default = defaultCode;
    }

    /// <summary>مراكز التكلفة كلّها، مرتَّبة برمزها ترتيباً حرفياً ثابتاً.</summary>
    public ImmutableArray<CostCenter> All => _centers;

    /// <summary>رمز المركز الافتراضي — ما يُرحَّل عليه حين لا يختار المستخدم شيئاً.</summary>
    public CostCenterCode Default => _default;

    /// <summary>المركز الافتراضي نفسه. موجود دائماً بحكم ثابتة النوع.</summary>
    public CostCenter DefaultCenter => _centers.First(center => center.Code == _default);

    /// <summary>عدد المراكز.</summary>
    public int Count => _centers.Length;

    /// <summary>عدد المراكز العاملة. واحد على الأقل دائماً — الافتراضي لا يُوقَف.</summary>
    public int ActiveCount => _centers.Count(static center => center.IsActive);

    /// <summary>
    /// يفتح سجلّاً بمركزٍ أول هو افتراضيّه. المصدر الوحيد للسجلّات.
    /// </summary>
    /// <param name="nameAr">اسم المركز الأول بالعربية — اسم المنشأة نفسه حين تكون المراكز واحداً.</param>
    /// <param name="translations">ترجمات الاسم، إن وُجدت.</param>
    public static Result<CostCenterRegister> Open(string nameAr, IReadOnlyDictionary<string, string>? translations)
    {
        List<Error> errors = [];
        string trimmed = Normalise(nameAr, errors);
        ImmutableSortedDictionary<string, string> names = NormaliseTranslations(translations, errors);

        if (errors.Count > 0)
        {
            return Result<CostCenterRegister>.Failure(errors);
        }

        CostCenterCode code = Mint(1);
        return Result<CostCenterRegister>.Success(
            new CostCenterRegister(
                [new CostCenter(code, new TranslatedName(trimmed, names), CostCenterState.Active, string.Empty)],
                code));
    }

    /// <summary>المركز صاحب هذا الرمز، أو <c>null</c>.</summary>
    /// <param name="code">الرمز.</param>
    public CostCenter? Find(CostCenterCode code) => _centers.FirstOrDefault(center => center.Code == code);

    /// <summary>يضيف مركزاً عاملاً جديداً ويُرجع سجلّاً جديداً.</summary>
    /// <param name="nameAr">الاسم العربي.</param>
    /// <param name="translations">الترجمات، إن وُجدت.</param>
    public Result<CostCenterRegister> Add(string nameAr, IReadOnlyDictionary<string, string>? translations)
    {
        List<Error> errors = [];
        string trimmed = Normalise(nameAr, errors);
        ImmutableSortedDictionary<string, string> names = NormaliseTranslations(translations, errors);

        if (_centers.Length >= CompanySetupLimits.MaximumCostCenters)
        {
            errors.Add(CompanySetupErrors.TooManyCostCenters);
        }

        if (errors.Count == 0 && HasName(trimmed, CostCenterCode.None))
        {
            errors.Add(CompanySetupErrors.CostCenterNameRepeated(trimmed));
        }

        if (errors.Count > 0)
        {
            return Result<CostCenterRegister>.Failure(errors);
        }

        CostCenter added = new(
            Mint(_centers.Length + 1), new TranslatedName(trimmed, names), CostCenterState.Active, string.Empty);
        return Result<CostCenterRegister>.Success(new CostCenterRegister(Order(_centers.Add(added)), _default));
    }

    /// <summary>
    /// يعيد تسمية مركز. <b>مسموح دائماً</b>: الاسم عرضٌ والهوية هي الرمز، وسطور القيود
    /// تحمل الرمز — فالتاريخ المُرحَّل يبقى مربوطاً ويُعرض بالاسم الجاري.
    /// </summary>
    /// <param name="code">رمز المركز.</param>
    /// <param name="nameAr">الاسم العربي الجديد.</param>
    /// <param name="translations">الترجمات الجديدة، إن وُجدت.</param>
    public Result<CostCenterRegister> Rename(
        CostCenterCode code,
        string nameAr,
        IReadOnlyDictionary<string, string>? translations)
    {
        CostCenter? existing = Find(code);
        List<Error> errors = [];

        if (existing is null)
        {
            errors.Add(CompanySetupErrors.CostCenterNotFound(code.Value ?? string.Empty));
        }

        string trimmed = Normalise(nameAr, errors);
        ImmutableSortedDictionary<string, string> names = NormaliseTranslations(translations, errors);

        if (errors.Count == 0 && HasName(trimmed, code))
        {
            errors.Add(CompanySetupErrors.CostCenterNameRepeated(trimmed));
        }

        if (errors.Count > 0)
        {
            return Result<CostCenterRegister>.Failure(errors);
        }

        return Result<CostCenterRegister>.Success(
            Replace(new CostCenter(
                code, new TranslatedName(trimmed, names), existing!.State, existing.SuspensionReason)));
    }

    /// <summary>
    /// يوقف مركزاً عن الترحيل — حالة عمل يضبطها إنسان بسبب مكتوب (ADR-0006)، لا حذف.
    /// <b>والافتراضي لا يُوقَف</b>: ذلك هو الحارس الذي يجعل «منشأة بلا مركز تكلفة» غير
    /// قابلة للتمثيل.
    /// </summary>
    /// <param name="code">رمز المركز.</param>
    /// <param name="reason">السبب المكتوب.</param>
    public Result<CostCenterRegister> Suspend(CostCenterCode code, string? reason)
    {
        CostCenter? existing = Find(code);

        if (existing is null)
        {
            return Result<CostCenterRegister>.Failure(CompanySetupErrors.CostCenterNotFound(code.Value ?? string.Empty));
        }

        List<Error> errors = [];

        if (code == _default)
        {
            errors.Add(CompanySetupErrors.DefaultCostCenterCannotBeSuspended(code.Value ?? string.Empty));
        }

        if (!existing.IsActive)
        {
            errors.Add(CompanySetupErrors.CostCenterAlreadySuspended(code.Value ?? string.Empty));
        }

        string written = reason?.Trim() ?? string.Empty;

        if (written.Length is < CompanySetupLimits.MinimumReasonLength or > CompanySetupLimits.MaximumReasonLength)
        {
            errors.Add(CompanySetupErrors.SuspensionReasonRequired);
        }

        return errors.Count > 0
            ? Result<CostCenterRegister>.Failure(errors)
            : Result<CostCenterRegister>.Success(
                Replace(new CostCenter(code, existing.Name, CostCenterState.Suspended, written)));
    }

    /// <summary>يعيد مركزاً موقوفاً إلى العمل.</summary>
    /// <param name="code">رمز المركز.</param>
    public Result<CostCenterRegister> Reinstate(CostCenterCode code)
    {
        CostCenter? existing = Find(code);

        if (existing is null)
        {
            return Result<CostCenterRegister>.Failure(CompanySetupErrors.CostCenterNotFound(code.Value ?? string.Empty));
        }

        return existing.IsActive
            ? Result<CostCenterRegister>.Failure(CompanySetupErrors.CostCenterAlreadyActive(code.Value ?? string.Empty))
            : Result<CostCenterRegister>.Success(
                Replace(new CostCenter(code, existing.Name, CostCenterState.Active, string.Empty)));
    }

    /// <summary>
    /// ينقل صفة «الافتراضي» إلى مركز عامل آخر. هذا هو الطريق المشروع الوحيد إلى إيقاف
    /// المركز الذي كان افتراضياً — ولذلك ليس حارس الإيقاف حارساً ضامراً: الحالة التي
    /// يمنعها بالغةٌ فعلاً بخطوتين.
    /// </summary>
    /// <param name="code">رمز المركز الذي يصير افتراضياً.</param>
    public Result<CostCenterRegister> MoveDefault(CostCenterCode code)
    {
        CostCenter? target = Find(code);

        if (target is null)
        {
            return Result<CostCenterRegister>.Failure(CompanySetupErrors.CostCenterNotFound(code.Value ?? string.Empty));
        }

        return target.IsActive
            ? Result<CostCenterRegister>.Success(new CostCenterRegister(_centers, code))
            : Result<CostCenterRegister>.Failure(CompanySetupErrors.DefaultMustBeActive(code.Value ?? string.Empty));
    }

    /// <summary>
    /// يحلّ مركز التكلفة الواصل على مستند: المذكور إن كان عاملاً، والافتراضي إن لم يُذكر
    /// شيء. <b>لا يُرجع فارغاً أبداً</b> — وهذا هو الموضع الذي تصير فيه ثابتة «‏<c>CostCenterId</c>
    /// غير فارغ في أي موضع» أمراً واقعاً لا وعداً.
    /// </summary>
    /// <param name="requested">الرمز المذكور على المستند، أو غيابه.</param>
    public Result<CostCenterCode> Resolve(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return Result<CostCenterCode>.Success(_default);
        }

        CostCenterCode code = new(requested);
        CostCenter? found = Find(code);

        if (found is null)
        {
            return Result<CostCenterCode>.Failure(CompanySetupErrors.CostCenterNotFound(requested));
        }

        return found.IsActive
            ? Result<CostCenterCode>.Success(code)
            : Result<CostCenterCode>.Failure(CompanySetupErrors.CostCenterAlreadySuspended(requested));
    }

    private static CostCenterCode Mint(int ordinal)
        => new("cc." + ordinal.ToString("000", CultureInfo.InvariantCulture));

    private static ImmutableArray<CostCenter> Order(ImmutableArray<CostCenter> centers)
        => [.. centers.OrderBy(static center => center.Code.Value, StringComparer.Ordinal)];

    private CostCenterRegister Replace(CostCenter center)
        => new(Order(_centers.RemoveAll(existing => existing.Code == center.Code).Add(center)), _default);

    private bool HasName(string nameAr, CostCenterCode except)
        => _centers.Any(center => center.Code != except && string.Equals(center.NameAr, nameAr, StringComparison.Ordinal));

    private static string Normalise(string? nameAr, List<Error> errors)
    {
        string trimmed = nameAr?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            errors.Add(CompanySetupErrors.CostCenterNameMissing);
        }
        else if (trimmed.Length > CompanySetupLimits.MaximumNameLength)
        {
            errors.Add(CompanySetupErrors.NameTooLong("nameAr"));
        }

        return trimmed;
    }

    internal static ImmutableSortedDictionary<string, string> NormaliseTranslations(
        IReadOnlyDictionary<string, string>? translations,
        List<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        ImmutableSortedDictionary<string, string>.Builder builder =
            ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        if (translations is null || translations.Count == 0)
        {
            return builder.ToImmutable();
        }

        if (translations.Count > CompanySetupLimits.MaximumTranslations)
        {
            errors.Add(CompanySetupErrors.TooManyTranslations);
            return builder.ToImmutable();
        }

        foreach (string tag in translations.Keys.Order(StringComparer.Ordinal))
        {
            if (!IsWellFormedTag(tag))
            {
                errors.Add(CompanySetupErrors.LanguageTagMalformed(tag));
                continue;
            }

            // العربية سجلٌّ لا ترجمة (ADR-0021 بند 1). ومدخلٌ باسم «ar» يُنتج اسمين
            // عربيين لكيان واحد ولا يوجد ما يجعلهما يتطابقان — فيُرفض بصوته لا يُطرَح صامتاً.
            if (IsRecordLanguage(tag))
            {
                errors.Add(CompanySetupErrors.ArabicIsNotATranslation(tag));
                continue;
            }

            string value = translations[tag]?.Trim() ?? string.Empty;

            if (value.Length == 0)
            {
                errors.Add(CompanySetupErrors.TranslationEmpty(tag));
                continue;
            }

            if (value.Length > CompanySetupLimits.MaximumNameLength)
            {
                errors.Add(CompanySetupErrors.NameTooLong(tag));
                continue;
            }

            builder[tag] = value;
        }

        return builder.ToImmutable();
    }

    private static bool IsWellFormedTag(string? tag)
        => tag is { Length: <= CompanySetupLimits.MaximumLanguageTagLength }
            && TranslatedName.IsWellFormedLanguageTag(tag);

    private static bool IsRecordLanguage(string tag)
        => string.Equals(tag, TranslatedName.RecordLanguageTag, StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith(TranslatedName.RecordLanguageTag + "-", StringComparison.OrdinalIgnoreCase);
}
