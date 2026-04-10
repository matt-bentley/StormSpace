# Application Context

StormSpace is an open-source collaborative Event Storming web application with AI-powered multi-agent facilitation. It provides a structured digital whiteboard for Domain-Driven Design workshops, where teams brainstorm domain events, commands, aggregates, and policies. The AI agents guide users through Event Storming phases and can autonomously populate the board.

## Problem Space

**Event Storming** is a workshop format for rapidly exploring complex business domains. Teams place coloured sticky notes on a large surface to model domain events, commands, aggregates, policies, and external systems. Traditional approaches use physical walls or generic tools (Miro, FigJam) that lack domain awareness.

StormSpace solves three problems:
1. **Structure** — Enforces Event Storming note types, colours, and spatial conventions that generic whiteboards don't understand
2. **AI Facilitation** — A team of AI agents guides the workshop, proposes domain events, and populates the board — lowering the barrier for teams new to Event Storming
3. **Portability** — Boards export as structured JSON, enabling downstream use in AI-driven code generation and domain modelling tools

## Target Users

| Persona | Use Case |
|---------|----------|
| Software development teams | DDD workshops to model a domain before implementation |
| Workshop facilitators / consultants | Running structured Event Storming sessions with clients |
| Students / learners | Exploring Event Storming methodology with AI guidance |

## Key Differentiators

- **Domain-aware canvas** — Note types, colours, and layout rules are Event Storming primitives, not generic shapes
- **AI agent team** — Multi-agent system (Facilitator, EventExplorer, TriggerMapper, DomainDesigner, Organiser, DomainExpert) that actively participates in the workshop
- **Autonomous mode** — Agents observe user activity and proactively suggest events, commands, and clusters without manual prompting
- **Structured export** — JSON export captures full domain model (notes, connections, bounded contexts), usable as input for AI-assisted coding workflows
- **Fully configurable agents** — Models, prompts, tools, active phases, and inter-agent communication are all editable per board through the UI

## Product Characteristics

| Aspect | Detail |
|--------|--------|
| License | Open source |
| Deployment | Flexible — Docker, self-hosted, or cloud |
| Persistence | Ephemeral by design — in-memory with 60-min sliding expiry. Export/import JSON for saving |
| Scale target | Small teams (2–8 users per board) |
| Auth | Optional — Entra ID (Azure AD) or fully open with self-identified display names |

## Feature Overview

### Board Management

Users create and manage boards from a splash/lobby page. Each board has a name, optional domain context, and a current Event Storming phase.

| Feature | Description |
|---------|-------------|
| Create / Delete boards | Via splash page and REST API |
| Board list | Shows all active boards with names |
| Domain context | Free-text description of the business domain being modelled |
| Phase progression | SetContext → IdentifyEvents → AddCommandsAndPolicies → DefineAggregates → BreakItDown |

Key paths: `src/eventstormingboard.client/src/app/splash/`, `src/EventStormingBoard.Server/Controllers/BoardsController.cs`

### Canvas & Note Editing

The board is an HTML Canvas with imperative drawing. Users create, move, resize, and edit coloured sticky notes representing Event Storming primitives.

| Feature | Description |
|---------|-------------|
| 8 note types | Event, Command, Aggregate, User, Policy, ReadModel, ExternalSystem, Concern — each with a fixed colour |
| Connections | Alt+click to draw arrows between notes (linking clusters) |
| Bounded Contexts | Resizable coloured rectangles that group related notes |
| Multi-select | Drag-select multiple notes and connections |
| Copy / Paste | Ctrl+C/V with new GUIDs and remapped connections |
| Undo / Redo | Command pattern on client, `IsUndo` flag on server events |
| Export to image | Renders canvas to PNG |
| Export / Import JSON | Full board state serialisation |

Key paths: `src/eventstormingboard.client/src/app/board/board-canvas/`, `src/eventstormingboard.client/src/app/_shared/models/note.model.ts`

### Real-Time Collaboration

All board mutations flow through a SignalR hub, broadcasting changes to every connected client in real time.

| Feature | Description |
|---------|-------------|
| Live sync | Note creation, movement, editing, deletion broadcast instantly |
| Cursor tracking | Remote user cursors rendered on canvas with username labels |
| User presence | Join/leave events tracked per board |
| Activity debouncing | `RecordUserActivity()` on every user action for autonomous agent debouncing |

Key paths: `src/EventStormingBoard.Server/Hubs/BoardsHub.cs`, `src/EventStormingBoard.Server/Services/BoardPresenceService.cs`, `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`

### AI Agent System

A multi-agent team built on the Microsoft Agent Framework and Azure OpenAI. The Facilitator orchestrates the session, delegating to phase-appropriate specialists who create and organise notes on the board.

| Feature | Description |
|---------|-------------|
| Chat | Users converse with the Facilitator for guidance and requests |
| Autonomous mode | Agents observe activity and proactively populate the board |
| Agent configuration | Per-board UI to edit models, prompts, tools, phases, icons, colours |
| Interaction diagram | Visual graph of agent communication flows |
| 3 protocols | Delegate (board changes), Review (advisory), Ask (domain Q&A) |

Key paths: `src/eventstormingboard.client/src/app/board/ai-chat-panel/`, `src/eventstormingboard.client/src/app/board/agent-config-modal/`, `src/EventStormingBoard.Server/Agents/`

### Authentication

Optional Entra ID integration. When disabled (default), the app is fully open with self-identified usernames. When enabled, MSAL handles login redirects and JWT tokens secure both REST and SignalR endpoints.

Key paths: `src/EventStormingBoard.Server/Controllers/AuthController.cs`, `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts`, `src/eventstormingboard.client/src/main.ts`

## UI Structure

```
Splash (board list)
 ├── Create Board Modal
 └── Select Board Modal

Board (main workspace)
 ├── Board Canvas (HTML Canvas — notes, connections, bounded contexts, cursors)
 ├── AI Chat Panel (agent conversation sidebar)
 ├── Board Context Modal (domain description, phase selection)
 ├── Agent Config Modal (per-agent settings)
 │    └── Agent Interaction Diagram
 └── Keyboard Shortcuts Modal
```