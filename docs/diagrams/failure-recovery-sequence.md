# Failure & Recovery Sequence

```mermaid
sequenceDiagram
    participant IoT as IoT Gateway
    participant API as Telemetry API
    participant DB as PostgreSQL
    participant Outbox as Outbox Publisher
    participant CAP as CAP / Broker
    participant Down as Downstream Consumer

    Note over IoT,Down: Duplicate & out-of-order telemetry
    IoT->>API: Event seq=106 (duplicate EventId)
    API->>DB: Lookup ProcessedTelemetry by EventId
    DB-->>API: Found (same payload)
    API-->>IoT: 200 Duplicate (no state change)

    IoT->>API: Event seq=104 (stale)
    API->>DB: Load aggregate (lastAccepted=105)
    API-->>IoT: 200 Stale (state unchanged)

    Note over IoT,Down: Happy path + outbox recovery
    IoT->>API: Event seq=107 accepted
    API->>DB: BEGIN — update state, processed, outbox
    DB-->>API: COMMIT
    API-->>IoT: 200 Accepted

    Outbox->>DB: Poll pending outbox
    Outbox->>CAP: Publish ShipmentMilestoneRecorded
    Note over Outbox,DB: Crash before mark published
    Outbox->>DB: Mark outbox Published (may retry publish)

    CAP->>Down: Deliver (at-least-once)
    Down->>DB: BEGIN — inbox + notification
    DB-->>Down: COMMIT
    CAP->>Down: Duplicate delivery
    Down->>DB: Unique inbox violation → ignore
```

## Failure Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| A — TX fails before commit | Nothing persisted |
| B — Commit OK, app crash before publish | Outbox stays pending; publisher retries |
| C — Published, crash before mark | Message may duplicate; downstream inbox dedups |
| D — Duplicate telemetry | No second milestone/outbox from same EventId |
| E — Stale telemetry | Newer operational state preserved |
| F — EventId payload conflict | Rejected; state not corrupted |
