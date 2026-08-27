using System.Collections.Immutable;
using System.Globalization;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Core.Persistence;

/// <summary>
/// مخزن التأسيس فوق PostgreSQL — <b>وهو ما يجعل خادماً أُعيد إقلاعه يعرف منشأته</b>.
/// <para>
/// النسخة السابقة كانت <c>InMemoryCompanySetupStore</c>: حالتُها عمرُ العملية. وكل
/// مسار كتابة في النظام يسأل <see cref="ICostCenterResolver"/> قبل أن يبني طلباً
/// (ADR-0026 · ADR-0029)، فخادمٌ أُقلع للتوّ كان يردّ كل ترحيل بـ
/// <c>company_setup.not_found</c> بينما تعمل شاشات القراءة كلّها — عرضٌ يقرأ ولا يكتب.
/// </para>
/// <para>
/// <b>والتحميل يمرّ من المُنشئ نفسه لا من جواره:</b>
/// <see cref="FoundedCompany.Rehydrate"/> و<see cref="CostCenterRegister.Rehydrate"/>
/// تناديان المُنشئ الخاصّ الذي يرمي على السجلّ الفارغ وعلى الافتراضي الغائب أو الموقوف.
/// فطبقةُ استمرارية تستطيع أن تُرجع كائناً مخالفاً تكون قد <b>أزالت الثابتة صامتةً</b>،
/// وهذا هو الموضع الذي يقع فيه ذلك عادةً.
/// </para>
/// <para>
/// <b>ولا يُحذف صفٌّ واحد في هذا النوع</b> عدا صفوف الترجمة: مراكز التكلفة تُضاف
/// وتُحدَّث ولا تُحذف — وصلاحية <c>DELETE</c> عليها مسحوبة من دور التطبيق أصلاً، فلو
/// كُتب حذفٌ هنا يوماً لردّه PostgreSQL بالرمز 42501 قبل أي منطق.
/// </para>
/// </summary>
internal sealed class PostgresCompanySetupStore : ICompanySetupStore
{
    private readonly DbContextOptions<CoreDbContext> _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public PostgresCompanySetupStore(CoreOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.AppConnectionString);
        _options = builder.Options;
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<FoundedCompany?> FindAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        await using CoreDbContext context = new(_options);
        return await ReadAsync(context, tenant, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryFoundAsync(FoundedCompany setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);

        await using CoreDbContext context = new(_options);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        Guid company = setup.Company.Value;

        context.CompanySetups.Add(new CompanySetupRow
        {
            CompanyId = company,
            NameAr = setup.NameAr,
            DecimalPlaces = setup.DisplayScale.Places,
            DefaultCostCenter = setup.CostCenters.Default.Value ?? string.Empty,
            FoundedAt = _clock.GetUtcNow(),
        });

        foreach (CostCenter center in setup.CostCenters.All)
        {
            context.CostCenters.Add(ToRow(company, center));
            AddTranslations(context, company, CoreTranslationKinds.CostCenter, center.Code.Value ?? string.Empty, center.Translations);
        }

        AddTranslations(context, company, CoreTranslationKinds.Company, KeyOf(company), setup.Translations);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException failure) when (IsUniqueViolation(failure))
        {
            // «مؤسَّسة من قبل» ليست عطلاً بل الجواب: التأسيس الثاني لا ينجح مرّتين،
            // والذرّية هنا مفتاحٌ أوّلي في PostgreSQL لا فحصٌ يسبق كتابةً (سباق).
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryReplaceCostCentersAsync(
        TenantId tenant,
        CostCenterRegister costCenters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costCenters);

        await using CoreDbContext context = new(_options);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        Guid company = tenant.Value;

        CompanySetupRow? setup = await context.CompanySetups
            .FirstOrDefaultAsync(row => row.CompanyId == company, cancellationToken)
            .ConfigureAwait(false);

        if (setup is null)
        {
            return false;
        }

        List<CostCenterRow> existing = await context.CostCenters
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, CostCenterRow> byCode =
            existing.ToDictionary(row => row.Code, StringComparer.Ordinal);

        foreach (CostCenter center in costCenters.All)
        {
            string code = center.Code.Value ?? string.Empty;

            if (byCode.TryGetValue(code, out CostCenterRow? row))
            {
                row.NameAr = center.NameAr;
                row.State = center.IsActive ? CostCenterStates.Active : CostCenterStates.Suspended;
                row.SuspensionReason = center.SuspensionReason;
            }
            else
            {
                context.CostCenters.Add(ToRow(company, center));
            }

            await ReplaceTranslationsAsync(
                context, company, CoreTranslationKinds.CostCenter, code, center.Translations, cancellationToken)
                .ConfigureAwait(false);
        }

        setup.DefaultCostCenter = costCenters.Default.Value ?? string.Empty;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<FoundedCompany?> ReadAsync(
        CoreDbContext context, TenantId tenant, CancellationToken cancellationToken)
    {
        Guid company = tenant.Value;

        CompanySetupRow? setup = await context.CompanySetups
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.CompanyId == company, cancellationToken)
            .ConfigureAwait(false);

        if (setup is null)
        {
            return null;
        }

        List<CostCenterRow> centers = await context.CostCenters
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CoreNameTranslationRow> translations = await context.NameTranslations
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Materialise(setup, centers, translations);
    }

    /// <summary>
    /// يبني المنشأة من صفوفها. <b>وكل رفضٍ هنا استثناء لا <c>null</c></b>: صفٌّ مخالف
    /// خللٌ في المخزن لا «غياب»، وإرجاعُه فارغاً يجعل الخادم يقول «لم تُؤسَّس» عن
    /// منشأة مؤسَّسة بصفوف تالفة — وهو أسوأ جواب ممكن لمن يشخّص.
    /// </summary>
    private static FoundedCompany Materialise(
        CompanySetupRow setup,
        IReadOnlyList<CostCenterRow> centers,
        IReadOnlyList<CoreNameTranslationRow> translations)
    {
        ImmutableArray<CostCenter> read =
        [
            .. centers.Select(row => new CostCenter(
                new CostCenterCode(row.Code),
                new TranslatedName(row.NameAr, TranslationsOf(translations, CoreTranslationKinds.CostCenter, row.Code)),
                StateOf(row),
                row.SuspensionReason)),
        ];

        CostCenterRegister register = CostCenterRegister.Rehydrate(read, new CostCenterCode(setup.DefaultCostCenter));

        return FoundedCompany.Rehydrate(
            new TenantId(setup.CompanyId),
            new TranslatedName(
                setup.NameAr,
                TranslationsOf(translations, CoreTranslationKinds.Company, KeyOf(setup.CompanyId))),
            setup.DecimalPlaces,
            register);
    }

    private static CostCenterState StateOf(CostCenterRow row) => row.State switch
    {
        CostCenterStates.Active => CostCenterState.Active,
        CostCenterStates.Suspended => CostCenterState.Suspended,
        _ => throw new InvalidOperationException(
            $"حالة مركز تكلفة مجهولة في المخزن: «{row.State}» على «{row.Code}». / "
            + $"An unknown cost-centre state in the store: '{row.State}' on '{row.Code}'."),
    };

    private static Dictionary<string, string> TranslationsOf(
        IReadOnlyList<CoreNameTranslationRow> rows, string kind, string key)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        foreach (CoreNameTranslationRow row in rows)
        {
            if (string.Equals(row.EntityKind, kind, StringComparison.Ordinal)
                && string.Equals(row.EntityKey, key, StringComparison.Ordinal))
            {
                map[row.LanguageTag] = row.Name;
            }
        }

        return map;
    }

    private static CostCenterRow ToRow(Guid company, CostCenter center) => new()
    {
        CompanyId = company,
        Code = center.Code.Value ?? string.Empty,
        NameAr = center.NameAr,
        State = center.IsActive ? CostCenterStates.Active : CostCenterStates.Suspended,
        SuspensionReason = center.SuspensionReason,
    };

    private static void AddTranslations(
        CoreDbContext context,
        Guid company,
        string kind,
        string key,
        ImmutableSortedDictionary<string, string> translations)
    {
        foreach (KeyValuePair<string, string> pair in translations)
        {
            context.NameTranslations.Add(new CoreNameTranslationRow
            {
                CompanyId = company,
                EntityKind = kind,
                EntityKey = key,
                LanguageTag = pair.Key,
                Name = pair.Value,
            });
        }
    }

    /// <summary>
    /// يستبدل ترجمات كيان. <b>والحذف هنا مقصود ومرخَّص</b>: إعادةُ تسمية تُسقط لغةً
    /// كانت مكتوبة، وترجمةٌ قديمة معلّقة على اسم جديد أسوأ من غياب الترجمة.
    /// </summary>
    private static async Task ReplaceTranslationsAsync(
        CoreDbContext context,
        Guid company,
        string kind,
        string key,
        ImmutableSortedDictionary<string, string> translations,
        CancellationToken cancellationToken)
    {
        List<CoreNameTranslationRow> existing = await context.NameTranslations
            .Where(row => row.CompanyId == company && row.EntityKind == kind && row.EntityKey == key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (CoreNameTranslationRow row in existing)
        {
            if (translations.TryGetValue(row.LanguageTag, out string? name))
            {
                row.Name = name;
            }
            else
            {
                context.NameTranslations.Remove(row);
            }
        }

        foreach (KeyValuePair<string, string> pair in translations)
        {
            if (!existing.Any(row => string.Equals(row.LanguageTag, pair.Key, StringComparison.Ordinal)))
            {
                context.NameTranslations.Add(new CoreNameTranslationRow
                {
                    CompanyId = company,
                    EntityKind = kind,
                    EntityKey = key,
                    LanguageTag = pair.Key,
                    Name = pair.Value,
                });
            }
        }
    }

    /// <summary>مفتاح المنشأة نصّاً — بثقافة ثابتة، فالمفتاح لا يقرأ ثقافة العملية (القاعدة 10).</summary>
    private static string KeyOf(Guid company) => company.ToString("D", CultureInfo.InvariantCulture);

    private static bool IsUniqueViolation(DbUpdateException failure)
        => failure.InnerException is PostgresException postgres
            && string.Equals(postgres.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
