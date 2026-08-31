using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BabelRelationalSpike.Support;

/// <summary>
/// Captures the exact SQL (and parameters) EF Core sends, so the very same
/// command can be replayed under EXPLAIN (ANALYZE). Without this you would be
/// EXPLAINing hand-written SQL and claiming it proves something about EF Core.
/// </summary>
public sealed class SqlCapture : DbCommandInterceptor
{
    public string? CommandText { get; private set; }
    public List<NpgsqlParameter> Parameters { get; } = [];
    /// <summary>Every command EF Core sent, including the non-query ones.</summary>
    public List<string> History { get; } = [];

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        History.Add(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        History.Add(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    private void Capture(DbCommand command)
    {
        History.Add(command.CommandText);
        CommandText = command.CommandText;
        Parameters.Clear();
        foreach (NpgsqlParameter p in command.Parameters)
            Parameters.Add(new NpgsqlParameter
            {
                ParameterName = p.ParameterName,
                Value = p.Value,
                NpgsqlDbType = p.NpgsqlDbType,
                DataTypeName = p.DataTypeName
            });
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    /// <summary>Replays the captured command under EXPLAIN (ANALYZE, BUFFERS).</summary>
    public async Task<string> ExplainAsync(string connectionString)
    {
        if (CommandText is null) return "(nothing captured)";
        await using var conn = await Sql.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("explain (analyze, buffers, verbose false) " + CommandText, conn);
        foreach (var p in Parameters)
            cmd.Parameters.Add(new NpgsqlParameter
            {
                ParameterName = p.ParameterName,
                Value = p.Value,
                NpgsqlDbType = p.NpgsqlDbType,
                DataTypeName = p.DataTypeName
            });
        await using var r = await cmd.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await r.ReadAsync()) lines.Add(r.GetString(0));
        return string.Join("\n", lines);
    }
}
