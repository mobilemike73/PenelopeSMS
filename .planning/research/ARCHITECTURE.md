# Architecture Research

**Domain:** Local SMS campaign operations app with cloud callback ingestion
**Researched:** 2026-03-12
**Confidence:** HIGH

## Standard Architecture

### System Overview

```text
┌─────────────────────────────────────────────────────────────┐
│                     Operator Console App                    │
├─────────────────────────────────────────────────────────────┤
│  Menu Flow  Import Worker  Verify Worker  Campaign Worker  │
│  Status Poller  Reporting   Config/Logging                 │
└───────────────┬───────────────────────────────┬─────────────┘
                │                               │
                │                               │
┌───────────────▼──────────────┐   ┌───────────▼────────────────┐
│ SQL Server Express (local)   │   │ External Services          │
│ Numbers / Imports / Campaigns│   │ Oracle / Twilio / AWS SQS  │
│ Messages / Status History    │   │ Public callback bridge      │
└───────────────┬──────────────┘   └───────────┬────────────────┘
                │                              │
                └──────────────┬───────────────┘
                               │
                    ┌──────────▼──────────┐
                    │ Callback Intake     │
                    │ API Gateway/Lambda  │
                    │ or hosted API       │
                    └─────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| Console application shell | Present menu actions and orchestrate workflows | .NET 9 console app using Generic Host and DI |
| Import pipeline | Read Oracle rows, normalize numbers, and persist deduped records | ODP.NET Core queries plus SQL Server repositories / EF Core |
| Verification pipeline | Call Twilio Lookup, map results, and update eligibility | Twilio SDK with throttled async processing |
| Campaign pipeline | Create campaign rows, send batches, and persist Twilio SIDs | Twilio Messaging API plus durable send ledger |
| Callback consumer | Ingest delivery status events and update message records idempotently | Local SQS long-poll worker or hosted webhook controller |

## Recommended Project Structure

```text
src/
├── PenelopeSMS.App/             # Console entry point and host setup
│   ├── Menu/                    # Menu handlers and prompts
│   ├── Workflows/               # Import, verify, campaign orchestration
│   └── Program.cs               # Host builder and app bootstrap
├── PenelopeSMS.Domain/          # Entities, enums, and business rules
├── PenelopeSMS.Infrastructure/  # EF Core, Oracle, Twilio, AWS integrations
│   ├── SqlServer/               # DbContext, migrations, repositories
│   ├── Oracle/                  # Import queries and source mapping
│   ├── Twilio/                  # Lookup and Messaging clients
│   └── Aws/                     # SQS polling and callback DTO mapping
├── PenelopeSMS.CallbackBridge/  # Optional public webhook bridge if self-hosted
└── PenelopeSMS.Tests/           # Unit and integration tests
```

### Structure Rationale

- **`PenelopeSMS.App/`:** keeps operator-facing flow separate from infrastructure code.
- **`PenelopeSMS.Domain/`:** centralizes send-eligibility and dedupe rules so they are testable.
- **`PenelopeSMS.Infrastructure/`:** isolates Oracle, Twilio, AWS, and SQL Server concerns behind interfaces.
- **`PenelopeSMS.CallbackBridge/`:** optional boundary if a public endpoint is needed outside AWS-native bridging.

## Architectural Patterns

### Pattern 1: Hosted Console Monolith

**What:** one local process hosting menu commands plus background workers.
**When to use:** default for this project; 70k records and a single operator do not justify service sprawl.
**Trade-offs:** simplest deployment, but long-running callback polling and interactive menu flow must coordinate cleanly.

### Pattern 2: Durable Message Ledger

**What:** persist every outbound attempt before and after Twilio API submission.
**When to use:** always for campaign sends.
**Trade-offs:** slightly more write volume, but it is the cleanest way to avoid duplicate sends and to correlate callbacks reliably.

### Pattern 3: Public Bridge, Private Processing

**What:** expose only a narrow public callback endpoint, then push events into SQS for internal processing.
**When to use:** recommended whenever the operator app remains local/private.
**Trade-offs:** adds AWS infrastructure, but keeps Twilio-facing ingress decoupled from the local machine and supports spike smoothing.

## Data Flow

### Request Flow

```text
[Menu Action]
    ↓
[Workflow Handler] → [Service] → [Integration Client] → [External System]
    ↓                    ↓              ↓                     ↓
[Console Result] ← [Mapped Outcome] ← [DTO / API result] ← [Provider]
```

### State Management

```text
[SQL Server]
    ↓
[Repositories / DbContext]
    ↓
[Workflow Services]
    ↓
[Console summaries and background consumers]
```

### Key Data Flows

1. **Import flow:** Oracle query -> normalized phone number -> dedupe / cross-reference -> SQL Server import audit tables.
2. **Verification flow:** eligible number selection -> Twilio Lookup -> line type / carrier mapping -> eligibility flags in SQL Server.
3. **Campaign flow:** campaign creation -> recipient snapshot -> batched Twilio sends -> Message SID persistence -> callback URL correlation.
4. **Delivery flow:** Twilio status callback -> public bridge -> SQS -> local consumer -> idempotent status/history update in SQL Server.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0-100k numbers / single operator | One hosted console app plus local SQL Server is sufficient |
| 100k-1M numbers or repeated high-volume campaigns | Split callback consumer and send workers from the interactive menu shell |
| Multi-operator or always-on campaigns | Move core workers to a hosted service and let the console become an admin client |

### Scaling Priorities

1. **First bottleneck:** callback ingestion and status processing spikes — smooth with SQS long polling and batch consumers.
2. **Second bottleneck:** database write amplification during import and campaign creation — mitigate with chunking and set-based updates.

## Anti-Patterns

### Anti-Pattern 1: Oracle as a second full ORM domain

**What people do:** create a second rich EF Core model for a source system they only read from.
**Why it's wrong:** adds provider-specific complexity and migration confusion.
**Do this instead:** keep Oracle access thin and purpose-built for import queries.

### Anti-Pattern 2: Direct callback writes to the local machine

**What people do:** try to have Twilio call a localhost or intermittently reachable workstation.
**Why it's wrong:** Twilio requires a public, stable endpoint.
**Do this instead:** use a public bridge, then forward into SQS or another durable ingestion path.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Oracle database | Read-only query pipeline | Import only the columns required for campaign operations. |
| Twilio Lookup | Per-number enrichment API | Use `line_type_intelligence`; treat exact SMS-capable inference carefully. |
| Twilio Messaging | Batched outbound sends with `StatusCallback` | Persist Twilio SID immediately after each accepted send request. |
| AWS SQS | Long-poll queue consumer | Set long polling and tune visibility timeout to processing time. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| App -> Infrastructure | Service interfaces | Keeps menu flows testable |
| Workflow -> SQL Server | Repositories / DbContext | Central place for transaction boundaries |
| Callback consumer -> campaign ledger | Idempotent update API | Prevent duplicate callback effects |

## Sources

- Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
- Microsoft Learn: https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- Twilio Docs: https://www.twilio.com/docs/messaging/guides/track-outbound-message-status
- Twilio Docs: https://www.twilio.com/docs/usage/webhooks/webhooks-security
- Twilio Blog: https://www.twilio.com/en-us/blog/serverless-twilio-status-callback-aws
- AWS SDK for .NET docs: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/ReceiveMessage.html
- AWS SQS Developer Guide: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html

---
*Architecture research for: PenelopeSMS*
*Researched: 2026-03-12*
