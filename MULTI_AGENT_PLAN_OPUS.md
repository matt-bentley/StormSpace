# Plan: Multi-Agent Decomposition for StormSpace

Break the monolithic `AgentService` (single agent with ~1200-line prompt and 13 tools) into **3 focused agents** — **Facilitator**, **Board Builder**, **Board Reviewer** — using a **sequential pipeline orchestrator**. Each agent gets a focused prompt, scoped tools, and a distinct visual identity (name, icon, color). The Facilitator signals delegation intent via lightweight tools; after it responds, the orchestrator automatically invokes the right specialist and broadcasts each response as a separate chat message.

---

## Phase 1: Backend — Agent Definitions & Prompt Split

### Step 1: Create agent identity model
- Add `AgentType` enum: `Facilitator`, `BoardBuilder`, `BoardReviewer`
- Add `AgentIdentity` static helper mapping type → name/icon/color:
  - Facilitator → "Facilitator", `school`, Indigo (#3f51b5)
  - Board Builder → "Board Builder", `construction`, Teal (#009688)
  - Board Reviewer → "Board Reviewer", `checklist`, Amber (#ff8f00)
- Add `AgentName` property to `AgentChatMessageDto`

### Step 2: Split the ~1200-line `BaseSystemPrompt` into 3 focused prompts
- **FacilitatorSystemPrompt** (~600 lines): Event Storming methodology, phases, facilitation style ("Do Less, Teach More"), all 22 behavioral rules. Plus delegation instructions: *"When you want board changes, call `RequestBoardChanges`. When you want a review, call `RequestBoardReview`."*
- **BoardBuilderSystemPrompt** (~300 lines): Note types with **sizes** (120×120, 60×60), **positioning guidelines** (30px gap, 160px between clusters, 500px rows), valid flow patterns, batch operation preference. *"Execute instructions from the Facilitator precisely."*
- **BoardReviewerSystemPrompt** (~200 lines): Flow rule validation, naming conventions, quality criteria. *"Review the board, add Concern notes where needed, summarize findings."*

### Step 3: Create `DelegationPlugin`
- `RequestBoardChanges(string instructions)` — records intent, returns "Board changes will be applied next."
- `RequestBoardReview(string? focusArea)` — records intent, returns "Board review will be performed next."
- Uses a `ConcurrentBag<DelegationIntent>` pattern (like `AgentToolCallFilter` captures tool calls)

### Step 4: Assign tools per agent type

| Tool | Facilitator | Builder | Reviewer |
|------|:-----------:|:-------:|:--------:|
| GetBoardState | ✓ | ✓ | ✓ |
| GetRecentEvents | ✓ | | ✓ |
| SetDomain / SetSessionScope / SetPhase | ✓ | | |
| CompleteAutonomousSession | ✓ | | |
| RequestBoardChanges / RequestBoardReview | ✓ | | |
| CreateNote / CreateNotes | | ✓ | ✓ (Concerns) |
| CreateConnection / CreateConnections | | ✓ | |
| EditNoteText / MoveNotes | | ✓ | |
| DeleteNotes | | ✓ (interactive) | |

---

## Phase 2: Backend — Orchestrator Pipeline

### Step 5: Refactor `AgentService` into sequential orchestrator *(depends on steps 1–4)*

`ChatAsync` flow:
1. Run **Facilitator** with user message + full conversation history → get reply + delegation intents
2. Broadcast Facilitator's response as `AgentResponse` (AgentName: "Facilitator")
3. For each delegation intent captured during the Facilitator's turn:
   - Build specialist agent with **fresh context** (board state + instructions, no shared history)
   - Run specialist → broadcast as separate `AgentResponse` with specialist's identity
   - Inject specialist summary back into Facilitator's conversation history (so it remembers)

`RunAutonomousTurnAsync` — same pipeline with autonomous prompt. Split 45s timeout: ~25s Facilitator, ~20s specialists.

### Step 6: Update `AgentToolCallFilter` for multi-agent *(parallel with step 5)*
- Reset captures between agent invocations (or use composite key `{boardId}:{agentType}`)
- Include agent name in `AgentToolCallStarted` SignalR broadcasts

### Step 7: Update `BoardsHub` and `AutonomousFacilitatorWorker` *(depends on step 5)*
- Hub: let orchestrator broadcast directly, or adapt `SendAgentMessage` for multi-message responses
- Worker: handle multi-agent responses and per-agent broadcasting

---

## Phase 3: Frontend — Agent Identity in Chat Panel

### Step 8: Update TypeScript models *(parallel with phase 2)*
- Add `agentName?: string` to `AgentChatMessage` and `ChatMessage` interfaces
- Derive icon and color from `agentName` in `mapToDisplayMessage()`

### Step 9: Update chat panel template *(depends on step 8)*
- Dynamic icon per agent (replacing the static `smart_toy`)
- Agent name label above/beside the message bubble
- Agent-colored left border on bubbles

### Step 10: Add agent-specific styles *(parallel with step 9)*
- `.agent-facilitator` → Indigo, `.agent-builder` → Teal, `.agent-reviewer` → Amber
- Active tool calls show current agent's icon and color during loading state

### Step 11: Update tool call display *(depends on steps 6 and 8)*
- `AgentToolCallStarted` events carry agent name → frontend shows which agent is working

---

## Phase 4: Testing

### Step 12: Backend tests *(depends on phase 2)*
- Update `AgentServiceToolPolicyTests` — verify per-agent tool assignments
- New `AgentOrchestratorTests`: Facilitator calls `RequestBoardChanges` → Builder invoked; calls `RequestBoardReview` → Reviewer invoked; no intents → only Facilitator message

### Step 13: Manual verification
- "Add events for e-commerce checkout" → Facilitator explains + Builder creates, as 2 separate messages with distinct icons/colors
- "Review the board" → Facilitator + Reviewer messages
- "What is event storming?" → Facilitator only (no delegation)
- Autonomous mode → multi-agent messages appear with correct identities
- Page reload → agent identity preserved in history

---

## Relevant Files

**New files:** `Models/AgentType.cs`, `Models/AgentIdentity.cs`, `Models/DelegationIntent.cs`, `Plugins/DelegationPlugin.cs`, `Tests/AgentOrchestratorTests.cs`

**Major changes:** `AgentService.cs` (prompt split, orchestrator pipeline, multi-agent `BuildAgent`)

**Modifications:** `AgentChatMessageDto.cs`, `AgentToolCallFilter.cs`, `BoardsHub.cs`, `AutonomousFacilitatorWorker.cs`, `ai-chat-panel.component.ts`, `ai-chat-panel.component.html`, `ai-chat-panel.component.scss`, `boards-signalr.service.ts`

---

## Decisions
- **3 agents** (not 2 or 4) — Facilitator, Board Builder, Board Reviewer
- **Sequential pipeline** — orchestrator routes after Facilitator, not tool-based inner invocation
- **Full visual identity** — distinct names, icons, and colors
- **Facilitator is stateful** (conversation history); Builder/Reviewer are **stateless** per invocation
- **BoardPlugin unchanged** — same 13 methods, selectively included per agent
- **Shared display history** — all agents' messages in one chat timeline
- **Out of scope**: chat persistence, LLM provider changes, coordinator/rate-limiting changes, new board tools