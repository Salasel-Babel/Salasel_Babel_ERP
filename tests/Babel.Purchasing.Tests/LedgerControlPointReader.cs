using Babel.Purchasing.Subledger;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Purchasing.Tests;

/// <summary>
/// محوّل نقطة الضبط: يقرأ من دفتر الأستاذ صافي حركة سطور دفتر مساعد بعينه.
/// <para>
/// <b>موضعه الطبيعي هو الجذر التركيبي</b> (‏<c>Babel.Api</c>): وحدة المشتريات تُعلن
/// المنفذ ولا تعرف الدفتر، والدفتر لا يعرف المشتريات، والجذر وحده يعرف الاثنين.
/// وُضع هنا لأن الجذر ليس ملك هذا التسليم — وهو بند في التقرير.
/// </para>
/// <para>
/// ولاحظ أنه <b>لا يسمّي حساباً</b>: الاستعلام على <c>subledger_kind</c> لا على رقم
/// حساب، فأي حساب ضابط يُضاف لاحقاً لهذا الدفتر المساعد يدخل المطابقة من تلقاء نفسه.
/// </para>
/// </summary>
internal sealed class LedgerControlPointReader(string connectionString) : IControlPointReader
{
    private readonly string _connectionString = connectionString;

    public async ValueTask<Result<ControlPointSnapshot>> ReadAsync(
        TenantId tenant,
        string subledgerKind,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        List<ControlPointMovement> movements = [];
        decimal net = 0m;

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select e.source_doc_type,
                   e.source_doc_id,
                   coalesce(l.subledger_party_id, ''),
                   sum(l.debit_company - l.credit_company)
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where l.company_id = $1
               and l.subledger_kind = $2
               and e.entry_date <= $3
             group by e.source_doc_type, e.source_doc_id, coalesce(l.subledger_party_id, '')
             order by e.source_doc_type, e.source_doc_id
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(subledgerKind);
        command.Parameters.AddWithValue(asOf);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            decimal value = reader.GetDecimal(3);
            net += value;
            movements.Add(new ControlPointMovement(reader.GetString(0), reader.GetString(1), reader.GetString(2), value));
        }

        return Result<ControlPointSnapshot>.Success(new ControlPointSnapshot(net, movements));
    }
}

/// <summary>قراءات مباشرة من الدفتر تُستعمل في الإثبات وحده.</summary>
internal static class LedgerProbe
{
    /// <summary>رصيد نقطة ضبط دفتر مساعد: مدين ناقص دائن.</summary>
    public static async Task<decimal> ControlNetAsync(
        string connectionString, TenantId tenant, string subledgerKind, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            select coalesce(sum(debit_company - credit_company), 0)
              from ledger.journal_line
             where company_id = $1 and subledger_kind = $2
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(subledgerKind);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>عدد القيود التي كُتبت لمستند بعينه.</summary>
    public static async Task<long> EntryCountAsync(
        string connectionString, TenantId tenant, string documentType, string documentId,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            select count(*) from ledger.journal_entry
             where company_id = $1 and source_doc_type = $2 and source_doc_id = $3
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(documentType);
        command.Parameters.AddWithValue(documentId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>حالة قيد بعينه — يُثبت أن الأصل لم يُمسّ بعد العكس.</summary>
    public static async Task<(string Status, long Lines)> EntryAsync(
        string connectionString, Guid entryId, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            select e.status, (select count(*) from ledger.journal_line l where l.entry_id = e.entry_id)
              from ledger.journal_entry e where e.entry_id = $1
            """, connection);
        command.Parameters.AddWithValue(entryId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return (reader.GetString(0), reader.GetInt64(1));
    }
}
