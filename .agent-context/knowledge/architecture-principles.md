# Architecture Principles

StormSpace follows a deliberately simple two-tier architecture (Angular frontend + ASP.NET backend) optimised for single-container deployment, fast iteration, and zero external dependencies. All architectural decisions prioritise simplicity and developer velocity over horizontal scaling.

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  Angular 21 Frontend (standalone components, RxJS Subjects)      │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────────────┐  │
│  │ BoardState   │  │BoardCanvasService│ │ Command<BoardState> │  │
│  │ (signals +   │←─│(undo/redo stack) │←│ (execute / undo)    │  │
│  │  models)     │  └────────┬────────┘  └──────────────────────┘ │
│  └──────────────┘           │                                    │
├─────────────────────────────┼────────────────────────────────────┤
│                    SignalR + REST                                 │
├─────────────────────────────┼────────────────────────────────────┤
│  ASP.NET 10 Backend         │                                    │
│        ┌────────────────────▼─────────────────┐                  │
│        │  BoardsHub / Controllers             │                  │
│        └──────────────┬───────────────────────┘                  │
│                       ▼                                          │
│        ┌──────────────────────────┐                              │
│        │   BoardEventPipeline     │                              │
│        │   ApplyAndLog(event)     │                              │
│        └──────┬───────────┬───────┘                              │
│               ▼           ▼                                      │
│     ┌──────────────┐ ┌────────────┐                              │
│     │BoardStateService│BoardEventLog│                            │
│     └───────┬──────┘ └────────────┘                              │
│             ▼                                                    │
│     ┌──────────────┐                                             │
│     │BoardsRepository│ (IMemoryCache, 1-hour sliding expiry)    │
│     └──────────────┘                                             │
│                                                                  │
│  AI Agents (Microsoft Agent Framework)                           │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐        │
│  │ Facilitator  │─►│ Specialists  │─►│ BoardPlugin      │        │
│  │ (orchestrate)│  │ (delegated)  │  │ (same pipeline)  │        │
│  └─────────────┘  └──────────────┘  └──────────────────┘        │
└──────────────────────────────────────────────────────────────────┘
```

## Core Principles

### 1. Two-Tier Monolith

StormSpace is a single deployable unit: an ASP.NET backend serving an Angular SPA, communicating via REST and SignalR.

| Aspect | Choice | Rationale |
|--------|--------|-----------|
| Deployment | Single Docker container | Zero-dependency deployment; `docker run` is all you need |
| Frontend | Angular 21 SPA | Served as static files from `/wwwroot` |
| Backend | ASP.NET 10 | REST + SignalR + AI agents in one process |
| Communication | SignalR (primary) + REST (CRUD) | Real-time collaboration is the core requirement |

This was chosen for simplicity, speed of iteration, and single-developer velocity. There is no BFF layer, API gateway, or microservice boundary.

### 2. Ephemeral In-Memory State

All board state lives in memory with no external database. This is a deliberate design choice, not a missing feature.

| Aspect | Detail |
|--------|--------|
| Storage | `IMemoryCache` with 1-hour sliding expiration |
| Persistence | None — boards lost on restart |
| Export | JSON files exported by users, stored alongside their code |
| Import | JSON import recreates full board state |
| Board list | Single `List<Board>` in cache (all boards) |

**Design rationale:**
- **Zero dependencies** — no database, Redis, or external storage to provision
- **Boards are ephemeral workshop artifacts** — Event Storming sessions are time-boxed; permanent storage isn't the primary concern
- **Export-as-code philosophy** — JSON exports live in repos alongside the code being modelled, making them versionable and portable

**Key file:** `src/EventStormingBoard.Server/Repositories/BoardsRepository.cs`

### 3. Event Pipeline (Single Mutation Path)

All board state mutations — whether from users or AI agents — flow through `IBoardEventPipeline.ApplyAndLog()`. Nothing mutates state directly.

```
Hub method / Agent tool → BoardEventPipeline.ApplyAndLog(event, userName)
                          ├── BoardStateService.Apply*(event)   // mutate state
                          └── BoardEventLog.Append(...)         // audit trail
                          → SignalR broadcast to group
```

| Component | Responsibility |
|-----------|----------------|
| `BoardEventPipeline` | Routes events to state service + event log (switch on event type) |
| `BoardStateService` | Applies mutations to `Board` entity under per-board lock |
| `BoardEventLog` | Circular buffer (50 entries per board) of human-readable summaries |

Events carry before/after values and an `IsUndo` flag, enabling bidirectional application for undo/redo. This is not full event sourcing — events are not replayed from a persistent log — but may evolve in that direction.

**Key files:** `src/EventStormingBoard.Server/Services/BoardEventPipeline.cs`, `src/EventStormingBoard.Server/Services/BoardStateService.cs`, `src/EventStormingBoard.Server/Services/BoardEventLog.cs`

### 4. Per-Board Concurrency with SemaphoreSlim

Each board has its own `SemaphoreSlim(1,1)` for serialised access. Cross-board operations are fully concurrent.

| Service | Lock Target | Purpose |
|---------|-------------|---------|
| `BoardStateService` | Board state mutations | Prevents lost updates within a board |
| `AgentService` | Agent chat turns | Serialises manual + autonomous agent turns per board |
| `BoardEventLog` | Per-board linked list | Guards circular buffer mutations |

Locks are stored in `ConcurrentDictionary<Guid, SemaphoreSlim>` and created lazily per board. There is no cross-board locking, eliminating deadlock risk.

**Key file:** `src/EventStormingBoard.Server/Services/BoardStateService.cs` (`WithBoard()` helper)

### 5. Singleton Service Model

All backend services are registered as singletons. This is required by the in-memory state model — services hold board state, conversation history, presence data, and autonomous coordination state in their fields.

| Service | State Held |
|---------|------------|
| `BoardsRepository` | All boards (via `IMemoryCache`) |
| `BoardPresenceService` | Connected users per board |
| `AgentService` | Conversation histories per board (200 msg cap) |
| `AutonomousFacilitatorCoordinator` | Debounce timers, run history, cooldowns |
| `BoardEventLog` | Recent event summaries per board (50 cap) |

**Scaling implication:** Single-instance only. Horizontal scaling would require externalising state (e.g., Redis, database). This is acceptable for the target scale of 2–8 users per board.

**Key file:** `src/EventStormingBoard.Server/Program.cs` (DI registrations)

### 6. Facilitator-Centric Agent Delegation

The AI agent system uses a hub-and-spoke orchestration pattern. The Facilitator is always the conversation entry point and delegates to specialists — specialists never communicate directly.

```
User message → Facilitator
                ├── DelegateToAgent(specialist)  → board mutations
                ├── RequestBoardReview(agent)     → read-only advisory
                └── AskAgentQuestion(agent)       → domain Q&A (read-only)
```

| Principle | Detail |
|-----------|--------|
| Single entry point | Facilitator receives all user messages |
| No specialist-to-specialist delegation | Prevents runaway agent loops |
| Cross-agent Q&A allowed | Specialists can use `AskAgentQuestion` to consult DomainExpert |
| Role-based tool restrictions | `AllowedTools` per agent in `AgentConfiguration`; enforced at creation time |
| Destructive tool filtering | `DeleteNotes` / `DeleteBoundedContext` stripped in autonomous (non-manual) mode |

**Design rationale:** Prevents agent loops and simplifies tracing/debugging of multi-agent interactions. The `AskAgentQuestion` tool allows controlled specialist-to-specialist knowledge sharing without delegation chains.

**Key files:** `src/EventStormingBoard.Server/Agents/FacilitatorGroupChat.cs`, `src/EventStormingBoard.Server/Agents/Plugins/DelegationPlugin.cs`

### 7. Frontend: RxJS Subjects + Command Pattern

The Angular frontend uses two complementary patterns for state management:

**SignalR event streams (RxJS Subjects):**
- `BoardsSignalRService` exposes a `Subject` per event type (e.g., `noteAdded$`, `agentResponse$`)
- Components subscribe with `takeUntilDestroyed()` or `toSignal()` for cleanup
- No centralised store (no NgRx) — state flows directly from SignalR events to components

**Canvas mutations (Command pattern):**
- All canvas changes are `Command<BoardState>` objects with `execute()` / `undo()` methods
- `BoardCanvasService` maintains local undo/redo stacks
- Commands execute locally, then broadcast to the SignalR hub for server-side application and broadcast to other clients

| Aspect | Choice |
|--------|--------|
| State management | RxJS Subjects — lightweight, fast iteration |
| Undo/redo | Client-side command stacks |
| Canvas rendering | Imperative HTML Canvas (not declarative) |
| Component architecture | Standalone components with `inject()` DI |
| Signal adoption | `signal()`, `computed()`, `effect()`, `input()`, `output()`, `viewChild()` / `viewChild.required()` |
| Cleanup | `DestroyRef` + `takeUntilDestroyed()` (no `destroy$` subjects) |

**Key files:** `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`, `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts`, `src/eventstormingboard.client/src/app/board/board.commands.ts`

### 8. Optional Authentication

Authentication (Entra ID / MSAL) is entirely optional. The application works fully without it — users self-identify with display names.

| Mode | Behaviour |
|------|-----------|
| No `EntraId` config | Anonymous access, self-identified usernames |
| Partial `EntraId` config | Fail-fast at startup with descriptive error |
| Full `EntraId` config | JWT Bearer auth on `/api/*` and `/hub/*`; MSAL in frontend |

The frontend fetches `/api/auth/config` before Angular bootstrap to determine auth mode. SignalR tokens are passed via query string during WebSocket negotiation.

**Key files:** `src/EventStormingBoard.Server/Program.cs`, `src/eventstormingboard.client/src/main.ts`

## Middleware Pipeline

The ASP.NET middleware pipeline is ordered:

1. `UseDefaultFiles()` / `UseStaticFiles()` — serve Angular SPA
2. `UseSwagger()` / `UseSwaggerUI()` — dev only
3. `UseAuthentication()` — optional Entra ID
4. `UseAuthorization()` — JWT Bearer when enabled
5. `MapControllers()` — REST API (`/api/*`)
6. `MapHub<BoardsHub>("/hub")` — SignalR real-time
7. `MapFallbackToFile("/index.html")` — SPA catch-all

**Key file:** `src/EventStormingBoard.Server/Program.cs`

## Design Trade-offs

| Decision | Benefit | Trade-off |
|----------|---------|-----------|
| In-memory state, no DB | Zero-dependency deployment; fast; simple | Boards lost on restart; no multi-instance scaling |
| All singletons | Services share state naturally | Single-instance only; would need Redis/DB for scale-out |
| Event pipeline (not event sourcing) | Simpler; sufficient for undo/redo | Can't replay to exact point; no durable audit trail |
| Per-board `SemaphoreSlim` locking | Board isolation; no cross-board deadlocks | Within a board, all ops serialised (throughput cap) |
| RxJS Subjects (no NgRx) | Lightweight; fast iteration | No time-travel debugging; no centralised devtools |
| HTML Canvas (imperative) | Full drawing control; no framework overhead | Harder to test; no declarative diffing |
| Facilitator-centric delegation | Prevents agent loops; simple tracing | Single bottleneck; Facilitator failure blocks all agents |
| Optional auth (not required) | Frictionless local use; easy Docker demos | Anonymous access means no audit trail by default |

## Extending the Architecture

When adding new features, follow these architectural constraints:

- **Board mutations** must go through `IBoardEventPipeline.ApplyAndLog()` — create a new `BoardEvent` subclass, add a handler in `BoardStateService`, and wire it in the pipeline switch
- **New services** should be registered as singletons and use per-board `ConcurrentDictionary` + `SemaphoreSlim` for any mutable state
- **Agent tools** are added to `BoardPlugin` / `DelegationPlugin` and gated by `AllowedTools` in `AgentConfiguration`
- **SignalR events** follow `Broadcast{EventName}` naming on the hub and match the event class name on the client
- **Frontend state** flows through `BoardsSignalRService` subjects — add a new Subject for new event types
- **Canvas commands** implement `Command<BoardState>` with `execute()` / `undo()` for undo/redo support
