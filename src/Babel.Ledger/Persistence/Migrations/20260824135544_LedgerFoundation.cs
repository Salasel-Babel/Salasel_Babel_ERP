using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Babel.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class LedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.CreateTable(
                name: "account",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_code = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_ar_search = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    parent_code = table.Column<string>(type: "text", nullable: true),
                    account_level = table.Column<int>(type: "integer", nullable: false),
                    account_type = table.Column<string>(type: "text", nullable: false),
                    natural_side = table.Column<string>(type: "text", nullable: false),
                    is_postable = table.Column<bool>(type: "boolean", nullable: false),
                    is_contra = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    statement_section = table.Column<string>(type: "text", nullable: true),
                    subledger_type = table.Column<string>(type: "text", nullable: false, defaultValue: "none"),
                    required_dimensions = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    currency_mode = table.Column<string>(type: "text", nullable: false, defaultValue: "any"),
                    currency_code = table.Column<string>(type: "text", nullable: true),
                    is_protected = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "drafted"),
                    source_ref = table.Column<string>(type: "text", nullable: true),
                    caveat_ar = table.Column<string>(type: "text", nullable: true),
                    caveat_en = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account", x => new { x.company_id, x.account_code });
                    table.CheckConstraint("ck_account_currency_mode", "currency_mode in ('any','company_only','fixed')");
                    table.CheckConstraint("ck_account_fixed_currency", "currency_mode <> 'fixed' or currency_code is not null");
                    table.CheckConstraint("ck_account_level", "account_level between 1 and 4");
                    table.CheckConstraint("ck_account_level_matches_code", "(account_level = 1 and length(account_code) = 1) or (account_level = 2 and length(account_code) = 2) or (account_level = 3 and length(account_code) = 3) or (account_level = 4 and length(account_code) >= 4)");
                    table.CheckConstraint("ck_account_natural_side", "natural_side in ('debit','credit')");
                    table.CheckConstraint("ck_account_parent_matches_code", "(parent_code is null and account_level = 1) or (parent_code is not null and account_code like parent_code || '%' and length(account_code) > length(parent_code))");
                    table.CheckConstraint("ck_account_postable_is_leaf", "not is_postable or account_level = 4");
                    table.CheckConstraint("ck_account_type", "account_type in ('asset','liability','equity','revenue','expense')");
                });

            migrationBuilder.CreateTable(
                name: "fiscal_period",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    period_no = table.Column<int>(type: "integer", nullable: false),
                    period_code = table.Column<string>(type: "text", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiscal_period", x => new { x.company_id, x.fiscal_year, x.period_no });
                    table.CheckConstraint("ck_fiscal_period_range", "ends_on >= starts_on");
                    table.CheckConstraint("ck_fiscal_period_state", "state in ('open','closed','permanently_closed')");
                });

            migrationBuilder.CreateTable(
                name: "journal_entry",
                schema: "ledger",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<string>(type: "text", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    entry_no = table.Column<long>(type: "bigint", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    period_code = table.Column<string>(type: "text", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    actor_search = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    memo = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    memo_ar = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    memo_ar_search = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    source_module = table.Column<string>(type: "text", nullable: false),
                    source_doc_type = table.Column<string>(type: "text", nullable: false),
                    source_doc_id = table.Column<string>(type: "text", nullable: false),
                    posting_trigger_code = table.Column<string>(type: "text", nullable: false),
                    posting_generation = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    event_code = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason_ar = table.Column<string>(type: "text", nullable: true),
                    reversal_reason_en = table.Column<string>(type: "text", nullable: true),
                    closed_period_permission = table.Column<string>(type: "text", nullable: true),
                    closed_period_authoriser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entry", x => x.entry_id);
                    table.CheckConstraint("ck_journal_entry_generation", "posting_generation >= 1");
                    table.CheckConstraint("ck_journal_entry_reversal_has_reason", "status <> 'REVERSAL' or (reverses_entry_id is not null and reversal_reason_ar is not null)");
                    table.CheckConstraint("ck_journal_entry_status", "status in ('POSTED','REVERSAL')");
                    table.ForeignKey(
                        name: "fk_journal_entry_reverses",
                        column: x => x.reverses_entry_id,
                        principalSchema: "ledger",
                        principalTable: "journal_entry",
                        principalColumn: "entry_id");
                });

            migrationBuilder.CreateTable(
                name: "posting_counter",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<string>(type: "text", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    next_entry_no = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    next_chain_seq = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posting_counter", x => new { x.company_id, x.book_id, x.fiscal_year });
                    table.CheckConstraint("ck_posting_counter_positive", "next_entry_no >= 1 and next_chain_seq >= 1");
                });

            migrationBuilder.CreateTable(
                name: "posting_role",
                schema: "ledger",
                columns: table => new
                {
                    role_code = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    expected_account_type = table.Column<string>(type: "text", nullable: true),
                    expected_side = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "drafted"),
                    note_ar = table.Column<string>(type: "text", nullable: true),
                    note_en = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posting_role", x => x.role_code);
                    table.CheckConstraint("ck_posting_role_side", "expected_side is null or expected_side in ('debit','credit')");
                });

            migrationBuilder.CreateTable(
                name: "process_event",
                schema: "ledger",
                columns: table => new
                {
                    process_event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    kind = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    event_code = table.Column<string>(type: "text", nullable: true),
                    source_doc_type = table.Column<string>(type: "text", nullable: true),
                    source_doc_id = table.Column<string>(type: "text", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    message_ar = table.Column<string>(type: "text", nullable: true),
                    message_en = table.Column<string>(type: "text", nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_event", x => x.process_event_id);
                });

            migrationBuilder.CreateTable(
                name: "property_dimension",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<string>(type: "text", nullable: false),
                    ownership_model = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_dimension", x => new { x.company_id, x.property_id });
                    table.CheckConstraint("ck_property_ownership_model", "ownership_model in ('own_property','managed_for_others')");
                });

            migrationBuilder.CreateTable(
                name: "account_balance",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<string>(type: "text", nullable: false),
                    period_code = table.Column<string>(type: "text", nullable: false),
                    account_code = table.Column<string>(type: "text", nullable: false),
                    debit = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    credit = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    entry_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_balance", x => new { x.company_id, x.book_id, x.period_code, x.account_code });
                    table.CheckConstraint("ck_account_balance_sign", "debit >= 0 and credit >= 0");
                    table.ForeignKey(
                        name: "fk_account_balance_account",
                        columns: x => new { x.company_id, x.account_code },
                        principalSchema: "ledger",
                        principalTable: "account",
                        principalColumns: new[] { "company_id", "account_code" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chain_link",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<string>(type: "text", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    chain_seq = table.Column<long>(type: "bigint", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canon_version = table.Column<string>(type: "text", nullable: false),
                    prev_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    entry_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    canonical_bytes = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chain_link", x => new { x.company_id, x.book_id, x.fiscal_year, x.chain_seq });
                    table.CheckConstraint("ck_chain_link_hash_length", "octet_length(entry_hash) = 32 and octet_length(prev_hash) = 32");
                    table.CheckConstraint("ck_chain_link_seq_positive", "chain_seq >= 1");
                    table.ForeignKey(
                        name: "fk_chain_link_entry",
                        column: x => x.entry_id,
                        principalSchema: "ledger",
                        principalTable: "journal_entry",
                        principalColumn: "entry_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_line",
                schema: "ledger",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_code = table.Column<string>(type: "text", nullable: false),
                    role_code = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    qualifier = table.Column<string>(type: "text", nullable: false, defaultValue: "*"),
                    debit = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    credit = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    currency = table.Column<string>(type: "text", nullable: false),
                    fx_rate = table.Column<decimal>(type: "numeric(19,8)", nullable: false, defaultValue: 1m),
                    debit_company = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    credit_company = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    branch_id = table.Column<string>(type: "text", nullable: true),
                    cost_center_id = table.Column<string>(type: "text", nullable: true),
                    project_id = table.Column<string>(type: "text", nullable: true),
                    property_id = table.Column<string>(type: "text", nullable: true),
                    unit_id = table.Column<string>(type: "text", nullable: true),
                    warehouse_id = table.Column<string>(type: "text", nullable: true),
                    boq_item_id = table.Column<string>(type: "text", nullable: true),
                    subledger_kind = table.Column<string>(type: "text", nullable: false, defaultValue: "none"),
                    subledger_party_id = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    description_ar = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    description_ar_search = table.Column<string>(type: "text", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_line", x => x.line_id);
                    table.CheckConstraint("ck_journal_line_company_side", "debit_company = 0 or credit_company = 0");
                    table.CheckConstraint("ck_journal_line_fx_positive", "fx_rate > 0");
                    table.CheckConstraint("ck_journal_line_one_side", "debit = 0 or credit = 0");
                    table.CheckConstraint("ck_journal_line_sign", "debit >= 0 and credit >= 0 and debit_company >= 0 and credit_company >= 0");
                    table.ForeignKey(
                        name: "fk_journal_line_account",
                        columns: x => new { x.company_id, x.account_code },
                        principalSchema: "ledger",
                        principalTable: "account",
                        principalColumns: new[] { "company_id", "account_code" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_journal_line_entry",
                        column: x => x.entry_id,
                        principalSchema: "ledger",
                        principalTable: "journal_entry",
                        principalColumn: "entry_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_account_map",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "text", nullable: false),
                    qualifier = table.Column<string>(type: "text", nullable: false),
                    account_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "drafted"),
                    note_ar = table.Column<string>(type: "text", nullable: true),
                    note_en = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_account_map", x => new { x.company_id, x.role_code, x.qualifier });
                    table.ForeignKey(
                        name: "fk_role_account_map_account",
                        columns: x => new { x.company_id, x.account_code },
                        principalSchema: "ledger",
                        principalTable: "account",
                        principalColumns: new[] { "company_id", "account_code" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_account_map_role",
                        column: x => x.role_code,
                        principalSchema: "ledger",
                        principalTable: "posting_role",
                        principalColumn: "role_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_parent",
                schema: "ledger",
                table: "account",
                columns: new[] { "company_id", "parent_code" });

            migrationBuilder.CreateIndex(
                name: "ix_account_search",
                schema: "ledger",
                table: "account",
                columns: new[] { "company_id", "name_ar_search" });

            migrationBuilder.CreateIndex(
                name: "IX_account_balance_company_id_account_code",
                schema: "ledger",
                table: "account_balance",
                columns: new[] { "company_id", "account_code" });

            migrationBuilder.CreateIndex(
                name: "uq_chain_link_entry",
                schema: "ledger",
                table: "chain_link",
                column: "entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_fiscal_period_code",
                schema: "ledger",
                table: "fiscal_period",
                columns: new[] { "company_id", "period_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_period",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "book_id", "period_code" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_reverses_entry_id",
                schema: "ledger",
                table: "journal_entry",
                column: "reverses_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_source",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "source_doc_type", "source_doc_id" });

            migrationBuilder.CreateIndex(
                name: "uq_journal_entry_no",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "book_id", "fiscal_year", "entry_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_posting_identity",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "source_doc_type", "source_doc_id", "posting_trigger_code", "posting_generation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_line_account",
                schema: "ledger",
                table: "journal_line",
                columns: new[] { "company_id", "account_code" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_line_property",
                schema: "ledger",
                table: "journal_line",
                columns: new[] { "company_id", "property_id" });

            migrationBuilder.CreateIndex(
                name: "uq_journal_line_no",
                schema: "ledger",
                table: "journal_line",
                columns: new[] { "entry_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_event_company",
                schema: "ledger",
                table: "process_event",
                columns: new[] { "company_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_role_account_map_company_id_account_code",
                schema: "ledger",
                table: "role_account_map",
                columns: new[] { "company_id", "account_code" });

            migrationBuilder.CreateIndex(
                name: "IX_role_account_map_role_code",
                schema: "ledger",
                table: "role_account_map",
                column: "role_code");

            // ── ما لا يعبّر عنه نموذج EF ──────────────────────────────────
            // مشغّل قيد DEFERRABLE INITIALLY DEFERRED يعمل عند COMMIT، ودوال
            // الحجب، والترحيل كمكالمة خادم واحدة. الترتيب مقصود: الجداول أولاً.
            // الصلاحيات ليست هنا لأنها تحتاج اسم دور التطبيق وقت النشر — انظر
            // LedgerSchemaDeployer.
            migrationBuilder.Sql(LedgerSchemaDeployer.Script("LedgerTriggers.sql"));
            migrationBuilder.Sql(LedgerSchemaDeployer.Script("PostEntryFunction.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop function if exists ledger.post_entry cascade");
            migrationBuilder.Sql("drop function if exists ledger.assert_entry_balanced cascade");
            migrationBuilder.Sql("drop function if exists ledger.assert_line_allowed cascade");

            migrationBuilder.DropTable(
                name: "account_balance",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "chain_link",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "fiscal_period",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "journal_line",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "posting_counter",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "process_event",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "property_dimension",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "role_account_map",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "journal_entry",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "account",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "posting_role",
                schema: "ledger");
        }
    }
}
