# ADR-001: Event Ordering, Idempotency & Concurrency Strategy

## Status

Accepted

## Context

Telemetry arrives at-least-once, out of order, duplicated, and concurrently from multiple workers. Device clocks may skew; broker offsets must not define business order. Operational milestone state must never regress.

## Decision

1. **Ordering**: Per-container monotonic `SequenceNumber` is authoritative. Device timestamp is audit metadata only.
2. **Stale**: Reject when `sequence < lastAcceptedSequence` without mutating aggregate.
3. **Sequence gap**: When `sequence > lastAcceptedSequence + 1`, retry with backoff (concurrent in-flight lower sequence).
4. **Idempotency**: Durable `processed_telemetry` table with unique `EventId` and unique `(ContainerId, SequenceNumber)`.
5. **Payload conflict**: Same `EventId` + different payload hash → reject permanently.
6. **Concurrency**: Optimistic concurrency via EF `Version` column on `ShipmentOperationalState`; losing worker retries up to 5 times.

## Alternatives Considered

| Alternative | Why Not |
|-------------|---------|
| Redis distributed locks | Not durable; adds failure mode; not required for correctness |
| Device timestamp ordering | Non-deterministic under clock skew |
| Broker offset as business cursor | Transport-level; lost on replay/reset |
| Pessimistic DB locks | Higher contention under 400K burst |

## Consequences

- Correctness survives replay, duplication, and concurrent workers.
- Sequence gaps may increase latency until lower sequences commit.
- `processed_telemetry` grows; retention job purges metadata after configurable days.
- Rejected events remain observable via `telemetry_statuses` and optional quarantine.

## Revisit When

- Per-shipment ordering requires cross-container coordination.
- Retention window causes replay beyond dedup horizon.
- Measurable retry storms indicate need for partition-level serialization.
