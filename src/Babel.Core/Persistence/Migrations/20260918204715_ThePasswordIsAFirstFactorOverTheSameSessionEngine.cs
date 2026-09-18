using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Core.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class ThePasswordIsAFirstFactorOverTheSameSessionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_sign_in",
                schema: "core",
                columns: table => new
                {
                    handle = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proof = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_sign_in", x => x.handle);
                    table.CheckConstraint("ck_access_sign_in_handle_normalised", "handle = lower(btrim(handle))");
                    table.CheckConstraint("ck_access_sign_in_handle_shape", "handle ~ '^[^[:space:]@]+@[^[:space:]@]+$'");
                    table.CheckConstraint("ck_access_sign_in_proof_shape", "proof ~ '^[a-z0-9-]+\\$[0-9]+\\$[A-Za-z0-9+/=]+\\$[A-Za-z0-9+/=]+$'");
                });

            migrationBuilder.CreateIndex(
                name: "ux_access_sign_in_user",
                schema: "core",
                table: "access_sign_in",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_sign_in",
                schema: "core");
        }
    }
}
