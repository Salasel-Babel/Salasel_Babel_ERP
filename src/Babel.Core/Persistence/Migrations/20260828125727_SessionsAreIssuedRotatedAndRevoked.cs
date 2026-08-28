using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class SessionsAreIssuedRotatedAndRevoked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_credential",
                schema: "core",
                columns: table => new
                {
                    digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_credential", x => x.digest);
                    table.CheckConstraint("ck_access_credential_digest_shape", "digest ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_access_credential_generation", "generation >= 1");
                    table.CheckConstraint("ck_access_credential_kind", "kind in ('access','refresh')");
                });

            migrationBuilder.CreateTable(
                name: "access_enrolment",
                schema: "core",
                columns: table => new
                {
                    digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_enrolment", x => x.digest);
                    table.CheckConstraint("ck_access_enrolment_digest_shape", "digest ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "access_membership",
                schema: "core",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_membership", x => new { x.company_id, x.user_id });
                    table.CheckConstraint("ck_access_membership_name_not_blank", "length(btrim(display_name_ar)) > 0");
                    table.CheckConstraint("ck_access_membership_role", "role in ('reader','contributor','owner')");
                });

            migrationBuilder.CreateTable(
                name: "access_session",
                schema: "core",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_session", x => x.session_id);
                    table.CheckConstraint("ck_access_session_generation", "generation >= 1");
                    table.CheckConstraint("ck_access_session_reason_closed", "revoked_reason in ('', 'signed_out', 'refresh_replayed')");
                    table.CheckConstraint("ck_access_session_reason_matches_state", "(revoked_at is not null) = (length(btrim(revoked_reason)) > 0)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_credential_session",
                schema: "core",
                table: "access_credential",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_membership_tenant_user",
                schema: "core",
                table: "access_membership",
                columns: new[] { "tenant_id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_credential",
                schema: "core");

            migrationBuilder.DropTable(
                name: "access_enrolment",
                schema: "core");

            migrationBuilder.DropTable(
                name: "access_membership",
                schema: "core");

            migrationBuilder.DropTable(
                name: "access_session",
                schema: "core");
        }
    }
}
