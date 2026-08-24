using System.Collections.Frozen;
using Npgsql;

namespace Babel.Ledger.PostingMatrix;

/// <summary>ما يحتاجه المحرك من حساب قبل أن يكتب سطراً عليه.</summary>
internal sealed record AccountFacts(
    string Code,
    string NameAr,
    string NameEn,
    string AccountType,
    string NaturalSide,
    bool IsPostable,
    IReadOnlyList<string> RequiredDimensions,
    string SubledgerType,
    string CurrencyMode,
    string? CurrencyCode,
    bool IsActive);

/// <summary>حالة فترة مالية كما يراها المحرك قبل النداء.</summary>
internal sealed record PeriodFacts(
    string PeriodCode,
    int FiscalYear,
    int PeriodNo,
    string State,
    DateOnly StartsOn,
    DateOnly EndsOn);

/// <summary>
/// البيانات المرجعية لشركة واحدة: الحسابات، وخريطة الدور⇒الحساب، وأبعاد العقار،
/// والفترات.
/// <para>
/// <b>لماذا لقطة في الذاكرة:</b> الترحيل مكالمة خادم واحدة (فخ-14)، ومكالمة واحدة
/// تعني ألا تُقرأ البيانات المرجعية في مسار الترحيل. وهذه البيانات <b>لا يستطيع
/// الدور التطبيقي تعديلها أصلاً</b>: الصلاحيات تمنحه <c>SELECT</c> فقط عليها
/// وتسحب <c>INSERT/UPDATE/DELETE</c> (LedgerGrants.sql) — فاللقطة لا يمكن أن
/// تتقادم بفعل التطبيق نفسه.
/// </para>
/// <para>
/// والاستثناء المقصود: <b>حالة الفترة</b>. الفترة يقفلها المالك، ولذلك حالتها
/// هنا للرسالة المبكرة المفهومة فقط، والفحص <b>الحاسم</b> داخل
/// <c>ledger.post_entry</c> حيث هو ذرّي مع الكتابة.
/// </para>
/// </summary>
internal sealed class CompanyReference
{
    private CompanyReference(
        Guid companyId,
        FrozenDictionary<string, AccountFacts> accounts,
        FrozenDictionary<string, string> roleMap,
        FrozenDictionary<string, string> propertyOwnership,
        FrozenDictionary<string, PeriodFacts> periods)
    {
        CompanyId = companyId;
        Accounts = accounts;
        RoleMap = roleMap;
        PropertyOwnership = propertyOwnership;
        Periods = periods;
    }

    public Guid CompanyId { get; }

    public FrozenDictionary<string, AccountFacts> Accounts { get; }

    /// <summary>المفتاح <c>role|qualifier</c>. لكل دور صفّ بالمؤهّل <c>*</c> إلزاماً.</summary>
    public FrozenDictionary<string, string> RoleMap { get; }

    public FrozenDictionary<string, string> PropertyOwnership { get; }

    public FrozenDictionary<string, PeriodFacts> Periods { get; }

    /// <summary>
    /// الفترة التي يقع فيها التاريخ. <b>من مدى الفترة لا من السنة الميلادية:</b> سنة
    /// مالية لا تبدأ في يناير ليست حالة نادرة، واشتقاق «yyyy-MM» من التاريخ يجعل
    /// كل قيد في شركة كهذه يقع في الفترة الخطأ بصمت.
    /// </summary>
    public PeriodFacts? PeriodOf(DateOnly date)
    {
        foreach (PeriodFacts period in Periods.Values)
        {
            if (date >= period.StartsOn && date <= period.EndsOn)
            {
                return period;
            }
        }

        return null;
    }

    /// <summary>
    /// يحلّ الدور إلى رمز حساب: المؤهّل المطلوب أولاً، ثم <c>*</c>.
    /// <b>لا يخترع حساباً ولا يقع على افتراضي:</b> دور بلا تعيين يوقف المحرك.
    /// </summary>
    public string? ResolveRole(string roleCode, string qualifier)
    {
        if (!string.IsNullOrEmpty(qualifier) && qualifier != "*"
            && RoleMap.TryGetValue(roleCode + "|" + qualifier, out string? qualified))
        {
            return qualified;
        }

        return RoleMap.TryGetValue(roleCode + "|*", out string? fallback) ? fallback : null;
    }

    public static async Task<CompanyReference> LoadAsync(
        NpgsqlDataSource dataSource,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        Dictionary<string, AccountFacts> accounts = new(StringComparer.Ordinal);
        Dictionary<string, string> roles = new(StringComparer.Ordinal);
        Dictionary<string, string> properties = new(StringComparer.Ordinal);
        Dictionary<string, PeriodFacts> periods = new(StringComparer.Ordinal);

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand command = new(
            """
            select account_code, name_ar, name_en, account_type, natural_side, is_postable,
                   required_dimensions, subledger_type, currency_mode, currency_code, is_active
              from ledger.account where company_id = $1
            """, connection))
        {
            command.Parameters.AddWithValue(companyId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                accounts[reader.GetString(0)] = new AccountFacts(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetBoolean(5), reader.GetFieldValue<string[]>(6),
                    reader.GetString(7), reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetBoolean(10));
            }
        }

        await using (NpgsqlCommand command = new(
            "select role_code, qualifier, account_code from ledger.role_account_map where company_id = $1", connection))
        {
            command.Parameters.AddWithValue(companyId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                roles[reader.GetString(0) + "|" + reader.GetString(1)] = reader.GetString(2);
            }
        }

        await using (NpgsqlCommand command = new(
            "select property_id, ownership_model from ledger.property_dimension where company_id = $1", connection))
        {
            command.Parameters.AddWithValue(companyId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                properties[reader.GetString(0)] = reader.GetString(1);
            }
        }

        await using (NpgsqlCommand command = new(
            "select period_code, fiscal_year, period_no, state, starts_on, ends_on from ledger.fiscal_period where company_id = $1", connection))
        {
            command.Parameters.AddWithValue(companyId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                periods[reader.GetString(0)] = new PeriodFacts(
                    reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
                    reader.GetFieldValue<DateOnly>(4), reader.GetFieldValue<DateOnly>(5));
            }
        }

        return new CompanyReference(
            companyId,
            accounts.ToFrozenDictionary(StringComparer.Ordinal),
            roles.ToFrozenDictionary(StringComparer.Ordinal),
            properties.ToFrozenDictionary(StringComparer.Ordinal),
            periods.ToFrozenDictionary(StringComparer.Ordinal));
    }
}

/// <summary>
/// مخزن اللقطات لكل شركة. <c>internal</c>: لا شيء خارج الدفتر يرى حساباً
/// (القاعدة 2).
/// </summary>
internal sealed class LedgerReferenceCache(NpgsqlDataSource dataSource) : IDisposable
{
    private readonly Dictionary<Guid, CompanyReference> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<CompanyReference> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(companyId, out CompanyReference? cached))
            {
                return cached;
            }
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(companyId, out CompanyReference? cached))
                {
                    return cached;
                }
            }

            CompanyReference loaded = await CompanyReference
                .LoadAsync(dataSource, companyId, cancellationToken).ConfigureAwait(false);

            lock (_cache)
            {
                _cache[companyId] = loaded;
            }

            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>يُسقط اللقطة بعد تغيير يملكه المالك (بذر حسابات، إقفال فترة).</summary>
    public void Invalidate(Guid companyId)
    {
        lock (_cache)
        {
            _cache.Remove(companyId);
        }
    }
}
