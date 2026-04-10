# Plan: End-to-End Agentic Development Framework

Create 6 new agents chaining into a full lifecycle. The Refiner handles plan review (it already wraps Adversarial Reviewer). A new lightweight **Phase Reviewer** does exactly 1 round of adversarial review + fix after each implementation phase.

**Updated agent chain:**
```
Orchestrator
  ├── Planner (creates phased plans from knowledge)
  ├── Refiner (existing — reviews + fixes plan, 1 round by default)
  │   └── Adversarial Reviewer (existing — 3-model tribunal)
  ├── [Pause for user approval unless --auto]
  ├── Per-phase loop:
  │   ├── git commit (Orchestrator commits baseline before phase)
  │   ├── Implementer (executes one phase)
  │   └── Phase Reviewer (1 round: scoped diff → Adversarial Review → fix → build/test verify)
  │       └── Adversarial Reviewer (existing)
  ├── Regression Tester (browser-based user journey testing)
  ├── Implementer (fix any regression issues)
  ├── Regression Tester (re-verify fixes)
  └── Knowledge Keeper (updates .agent-context/knowledge/)
```

## Steps

### Phase 1: Directory Structure & Instructions

1. Create `.agent-context/progress/README.md` — explains the directory holds per-task progress tracking files created by the Orchestrator during pipeline execution, with format defined by `agent-progress.instructions.md`
2. Create `.agent-context/plans/README.md` — explains the directory holds phased implementation plans created by the Planner, with format defined by `agent-plans.instructions.md`
3. Create `.github/instructions/agent-progress.instructions.md` (`applyTo: "**/.agent-context/progress/**"`) — defines progress file format: task metadata, per-phase status table, issues log, knowledge updates log
4. Create `.github/instructions/agent-plans.instructions.md` (`applyTo: "**/.agent-context/plans/**"`) — defines phase plan format: objective, files to modify, code examples, links to knowledge/instructions, verification criteria

### Phase 2: Core Agents *(parallel with Phase 1)*

5. **`orchestrator.agent.md`** (name: `Orchestrator`, model: `Claude Sonnet 4.6`) — Top-level lifecycle coordinator. Detects `--auto` anywhere in the user's message to skip plan approval gate (defaults to pausing). Chains: Planner → Refiner (plan + phase files, 1 round) → [approve] → per-phase (commit baseline → Implementer → Phase Reviewer) → Regression Tester → Implementer (fixes) → Regression Tester (re-verify) → Knowledge Keeper. Creates and updates progress files in `.agent-context/progress/`. Inter-phase isolation: commits a baseline (`git add -A && git commit -m "phase-N baseline"`) before each phase so Phase Reviewer can diff against that commit. Before invoking Regression Tester: checks app accessibility at `https://localhost:51710` and halts with startup instructions if unavailable. On agent failure or Phase Reviewer `fail` status: logs the error to progress file and halts with a summary (no silent continuation). Tools: `[read, edit, search, execute, agent, todo]`. Subagents: `[Planner, Refiner, Implementer, Phase Reviewer, Regression Tester, Knowledge Keeper]`

6. **`planner.agent.md`** (name: `Planner`, model: `Claude Opus 4.6`) — Reads ALL `.agent-context/knowledge/` files, searches codebase for analogous implementations, uses MCP tools (Context7, MicrosoftDocs) for library/API research, checks `.github/skills/` for applicable skills. Writes master plan + per-phase detail files with actual code examples and links. Tools: `[read, edit, search]`. Note: MCP tools (Context7, MicrosoftDocs) are available via VS Code's MCP server configuration in `.vscode/mcp.json` — they do not require a separate tool category in frontmatter.

7. **`implementer.agent.md`** (name: `Implementer`, model: `GPT-5.3-Codex`) — Takes a single phase plan file, loads referenced instructions/skills, implements it precisely, runs verification criteria: `dotnet build` + `dotnet test` for backend, `npm run build` for frontend (when phase touches `src/eventstormingboard.client/`). Tools: `[read, edit, search, execute, todo]`

8. **`phase-reviewer.agent.md`** *(NEW)* (name: `Phase Reviewer`, model: `Claude Sonnet 4.6`) — Lightweight single-round review+fix. Captures a scoped diff against the phase baseline commit (`git diff <baseline>..HEAD`) and passes the diff + changed file list directly to the `Adversarial Reviewer` (bypassing AR's own `git diff` detection). Loads the same `.github/instructions/` and `.github/skills/` files as the Implementer to ensure fixes follow project conventions. Fixes all CRITICAL/MAJOR findings. After fixes, runs verification: `dotnet build` + `dotnet test` for backend, `npm run build` for frontend (when phase touched client code). Reports outcome with mandatory output format:
   - `## Phase Review Status: PASS` (build/test green)
   - `## Phase Review Status: FAIL` (build/test broken after fix attempt — Orchestrator halts pipeline)
   Exactly 1 review + 1 fix + 1 verify pass. Tools: `[read, edit, search, execute, agent]`. Subagents: `[Adversarial Reviewer]`

9. **`regression-tester.agent.md`** (name: `Regression Tester`, model: `Claude Sonnet 4.6`) — Reads `.agent-context/knowledge/user-journeys.md`, walks through all 10 user journeys via the browser tool, prioritising journeys affected by the changed files (receives changed-file summary from Orchestrator). Reports issues (does NOT fix them). Preconditions: requires running app (backend + frontend); agent must verify app is accessible at `https://localhost:51710` before starting journeys. Reports with mandatory output format:
   - `## Regression Status: PASS ({N} journeys passed, {M} manual verification needed)`
   - `## Regression Status: FAIL ({N} automated failures)` followed by failure details
   Tools: `[read, search, execute, browser]`

10. **`knowledge-keeper.agent.md`** (name: `Knowledge Keeper`, model: `Claude Opus 4.6`) — Reviews implementation, updates affected `.agent-context/knowledge/` files following `.github/instructions/agent-knowledge.instructions.md` standards. Runs last in the chain (after regression fixes land) to ensure knowledge reflects final, verified state. Tools: `[read, edit, search]`

### Phase 3: Integration

11. Update `.agent-context/knowledge/README.md` — add entries for sibling `plans/` and `progress/` directories with cross-references to their own READMEs

### Agent `name` Field Values

All new agents must use these exact `name` values in their frontmatter (these are the values referenced in `agents` arrays):

| File | `name` field | `user-invocable` |
|------|--------------|------------------|
| `orchestrator.agent.md` | `Orchestrator` | `true` (entry point) |
| `planner.agent.md` | `Planner` | `false` |
| `implementer.agent.md` | `Implementer` | `false` |
| `phase-reviewer.agent.md` | `Phase Reviewer` | `false` |
| `regression-tester.agent.md` | `Regression Tester` | `false` |
| `knowledge-keeper.agent.md` | `Knowledge Keeper` | `false` |

## Files to Create

### New Agents (`.github/agents/`)
- `orchestrator.agent.md`
- `planner.agent.md`
- `implementer.agent.md`
- `phase-reviewer.agent.md`
- `regression-tester.agent.md`
- `knowledge-keeper.agent.md`

### New Instructions (`.github/instructions/`)
- `agent-progress.instructions.md`
- `agent-plans.instructions.md`

### New Directories
- `.agent-context/progress/README.md`
- `.agent-context/plans/README.md`

## Existing Agents (modified)

- `.github/agents/refiner.agent.md` — Update to make max rounds configurable. The invoking agent specifies the desired round count (e.g. "Review this plan with 1 round"). Default remains 3 when not specified. Changes: replace hardcoded "3 rounds" with a parameter the Refiner extracts from the invocation prompt

## Existing Agents (reused, not modified)

- `.github/agents/adversarial-reviewer.agent.md` — 3-model tribunal (Opus 4.6, GPT-5.4, GPT-5.3-Codex)
- `.github/agents/refiner.agent.md` — Review-fix loop (configurable rounds, default 3)
- `.github/agents/reviewer-opus.agent.md` — Pinned Claude Opus 4.6 reviewer
- `.github/agents/reviewer-gpt54.agent.md` — Pinned GPT-5.4 reviewer
- `.github/agents/reviewer-gpt53-codex.agent.md` — Pinned GPT-5.3-Codex reviewer

## Existing Resources (consumed by agents)

- `.agent-context/knowledge/` — All 5 knowledge files consumed by Planner at planning time
- `.agent-context/knowledge/user-journeys.md` — Browser selectors and 10 journey definitions for Regression Tester
- `.github/instructions/agent-knowledge.instructions.md` — Knowledge doc standards followed by Knowledge Keeper
- `.github/skills/frontend-design/SKILL.md` — Referenced by Planner/Implementer for UI work
- `.github/skills/nuget-manager/SKILL.md` — Referenced by Planner/Implementer for package changes
- `.vscode/mcp.json` — Context7 (library docs) and MicrosoftDocs (MS Learn) MCP servers
- `.github/prompts/add-agent-knowledge.prompt.md` — Knowledge update workflow pattern (reused in Knowledge Keeper). **Prerequisite fix**: update stale path `0-knowledge/README.md` → `knowledge/README.md` in this file before implementation

## Verification

1. All 6 new agent files in `.github/agents/` with valid frontmatter — `agents` arrays reference only existing agent `name` fields, `model` field set per Decisions section
2. Both instruction files have correct `applyTo` globs
3. `.agent-context/progress/` directory exists
4. Smoke test:
   - **Setup**: Start backend (`dotnet run`) and frontend (`npm start`), confirm app accessible at `https://localhost:51710`
   - **Invoke**: `@Orchestrator Add a tooltip to the board name --auto`
   - **Pass criteria**: All pipeline stages complete (plan file created → refined → phases implemented → phase reviews pass build/test → regression tester reports 0 automated regressions and 0 or more "manual verification needed" items → knowledge files updated). Progress file at `.agent-context/progress/add-tooltip-board-name.md` shows all phases completed.
   - **Fail criteria**: Any agent errors out, build/test fails after Phase Reviewer fix pass, or regression tester finds automated regressions that Implementer cannot resolve in one fix cycle

## Risks & Limitations

- **`browser` tool for Canvas interactions**: No existing agents use the `browser` tool. StormSpace uses HTML Canvas with imperative drawing — click-and-drag, WebSocket connections, and complex UI state may not be fully testable via browser automation. Regression Tester should degrade gracefully: test what's automatable (navigation, form inputs, chat), report Canvas interactions as "manual verification needed". "Manual verification needed" results do NOT count as regressions for gating purposes — only automated test failures block the pipeline
- **Regression fix loop cap**: The Orchestrator runs at most 1 fix cycle (Implementer → Regression Tester re-verify). If regressions persist after one fix attempt, the pipeline halts and reports remaining issues rather than looping indefinitely
- **Partial failure / no rollback**: On pipeline failure, partial changes remain in the working tree. The pipeline halts and reports status rather than rolling back. This is intentional for an editor-side workflow — the user can inspect and manually revert if needed
- **No frontend tests**: Per project conventions, no frontend tests currently exist. Phase Reviewer and Implementer verify with backend build/test (`dotnet build`, `dotnet test`) plus frontend build (`npm run build`) for Angular changes. When frontend tests are added, verification commands should use non-interactive `ng test --watch=false`

## Decisions

- Refiner for plan review (configurable rounds — Orchestrator invokes with 1 round for plan review; users invoking Refiner directly get the default of 3)
- Phase Reviewer (new) for per-phase code review (exactly 1 round — lighter than Refiner), with build/test re-validation after fixes. Must load same instructions/skills as Implementer for convention-compliant fixes
- Inter-phase isolation: Orchestrator commits a baseline before each phase; Phase Reviewer diffs against that commit and passes the scoped diff directly to Adversarial Reviewer (bypassing AR's own git diff detection)
- Agent output contracts: Phase Reviewer must output `## Phase Review Status: PASS|FAIL`; Regression Tester must output `## Regression Status: PASS|FAIL`. Orchestrator parses these headings to determine pipeline flow
- `--auto` flag on Orchestrator skips plan approval, default pauses. Detection: Orchestrator scans the user's message for the literal string `--auto` (case-insensitive)
- Regression Tester is report-only; Orchestrator routes fixes to Implementer, then re-invokes Regression Tester to verify
- Knowledge updates happen after regression testing and fixes to ensure docs reflect final verified state
- Refiner reviews and fixes both the master plan and per-phase detail files during its review-fix rounds (no separate Planner re-invocation needed)
- Progress files: `.agent-context/progress/{task-slug}.md` — created and maintained by Orchestrator
- Plan files: `.agent-context/plans/{task-slug}/plan.md` + `phase-N-name.md`
- On failure: Orchestrator logs error to progress file and halts (no silent continuation)

### Model Assignments

| Agent | Model | `model` frontmatter | Rationale |
|-------|-------|--------------------|-----------|
| Orchestrator | Claude Sonnet 4.6 | `model: "Claude Sonnet 4.6"` | Cheaper than Opus, strong at agentic coordination |
| Planner | Claude Opus 4.6 | `model: "Claude Opus 4.6"` | Heavy reasoning for plan generation |
| Implementer | GPT-5.3-Codex | `model: "GPT-5.3-Codex"` | Optimised for code generation |
| Phase Reviewer | Claude Sonnet 4.6 | `model: "Claude Sonnet 4.6"` | Coordinates review + fix, moderate reasoning |
| Regression Tester | Claude Sonnet 4.6 | `model: "Claude Sonnet 4.6"` | Browser-based testing, agentic workflow |
| Knowledge Keeper | Claude Opus 4.6 | `model: "Claude Opus 4.6"` | Heavy reasoning for documentation quality |
