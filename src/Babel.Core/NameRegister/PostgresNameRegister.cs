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
/// <para>
/// <b>وهذا النوع يُنفّذ منفذ السبر <u>وحده</u> — وهو حارسٌ لا ترتيب.</b> كان يُنفّذ
/// المنفذَين معاً، فكان <c>((INameCandidateSheetSource)source).ListForSheetAsync(request, 100000)</c>
/// على متغيّرٍ نوعُه منفذ السبر يُعيد <b>ثمانمئة صفّ بأسمائها ورموزها</b> — أي الأسماء
/// والصفوف والعدد الدقيق، الثلاثة التي قال المالك إنها لا تعبر. و«الفصل هو الحارس»
/// جملةٌ صحيحة في العقد وكانت كاذبة في التنفيذ: كائنٌ واحد يحمل الوجهين يُبطلها بتحويلٍ
/// واحد. فصار الجَرد في <see cref="PostgresNameSheet"/> — <b>كائنٌ آخر</b> — ومعه سقفٌ
/// لا يُتجاوز، وحارسٌ معماريّ يمنع <c>Babel.Ai</c> من الإشارة إلى منفذ الجَرد أصلاً.
/// </para>
/// </summary>
public sealed class PostgresNameRegister : INameCandidateSource
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
        _probeSql = NameRegisterSql.Probe(table);
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

    private Task SetThresholdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
        => NameRegisterSql.SetThresholdAsync(connection, transaction, _threshold, cancellationToken);

    private void Bind(NpgsqlCommand command, NameCandidateRequest request)
        => NameRegisterSql.Bind(command, _table, _threshold, request);
}
