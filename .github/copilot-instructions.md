# Project Guidelines — StormSpace

StormSpace is a collaborative Event Storming web application with AI-powered multi-agent facilitation.

## Architecture

Two-tier app: Angular 21 frontend + ASP.NET 10 backend, communicating via REST and SignalR.

```
src/eventstormingboard.client/    # Angular 21, Material 21, HTML Canvas
src/EventStormingBoard.Server/    # ASP.NET 10, SignalR, Microsoft Agent Framework
tests/EventStormingBoard.Server.Tests/  # xUnit, hand-rolled test doubles
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

- **Method naming**: `Method_Scenario_Expected` (e.g., `ApplyAndLog_BoardNameUpdated_AppliesStateAndAppendsLog`)
- **Test doubles**: Hand-rolled spy/stub classes (no Moq/NSubstitute) — e.g., `SpyBoardStateService`
- **Manual DI**: Direct construction, no DI container in tests
- **No frontend tests** currently exist

## Conventions

- **Event pipeline**: All board mutations must go through `IBoardEventPipeline.ApplyAndLog()` — never mutate state directly
- **Agent tool policies**: Facilitator has full toolset, specialists are read-only, WallScribe handles mutations. `DeleteNotes` restricted to manual (non-autonomous) mode. Policies are tested in `AgentServiceToolPolicyTests`
- **SignalR hub methods**: Match event names (e.g., `NoteCreated`, `BoardNameUpdated`). Hub calls `RecordUserActivity()` on every user action for autonomous debouncing
- **Board phases**: SetContext → IdentifyEvents → AddCommandsAndPolicies → DefineAggregates → BreakItDown
- **Agent configuration**: Per-board via `AgentConfiguration` entities — models, prompts, active phases all configurable through the UI
