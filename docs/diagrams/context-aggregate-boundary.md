# Context & Aggregate Consistency Boundary

```mermaid
flowchart TB
    subgraph BoundedContext["Bounded Context: Shipment Telemetry Operations"]
        subgraph WriteModel["Write Model (Strong Consistency)"]
            Agg["Aggregate Root<br/>ShipmentOperationalState"]
            PT["ProcessedTelemetry"]
            OB["Outbox"]
            Agg --> PT
            Agg --> OB
        end

        subgraph ReadModel["Read Model (Eventually Consistent)"]
            RM["ShipmentOperationalReadModel"]
        end

        Agg -. projection .-> RM
    end

    subgraph IntegrationBoundary["Integration Boundary"]
        CAP["CAP Publisher"]
        Inbox["ProcessedIntegrationMessages"]
        DS["DownstreamMilestoneNotifications"]
    end

    API["Telemetry API"] --> Agg
    API --> RM
    OB --> CAP
    CAP --> Inbox
    Inbox --> DS

    PG[(PostgreSQL)] --- WriteModel
    PG --- ReadModel
    PG --- IntegrationBoundary
    Redis[(Redis - optional cache)] -.-> RM
```

## Consistency Rules

- **Single transaction** commits: operational state, processed telemetry, outbox, read model, telemetry status.
- **Aggregate version** (`uint Version`) provides optimistic concurrency on `ShipmentOperationalState`.
- **Unique indexes** enforce idempotency (`EventId`) and sequence ownership (`ContainerId + SequenceNumber`).
- Redis cache is invalidated after successful writes; PostgreSQL remains authoritative.
