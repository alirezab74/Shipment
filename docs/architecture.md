# Architecture Overview

## Pattern Stack

| Layer | Project | Responsibility |
|-------|---------|----------------|
| API | `ShipmentTelemetry.Api` | HTTP endpoints, validation, correlation IDs |
| Application | `ShipmentTelemetry.Application` | MediatR commands/queries, orchestration |
| Domain | `ShipmentTelemetry.Domain` | Aggregates, value objects, invariants |
| Infrastructure | `ShipmentTelemetry.Infrastructure` | EF Core, Redis, CAP, outbox publisher |
| Contracts | `ShipmentTelemetry.Contracts` | Integration event DTOs |

## Primary Flow

```
POST /api/telemetry/events
  → ProcessTelemetryCommand (MediatR)
  → Idempotency + sequence checks
  → ShipmentOperationalState.ProcessTelemetry()
  → Single EF SaveChanges (state + processed + outbox + read model)
  → OutboxPublisher → CAP → RabbitMQ/Kafka
  → ShipmentMilestoneRecordedConsumer (inbox + downstream notification)
```

## Persistence Model

| Table | Purpose | Key Constraints |
|-------|---------|-----------------|
| `shipment_operational_states` | Aggregate store | PK `ShipmentId`, concurrency `Version` |
| `processed_telemetry` | Idempotency | Unique `EventId`, unique `(ContainerId, SequenceNumber)` |
| `outbox_messages` | Integration intent | Index `(Status, CreatedAt)` |
| `telemetry_statuses` | Query processing outcome | PK `EventId` |
| `shipment_operational_read_models` | CQRS read side | PK `ShipmentId` |
| `processed_integration_messages` | Downstream inbox | Unique `MessageId` (= EventId) |
| `downstream_milestone_notifications` | Downstream effect | Unique `IntegrationEventId` |

## Stream Transport Comparison

| Aspect | RabbitMQ | Kafka | In-Process |
|--------|----------|-------|------------|
| Ordering | Queue/exchange routing | Partition key | Single thread |
| Replay | Limited (TTL/dead-letter) | Consumer group reset + retention | Manual re-invoke |
| Retention | Short by default | Configurable log retention | None |
| Backpressure | Prefetch / QoS | Consumer lag | Caller blocks |
| Ops complexity | Lower | Higher | Minimal |
| **Recommendation** | Local dev / moderate volume | **Production telemetry at scale** | Unit/integration tests |

**Production recommendation**: Kafka with partition key = `ContainerId` preserves per-stream ordering and supports 24h replay for consumer group resets.

## CAP Integration

CAP sits at the infrastructure boundary. Domain/application code enqueues outbox rows; `OutboxPublisherBackgroundService` calls `ICapPublisher`. CAP does not drive domain state.

## Requirement Traceability

| Requirement | Implementation | Test |
|-------------|----------------|------|
| Duplicate EventId | `ProcessedTelemetryStore` | `DuplicateEventId_ProducesOneBusinessEffect` |
| Payload conflict | Handler hash compare | `SameEventIdWithDifferentPayload_IsRejected` |
| Stale sequence | Aggregate + domain | `StaleSequence_DoesNotOverwriteNewerState` |
| Invalid transition | `MilestoneTransitionRules` | `InvalidMilestoneTransition_DoesNotCorruptState` |
| Concurrency | Optimistic `Version` + gap retry | `ConcurrentEvents_ProduceValidDeterministicState` |
| Transactional outbox | `OutboxWriter` + same UoW | `AcceptedMilestone_PersistsPendingOutboxMessage` |
| Downstream idempotency | Inbox + notification TX | `DuplicateIntegrationEventDelivery_ProducesOneDownstreamEffect` |
| Replay safety | EventId dedup | `ReplayOfProcessedTelemetry_DoesNotDuplicateMilestones` |

See [event-storming.md](./event-storming.md), [scale-note.md](./scale-note.md), and [ADRs](./adr/).
