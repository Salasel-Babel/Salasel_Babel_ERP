using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Metering;

public sealed record ModuleUsageRow(string ModuleCode, long Events, decimal Quantity);

public sealed record BillingPreview(
    Guid TenantId, string PeriodCode, string PlanCode,
    decimal PlanMonthly, decimal PerUserPrice, int IncludedUsers,
    string UserStrategyCode, int BillableUsers, int ChargeableUsers,
    decimal UserCharge, decimal Total, string Currency,
    IReadOnlyList<ModuleUsageRow> ModuleUsage,
    IReadOnlyList<BillableUserCount> AllStrategyCounts);

/// <summary>
/// استعلامات الفوترة. كل مبلغ <c>decimal</c> ⇄ <c>numeric(19,4)</c>؛ ولا عائم
/// في أي طبقة، ولا <c>double</c> في تجميع وسيط «لأنه أسرع».
/// </summary>
public sealed class MeteringQueries(ControlPlaneOptions options)
{
    public async Task EnsurePeriodAsync(string periodCode, CancellationToken ct = default)
    {
        var year = int.Parse(periodCode[..4]);
        var month = int.Parse(periodCode[5..7]);
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        await Db.WriteAsync(c, """
            insert into control.billing_period (period_code, starts_on, ends_on)
            values (@p, @s, @e)
            on conflict (period_code) do update
               set starts_on = excluded.starts_on, ends_on = excluded.ends_on
            """, 1, p =>
            {
                p.AddWithValue("p", periodCode);
                p.Add(Db.P("s", new DateOnly(year, month, 1), NpgsqlDbType.Date));
                p.Add(Db.P("e", new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
                    NpgsqlDbType.Date));
            }, null, ct);
    }

    public async Task<List<ModuleUsageRow>> ModuleUsageAsync(Guid tenantId, string periodCode,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        return await Db.QueryAsync(c, """
            select module_code, count(*), coalesce(sum(quantity), 0)
              from control.usage_event
             where tenant_id = @t and period_code = @p
             group by module_code
             order by module_code asc
            """,
            r => new ModuleUsageRow(r.GetString(0), r.GetInt64(1), r.GetDecimal(2)),
            p => { p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)); p.AddWithValue("p", periodCode); },
            null, ct);
    }

    public async Task<List<BillableUserCount>> AllUserCountsAsync(Guid tenantId, string periodCode,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        var result = new List<BillableUserCount>();
        foreach (var s in BillableUserStrategies.All)
            result.Add(new BillableUserCount(s.Code, s.NameAr, s.NameEn,
                await s.CountAsync(c, tenantId, periodCode, ct)));
        return result;
    }

    /// <summary>
    /// معاينة فاتورة على <b>المحورين معاً</b>: سعر الخطة الشهري (محور الوحدة)
    /// زائد سعر المستخدم عن المستخدمين الزائدين عن المُضمَّن (محور المستخدم).
    /// </summary>
    public async Task<BillingPreview> PreviewAsync(Guid tenantId, string periodCode,
        IBillableUserStrategy? strategy = null, CancellationToken ct = default)
    {
        strategy ??= BillableUserStrategies.Default;
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);

        var plan = await Db.QueryAsync(c, """
            select p.plan_code, p.monthly_price, p.per_user_price, p.included_users, p.currency
              from control.subscription s
              join control.plan p on p.plan_code = s.plan_code
             where s.tenant_id = @t and s.state = 'Active'
             order by s.started_on desc
             limit 1
            """,
            r => (Code: r.GetString(0), Monthly: r.GetDecimal(1), PerUser: r.GetDecimal(2),
                  Included: r.GetInt32(3), Cur: r.GetString(4)),
            p => p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)), null, ct);

        if (plan.Count == 0)
            throw new InvalidOperationException("لا اشتراك فعّال لهذا المستأجر — لا فاتورة تُبنى بلا خطة.");

        var users = await strategy.CountAsync(c, tenantId, periodCode, ct);
        var chargeable = Math.Max(0, users - plan[0].Included);
        var userCharge = decimal.Round(chargeable * plan[0].PerUser, 4, MidpointRounding.ToEven);
        var total = decimal.Round(plan[0].Monthly + userCharge, 4, MidpointRounding.ToEven);

        return new BillingPreview(tenantId, periodCode, plan[0].Code,
            plan[0].Monthly, plan[0].PerUser, plan[0].Included,
            strategy.Code, users, chargeable, userCharge, total, plan[0].Cur,
            await ModuleUsageAsync(tenantId, periodCode, ct),
            await AllUserCountsAsync(tenantId, periodCode, ct));
    }

    public async Task<long> EventCountAsync(Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        await Db.ScalarAsync<long>(options.ControlConnectionString, """
            select count(*) from control.usage_event where tenant_id = @t and period_code = @p
            """, p => { p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)); p.AddWithValue("p", periodCode); },
            ct);
}
