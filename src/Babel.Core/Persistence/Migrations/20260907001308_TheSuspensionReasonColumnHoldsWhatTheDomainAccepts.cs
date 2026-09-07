using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <summary>
    /// <b>عمودُ سبب الإيقاف يسع ما يقبله المجال.</b> كان <c>character varying(400)</c> بينما
    /// <c>CompanySetupLimits.MaximumReasonLength = 512</c> — فسببٌ من 401 محرفاً يمرّ من النوع
    /// ويسقط في القاعدة برسالةِ مخطّطٍ لا برفضٍ مُسمّى. والطولُ الآن يُشتقّ من الحدّ نفسه
    /// (ADR-0091)، فلا يفترقان ثانيةً.
    /// </summary>
    internal partial class TheSuspensionReasonColumnHoldsWhatTheDomainAccepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "suspension_reason",
                schema: "core",
                table: "cost_center",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldDefaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "suspension_reason",
                schema: "core",
                table: "cost_center",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldDefaultValue: "");
        }
    }
}
