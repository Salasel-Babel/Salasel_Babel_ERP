using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <summary>
    /// <b>المعامِل إصدارٌ مؤرَّخُ السريان لمجموعةٍ كاملة — لا رقمٌ سائب.</b>
    /// <para>
    /// ثلاثةُ جداول: ترويسةُ الإصدار، وقيمُه، وسجلُّ من استعمله. والمفتاح الأجنبي
    /// بينها ممكنٌ لأنها في قاعدةٍ واحدة — وهو بعينه ما <b>لا</b> يمكن بين مستندٍ في
    /// <c>babel_purchasing</c> وإصدارٍ هنا، ولذلك يحمل المستند لقطته.
    /// </para>
    /// </summary>
    internal partial class AParameterIsAVersionedSetNotALooseNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parameter_version",
                schema: "core",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    approval = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    approved_on = table.Column<DateOnly>(type: "date", nullable: true),
                    source_ref = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    deposited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter_version", x => x.version_id);
                    table.CheckConstraint("ck_parameter_version_approval", "approval in ('platform_default','tenant_approved','auditor_signed')");
                    table.CheckConstraint("ck_parameter_version_approved_on_matches_approval", "(approval <> 'platform_default') = (approved_on is not null)");
                    table.CheckConstraint("ck_parameter_version_approver_matches_approval", "(approval <> 'platform_default') = (length(btrim(approved_by)) > 0)");
                    table.CheckConstraint("ck_parameter_version_scope", "scope in ('platform','tenant')");
                    table.CheckConstraint("ck_parameter_version_scope_matches_approval", "(scope = 'platform') = (approval = 'platform_default')");
                    table.CheckConstraint("ck_parameter_version_set_shape", "set_code ~ '^[a-z][a-z0-9_.]{0,63}$'");
                    table.CheckConstraint("ck_parameter_version_source_not_blank", "length(btrim(source_ref)) > 0");
                    table.CheckConstraint("ck_parameter_version_tenant_matches_scope", "(scope = 'tenant') = (tenant_id <> '00000000-0000-0000-0000-000000000000')");
                });

            migrationBuilder.CreateTable(
                name: "parameter_usage",
                schema: "core",
                columns: table => new
                {
                    sequence_no = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    document_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posted_on = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter_usage", x => x.sequence_no);
                    table.CheckConstraint("ck_parameter_usage_document_type_shape", "document_type ~ '^[A-Z][A-Z0-9_]{0,31}$'");
                    table.CheckConstraint("ck_parameter_usage_module", "module >= 1");
                    table.ForeignKey(
                        name: "fk_parameter_usage_version",
                        column: x => x.version_id,
                        principalSchema: "core",
                        principalTable: "parameter_version",
                        principalColumn: "version_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parameter_value",
                schema: "core",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value = table.Column<decimal>(type: "numeric(28,10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter_value", x => new { x.version_id, x.key });
                    table.CheckConstraint("ck_parameter_value_key_shape", "key ~ '^[a-z][a-z0-9_]{0,63}$'");
                    table.CheckConstraint("ck_parameter_value_kind", "kind in ('rate','money','count')");
                    table.CheckConstraint("ck_parameter_value_not_negative", "value >= 0");
                    table.CheckConstraint("ck_parameter_value_rate_is_a_fraction", "kind <> 'rate' or value <= 1");
                    table.ForeignKey(
                        name: "fk_parameter_value_version",
                        column: x => x.version_id,
                        principalSchema: "core",
                        principalTable: "parameter_version",
                        principalColumn: "version_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parameter_usage_version_id",
                schema: "core",
                table: "parameter_usage",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ux_parameter_usage_document",
                schema: "core",
                table: "parameter_usage",
                columns: new[] { "tenant_id", "version_id", "module", "document_type", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parameter_version_tenant_set_effective",
                schema: "core",
                table: "parameter_version",
                columns: new[] { "tenant_id", "set_code", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ux_parameter_version_level_set_effective",
                schema: "core",
                table: "parameter_version",
                columns: new[] { "scope", "tenant_id", "set_code", "effective_from" },
                unique: true);

            // ‏**المشغّلات داخل الهجرة نفسها** — كما في `TheAuditTrailAndTheUsageMeterSurviveARestart`.
            // وجودُها هنا لا في خطوة النشر يعني أن `dotnet ef database update` وحده ينتج
            // مخطّطاً صحيحاً كاملاً: جدولُ إصداراتٍ بلا مشغّل الإلحاق **يبدو** حصيناً وليس
            // كذلك، ولا شيء في المخطّط يقول إن نصفه ينقص.
            migrationBuilder.Sql(CoreSchemaDeployer.Script("CoreParameterAppendOnly.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parameter_usage",
                schema: "core");

            migrationBuilder.DropTable(
                name: "parameter_value",
                schema: "core");

            migrationBuilder.DropTable(
                name: "parameter_version",
                schema: "core");
        }
    }
}
