# ADR-002: Stream Transport, Outbox & Delivery Semantics

## Status

Accepted

## Context

Milestone integration events must be published reliably without dual-write bugs. CAP abstracts RabbitMQ/Kafka. Downstream systems require at-least-once delivery with idempotent consumption.

## Decision

1. **Single correctness boundary**: PostgreSQL transaction writes operational state, processed telemetry, and **application outbox** together.
2. **Outbox publisher**: Background service polls `outbox_messages`, publishes via CAP, marks published (allows duplicate publish on crash-after-send).
3. **CAP role**: Transport only — CAP PostgreSQL storage is used for CAP internals, not as domain source of truth.
4. **Default local broker**: RabbitMQ in Docker Compose; Kafka supported via `Cap:Transport=Kafka`.
5. **Downstream idempotency**: `processed_integration_messages` unique on `EventId` + business write in same transaction.
6. **Semantics**: End-to-end **at-least-once**; no exactly-once claim.

## Alternatives Considered

| Alternative | Why Not |
|-------------|---------|
| CAP outbox only (no app outbox) | Couples domain TX to CAP schema; harder to audit business intent |
| Direct publish without outbox | Dual-write risk on DB commit vs broker send |
| In-process only transport | Insufficient for production integration testing |
| Exactly-once Kafka EOS | Cannot span DB + broker without complex protocol support |

## Consequences

- Restart-safe publication from pending outbox rows.
- Duplicate integration deliveries possible; downstream inbox prevents duplicate effects.
- Poison outbox messages quarantined after `MaxRetries`.
- Operational lag observable via `outbox_backlog` metric.

## Revisit When

- Throughput exceeds single publisher capacity (shard outbox by shipment/container).
- Kafka ordering/partition strategy needs formal hot-key mitigation at broker level.
