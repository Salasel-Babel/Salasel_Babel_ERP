using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class TheCompanysConstantsAreDecidedOnceAndNumbersAreAllocatedNotTyped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_preset",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_preset", x => new { x.company_id, x.key });
                    table.CheckConstraint("ck_company_preset_key_shape", "key ~ '^[a-z][a-z0-9_.]{0,63}$'");
                    table.CheckConstraint("ck_company_preset_value_present", "length(value) > 0");
                });

            migrationBuilder.CreateTable(
                name: "document_counter",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    next_no = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_counter", x => new { x.company_id, x.series, x.fiscal_year });
                    table.CheckConstraint("ck_document_counter_positive", "next_no >= 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_preset",
                schema: "core");

            migrationBuilder.DropTable(
                name: "document_counter",
                schema: "core");
        }
    }
}
