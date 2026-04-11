# Project Guidelines — StormSpace

StormSpace is a collaborative Event Storming web application with AI-powered multi-agent facilitation.

## Architecture

Two-tier app: Angular 21 frontend + ASP.NET 10 backend, communicating via REST and SignalR.

```
src/eventstormingboard.client/    # Angular 21, Material 21, HTML Canvas
src/EventStormingBoard.Server/    # ASP.NET 10, SignalR, Microsoft Agent Framework
tests/EventStormingBoard.Server.Tests/  # xUnit, Moq, AwesomeAssertions
```

**Backend flow**: Hub/Controller → `IBoardEventPipeline.ApplyAndLog()` → `IBoardStateService` + `IBoardEventLog` → `IBoardsRepository` (in-memory cache). All state changes broadcast to clients via SignalR.

**AI agent system**: Multi-agent group chat (Facilitator → Specialist → WallScribe). Agents created by `BoardAgentFactory`, orchestrated by `FacilitatorGroupChat`, with `AgentToolCallFilter` middleware for tool call tracking. See [README.md](../README.md#ai-agents) for agent roles and protocols.

**Frontend state**: RxJS Subject-based event streaming via `BoardsSignalRService` — no NgRx. Board canvas is HTML Canvas with imperative drawing logic.

## Build and Test

```bash
# Backend
dotnet build src/EventStormingBoard.Server/EventStormingBoard.Server.csproj
dotnet test tests/EventStormingBoard.Server.Tests/

# Frontend
cd src/eventstormingboard.client
npm install
npm start          # Dev server with HTTPS
npm run build      # Production build

# Docker
docker run -it -p 8080:8080 --rm mabentley/stormspace
```

## Code Style

### Backend (.NET 10)

- **Nullable enabled**, implicit usings enabled — annotate all reference types
- **Sealed classes** for service implementations (e.g., `sealed class BoardStateService : IBoardStateService`)
- **Naming**: `I{Name}` interfaces, `{Entity}{Action}Event` events, `{Entity}Dto` models
- **Concurrency**: `ConcurrentDictionary<Guid, T>` for collections, `SemaphoreSlim` for board-level locks
- **All services registered as Singletons** (in-memory state model)

### Frontend (Angular 21)

- **Standalone components** — no NgModules, imports declared in `@Component({ imports: [...] })`
- **Services**: `@Injectable({ providedIn: 'root' })` with RxJS Subjects for event streams
- **Unsubscription**: `destroy$` subject pattern with `takeUntil()`
- **Models**: Separate `.model.ts` files in `_shared/models/`
- **`@for` track expressions**: Use `track $index` when duplicate values are possible (prevents NG0955)
- **Theming**: See the `frontend-design` skill for the Kinetic Ionization design system

### Tests (xUnit)

- **Method naming**: `Given{Precondition}_When{Action}_Then{Expected}` (e.g., `GivenBoardNameUpdatedEvent_WhenApplying_ThenBoardNameIsUpdated`)
- **Test body**: Explicit `// Arrange`, `// Act`, `// Assert` comment sections
- **Mocking**: Moq (`Mock<T>`) for interfaces; existing hand-rolled spies/stubs (e.g., `SpyBoardStateService`, `InMemoryBoardsRepository`) remain valid
- **Assertions**: AwesomeAssertions fluent chains (`.Should().Be(...)`) — never xUnit `Assert.*`
- **Manual DI**: Direct construction, no DI container in tests
- **No frontend tests** currently exist

## Event Storming Domain

### Sticky Note Types

Notes are 120×120 px (except User at 60×60 px). Each type has a specific colour and role:

| Type | Role | Placement |
|------|------|-----------|
| **Event** | Something that happened (past tense, e.g. "Order Created"). Unique. | Ordered left-to-right chronologically, 600 px apart centre-to-centre |
| **Command** | Action/intent that triggers an Event. Unique. | 140 px left of its Event |
| **Aggregate** | Cluster of domain objects treated as one unit. May be duplicated. | Above the Command/Event pair (midpoint x, 130 px above) |
| **User** | Actor/persona who triggers a Command or manual Policy. 60×60 px. May be duplicated. | Bottom-left of parent (x − 30, y + 80) |
| **Policy** | Business rule or automated reaction following an Event. | 140 px right of its Event. Multiple policies stack vertically (135 px gap) |
| **ReadModel** | Data view for UI. Only added when explicitly requested. | Left of the Command |
| **ExternalSystem** | Outside dependency. May be duplicated. | Below the Command/Event pair (130 px below) |
| **Concern** | Problem, risk, question, or hotspot. | Near the related note (typically 275 px above) |

### Cluster Patterns

Notes group into **clusters**, each centred on one Event:

1. **Manual Command** — User → Command → Event → [Policy/Policies] (person triggers the command)
2. **Automated Command** — Command → Event → [Policy/Policies] (triggered by a Policy in a preceding cluster via connection)
3. **ExternalSystem-triggered** — ExternalSystem → Command → Event → [Policy/Policies]
4. **Manual Policy** — Policy + User (standalone decision point, no Command/Event)

Connections link clusters together (outer note of one cluster → Command of next). Never connect notes within the same cluster.

### Workshop Phases

Phases progress in order. Each phase has specialist agents that are active during it.

1. **SetContext** — Understand the domain and scope. Facilitator guides directly; no board changes yet.
2. **IdentifyEvents** — Brainstorm Events, order chronologically. Active agent: `EventExplorer`.
3. **AddCommandsAndPolicies** — Determine what triggers each Event (Commands, Policies, Users, ExternalSystems). Active agent: `TriggerMapper`.
4. **DefineAggregates** — Place Aggregates above Command/Event pairs. Active agent: `DomainDesigner`.
5. **BreakItDown** — Group flows into Bounded Contexts and Subdomains. Active agent: `DomainDesigner`.

The `Organiser` agent is active in phases 2–5 for layout tidying. The `DomainExpert` agent is active in all phases for domain Q&A (read-only, no board changes).

### AI Agent Roles

| Agent | Role | Tools |
|-------|------|-------|
| **Facilitator** | Guides the workshop, delegates to specialists, never modifies the board directly | `GetBoardState`, `SetDomain`, `SetPhase`, `DelegateToAgent`, `RequestBoardReview`, `AskAgentQuestion` |
| **EventExplorer** | Creates Events and Concerns during IdentifyEvents phase | `CreateNotes`, `EditNoteTexts`, `MoveNotes`, `DeleteNotes` |
| **TriggerMapper** | Builds clusters (Commands, Policies, Users, ExternalSystems) around Events | `CreateNotes`, `CreateConnections`, `EditNoteTexts`, `MoveNotes`, `DeleteNotes` |
| **DomainDesigner** | Places Aggregates, recommends Bounded Contexts | `CreateNotes`, `CreateConnections`, `EditNoteTexts`, `MoveNotes`, `DeleteNotes` |
| **Organiser** | Tidies board layout only — moves notes, creates Concerns | `MoveNotes`, `CreateNotes` (Concern only) |
| **DomainExpert** | Answers domain questions (eCommerce SME) — read-only | `GetBoardState` |

All specialist agents can also call `GetBoardState`, `GetRecentEvents`, and some can use `AskAgentQuestion` to consult the DomainExpert.

## Conventions

- **Event pipeline**: All board mutations must go through `IBoardEventPipeline.ApplyAndLog()` — never mutate state directly
- **Agent tool policies**: Facilitator has full toolset, specialists are read-only, WallScribe handles mutations. `DeleteNotes` restricted to manual (non-autonomous) mode. Policies are tested in `AgentServiceToolPolicyTests`
- **SignalR hub methods**: Match event names (e.g., `NoteCreated`, `BoardNameUpdated`). Hub calls `RecordUserActivity()` on every user action for autonomous debouncing
- **Board phases**: SetContext → IdentifyEvents → AddCommandsAndPolicies → DefineAggregates → BreakItDown
- **Agent configuration**: Per-board via `AgentConfiguration` entities — models, prompts, active phases all configurable through the UI

## GitHub Project

The agentic development pipeline tracks work on a GitHub Project board. The **Delivery Manager** agent manages all GitHub interactions.

- **Repository**: `matt-bentley/StormSpace`
- **Project**: `matt-bentley/projects/1` (name: "StormSpace")
- **Board columns**: Backlog, Ready, In progress, In review, Done

### Tracking Model

Each Orchestrator pipeline run creates:
- **Tracking Issue** — parent issue for the task, labelled `agent-task`
- **Phase sub-issues** — one per implementation phase, linked as sub-issues
- **Regression sub-issue** — tracks regression testing, linked as sub-issue
- **Branch** — `task/{task-slug}`, created at pipeline start
- **PR** — created after Implementation Review passes

### Labels

- `agent-task` — applied to all tracking issues created by the Delivery Manager
- `failed` — added to tracking issue if the pipeline fails or halts
