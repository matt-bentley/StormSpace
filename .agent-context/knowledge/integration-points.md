# Integration Points

StormSpace integrates with Azure OpenAI for AI agent facilitation, Microsoft Entra ID for optional authentication, and uses SignalR for real-time client-server communication. All state is intentionally in-memory — boards are exported/imported as JSON files stored alongside code.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Angular Frontend                        │
│   MSAL Browser ←→ Entra ID     SignalR Client ←→ Hub       │
│   HTTP Services ←→ REST API    Board Export/Import (JSON)   │
└──────────┬────────────────────────────┬─────────────────────┘
           │ REST (/api/*)              │ WebSocket (/hub)
           │ + JWT Bearer               │ + JWT via query string
┌──────────▼────────────────────────────▼─────────────────────┐
│                    ASP.NET Backend                           │
│   Controllers ──→ BoardEventPipeline ──→ In-Memory State    │
│   BoardsHub   ──→ BoardEventPipeline ──→ SignalR Broadcast  │
│   AgentService ──→ Azure OpenAI ──→ Tool Execution ──→      │
│                    BoardEventPipeline ──→ SignalR Broadcast  │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTPS
                ┌──────────▼──────────┐
                │   Azure OpenAI      │
                │   (GPT-4.1 / 5.2)  │
                └─────────────────────┘
```

## Integration Summary

| Integration | Protocol | Direction | Auth | Required |
|-------------|----------|-----------|------|----------|
| SignalR | WebSocket | Bidirectional | JWT via query string | Yes |
| REST API | HTTP/HTTPS | Request-Response | JWT Bearer (optional) | Yes |
| Azure OpenAI | HTTPS | Outbound | API Key or DefaultAzureCredential | Yes (for AI features) |
| Entra ID | HTTPS (OIDC) | Outbound | PKCE auth code flow | Optional |

## SignalR (Real-Time Communication)

The primary communication channel between frontend and backend. All board mutations and AI agent interactions flow through SignalR.

### Key Files

| File | Purpose |
|------|---------|
| `src/EventStormingBoard.Server/Hubs/BoardsHub.cs` | Server hub — receives client events, applies via pipeline, broadcasts |
| `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` | Client connection, event listeners, and emitters |
| `src/EventStormingBoard.Server/Services/BoardPresenceService.cs` | Tracks connected users per board |

### Hub Methods (Client → Server)

Board mutations — each calls `RecordUserActivity()` then `IBoardEventPipeline.ApplyAndLog()` then broadcasts to group:

| Method | Purpose |
|--------|---------|
| `JoinBoard` / `LeaveBoard` | Presence management, adds/removes from SignalR group |
| `BroadcastNoteCreated` | Create sticky note |
| `BroadcastNotesMoved` | Reposition notes |
| `BroadcastNoteResized` | Resize a note |
| `BroadcastNotesDeleted` | Delete notes (cascades connections) |
| `BroadcastNoteTextEdited` | Edit note text |
| `BroadcastConnectionCreated` | Create arrow between notes |
| `BroadcastPasted` | Paste notes from clipboard |
| `BroadcastBoardNameUpdated` / `BroadcastBoardContextUpdated` | Board metadata changes |
| `BroadcastBoundedContextCreated` / `Updated` / `Deleted` | Bounded context frames |
| `BroadcastCursorPositionUpdated` | Live cursor tracking |
| `SendAgentMessage` | User message to AI facilitator |
| `GetAgentHistory` / `ClearAgentHistory` | Agent chat history management |

### Server → Client Events

| Event | Purpose |
|-------|---------|
| `ConnectedUsers` | Full user list on join |
| `UserJoinedBoard` / `UserLeftBoard` | Presence updates |
| `NoteCreated`, `NotesMoved`, `NotesDeleted`, etc. | Board state mutations |
| `AgentUserMessage` / `AgentResponse` | Chat messages in/out |
| `AgentStepUpdate` | Streaming agent reasoning steps |
| `AgentToolCallStarted` | Tool execution with name and arguments |
| `AgentChatComplete` | End-of-response signal |
| `AutonomousFacilitatorStatusChanged` | Autonomous mode state |
| `AgentChatHistory` | Historical messages replay |
| `AgentConfigurationsUpdated` | Agent config changes |

### Connection Pattern

- Frontend creates `HubConnectionBuilder` targeting `/hub`
- JWT tokens passed via query string during WebSocket negotiation (`/hub?access_token=...`)
- Backend extracts token in `JwtBearerEvents.OnMessageReceived` in `Program.cs`
- Users added to board-specific SignalR groups on `JoinBoard`

### Data Flow

1. User action in canvas → hub method call
2. Hub validates membership, calls `RecordUserActivity()`
3. `IBoardEventPipeline.ApplyAndLog()` applies event to state and logs it
4. Hub broadcasts event to all group members via `Clients.Group(boardId)`
5. Other clients receive event, update local canvas

## REST API

Secondary communication channel for CRUD operations that don't need real-time broadcast.

### Key Files

| File | Purpose |
|------|---------|
| `src/EventStormingBoard.Server/Controllers/BoardsController.cs` | Board and agent configuration endpoints |
| `src/EventStormingBoard.Server/Controllers/AuthController.cs` | Auth configuration endpoint |
| `src/eventstormingboard.client/src/app/_shared/services/boards.service.ts` | Frontend HTTP client |

### Endpoints

**Board Management** (`/api/boards`):

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/boards` | List all board summaries |
| `POST` | `/api/boards` | Create new board |
| `GET` | `/api/boards/{id}` | Get full board state |
| `DELETE` | `/api/boards/{id}` | Delete board and clear singletons |
| `GET` | `/api/boards/{id}/events` | Recent board events (accepts `count` query param) |

**Agent Configuration** (`/api/boards/{boardId}/agents`):

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/boards/{boardId}/agents` | List agent configs |
| `POST` | `/api/boards/{boardId}/agents` | Add agent config |
| `PUT` | `/api/boards/{boardId}/agents/{agentId}` | Update agent config |
| `DELETE` | `/api/boards/{boardId}/agents/{agentId}` | Delete agent (Facilitator protected) |
| `GET` | `/api/boards/{boardId}/agents/available-tools` | List all available tool definitions |

**Auth** (`/api/auth`):

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/auth/config` | Returns `AuthConfigDto` (enabled, clientId, tenantId, instance, scopes). AllowAnonymous. |

### Swagger

In development mode, Swagger UI is available at `/swagger` and the OpenAPI spec at `/swagger/v1/swagger.json`.

## Azure OpenAI

Agents use Azure OpenAI for chat completions. The integration supports two model tiers with different capabilities.

### Key Files

| File | Purpose |
|------|---------|
| `src/EventStormingBoard.Server/Agents/BoardAgentFactory.cs` | Creates AI agents, resolves model deployments, builds chat clients |
| `src/EventStormingBoard.Server/Models/AzureOpenAIOptions.cs` | Configuration model (`Endpoint`, `Gpt41DeploymentName`, `Gpt52DeploymentName`, `ApiKey`) |
| `src/EventStormingBoard.Server/Services/AgentService.cs` | Orchestrates agent turns, maintains conversation history |
| `src/EventStormingBoard.Server/Filters/AgentToolCallFilter.cs` | Middleware that logs tool calls and broadcasts them via SignalR |

### Configuration

In `appsettings.Development.json` (section `AzureOpenAI`):

| Setting | Purpose |
|---------|---------|
| `Endpoint` | Azure OpenAI resource URL |
| `Gpt41DeploymentName` | Deployment name for GPT-4.1 |
| `Gpt52DeploymentName` | Deployment name for GPT-5.2 (reasoning model) |
| `ApiKey` | Optional — falls back to `DefaultAzureCredential` if absent |

### Authentication

1. **API Key** — `AzureKeyCredential(apiKey)` when `ApiKey` is set in config
2. **Managed Identity** — `DefaultAzureCredential()` when `ApiKey` is absent (uses Azure SDK auth chain)

### Model Resolution

Each `AgentConfiguration` specifies a model name. `BoardAgentFactory` resolves it:

| Model Name | Deployment | Special Behaviour |
|------------|------------|-------------------|
| `gpt-4.1` | `Gpt41DeploymentName` | Standard chat completion with temperature |
| `gpt-5.2` | `Gpt52DeploymentName` | Reasoning mode enabled, uses `ReasoningEffort` instead of temperature |

### Agent Execution Flow

1. User sends message via `SendAgentMessage` hub method
2. `AgentService.ChatAsync()` invokes `FacilitatorGroupChat`
3. Facilitator agent calls Azure OpenAI, receives completion
4. If tool calls are needed, `AgentToolCallFilter` logs and broadcasts each call
5. Tool results fed back into conversation, model generates next response
6. Steps streamed to clients via `AgentStepUpdate` SignalR events
7. `AgentChatComplete` signals end of turn

### Autonomous Facilitation

| File | Purpose |
|------|---------|
| `src/EventStormingBoard.Server/Services/AutonomousFacilitatorCoordinator.cs` | Evaluates trigger conditions and rate limits |
| `src/EventStormingBoard.Server/Services/AutonomousFacilitatorWorker.cs` | Background worker polling every 10 seconds |

**Trigger conditions** — autonomous runs execute when:

- User activity has been idle for **20 seconds** (activity debounce)
- At least **45 seconds** since last run (cooldown)
- Max **4 turns** per **10-minute** window (rate limit)
- Periodic timer fires every **3 minutes** if no recent activity

Autonomous runs use `allowDestructiveChanges=false`, preventing `DeleteNotes` and other destructive tools.

## Entra ID Authentication (Optional)

Authentication is optional. When the `EntraId` configuration section is present and complete, JWT Bearer authentication is enabled. When absent, all endpoints are anonymous.

### Key Files

| File | Purpose |
|------|---------|
| `src/EventStormingBoard.Server/Program.cs` | Backend auth setup — validates config, registers JWT Bearer |
| `src/eventstormingboard.client/src/main.ts` | Pre-bootstrap auth config fetch _before_ Angular initializes |
| `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts` | Token acquisition (silent + interactive fallback), user info |
| `src/eventstormingboard.client/src/app/app.config.ts` | MSAL interceptor for `/api/*` and `/hub/*`, route guards |

### Configuration

In `appsettings.Development.json` (section `EntraId`):

| Setting | Purpose |
|---------|---------|
| `ClientId` | App registration client ID |
| `TenantId` | Entra ID tenant |
| `Scopes` | API scopes (e.g., `api://{clientId}/access_as_user`) |

**Fail-fast behaviour**: If the `EntraId` section is partially configured (e.g., `ClientId` present but `TenantId` missing), the backend throws a detailed error at startup. This prevents silent auth misconfiguration.

### Frontend Auth Flow

1. Before Angular bootstrap, `main.ts` fetches `/api/auth/config` (5-second timeout)
2. If auth enabled, MSAL `PublicClientApplication` is initialized
3. Redirect handled _before_ Angular bootstrap (prevents MsalGuard double-redirect)
4. Cache strategy: `BrowserCacheLocation.SessionStorage`
5. MSAL interceptor attaches Bearer tokens to `/api/*` and `/hub/*` requests
6. `auth.service.ts` acquires tokens silently, falls back to interactive redirect

### SignalR Token Handling

SignalR WebSocket connections cannot use standard Authorization headers. The token is passed via query string (`/hub?access_token=...`) and extracted server-side in `JwtBearerEvents.OnMessageReceived`.

## Board Export/Import (Persistence Strategy)

Boards are intentionally in-memory with no external database. Users export boards as JSON files stored alongside their code, then import them when needed.

### Key File

| File | Purpose |
|------|---------|
| `src/eventstormingboard.client/src/app/board/board.component.ts` | `exportBoardAsJSON()` and `importBoardFromJSON()` methods |

### Export Format

The JSON export includes: board name, domain, session scope, phase, autonomous settings, notes, connections, bounded contexts, and agent configurations.

### In-Memory Storage

| Store | Implementation | Purpose |
|-------|---------------|---------|
| Board state | `IBoardsRepository` (`ConcurrentDictionary<Guid, Board>`) | Notes, connections, bounded contexts |
| Conversation history | `AgentService._conversationHistories` (`ConcurrentDictionary<Guid, List<ChatMessage>>`) | Agent chat per board (max 200 messages, FIFO trimmed) |
| User presence | `IBoardPresenceService` (`ConcurrentDictionary`) | Active connections per board |
| Autonomous state | `AutonomousFacilitatorCoordinator` | Debounce timers, run history, cooldowns |

## Docker Deployment

Single-container deployment with integrated SPA and API.

### Key File

| File | Purpose |
|------|---------|
| `Dockerfile` | Multi-stage build: .NET SDK + Node 20 → build → aspnet runtime |

### Build Stages

1. **Build stage** — .NET SDK 10 + Node 20 via APT, `npm install` + `npm build` for Angular, `dotnet publish` for backend
2. **Runtime stage** — `mcr.microsoft.com/dotnet/aspnet:10.0`, port 8080, non-root user (`1000:1000`)

### Runtime

- Angular static files served from `/app/wwwroot`
- .NET backend serves API and SignalR hub
- Single port: `8080`
- Run: `docker run -it -p 8080:8080 --rm mabentley/stormspace`

## Dev Environment

### Frontend Dev Proxy

| File | Purpose |
|------|---------|
| `src/eventstormingboard.client/src/proxy.conf.js` | Proxies `/api` and `/hub` to backend |

- Frontend dev server: port **51710** (Angular CLI)
- Backend: reads `ASPNETCORE_HTTPS_PORT` or `ASPNETCORE_URLS` environment variables
- WebSocket proxying enabled (`ws: true`) for SignalR

## Key Dependencies

### Backend (NuGet)

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.AI.Workflows` (1.0.0-rc4) | Agent orchestration (group chat, delegation) |
| `Microsoft.Agents.AI` (1.0.0-rc4) | AI agent core |
| `Microsoft.Agents.AI.OpenAI` (1.0.0-rc4) | OpenAI integration for agents |
| `Azure.AI.OpenAI` | Azure OpenAI chat completion client |
| `Azure.Identity` | `DefaultAzureCredential` for managed identity |
| `Microsoft.Identity.Web` | Entra ID JWT Bearer authentication |
| `Microsoft.AspNetCore.SpaProxy` | SPA dev proxy |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI (dev only) |

### Frontend (npm)

| Package | Purpose |
|---------|---------|
| `@microsoft/signalr` | SignalR client |
| `@azure/msal-angular` + `@azure/msal-browser` | MSAL auth |
| `@angular/material` | Material 21 UI |
| `marked` + `dompurify` | Markdown rendering with sanitization for agent responses |
| `uuid` | Client-side GUID generation |
