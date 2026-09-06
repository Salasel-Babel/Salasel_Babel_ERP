using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <summary>
    /// <b>العملةُ ووحدتُها الصغرى تتبعان المنشأة لا الشيفرة (ADR-0089).</b>
    /// <para>
    /// عمودان على صفّ التأسيس: <c>currency_code</c> و<c>minor_units</c>. ولا افتراضَ
    /// يبقى على الجدول بعد هذه الهجرة: الافتراضُ المؤقّت الذي يظهر في <c>AddColumn</c>
    /// هو <b>الحقيقةُ التاريخية للصفوف القائمة</b> — كلُّ منشأةٍ أُسِّست قبل هذه الهجرة
    /// عملت تحت <c>CompanyCurrency = "SAR"</c> و<c>Halalas = 2</c> المكتوبَين في
    /// الشيفرة، فتسجيلُهما على صفّها تدوينُ واقعةٍ لا تخمين. ثم يُسقَط الافتراضُ فوراً
    /// كي لا يُؤسَّس صفٌّ جديد بعملةٍ لم يكتبها أحد.
    /// </para>
    /// </summary>
    internal partial class TheCurrencyAndItsMinorUnitFollowTheCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ‏الافتراضُ هنا تدوينٌ لما عملت به الصفوفُ القائمة فعلاً، ويُسقَط في السطر التالي.
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "core",
                table: "company_setup",
                type: "character(3)",
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<int>(
                name: "minor_units",
                schema: "core",
                table: "company_setup",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("alter table core.company_setup alter column currency_code drop default;");
            migrationBuilder.Sql("alter table core.company_setup alter column minor_units drop default;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_setup_currency_shape",
                schema: "core",
                table: "company_setup",
                sql: "currency_code ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_setup_minor_units_range",
                schema: "core",
                table: "company_setup",
                sql: "minor_units between 0 and 4");

            // ‏المشغّل يُعاد تعريفه بعد الأعمدة كي يحرس العملةَ أيضاً — نصُّه في CoreTriggers.sql
            // واحدٌ لكلّ الهجرات، ويُعاد تطبيقه حيث يتغيّر ما يحرسه.
            migrationBuilder.Sql(CoreSchemaDeployer.Script("CoreTriggers.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_company_setup_currency_shape",
                schema: "core",
                table: "company_setup");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_setup_minor_units_range",
                schema: "core",
                table: "company_setup");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "core",
                table: "company_setup");

            migrationBuilder.DropColumn(
                name: "minor_units",
                schema: "core",
                table: "company_setup");
        }
    }
}
