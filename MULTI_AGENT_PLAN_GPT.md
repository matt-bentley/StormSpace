# Multi-Agent Plan

## Overview

Replace the current single-agent reply path with a sequential orchestration pipeline. A PlannerAgent chooses the next steps, then specialist agents run in order with their own identities, prompts, and tool policies. Each agent utterance, handoff, and tool activity should be emitted as its own chat event so the Angular panel can show inter-agent communication as separate messages while board mutations remain serialized and safe.

## Phased Implementation

### Phase 1: Normalize the agent chat contract

1. Extend server chat payloads to carry agent identity and execution metadata.
2. Add `AgentId`, `AgentName`, `MessageKind`, and `ExecutionId` to `AgentChatMessageDto` so the UI can distinguish planner messages, specialist replies, and visible handoffs without inferring identity from `Role` alone.
3. Extend the `AgentToolCallStarted` payload with the same identity metadata so active tool-call chips can be attributed to the correct agent run.
4. Introduce a small chat history and broadcast abstraction that both appends to in-memory history and pushes SignalR events immediately. This removes the current single final `AgentResponse` bottleneck.
5. Keep user messages on the existing user-message path, but normalize all agent-originated messages through one shared broadcast path so interactive and autonomous runs use the same model.

### Phase 2: Isolate tool capture per agent execution

1. Refactor `AgentToolCallFilter` so capture scope is keyed by `ExecutionId` rather than `BoardId` alone. The current board-only `ConcurrentDictionary` would mix tool calls if multiple agent steps run inside one orchestrated turn.
2. Begin and end tool capture around each agent execution step, not around the whole board conversation. This lets the planner, analyst, and editor each emit their own tool list and live tool events.
3. Keep the first implementation strictly sequential per board. Do not introduce concurrent specialist execution in v1, because mutation conflicts and tool-call interleaving are not solved today.

### Phase 3: Split orchestration responsibilities in the server

1. Preserve `IAgentService` as the hub and worker entrypoint, but move raw LLM invocation into a reusable executor or factory service that can build different agents from different prompts and tool allowlists.
2. Add explicit agent definitions for the first granular team:
   - `PlannerAgent`: classifies the user or autonomous goal, chooses which specialists should run next, and emits visible handoff messages.
   - `BoardAnalystAgent`: read-only inspection of board state, recent events, and modeling gaps.
   - `FacilitationCoachAgent`: explains the current workshop phase, teaches the next step, and produces participant-facing guidance.
   - `BoardEditorAgent`: performs note, connection, move, edit, and phase-change operations with a constrained write-capable tool set.
3. Route interactive messages through `PlannerAgent` first, then run the chosen specialists sequentially.
4. Route autonomous turns through the same orchestration path, but keep autonomous-specific restrictions such as no destructive tools and existing stop semantics.
5. Pass structured handoff summaries between agents instead of replaying the full visible transcript into every next agent. Visible inter-agent messages should remain in display history, while model context should be kept compact and purpose-built.

### Phase 4: Integrate the new orchestration into the hub and worker

1. Update `BoardsHub.SendAgentMessage` so it broadcasts the user message, starts orchestration, and relies on progressive server-side broadcasts for each planner or specialist message instead of waiting for one final assistant response.
2. Update `GetHistory` and `ClearHistory` to use the normalized multi-agent message stream.
3. Update `AutonomousFacilitatorWorker` so planner and specialist outputs are emitted as separate messages during autonomous runs, while `AutonomousFacilitatorStatusChanged` continues to reflect worker state.
4. Preserve the existing coordinator safeguards around manual-response suppression and failure handling. The worker should still treat the overall orchestrated turn as a single autonomous run for scheduling purposes.

### Phase 5: Update the Angular SignalR client and chat panel

1. Extend the frontend `AgentChatMessage` and `AgentToolCallStartedEvent` models with `AgentId`, `AgentName`, `MessageKind`, and `ExecutionId`.
2. Update `BoardsSignalRService` mapping so those fields flow into client state and history without special-casing a single assistant identity.
3. Update the AI chat panel message model, template, and styles to render assistant sender labels, agent badges or icons, and visual differentiation for planner versus specialist messages while keeping the timeline linear.
4. Replace the single generic loading bubble with agent-scoped in-progress state keyed by `ExecutionId` and `AgentId` so tool calls from different agents appear under the right in-flight message.
5. Keep the v1 UI as a single chronological chat stream. Show inter-agent communication inline rather than introducing threaded or collapsible sub-conversations.

### Phase 6: Add focused test coverage and verify behavior

1. Add server tests for orchestration order, specialist selection, agent-specific tool allowlists, execution-scoped tool capture, history ordering, and autonomous multi-message emission.
2. Extend existing tool-policy tests so the allowlist is validated per specialist rather than only interactive versus autonomous.
3. Add minimal Angular tests for SignalR message mapping and chat-panel rendering of multiple agent identities and separate assistant messages.
4. Run a manual interactive scenario and a manual autonomous scenario to confirm that planner and specialists appear as distinct chat messages and that each tool call is attributed to the correct agent.

## Relevant Files

- `src/EventStormingBoard.Server/Services/AgentService.cs`: preserve `ChatAsync` and `RunAutonomousTurnAsync` as entrypoints, but move single-agent execution logic behind a multi-agent orchestrator and reusable agent executor.
- `src/EventStormingBoard.Server/Filters/AgentToolCallFilter.cs`: change `BeginCapture`, `EndCapture`, and `OnFunctionInvocationAsync` to execution-scoped capture and agent-aware tool-call broadcasts.
- `src/EventStormingBoard.Server/Models/AgentChatMessageDto.cs`: add agent identity and message-kind metadata to the DTO used by SignalR history and live events.
- `src/EventStormingBoard.Server/Hubs/BoardsHub.cs`: stop assuming a single final agent reply from `SendAgentMessage` and integrate progressive multi-agent broadcasting.
- `src/EventStormingBoard.Server/Services/AutonomousFacilitatorWorker.cs`: emit planner and specialist outputs separately during autonomous runs while preserving the current run-status lifecycle.
- `src/EventStormingBoard.Server/Program.cs`: register the new orchestrator, agent-definition or factory services, and any new chat-broadcast abstraction in DI.
- `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`: extend `mapAgentChatMessage` and `AgentToolCallStarted` mapping with agent identity and execution metadata.
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.ts`: preserve the panel behavior but update `mapToDisplayMessage`, active-tool-call state, and assistant identity rendering.
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.html`: replace the single assistant avatar and header assumptions with per-message agent labels or badges.
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.scss`: add visual differentiation for multiple agent identities while keeping the current layout and chronological message flow.
- `tests/EventStormingBoard.Server.Tests/AgentServiceToolPolicyTests.cs`: evolve the current reflection-based tool-policy coverage to validate specialist allowlists.
- `tests/EventStormingBoard.Server.Tests/AutonomousFacilitatorCoordinatorTests.cs`: keep the current coordinator behavior intact and add coverage where multi-agent orchestration must not break scheduling expectations.
- `tests/EventStormingBoard.Server.Tests`: add new orchestration, history, and tool-capture tests here.

## Verification

1. Run `dotnet build src/EventStormingBoard.Server/EventStormingBoard.Server.csproj`.
2. Run `dotnet test tests/EventStormingBoard.Server.Tests/EventStormingBoard.Server.Tests.csproj`.
3. Run `npm test -- --watch=false` from `src/eventstormingboard.client`.
4. Run `npm run build` from `src/eventstormingboard.client`.
5. Manually open a board, send a normal chat request that should require planning plus a board change, and confirm the chat shows at least planner plus one specialist as separate messages in order.
6. Enable autonomy on a board with active users, wait for one autonomous cycle, and confirm planner and specialist messages appear separately and tool calls are visually attributed to the correct agent.
7. Clear chat history and reload the board to confirm the multi-agent history rehydrates in the same order and with agent identities intact.

## Decisions

- Included scope: sequential planner-plus-specialists orchestration for both interactive and autonomous flows, visible inter-agent handoffs in the chat panel, and per-agent tool-call attribution.
- Excluded scope: parallel agent execution on the same board, durable persistence beyond current in-memory history, and redesigning the board event model outside agent-chat payload changes.
- Recommended implementation boundary: keep board mutations serialized through one specialist at a time and treat the full orchestrated pass as one logical run from the coordinator's point of view.
- Recommended UX boundary: render inter-agent communication inline in the existing chat timeline rather than introducing threads, tabs, or agent-specific side panels in this iteration.

## Further Considerations

1. Normalize all non-user messages through one server-side broadcast helper even if the public SignalR event names stay backward-compatible, because it prevents the hub, worker, and filter from each inventing slightly different payloads.
2. Start with four agents only. Phase-specific specialists can be added later if the planner plus analyst plus coach plus editor split proves too coarse.
3. Keep visible inter-agent messages in display history, but pass compact structured summaries between agents so prompt size and repeated reasoning do not grow without bound.