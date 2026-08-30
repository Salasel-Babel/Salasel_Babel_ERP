using Npgsql;
using Xunit;

namespace Babel.RealEstate.Tests;

/// <summary>
/// <b>الطبقة الثالثة تُفحَص في المخطّط المنشور لا في ملفّ النصّ.</b>
/// <para>
/// حارسٌ مكتوبٌ في <c>LedgerTriggers.sql</c> ليس حارساً حتى يصل إلى القاعدة. وهذا
/// الفرق ليس نظرياً: النصّ يُنفَّذ <b>داخل هجرة الأساس</b>، والهجرة التي طُبِّقت لا
/// تُعاد أبداً — فقاعدةُ عميلٍ قائمة تبقى على النسخة القديمة من
/// <c>ledger.assert_line_allowed</c> مهما أُضيفت إليها قاعدة حجب، ويُقرأ المستودعُ
/// على أنه يحرس ما لا يحرسه.
/// </para>
/// <para>
/// فالفحص هنا يسأل <b>القاعدة نفسها</b>: هل الدالّة المنشورة تحمل GR-RE-002؟ وهل
/// القيد الذي يمنع وحدةً بلا عقارها موجود على <c>ledger.journal_line</c>؟
/// </para>
/// </summary>
[Collection("realestate")]
public sealed class LedgerGuardLayerTests
{
    [Fact]
    public async Task TheDeployedTriggerCarriesTheRealEstateGuardsAndTheUnitPairingConstraint()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        _ = harness;

        await using NpgsqlConnection connection = new(RealEstateTestEnvironment.Ledger.OwnerConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(true);

        string definition = await ScalarAsync(
            connection,
            "select pg_get_functiondef('ledger.assert_line_allowed'::regproc)",
            token).ConfigureAwait(true) ?? string.Empty;

        Proof.Require(
            definition.Contains("GR-RE-001", StringComparison.Ordinal),
            "الدالّة المنشورة تحمل GR-RE-001 — الطبقة الثالثة القائمة",
            "طول تعريف الدالّة = " + definition.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Proof.Require(
            definition.Contains("GR-RE-002", StringComparison.Ordinal),
            "والدالّة المنشورة تحمل GR-RE-002 كذلك — لا في ملفّ النصّ وحده",
            "investment_property_depreciation_expense موجودٌ في التعريف: "
            + definition.Contains("investment_property_depreciation_expense", StringComparison.Ordinal)
                .ToString(System.Globalization.CultureInfo.InvariantCulture));

        string? constraint = await ScalarAsync(
            connection,
            """
            select pg_get_constraintdef(oid) from pg_constraint
             where conname = 'ck_journal_line_unit_requires_property'
               and conrelid = 'ledger.journal_line'::regclass
            """,
            token).ConfigureAwait(true);

        Proof.Require(
            constraint is not null && constraint.Contains("unit_id", StringComparison.Ordinal),
            "وقيدُ «الوحدة لا تقف بلا عقارها» موجودٌ على سطر القيد — يفرضه على أي كاتب",
            constraint ?? "«غائب»");

        // ‏**والسبب الذي يجعل الفحصين أعلاه لازمين مكتوبٌ في القاعدة نفسها:** هجرة
        // الأساس مُسجَّلة في تاريخ الهجرات، فالنصّ الذي بداخلها لن يُنفَّذ ثانيةً أبداً.
        string? applied = await ScalarAsync(
            connection,
            """
            select "MigrationId" from "__EFMigrationsHistory"
             where "MigrationId" like '%LedgerFoundation'
            """,
            token).ConfigureAwait(true);

        Proof.Require(
            applied is not null,
            "هجرة الأساس مُسجَّلة في تاريخ الهجرات — فنصّها لا يُعاد تنفيذه، ولذلك يُعيده الناشر بعدها",
            applied ?? "«غائبة»");
    }

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return value as string;
    }
}
