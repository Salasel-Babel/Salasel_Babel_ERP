namespace BabelRelationalSpike.Support;

/// <summary>
/// The honest accounting of what Marten's event store gives that this relational
/// log does not, and how much of it this project's "process narrative" needs.
/// جرد صريح لما يقدّمه Marten ولا يقدّمه سجل الأحداث العلائقي.
/// </summary>
public static class MartenGap
{
    public const string Text = """
        WHAT IS LOST VERSUS MARTEN'S EVENT STORE  /  ما الذي نفقده مقابل Marten
        ----------------------------------------------------------------------
        1. PROJECTION CHECKPOINTING
           Marten keeps a per-projection high-water mark (mt_event_progression)
           and resumes exactly where it stopped. Here: nothing. A read model must
           carry its own last-event-seen row. That is a few dozen lines of SQL,
           but it IS work, and it is easy to get subtly wrong: at-least-once vs
           exactly-once, and gaps caused by a lower id committing AFTER a higher
           one. Ordering by a bigint identity is not safe on its own.
           => the single real gap for us.

        2. THE ASYNC DAEMON
           Marten projects out of band with batching, back-pressure and error
           policies. Here: nothing built in - you write it, or you drive it from
           Wolverine handlers fed by the outbox proved in (A).
           But note WHY this evaluation started: that daemon defaults to
           SkipApplyErrors = SkipSerializationErrors = SkipUnknownEvents = true,
           so a ledger event that fails to apply is dead-lettered SILENTLY.
           For the process narrative the projections are inline and tiny (one
           status column, one DISTINCT ON view), so the daemon buys little.

        3. PROJECTION REBUILD TOOLING
           Marten ships rebuild with progress reporting and a documented
           rebuild-while-running story. The relational equivalent proved above is
           REFRESH MATERIALIZED VIEW, or CREATE TABLE AS + rename. Adequate for
           read models of a few million rows; genuinely weaker beyond that.

        4. MULTI-INSTANCE COORDINATION
           Marten's daemon elects a leader so only one node projects. Here:
           PostgreSQL advisory locks - or Wolverine, which is already in the stack
           and already does leader election and node assignment (the
           wolverine_nodes / wolverine_node_assignments tables shown in (A)).

        HOW MUCH DOES THE PROCESS NARRATIVE ACTUALLY NEED?
           Drafts, approvals, POS shift lifecycle, ZATCA submission/retry history,
           lease lifecycle and AI suggestions (accepted AND rejected) are all SHORT
           streams - 2 to 20 events - read by stream id, whose whole projection is
           one current-status row per stream. That is item 3 at toy scale, and
           items 2 and 4 not at all. Item 1 matters only when we add a genuinely
           asynchronous, expensive read model. Nothing in that list is one.
        """;
}
