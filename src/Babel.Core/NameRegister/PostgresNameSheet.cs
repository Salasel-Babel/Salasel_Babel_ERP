using Babel.Contracts.Lookup;
using Npgsql;

namespace Babel.Core.NameRegister;

/// <summary>
/// <b>جَردُ ورقة السؤال — كائنٌ آخر، لا وجهٌ آخر لكائنٍ واحد.</b>
/// <para>
/// <b>ولماذا نوعٌ مستقلّ:</b> العقد يقول إن الفصل بين
/// <see cref="INameCandidateSource"/> و<see cref="INameCandidateSheetSource"/>
/// <b>هو</b> الحارس — «ومن يحقن هذا في مسار النموذج يكون قد فعل ذلك باسمٍ يقول ما
/// يفعل». وكان كائنٌ واحد يُنفّذ المنفذَين، فكان تحويلٌ واحد على المتغيّر المُسجَّل
/// منفذَ سبرٍ يُعيد <b>الأسماء والصفوف والعدد</b>. فالفصل الاسمي لا يحرس شيئاً:
/// الحارس أن يكون الجَرد في <b>كائنٍ لا يُسجَّل في مسار النموذج أصلاً</b>.
/// </para>
/// <para>
/// <b>وسقف الورقة يُفرض هنا لا يُمرَّر رجاءً:</b> كان <c>ListForSheetAsync</c> يقبل أي
/// سقفٍ ≥ 1، و<c>QuestionSheetCap</c> لا يبلغ المحوّل — فكان <c>cap: 100000</c> جَرداً
/// كاملاً. والسقف الآن في المُنشئ، وطلبٌ فوقه <b>يُقصّ إليه</b> لأن الورقة عرضٌ للإنسان
/// لا مورد يُرفض؛ وطلبٌ دونه يُحترَم.
/// </para>
/// </summary>
public sealed class PostgresNameSheet : INameCandidateSheetSource
{
    private readonly string _connectionString;
    private readonly NameRegisterTable _table;
    private readonly decimal _threshold;
    private readonly int _ceiling;
    private readonly string _sheetSql;

    /// <summary>ينشئ محوّل الجَرد.</summary>
    /// <param name="connectionString">اتصال <b>التطبيق</b>.</param>
    /// <param name="table">وصف الجدول.</param>
    /// <param name="similarityThreshold">عتبة التشابه بعد الطيّ.</param>
    /// <param name="rowCeiling">سقف صفوف الورقة — <b>لا يُتجاوز مهما طُلب</b>.</param>
    public PostgresNameSheet(
        string connectionString,
        NameRegisterTable table,
        decimal similarityThreshold,
        int rowCeiling)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCeiling, 1);

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
        _ceiling = rowCeiling;
        _sheetSql = NameRegisterSql.Sheet(table);
    }

    /// <inheritdoc />
    public string RegisterKey => _table.RegisterKey;

    /// <summary>سقف الصفوف المفروض — معلَنٌ ليُقرأ في الإثبات.</summary>
    public int RowCeiling => _ceiling;

    /// <summary>نصّ استعلام الجَرد — معلَنٌ ليُقرأ في الإثبات.</summary>
    public string SheetCommandText => _sheetSql;

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

        await NameRegisterSql
            .SetThresholdAsync(connection, transaction, _threshold, cancellationToken)
            .ConfigureAwait(false);

        await using NpgsqlCommand command = new(_sheetSql, connection, transaction);
        NameRegisterSql.Bind(command, _table, _threshold, request);
        command.Parameters.AddWithValue("cap", Math.Min(cap, _ceiling));

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
}
