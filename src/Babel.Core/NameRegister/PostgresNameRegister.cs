using System.Globalization;
using Babel.Contracts.Lookup;
using Npgsql;

namespace Babel.Core.NameRegister;

/// <summary>
/// <b>محوّل السجلّ على PostgreSQL — واحدٌ يخدم كل وحدةٍ مالكة بوصفِ جدولها.</b>
/// <para>
/// <b>ولماذا محوّلٌ واحد لا ستّة:</b> ستّ نسخٍ من أربعين سطر Npgsql تنحرف إحداها،
/// والانحراف هنا يعني سجلّاً واحداً يُطابَق بلا نطاق منشأة. والوصف يُمرَّر، فلا يظهر
/// في هذا الملفّ اسم وحدةٍ ولا اسم جدول (القاعدة 5).
/// </para>
/// <para>
/// <b>والاستعلام يقف عند صفّين — <c>limit 2</c>.</b> ليس تحسيناً: هو الحارس. صفرٌ أو
/// واحدٌ أو «أكثر»، وثلاثتها كلّ ما يستطيع هذا المحوّل أن يقوله. فالسؤال «كم اسماً يشبه
/// هذا؟» لا يُحذف من الجواب — <b>لا يُطرح على القاعدة أصلاً</b>.
/// </para>
/// <para>
/// <b>والمنشأة في المفتاح لا بجانبه:</b> كل استعلامٍ هنا مقيَّدٌ بأعمدة النطاق، فمِقبضٌ
/// مسرَّب من منشأةٍ أخرى لا يجد صفّاً حتى لو سقطت المقارنة التي تسبقه.
/// </para>
/// </summary>
public sealed class PostgresNameRegister : INameCandidateSource, INameCandidateSheetSource
{
    private readonly string _connectionString;
    private readonly NameRegisterTable _table;
    private readonly decimal _threshold;
    private readonly string _probeSql;

    /// <summary>ينشئ المحوّل.</summary>
    /// <param name="connectionString">اتصال <b>التطبيق</b> — القراءة لا تحتاج مالكاً.</param>
    /// <param name="table">وصف الجدول.</param>
    /// <param name="similarityThreshold">عتبة التشابه بعد الطيّ. ‏0.45 مقيسة.</param>
    public PostgresNameRegister(string connectionString, NameRegisterTable table, decimal similarityThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(table);

        if (similarityThreshold is <= 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(similarityThreshold),
                similarityThreshold,
                "عتبة التشابه خارج المدى ]0,1]. / the similarity threshold is outside (0,1].");
        }

        _connectionString = connectionString;
        _table = table;
        _threshold = similarityThreshold;
        _probeSql = BuildProbe(table);
    }

    /// <inheritdoc />
    public string RegisterKey => _table.RegisterKey;

    /// <summary>نصّ استعلام السبر — معلَنٌ ليُقرأ في الإثبات، فالحارس <c>limit 2</c> يُرى لا يُوصف.</summary>
    public string ProbeCommandText => _probeSql;

    /// <inheritdoc />
    public async Task<NameCandidateProbe> ProbeAsync(
        NameCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**عتبة `%` تُضبط داخل معاملةٍ بـ`SET LOCAL`** فتعود عند الإيداع ولا تلوّث
        // اتصالاً مُعاداً من المجمّع. والصحّة لا تتعلّق بها أصلاً: شرط `similarity() >=`
        // الصريح قائمٌ في الاستعلام، و`%` عند عتبةٍ أدنى يُعيد مجموعةً أوسع تُصفّى بعده.
        await using NpgsqlTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SetThresholdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(_probeSql, connection, transaction);
        Bind(command, request);

        Guid first = Guid.Empty;
        int seen = 0;

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (seen == 0)
                {
                    first = reader.GetGuid(0);
                }

                seen++;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // ‏`seen` لا يتجاوز 2 لأن الاستعلام يقف هناك، والنوع المُعاد لا يحمل عدداً أصلاً.
        return seen switch
        {
            0 => NameCandidateProbe.None,
            1 => NameCandidateProbe.One(first),
            _ => NameCandidateProbe.Many,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NameCandidate>> ListForSheetAsync(
        NameCandidateRequest request,
        int cap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(cap, 1);

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SetThresholdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(BuildSheet(_table), connection, transaction);
        Bind(command, request);
        command.Parameters.AddWithValue("cap", cap);

        List<NameCandidate> candidates = [];

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                candidates.Add(new NameCandidate(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return candidates;
    }

    private async Task SetThresholdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand limit = new(
            "set local pg_trgm.similarity_threshold = "
            + _threshold.ToString("0.####", CultureInfo.InvariantCulture),
            connection,
            transaction);

        await limit.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private void Bind(NpgsqlCommand command, NameCandidateRequest request)
    {
        command.Parameters.AddWithValue("text", request.Text);
        command.Parameters.AddWithValue("threshold", (float)_threshold);
        command.Parameters.AddWithValue("scope0", request.Tenant.Value);

        if (_table.ScopeColumns.Count > 1)
        {
            command.Parameters.AddWithValue("scope1", request.CompanyId);
        }
    }

    /// <summary>الشرط المشترك: النطاق، ثم السريان، ثم المطابقة على المفتاحين.</summary>
    private static string Where(NameRegisterTable table)
    {
        string scope = string.Join(
            " and ",
            table.ScopeColumns.Select(static (column, index) =>
                NameRegisterTable.Quote(column)
                + " = @scope"
                + index.ToString(CultureInfo.InvariantCulture)));

        string active = table.ActiveColumn is null
            ? string.Empty
            : " and " + NameRegisterTable.Quote(table.ActiveColumn);

        return " where " + scope + active
            + " and (similarity(search_key, babel.fold_arabic(@text)) >= @threshold"
            + " or search_key_tight = babel.fold_arabic_tight(@text))";
    }

    /// <summary>
    /// استعلام السبر. <b>‏<c>limit 2</c> هو الحارس</b>، و<c>order by</c> على المعرّف
    /// ليكون الجواب حتمياً عند الصفّ الواحد — ولا ترتيب بالدرجة، فلا «أفضل تطابق» هنا.
    /// </summary>
    private static string BuildProbe(NameRegisterTable table)
        => "select " + NameRegisterTable.Quote(table.IdColumn)
        + " from " + table.QualifiedName
        + Where(table)
        + " order by " + NameRegisterTable.Quote(table.IdColumn)
        + " limit 2";

    /// <summary>استعلام الورقة — <b>يُعيد أسماءً، ولا يُستدعى في بناء رسالةٍ لنموذج</b>.</summary>
    private static string BuildSheet(NameRegisterTable table)
        => "select " + NameRegisterTable.Quote(table.IdColumn)
        + ", " + NameRegisterTable.Quote(table.NameColumn)
        + ", " + (table.SubtitleColumn is null ? "null::text" : NameRegisterTable.Quote(table.SubtitleColumn))
        + " from " + table.QualifiedName
        + Where(table)
        + " order by " + NameRegisterTable.Quote(table.NameColumn)
        + ", " + NameRegisterTable.Quote(table.IdColumn)
        + " limit @cap";
}
