using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class TheAuditTrailAndTheUsageMeterSurviveARestart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "core",
                columns: table => new
                {
                    sequence_no = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.sequence_no);
                    table.CheckConstraint("ck_audit_entry_action_shape", "action ~ '^[a-z][a-z0-9_.]{0,63}$'");
                });

            migrationBuilder.CreateTable(
                name: "module_usage",
                schema: "core",
                columns: table => new
                {
                    sequence_no = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    operation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_usage", x => x.sequence_no);
                    table.CheckConstraint("ck_module_usage_module", "module >= 1");
                    table.CheckConstraint("ck_module_usage_operation_not_blank", "length(btrim(operation)) > 0");
                    table.CheckConstraint("ck_module_usage_quantity", "quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "user_activity",
                schema: "core",
                columns: table => new
                {
                    sequence_no = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    activity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entitlement_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_activity", x => x.sequence_no);
                    table.CheckConstraint("ck_user_activity_activity_not_blank", "length(btrim(activity)) > 0");
                    table.CheckConstraint("ck_user_activity_entitlement_state_shape", "entitlement_state ~ '^[A-Za-z][A-Za-z0-9]{0,31}$'");
                    table.CheckConstraint("ck_user_activity_module", "module >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_tenant_occurred",
                schema: "core",
                table: "audit_entry",
                columns: new[] { "tenant_id", "occurred_at", "sequence_no" });

            migrationBuilder.CreateIndex(
                name: "ix_module_usage_tenant_occurred",
                schema: "core",
                table: "module_usage",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_tenant_occurred",
                schema: "core",
                table: "user_activity",
                columns: new[] { "tenant_id", "occurred_at" });

            // ‏**المشغّلات داخل الهجرة نفسها** — كما في `CompanySetupIsNotLostOnRestart`.
            // ووجودُها هنا لا في خطوة النشر يعني أن `dotnet ef database update` وحده
            // ينتج مخطّطاً صحيحاً كاملاً: جدولٌ يُنشأ بلا مشغّل الإلحاق هو جدولٌ يبدو
            // حصيناً وليس كذلك، ولا شيء في المخطّط يقول إن نصفه ينقص.
            migrationBuilder.Sql(CoreSchemaDeployer.Script("CoreAppendOnlyTriggers.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "core");

            migrationBuilder.DropTable(
                name: "module_usage",
                schema: "core");

            migrationBuilder.DropTable(
                name: "user_activity",
                schema: "core");

            // مشغّلات الجداول تسقط معها، والدالّتان لا تسقطان — فتُسقَطان صراحةً كي لا
            // يترك التراجع دالّتين معلّقتين في مخطّطٍ لا يستعملهما شيء.
            migrationBuilder.Sql("drop function if exists core.append_only_row()");
            migrationBuilder.Sql("drop function if exists core.append_only_statement()");
        }
    }
}
