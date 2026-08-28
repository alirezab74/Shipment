# AI Engineering Note

> **Human fill-in required**: Replace bracketed placeholders with your actual tooling/session details before submission.

## Tools / Agents Used

- [ ] Cursor IDE with Claude / GPT agent
- [ ] Other: _______________

## Prompt Patterns That Worked

- Single comprehensive challenge prompt with numbered requirements (sections 1–40)
- Explicit "implement, don't sketch" instruction
- Mandatory test list with concurrency/replay/outbox scenarios
- Architecture constraints (Clean Architecture, DDD, no microservice-per-entity)

## AI-Generated / Modified Artifacts

| Area | AI Role |
|------|---------|
| Solution structure & projects | Generated |
| Domain aggregate & milestone rules | Generated + reviewed |
| ProcessTelemetryCommandHandler | Generated + sequence-gap fix |
| Integration tests (Testcontainers) | Generated |
| Documentation, ADRs, diagrams | Generated |
| Docker Compose & Dockerfile | Generated |

## Verification Performed

- [ ] `dotnet build ShipmentTelemetry.sln`
- [ ] `dotnet test` (unit + integration + architecture)
- [ ] Manual API smoke test via Docker Compose
- [ ] Review of concurrency test (Barrier + independent scopes)

## Recommendation Rejected

**Redis-based deduplication** — rejected because challenge requires durable PostgreSQL idempotency; Redis retained only as optional read cache with graceful degradation.

## Incorrect Assumption Discovered

**Initial concurrent worker handling** — without sequence-gap retry, concurrent events (201 GateIn + 202 Load) could incorrectly reject the higher sequence as invalid transition instead of waiting for the in-flight lower sequence. Fixed by retrying when `sequence > lastAccepted + 1`.

---

*If this note was produced entirely by AI during generation, confirm verification steps above were actually executed before submission.*
