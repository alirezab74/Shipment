# Event Storming — Shipment Telemetry Operations

## Ubiquitous Language

| Term | Meaning |
|------|---------|
| **Telemetry Event** | Raw IoT message identified by `EventId`, ordered by per-container `SequenceNumber` |
| **Shipment Operational State** | Authoritative milestone progress for one shipment/container stream |
| **Milestone** | Business fact: ArrivedAtPort → GateIn → LoadedOnVessel → DepartedPort → GateOut |
| **Processed Telemetry** | Durable idempotency record keyed by `EventId` |
| **Outbox Message** | Integration intent written atomically with state |
| **Integration Event** | `ShipmentMilestoneRecorded` published at-least-once to downstream systems |

## Event Storming Model

```mermaid
flowchart LR
    subgraph Producers
        IoT[IoT Device / Container Gateway]
    end

    subgraph Commands
        Ingest[Ingest Telemetry]
    end

    subgraph Policies
        P1[Validate envelope + sequence]
        P2[Check idempotency by EventId]
        P3[Validate milestone transition]
        P4[Persist state + processed + outbox atomically]
        P5[Publish outbox via CAP]
        P6[Downstream idempotent consume]
    end

    subgraph DomainEvents
        E1[TelemetryReceived]
        E2[TelemetryAccepted]
        E3[TelemetryRejectedAsDuplicate]
        E4[TelemetryRejectedAsStale]
        E5[MilestoneTransitionValidated]
        E6[ShipmentMilestoneRecorded]
        E7[MilestoneTransitionRejected]
    end

    subgraph Aggregates
        A1[(ShipmentOperationalState)]
    end

    subgraph External
        PG[(PostgreSQL)]
        Redis[(Redis cache - optional)]
        Broker[(RabbitMQ / Kafka via CAP)]
        Downstream[Downstream Notification Service]
    end

    IoT --> E1 --> Ingest
    Ingest --> P1 --> P2 --> A1
    A1 --> P3
    P3 --> E5
    P3 --> E7
    P2 --> E3
    P1 --> E4
    A1 --> E6
    P4 --> PG
    P4 --> E2
    P5 --> Broker
    P6 --> Downstream
    A1 -. read cache .-> Redis
```

## Hotspots & Decisions

| Hotspot | Decision |
|---------|----------|
| Who owns operational state? | `ShipmentOperationalState` aggregate (one row per `ShipmentId`) |
| Aggregate boundary | Shipment + container stream; milestone invariants enforced inside aggregate |
| Event ordering | `SequenceNumber` per container stream; device timestamp is informational only |
| Clock skew | Never used for ordering; stale = `sequence < lastAccepted` |
| Duplicate definition | Same `EventId` + same payload hash → duplicate |
| Payload conflict | Same `EventId` + different payload → reject/quarantine, never mutate |
| Sequence conflict | Same `(ContainerId, SequenceNumber)` + different `EventId` → reject |
| Domain vs integration events | Milestone recorded = domain event; CAP publishes integration contract |
| Replay vs new event | Replay shares existing `EventId`; dedup table prevents second business effect |

## Human-Readable Flow

1. IoT gateway posts telemetry to the API.
2. Application validates idempotency (`EventId`) and sequence ownership.
3. Aggregate loads current milestone and last accepted sequence.
4. If sequence is stale, duplicate, conflicting, or transition-invalid → observable rejection, no state mutation.
5. If sequence gap detected (in-flight lower sequence) → retry with backoff.
6. On acceptance, PostgreSQL transaction updates operational state, processed telemetry, read model, telemetry status, and outbox row.
7. Background outbox publisher delivers `ShipmentMilestoneRecorded` through CAP (RabbitMQ/Kafka).
8. Downstream consumer writes inbox + business notification in one transaction (idempotent).
