# Domain Model

StormSpace models collaborative Event Storming workshops as a digital whiteboard. The domain centres on a **Board** aggregate containing sticky notes, connections between them, and bounded context frames — all manipulated by human users and AI agents in real time.

## Core Entities

### Board (Aggregate Root)

The central aggregate. All other entities exist within a Board's context.

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Display name |
| `Domain` | `string?` | Business domain being modelled (e.g., "eCommerce") |
| `SessionScope` | `string?` | Workshop scope constraint set by facilitator |
| `Phase` | `EventStormingPhase?` | Current workshop phase |
| `AutonomousEnabled` | `bool` | Whether AI agents act autonomously |
| `Notes` | `List<Note>` | All sticky notes on the board |
| `Connections` | `List<Connection>` | Directed links between notes |
| `BoundedContexts` | `List<BoundedContext>` | DDD bounded context frames |
| `AgentConfigurations` | `List<AgentConfiguration>` | Per-board AI agent definitions |

**File:** `src/EventStormingBoard.Server/Entities/Board.cs`

### Note

A sticky note on the canvas representing an Event Storming concept.

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Unique identifier |
| `Text` | `string?` | Display text |
| `X`, `Y` | `double` | Canvas position |
| `Width`, `Height` | `double` | Dimensions (defaults vary by type) |
| `Color` | `string?` | Hex colour override |
| `Type` | `NoteType` | Semantic type — determines colour and placement rules |

**File:** `src/EventStormingBoard.Server/Entities/Note.cs`

### Connection

A directed edge between two notes, representing causal flow between clusters.

| Property | Type | Purpose |
|----------|------|---------|
| `FromNoteId` | `Guid` | Source note |
| `ToNoteId` | `Guid` | Target note |

Connections link **outer notes of one cluster to the Command of the next cluster**. They are never used within a single cluster.

**File:** `src/EventStormingBoard.Server/Entities/Connection.cs`

### BoundedContext

A rectangular frame grouping related notes into a DDD bounded context (used in the BreakItDown phase).

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string?` | Context name |
| `X`, `Y`, `Width`, `Height` | `double` | Frame dimensions |
| `Color` | `string?` | Frame colour |

**File:** `src/EventStormingBoard.Server/Entities/BoundedContext.cs`

### AgentConfiguration

Per-board configuration for an AI agent. Each board gets a default set on creation (via `DefaultAgentConfigurations.CreateDefaults()`), and users can customise agents through the UI.

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Agent display name (e.g., "EventExplorer") |
| `IsFacilitator` | `bool` | Marks the orchestrating Facilitator agent (one per board, cannot be deleted) |
| `SystemPrompt` | `string` | LLM system instruction |
| `ActivePhases` | `List<EventStormingPhase>?` | Phases where this agent can be invoked (null = all phases) |
| `AllowedTools` | `List<string>` | Tool names this agent may call |
| `CanAskAgents` | `List<string>?` | Other agents this one can consult via `AskAgentQuestion` |
| `ModelType` | `string` | LLM model (e.g., "gpt-4.1", "gpt-5.2") |
| `Temperature` | `float?` | Sampling temperature (standard models only) |
| `ReasoningEffort` | `string?` | Reasoning effort level (reasoning models only) |
| `Order` | `int` | Display order in UI |
| `Icon` | `string` | Material icon name |
| `Color` | `string` | Hex colour for UI |

**File:** `src/EventStormingBoard.Server/Entities/AgentConfiguration.cs`

## Enums

### NoteType

Determines the semantic role, default colour, and placement rules for a sticky note.

| Value | Role | Default Size | Placement Rule |
|-------|------|-------------|----------------|
| `Event` | Something that happened (past tense) | 120×120 | Chronological left-to-right, 600px apart |
| `Command` | Action/intent triggering an Event | 120×120 | 140px left of its Event |
| `Aggregate` | Cluster of domain objects | 120×120 | Above Command/Event midpoint, 130px up |
| `User` | Actor triggering a Command or manual Policy | 60×60 | Bottom-left of parent (x−30, y+80) |
| `Policy` | Business rule reacting to an Event | 120×120 | 140px right of its Event; stacks vertically at 135px gap |
| `ReadModel` | Data view for UI | 120×120 | Left of the Command |
| `ExternalSystem` | Outside dependency | 120×120 | 130px below the Command/Event pair |
| `Concern` | Problem, risk, or question | Varies¹ | Near related note, typically 275px above |

¹ Concern dimensions scale with text length: ≤50 chars → 120×120, 50–100 → 160×160, >100 → 200×200.

**Files:** `src/EventStormingBoard.Server/Models/NoteType.cs`, `src/eventstormingboard.client/src/app/_shared/models/note.model.ts`

### EventStormingPhase

Workshop phases progress in order. Each phase activates different specialist agents.

| Value | Purpose | Active Specialists |
|-------|---------|-------------------|
| `SetContext` | Define domain and scope | Facilitator only |
| `IdentifyEvents` | Brainstorm and order Events | EventExplorer, Organiser |
| `AddCommandsAndPolicies` | Add triggers around Events | TriggerMapper, Organiser |
| `DefineAggregates` | Place Aggregates above clusters | DomainDesigner, Organiser |
| `BreakItDown` | Group into Bounded Contexts | DomainDesigner, Organiser |

The DomainExpert agent is active in all phases (read-only domain Q&A).

**Files:** `src/EventStormingBoard.Server/Models/EventStormingPhase.cs`, `src/eventstormingboard.client/src/app/_shared/models/board.model.ts`

## Event Storming Cluster Patterns

Notes group into **clusters**, each centred on one Event. The cluster pattern determines which notes surround the Event:

| Pattern | Structure | Trigger |
|---------|-----------|---------|
| Manual Command | User → Command → Event → [Policy/ies] | Person triggers the command |
| Automated Command | Command → Event → [Policy/ies] | Policy in a preceding cluster (via Connection) |
| ExternalSystem-triggered | ExternalSystem → Command → Event → [Policy/ies] | External system initiates |
| Manual Policy | Policy + User | Standalone decision point, no Command/Event |

Connections always link an outer note of one cluster to the Command of the next cluster — never between notes within the same cluster.

## Event System

All state changes are modelled as immutable events inheriting from `BoardEvent` (base: `BoardId`, `IsUndo`). The `IsUndo` flag enables bidirectional application for undo/redo support.

### Board Events

| Event | Payload Summary |
|-------|----------------|
| `NoteCreatedEvent` | Full `NoteDto` |
| `NoteTextEditedEvent` | `NoteId`, `ToText`, `FromText` |
| `NotesMovedEvent` | `From`/`To` coordinate lists |
| `NoteResizedEvent` | `NoteId`, `From`/`To` size |
| `NotesDeletedEvent` | Deleted notes + orphaned connections |
| `ConnectionCreatedEvent` | `FromNoteId`, `ToNoteId` |
| `PastedEvent` | Lists of notes + connections |
| `BoardNameUpdatedEvent` | `NewName`, `OldName` |
| `BoardContextUpdatedEvent` | Domain, scope, phase, autonomous (old + new) |
| `BoundedContextCreatedEvent` | Full `BoundedContextDto` |
| `BoundedContextUpdatedEvent` | Partial: changed fields only (old + new) |
| `BoundedContextDeletedEvent` | Full `BoundedContextDto` |

### Transient Events (not persisted to state)

| Event | Purpose |
|-------|---------|
| `CursorPositionUpdatedEvent` | Real-time cursor position broadcast |
| `UserJoinedBoardEvent` | Presence tracking |
| `UserLeftBoardEvent` | Presence tracking |

**Files:** `src/EventStormingBoard.Server/Events/`, `src/eventstormingboard.client/src/app/_shared/models/board-events.model.ts`

## Frontend Command Pattern

The Angular frontend uses a **Command pattern** for all canvas mutations, enabling undo/redo.

Each command implements `execute()` and `undo()` on `BoardState`. The `BoardCanvasService` maintains an undo/redo stack and broadcasts executed commands to the SignalR hub.

| Command | Purpose |
|---------|---------|
| `CreateNoteCommand` | Add note |
| `EditNoteTextCommand` | Change note text |
| `DeleteNotesCommand` | Remove notes + connections |
| `MoveNotesCommand` | Reposition notes |
| `ResizeNoteCommand` | Resize note |
| `CreateConnectionCommand` | Add directed connection |
| `PasteCommand` | Paste copied entities |
| `CreateBoundedContextCommand` | Add bounded context frame |
| `UpdateBoundedContextCommand` | Edit frame properties |
| `DeleteBoundedContextCommand` | Remove frame |
| `MoveBoundedContextCommand` | Reposition frame |
| `ResizeBoundedContextCommand` | Resize frame |
| `UpdateBoardNameCommand` | Rename board |
| `UpdateBoardContextCommand` | Change domain/phase/scope/autonomous |

**Files:** `src/eventstormingboard.client/src/app/board/board.commands.ts`, `src/eventstormingboard.client/src/app/_shared/models/command.model.ts`

## Service Layer

### Backend Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `BoardEventPipeline` | `IBoardEventPipeline` | Central mutation dispatcher — `ApplyAndLog()` routes events to state + log |
| `BoardStateService` | `IBoardStateService` | Applies events to `Board` entities with per-board `SemaphoreSlim` locks |
| `BoardEventLog` | `IBoardEventLog` | In-memory sliding window (max 50 entries per board) of human-readable event summaries |
| `BoardPresenceService` | `IBoardPresenceService` | Tracks connected users per board (`ConcurrentDictionary`) |
| `BoardsRepository` | `IBoardsRepository` | In-memory board storage (`IMemoryCache`, 1-hour sliding expiration) |
| `AgentService` | `IAgentService` | Orchestrates AI chat — invokes `FacilitatorGroupChat`, manages per-board conversation history (200 msg cap) |
| `AutonomousFacilitatorCoordinator` | `IAutonomousFacilitatorCoordinator` | Autonomous mode state machine — debounce (20s), cooldown (45s), periodic (3min), rate limit (4/10min), failure limit (3) |
| `AutonomousFacilitatorWorker` | `BackgroundService` | Polls every 10s for enabled boards; orchestrates autonomous agent turns |

**Files:** `src/EventStormingBoard.Server/Services/`

### Frontend Services

| Service | Purpose |
|---------|---------|
| `BoardsSignalRService` | SignalR client — emits RxJS Subjects for all board events; manages connection lifecycle |
| `BoardsService` | REST HTTP client for board CRUD and agent configuration management |
| `BoardCanvasService` | Canvas state, command execution engine, undo/redo stack, zoom/pan |
| `AuthService` | Optional Azure MSAL authentication |
| `UserService` | User display name management |
| `ThemeService` | Dark/light theme toggle (persisted to localStorage) |

**Files:** `src/eventstormingboard.client/src/app/_shared/services/`

## AI Agent Tool System

Agent tools are exposed via two plugins, with access controlled per-agent by `AllowedTools` in `AgentConfiguration`.

### BoardPlugin Tools

| Tool | Category | Purpose |
|------|----------|---------|
| `GetBoardState` | Read | Formatted board state dump |
| `GetRecentEvents` | Read | Recent event log entries |
| `SetDomain` | Config | Set board domain |
| `SetSessionScope` | Config | Set session scope |
| `SetPhase` | Config | Transition workshop phase |
| `CompleteAutonomousSession` | Config | Disable autonomous mode with summary |
| `CreateNote` / `CreateNotes` | Mutate | Create sticky notes |
| `EditNoteTexts` | Mutate | Batch-edit note text |
| `MoveNotes` | Mutate | Batch-reposition notes |
| `DeleteNotes` | Mutate | Delete notes + orphaned connections |
| `CreateConnection` / `CreateConnections` | Mutate | Create directed links |
| `CreateBoundedContext` / `CreateBoundedContexts` | Mutate | Create frames |
| `UpdateBoundedContext` | Mutate | Edit frame properties |
| `DeleteBoundedContext` | Mutate | Remove frame |

### DelegationPlugin Tools (Facilitator only)

| Tool | Purpose |
|------|---------|
| `DelegateToAgent` | Route task to a specialist with instructions |
| `RequestBoardReview` | Non-blocking advisory review (read-only) |
| `AskAgentQuestion` | Domain Q&A without board changes |

**Files:** `src/EventStormingBoard.Server/Agents/Plugins/BoardPlugin.cs`, `src/EventStormingBoard.Server/Agents/Plugins/DelegationPlugin.cs`

## Data Flow Patterns

### User action → Board state

1. User interacts with canvas
2. `BoardCanvasService.executeCommand()` applies `Command<BoardState>` locally
3. Command pushed to undo stack
4. `BoardsSignalRService.broadcast*()` sends event to `BoardsHub`
5. Hub calls `BoardEventPipeline.ApplyAndLog(event)` → `BoardStateService` + `BoardEventLog`
6. Hub broadcasts event to other clients in the board group

### AI agent action → Board state

1. Chat message or autonomous trigger → `AgentService`
2. `FacilitatorGroupChat.RunAsync()` orchestrates facilitator + delegated specialists
3. Specialist calls tool (e.g., `CreateNotes`) on `BoardPlugin`
4. `BoardPlugin` creates event → `BoardEventPipeline.ApplyAndLog()` → broadcast via SignalR hub
5. All clients (including the originator) receive the event and update local state

### Undo/Redo

Events carry old + new values (e.g., `FromText`/`ToText`, `From`/`To` coordinates). The `IsUndo` flag on `BoardEvent` tells `BoardStateService` to apply the reverse direction. On the frontend, `Command.undo()` reverses the local state and broadcasts the event with `isUndo: true`.

## Adding a New Entity Type

Checklist for adding a new entity (e.g., a "Swimlane"):

- [ ] Create entity class in `src/EventStormingBoard.Server/Entities/`
- [ ] Add collection property to `Board` entity
- [ ] Create DTO in `src/EventStormingBoard.Server/Models/`
- [ ] Create event(s) in `src/EventStormingBoard.Server/Events/` (inheriting `BoardEvent`)
- [ ] Add `Apply*` method(s) to `IBoardStateService` / `BoardStateService`
- [ ] Add case(s) to `BoardEventPipeline` switch dispatch
- [ ] Add event summary to `BoardEventLog`
- [ ] Add hub method(s) to `BoardsHub` (broadcast pattern: `OthersInGroup`)
- [ ] Add tool method(s) to `BoardPlugin` if AI agents should interact with it
- [ ] Create frontend model in `src/eventstormingboard.client/src/app/_shared/models/`
- [ ] Create frontend event interface in `board-events.model.ts`
- [ ] Create `Command<BoardState>` subclass in `board.commands.ts`
- [ ] Add SignalR listener + Subject in `BoardsSignalRService`
- [ ] Add canvas rendering logic in `BoardCanvasService` / canvas component
- [ ] Add to `BoardDto` / `BoardState` serialisation
- [ ] Write backend tests (Given/When/Then naming, AwesomeAssertions)
