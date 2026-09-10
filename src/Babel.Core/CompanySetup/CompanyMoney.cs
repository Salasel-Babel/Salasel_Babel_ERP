using System.Collections.Concurrent;
using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// <b>عملةُ المنشأة ووحدتُها الصغرى — يُسنَدان عند التأسيس ولا يتغيّران بعده.</b>
/// <para>
/// هذا هو الجواب على سؤالٍ كان يُجاب عنه في الشيفرة: <c>CompanyCurrency = "SAR"</c> في
/// سبعة أنواعِ إعدادات، و<c>Halalas = 2</c> في أربعة أنواعِ حساب. فكانت المنشأةُ
/// الكويتية تُقرَّب فواتيرُها إلى الهللة وهي تدفع بالفلس، وكان تغييرُ العملة إعادةَ
/// ترجمة. والموضعُ الصحيح صفُّ التأسيس: <b>عملةُ العرض والقياس</b> (IAS 21 — العملة
/// الوظيفية) تُسنَد مرّة، وتغييرُها ليس تعديلَ إعدادٍ بل حدثٌ محاسبيّ يُطبَّق
/// <b>مستقبلاً</b> على دفاترَ تُفتح من جديد؛ ولذلك لا يوجد في الشجرة توقيعٌ يحمل
/// عملةً ثانية إلى منشأةٍ قائمة، ومشغّلُ <c>company_setup_is_immutable</c> يقفل
/// البابَ الآخر في PostgreSQL (ADR-0089).
/// </para>
/// <para>
/// <b>والوحدةُ الصغرى ليست قراراً:</b> تُشتقّ من ISO 4217 (<see cref="Iso4217"/>)
/// وتُخزَّن بجوار العملة كي يصف الصفُّ نفسَه إلى الأبد — قيدٌ رُحِّل بثلاث خانات
/// يبقى مفهوماً ولو تغيّر الجدولُ المرجعي بعده.
/// </para>
/// <para>
/// <b>وما لا تحكمه هذه القيمة:</b> التخزين (<see cref="Money.CanonicalScale"/> — أربعٌ
/// دائماً) ولا مقياسَ العرض (<see cref="DisplayScale"/> — ما يراه الإنسان). ما تحكمه
/// هو <b>تقريبُ السطر والمستند</b>: أصغرُ مبلغٍ يمكن أن يُدفع فعلاً، وهو ما يُوقَف
/// عنده حسابُ الضريبة على كلّ سطر ثم يُجمع.
/// </para>
/// </summary>
public readonly record struct CompanyMoney
{
    private readonly bool _assigned;

    private CompanyMoney(CurrencyCode currency, int minorUnits)
    {
        Currency = currency;
        MinorUnits = minorUnits;
        _assigned = true;
    }

    /// <summary>عملةُ المنشأة.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>عددُ خانات الوحدة الصغرى — الهللة خانتان، والفلسُ الكويتي ثلاث.</summary>
    public int MinorUnits { get; }

    /// <summary>هل القيمةُ مُسنَدة؟ قيمةٌ افتراضية من هذا النوع خطأٌ برمجي لا عملة.</summary>
    public bool IsAssigned => _assigned;

    /// <summary>
    /// يبني عملةَ منشأة، أو يرفض: رمزٌ مشوَّه، أو عملةٌ ليست في الجدول المرجعي —
    /// <b>ولا يُخمَّن لها عددُ خانات</b>.
    /// </summary>
    /// <param name="code">رمز العملة كما كُتب.</param>
    public static Result<CompanyMoney> Of(string? code)
    {
        string trimmed = code?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return Result<CompanyMoney>.Failure(CompanySetupErrors.CurrencyMissing);
        }

        CurrencyCode currency;

        try
        {
            currency = CurrencyCode.FromString(trimmed);
        }
        catch (ArgumentException)
        {
            return Result<CompanyMoney>.Failure(CompanySetupErrors.CurrencyMalformed(trimmed));
        }

        return Iso4217.MinorUnitsOf(currency) is int units
            ? Result<CompanyMoney>.Success(new CompanyMoney(currency, units))
            : Result<CompanyMoney>.Failure(CompanySetupErrors.CurrencyNotInReferenceTable(trimmed));
    }

    /// <summary>
    /// يُعيد بناء القيمة من الصفّ المخزَّن — بما خُزِّن، لا بما يقوله الجدولُ المرجعي
    /// اليوم: الصفُّ يصف نفسه.
    /// </summary>
    /// <param name="currencyCode">الرمز المخزَّن.</param>
    /// <param name="minorUnits">عددُ الخانات المخزَّن.</param>
    internal static CompanyMoney Rehydrate(string currencyCode, int minorUnits)
    {
        if (minorUnits is < 0 or > Money.CanonicalScale)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"وحدةٌ صغرى مخزَّنة خارج المدى: {minorUnits} للعملة «{currencyCode}». / A stored minor unit is out of range."));
        }

        return new CompanyMoney(CurrencyCode.FromString(currencyCode), minorUnits);
    }

    /// <summary>
    /// يقرّب إلى الوحدة الصغرى، والنصفُ يبتعد عن الصفر — وهي سياسةُ التقريب التجاري
    /// التي تطلبها الهيئات الضريبية للمبالغ على المستند، والسياسةُ نفسها في كلّ وحدة.
    /// </summary>
    /// <param name="value">القيمة.</param>
    public decimal Round(decimal value)
        => decimal.Round(value, _assigned ? MinorUnits : throw Unassigned(), MidpointRounding.AwayFromZero);

    /// <summary>مبلغٌ بعملة المنشأة.</summary>
    /// <param name="amount">المبلغ.</param>
    public Money Amount(decimal amount) => Money.Of(amount, _assigned ? Currency : throw Unassigned());

    /// <summary>صفرٌ بعملة المنشأة.</summary>
    public Money Zero => Money.Zero(_assigned ? Currency : throw Unassigned());

    /// <inheritdoc />
    public override string ToString()
        => _assigned ? string.Create(CultureInfo.InvariantCulture, $"{Currency} ({MinorUnits})") : "؟";

    private static InvalidOperationException Unassigned()
        => new("عملةُ منشأة غير مُسنَدة. / Uninitialised company money.");
}

/// <summary>
/// <b>يجيب الوحداتِ عن عملة المنشأة ووحدتها الصغرى</b> — بالنمط نفسه الذي تُحلّ به
/// مراكزُ التكلفة (<see cref="ICostCenterResolver"/>): المنشأةُ في كلّ نداء، والجوابُ
/// من صفّ التأسيس لا من نوعِ إعدادات. ومنشأةٌ لم تُؤسَّس بعد لا عملةَ لها، فالجوابُ
/// رفضٌ باسمه لا ريالٌ مُخترَع.
/// </summary>
public interface ICompanyMoneyResolver
{
    /// <summary>عملةُ المنشأة، أو رفضٌ إن لم تُؤسَّس.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<CompanyMoney>> ResolveAsync(TenantId company, CancellationToken cancellationToken = default);
}

/// <summary>
/// التطبيق فوق مخزن التأسيس. <b>ويحفظ الجوابَ الناجح إلى الأبد</b> — لا لأنّ القراءة
/// غالية بل لأنّ القيمة لا تتغيّر بحكم البنية والمشغّل معاً؛ وما لا يتغيّر لا يُعاد
/// سؤالُه في كلّ سطر فاتورة. والرفضُ لا يُحفظ: منشأةٌ تُؤسَّس بعد أوّل سؤال تُجاب
/// في السؤال التالي.
/// </summary>
public sealed class CompanyMoneyResolver : ICompanyMoneyResolver
{
    private readonly ICompanySetupStore _store;
    private readonly ConcurrentDictionary<TenantId, CompanyMoney> _resolved = new();

    /// <summary>ينشئ المُحلّل.</summary>
    /// <param name="store">مخزن التأسيس.</param>
    public CompanyMoneyResolver(ICompanySetupStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<Result<CompanyMoney>> ResolveAsync(TenantId company, CancellationToken cancellationToken = default)
    {
        if (_resolved.TryGetValue(company, out CompanyMoney cached))
        {
            return Result<CompanyMoney>.Success(cached);
        }

        FoundedCompany? setup = await _store.FindAsync(company, cancellationToken).ConfigureAwait(false);

        if (setup is null)
        {
            return Result<CompanyMoney>.Failure(CompanySetupErrors.NotFound);
        }

        _resolved.TryAdd(company, setup.Money);
        return Result<CompanyMoney>.Success(setup.Money);
    }
}
