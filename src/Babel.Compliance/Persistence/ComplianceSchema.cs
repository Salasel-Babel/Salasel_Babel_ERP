namespace Babel.Compliance.Persistence;

/// <summary>
/// مخطط قاعدة البيانات صراحةً بـSQL، على نمط <c>spikes/relational-stack</c>:
/// المخطط مقروء ومراجَع كنص، لا مُولَّداً من هجرات لا يقرأها أحد.
/// <para/>
/// <b>كل عمود مال <c>numeric(19,4)</c>. لا <c>float</c> ولا <c>double precision</c> في أي مكان.</b>
/// </summary>
internal static class ComplianceSchema
{
    public const string SchemaName = "compliance";

    public static string CreateSql(string schema = SchemaName) => $"""
        create schema if not exists {schema};

        create table if not exists {schema}.chain_head (
            tenant_id           text        not null,
            issuing_unit_id     text        not null,
            next_counter        bigint      not null,
            head_hash           bytea       not null,
            updated_at          timestamptz not null,
            is_halted           boolean     not null default false,
            halt_reason_ar      text,
            halt_reason_en      text,
            primary key (tenant_id, issuing_unit_id),
            constraint ck_chain_head_counter check (next_counter >= 1)
        );

        create table if not exists {schema}.document (
            document_id               uuid          primary key,
            document_uuid             uuid          not null,
            tenant_id                 text          not null,
            issuing_unit_id           text          not null,
            environment               text          not null,
            kind                      text          not null,
            flow                      text          not null,
            document_number           text          not null,
            journal_entry_id          uuid          not null,
            issued_at                 timestamptz   not null,
            counter                   bigint        not null,
            previous_hash             bytea         not null,
            document_hash             bytea         not null,
            frozen_payload            bytea         not null,
            seal_state                text          not null,
            submission_fingerprint    text          not null,
            rendered_body             bytea,
            net_total                 numeric(19,4) not null,
            tax_total                 numeric(19,4) not null,
            gross_total               numeric(19,4) not null,
            currency_code             text          not null,
            status                    text          not null,
            attempt_count             integer       not null default 0,
            resolution_attempt_count  integer       not null default 0,
            queued_at                 timestamptz,
            settled_at                timestamptz,
            provider_reference        text,
            stamped_document          bytea,
            notices                   jsonb         not null default '[]'::jsonb,
            human_review_reason_ar    text,
            human_review_reason_en    text,
            version                   bigint        not null default 0,
            constraint ck_document_counter check (counter >= 1)
        );

        -- التسلسل بلا فجوات لكل وحدة إصدار: مضمون في القاعدة، لا في الكود وحده.
        create unique index if not exists ux_document_unit_counter
            on {schema}.document (issuing_unit_id, counter);
        create index if not exists ix_document_tenant_status
            on {schema}.document (tenant_id, status);
        create index if not exists ix_document_journal_entry
            on {schema}.document (journal_entry_id);

        create table if not exists {schema}.submission_attempt (
            attempt_id                  uuid        primary key,
            document_id                 uuid        not null references {schema}.document(document_id),
            attempt_no                  integer     not null,
            started_at                  timestamptz not null,
            completed_at                timestamptz,
            payload_fingerprint         text        not null,
            is_resolution               boolean     not null default false,
            outcome                     text        not null,
            fault_class                 text,
            fault_code                  text,
            fault_message_ar            text,
            fault_message_en            text,
            provider_reference          text,
            provider_reported_duplicate boolean     not null default false
        );
        create unique index if not exists ux_attempt_document_no
            on {schema}.submission_attempt (document_id, attempt_no);

        create table if not exists {schema}.status_transition (
            transition_id uuid        primary key,
            document_id   uuid        not null references {schema}.document(document_id),
            seq           integer     not null,
            status_from   text        not null,
            status_to     text        not null,
            at            timestamptz not null,
            actor         text        not null,
            reason_ar     text        not null,
            reason_en     text        not null,
            attempt_id    uuid
        );
        create unique index if not exists ux_transition_document_seq
            on {schema}.status_transition (document_id, seq);

        create table if not exists {schema}.work_item (
            work_item_id  uuid        primary key,
            document_id   uuid        not null references {schema}.document(document_id),
            tenant_id     text        not null,
            kind          text        not null,
            not_before    timestamptz not null,
            attempts      integer     not null default 0,
            enqueued_at   timestamptz not null,
            last_error_ar text,
            last_error_en text,
            done          boolean     not null default false
        );
        create index if not exists ix_work_due on {schema}.work_item (done, not_before);

        create table if not exists {schema}.reconciliation_finding (
            finding_id          uuid          primary key,
            tenant_id           text          not null,
            kind                text          not null,
            severity            text          not null,
            detected_at         timestamptz   not null,
            document_id         uuid,
            issuing_unit_id     text,
            counter             bigint,
            journal_entry_id    uuid,
            expected_amount     numeric(19,4),
            observed_amount     numeric(19,4),
            summary_ar          text          not null,
            summary_en          text          not null,
            next_step_ar        text          not null,
            next_step_en        text          not null,
            auto_resolved       boolean       not null default false,
            resolved            boolean       not null default false,
            resolved_at         timestamptz,
            resolved_by         text,
            resolution_note_ar  text,
            resolution_note_en  text
        );
        create index if not exists ix_finding_open on {schema}.reconciliation_finding (tenant_id, resolved);
        """;
}
