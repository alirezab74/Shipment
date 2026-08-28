# Scale Note — 400K Telemetry Burst

## Assumptions

- Peak burst: **400,000 events** over a short window
- Multiple containers; some hot keys possible
- Per-container ordering must be preserved
- PostgreSQL is authoritative; Redis optional

## Partitioning / Sharding Key

- **Kafka**: partition by `ContainerId` (or `ShipmentId` if 1:1)
- **Consumer parallelism**: ≤ partition count for ordering guarantees
- **API ingestion**: horizontally scalable; contention is per-shipment aggregate

## Hot-Key Mitigation

- Serialize writes per `ShipmentId` via optimistic concurrency (not global lock)
- Sequence gap retry absorbs concurrent events on same stream
- Consider hashing high-volume containers to dedicated worker pools

## Batching & Backpressure

- Outbox publisher batches (`OutboxPublisher:BatchSize`)
- Monitor `outbox_backlog`, `telemetry_processing_duration_seconds`
- Apply HTTP 429 / queue depth limits at gateway under sustained overload

## DB Contention

- Primary row update: one `ShipmentOperationalState` per shipment
- Indexes support idempotency lookups; `processed_telemetry` grows with volume
- Retention job (`TelemetryRetention:RetentionDays`) controls dedup storage growth

## Dedup Storage Growth

- ~400K rows per burst if all unique EventIds
- 30-day default retention → plan storage accordingly
- Replay within retention window remains safe

## Replay Behavior

- Consumer group reset replays integration events → downstream inbox dedups
- Telemetry replay with same EventId → duplicate, no milestone regression
- Stale/out-of-order events never overwrite newer sequences

## Poison Events

- Outbox: quarantine after `MaxRetries` (status = Quarantined)
- CAP: built-in retry with `FailedRetryCount`
- Quarantined messages do not block unrelated shipment streams

## Operational Lag Metrics

- `outbox_backlog`
- `telemetry_processing_duration_seconds`
- `telemetry_concurrency_conflict_total`
- Application logs with EventId, ShipmentId, SequenceNumber, TraceId

## Limits (Honest)

- Single modular monolith has vertical scaling ceiling
- Cross-shipment parallelism scales; single hot shipment serializes on aggregate row
- Not claiming infinite scalability — bounded by PostgreSQL write throughput per hot key
