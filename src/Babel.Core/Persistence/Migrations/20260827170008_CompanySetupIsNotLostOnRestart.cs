using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class CompanySetupIsNotLostOnRestart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "capability_profile_capability",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    capability = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capability_profile_capability", x => new { x.company_id, x.document_type, x.capability });
                });

            migrationBuilder.CreateTable(
                name: "capability_profile_default",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capability_profile_default", x => new { x.company_id, x.document_type, x.field });
                });

            migrationBuilder.CreateTable(
                name: "capability_profile_document",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capability_profile_document", x => new { x.company_id, x.document_type });
                });

            migrationBuilder.CreateTable(
                name: "company_setup",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false),
                    default_cost_center = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    founded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_setup", x => x.company_id);
                    table.CheckConstraint("ck_company_setup_default_shape", "default_cost_center ~ '^[a-z0-9._]{1,32}$'");
                    table.CheckConstraint("ck_company_setup_name_not_blank", "length(btrim(name_ar)) > 0");
                    table.CheckConstraint("ck_company_setup_scale_range", "decimal_places between 0 and 4");
                });

            migrationBuilder.CreateTable(
                name: "cost_center",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    suspension_reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_center", x => new { x.company_id, x.code });
                    table.CheckConstraint("ck_cost_center_code_shape", "code ~ '^[a-z0-9._]{1,32}$'");
                    table.CheckConstraint("ck_cost_center_name_not_blank", "length(btrim(name_ar)) > 0");
                    table.CheckConstraint("ck_cost_center_reason_matches_state", "(state = 'suspended') = (length(btrim(suspension_reason)) > 0)");
                    table.CheckConstraint("ck_cost_center_state", "state in ('active','suspended')");
                });

            migrationBuilder.CreateTable(
                name: "name_translation",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language_tag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_core_name_translation", x => new { x.company_id, x.entity_kind, x.entity_key, x.language_tag });
                    table.CheckConstraint("ck_core_name_translation_key_not_blank", "length(btrim(entity_key)) > 0");
                    table.CheckConstraint("ck_core_name_translation_kind", "entity_kind in ('company','cost_center')");
                    table.CheckConstraint("ck_core_name_translation_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_core_name_translation_not_arabic", "lower(language_tag) <> 'ar' and lower(language_tag) not like 'ar-%'");
                    table.CheckConstraint("ck_core_name_translation_tag_shape", "language_tag ~ '^[A-Za-z][A-Za-z0-9]*(-[A-Za-z0-9]+)*$' and length(language_tag) <= 35");
                });

            // ما لا يعبّر عنه نموذج EF: ثباتُ مقياس العرض بمشغّل، ورابطُ المركز
            // بمنشأته. وداخل الهجرة لا بجوارها — كي ينتج `dotnet ef database update`
            // وحده مخطّطاً صحيحاً كاملاً، لا مخطّطاً ينتظر خطوةً يتذكّرها إنسان.
            migrationBuilder.Sql(CoreSchemaDeployer.Script("CoreTriggers.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop function if exists core.company_setup_is_immutable() cascade");

            migrationBuilder.DropTable(
                name: "capability_profile_capability",
                schema: "core");

            migrationBuilder.DropTable(
                name: "capability_profile_default",
                schema: "core");

            migrationBuilder.DropTable(
                name: "capability_profile_document",
                schema: "core");

            // المركز قبل المنشأة: مفتاحٌ خارجي من cost_center إلى company_setup يجعل
            // الترتيب المولَّد يسقط. (يُحرَّر بيد عمداً — والمولِّد لا يرى قيداً كُتب SQL.)
            migrationBuilder.DropTable(
                name: "cost_center",
                schema: "core");

            migrationBuilder.DropTable(
                name: "company_setup",
                schema: "core");

            migrationBuilder.DropTable(
                name: "name_translation",
                schema: "core");
        }
    }
}
