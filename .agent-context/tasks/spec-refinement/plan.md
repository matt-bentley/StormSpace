# Plan: Spec Refinement Stage with Remote Q&A

Add a Specification stage to the Orchestrator pipeline that always delegates spec authoring to a Spec Writer subagent. The Orchestrator never writes spec.md directly. The Spec Writer receives a `refine` setting: when true, it invokes the Question Relay subagent for remote Q&A via GitHub issue comments and incorporates answers; when false, it drafts the spec without Q&A. The Question Relay remains a standalone reusable agent invocable by any agent that needs user input.

## Design Decisions

- **Trigger**: Opt-in via `--refine-spec` flag (detected alongside `--auto`)
- **Q&A mechanism**: Post questions as an issue comment via `mcp_github_add_issue_comment`; poll `mcp_github_issue_read` (`get_comments`) for user replies after the question comment
- **Polling**: Dedicated Question Relay agent stays alive (hot context), polls periodically with a terminal sleep between checks, up to a configurable max wait. Returns answers (or timeout) to the calling agent
- **Reusable**: Question Relay is a standalone agent invocable by any agent that needs user input (Spec Writer, Planner, etc.)
- **Spec Writer owns Q&A**: The Spec Writer directly invokes the Question Relay as a subagent when it has questions — the Orchestrator does not coordinate between them
- **Spec format**: Lightweight industry-standard specification (simplified PRD/SRS) — non-technical

## Steps

### Phase 1: Question Relay Agent

Create a new Question Relay agent at `.github/agents/question-relay.agent.md`. This agent owns the entire Q&A lifecycle: posting questions to a GitHub issue and polling until the user responds.

1. **Create the Question Relay agent** with:
   - Model: Claude Sonnet 4.6 (fast, low cost — mostly waiting)
   - Tools: `[read, "github/*"]` (needs GitHub access to post/read comments)
   - Single mode of operation: receives questions, posts them, polls for answers, returns results

2. **Agent input** (provided by the calling agent in the prompt):
   - Tracking issue number
   - Source agent name (e.g., "Spec Writer", "Planner") — for attribution in the comment
   - List of numbered questions
   - Poll interval (default: 60 seconds)
   - Max attempts (default: 30)

3. **Agent behaviour**:
   - **Step 1**: Post a comment on the tracking issue via `mcp_github_add_issue_comment`:
     ```
     ## 🤖 Questions from {source agent}

     @matt-bentley — Please reply to this comment with your answers.

     1. {question 1}
     2. {question 2}
     ...
     ```
   - **Step 2**: Enter a poll loop:
     - Run `Start-Sleep -Seconds {interval}` in terminal (PowerShell)
     - Read issue comments via `mcp_github_issue_read` (method: `get_comments`)
     - Look for comments posted *after* the questions comment that are not from a bot
     - If user reply found → exit loop
     - If no reply → repeat (up to max attempts)
   - **Step 3**: Return structured output:
     - On answers found: `## QA: ANSWERS_FOUND\n{raw comment body}`
     - On timeout: `## QA: TIMEOUT\nNo response after {max attempts} attempts ({total wait} minutes)`

4. **Agent prompt protocol**:
   - The agent must NOT interpret or process answers — it returns raw comment text
   - The agent must log each poll attempt (attempt N of M) for observability
   - On timeout, the agent returns gracefully (no error) so the calling agent can decide how to proceed

### Phase 2: Spec Writer Agent

Create a new Spec Writer agent at `.github/agents/spec-writer.agent.md`.

4. **Create the Spec Writer agent** with:
   - Model: Claude Opus 4.6
   - Tools: `[read, search]` (read-only codebase access — no board edits, no direct GitHub access)
   - Subagent access: Can invoke the Question Relay agent via `runSubagent` for Q&A
   - Single invocation from the Orchestrator — the Spec Writer always writes the spec:
     1. Draft the spec from the user request
     2. If `refine: true` AND clarifying questions exist → invoke Question Relay subagent with the tracking issue # and questions
     3. If answers received → incorporate answers and refine the spec
     4. If timeout → leave unanswered questions in `## Open Questions` and return the current spec
     5. If `refine: false` → skip Q&A entirely (no Question Relay invocation)
     6. Return the final `spec.md`

5. **Spec.md format** — simplified PRD/requirements doc (non-technical):
   ```markdown
   # Specification: {Title}

   ## Overview
   {1-2 paragraph summary of what is being built and why}

   ## Goals
   - {Goal 1}
   - {Goal 2}

   ## User Stories
   - As a {persona}, I want {action} so that {benefit}

   ## Functional Requirements
   | ID | Requirement | Priority |
   |----|------------|----------|
   | FR-1 | {requirement} | Must / Should / Could |

   ## Non-Functional Requirements
   - {Performance, security, accessibility constraints}

   ## Constraints & Assumptions
   - {Known constraints}
   - {Assumptions made}

   ## Out of Scope
   - {Explicitly excluded items}

   ## Open Questions
   - {Any unresolved questions — populated from unanswered GitHub questions}
   ```

6. **Spec Writer prompt protocol**:
   - The agent receives: user request, tracking issue number, task slug, `refine` setting (true/false), and relevant repository context
   - The agent always drafts a structured spec from the user request
   - If `refine: true`, the agent self-evaluates whether clarifying questions are needed
   - If `refine: false`, the agent skips Q&A and returns the draft spec immediately
   - If questions are needed (and `refine: true`), the agent invokes the Question Relay subagent directly:
     ```
     runSubagent("Question Relay", prompt: "
       Tracking issue: #{issue_number}
       Source agent: Spec Writer
       Questions:
       1. {question 1}
       2. {question 2}
       Poll interval: 60 seconds
       Max attempts: 30
     ")
     ```
   - On receiving answers from Question Relay (`## QA: ANSWERS_FOUND`), the agent incorporates them into the spec and removes answered items from Open Questions
   - On timeout (`## QA: TIMEOUT`), the agent logs a note and leaves unanswered questions in `## Open Questions`
   - If no questions are needed, the agent skips Q&A entirely
   - The agent writes the final `spec.md` to `.agent-context/tasks/{task-slug}/spec.md` and returns a summary of what was produced

### Phase 3: Orchestrator Pipeline Changes

Modify the Orchestrator to add the Specification stage between Initialisation and Planning.

7. **Add `--refine-spec` flag detection** alongside existing `--auto` detection in the "Auto Mode Detection" section
   - Scan user message for `--refine-spec` (case-insensitive)
   - Remove it from the task description when creating files

8. **Add new Stage 2: Specification** (shifts all subsequent stages by 1):
   - **Step 1 — Always**: Invoke Spec Writer subagent (single invocation):
     - Input: raw user request, tracking issue #, task slug, `refine: true/false` (based on `--refine-spec` flag), relevant repository context
     - The Spec Writer always drafts and writes the structured spec.md
     - If `refine: true`, the Spec Writer handles Q&A internally via Question Relay → refine → write final spec.md
     - If `refine: false`, the Spec Writer drafts the spec without Q&A and writes it immediately
     - The Orchestrator never writes spec.md directly — the Spec Writer owns all spec authoring
   - **Step 2**: Invoke Delivery Manager `stage_update`: Stage "Specification", Status: Completed
   - **Step 3**: Update progress file: Specification → Completed (with notes on whether refinement was enabled and whether questions were asked/answered)

9. **Update progress file reference** to include `**Spec**: [spec.md](spec.md)` alongside the existing `**Plan**` link

10. **Pass spec.md to the Planner**: Update the Planner invocation to include the spec file path so the Planner uses the refined spec as its input rather than the raw task description

### Phase 4: Progress & Template Updates

11. **Update progress file template** in [.github/instructions/agent-progress.instructions.md](../../.github/instructions/agent-progress.instructions.md):
    - Add `Specification` row to Pipeline Stages table (between Planning row — it becomes the first stage after Init)
    - Add `**Spec**: [spec.md](spec.md)` to file header fields

12. **Update Issue Body Templates** in the Delivery Manager:
    - Add "Specification" row to both Init and Plan Ready Pipeline tables
    - Init template shows `Specification | 🔄 In Progress`
    - No new commands are added to the Delivery Manager — Q&A is handled by the Question Relay agent

13. **Update Orchestrator stage numbering**: Renumber all stages (current Stage 2: Planning becomes Stage 3, etc.)

## Relevant Files

- `.github/agents/orchestrator.agent.md` — Add flag detection, new Specification stage, Spec Writer invocation (single call), renumber stages
- `.github/agents/question-relay.agent.md` — **New file**: Question Relay agent (posts questions to GitHub, polls for answers)
- `.github/agents/spec-writer.agent.md` — **New file**: Spec Writer agent definition
- `.github/agents/delivery-manager.agent.md` — Update issue body templates only (no new commands)
- `.github/instructions/agent-progress.instructions.md` — Add Specification stage row, spec file reference

## Verification

1. Read through the Orchestrator flow end-to-end and verify stage numbering is consistent (no gaps, no duplicate stage numbers)
2. Verify Question Relay agent has GitHub tools (`github/*`) and follows the agent file conventions
3. Verify Spec Writer agent has the correct tool permissions (read-only codebase access + subagent invocation for Question Relay)
4. Verify the progress file template includes the new Specification stage
5. Verify the issue body templates include the Specification stage in the Pipeline table
6. Verify the `--refine-spec` flag is stripped from the task description (same pattern as `--auto`)
7. Verify the Question Relay's poll loop has a clear timeout and graceful fallback
8. Trace a full pipeline run mentally: with `--refine-spec` (questions + answers), with `--refine-spec` (questions + timeout), without `--refine-spec` (Spec Writer drafts without Q&A)
9. Verify the Spec Writer's subagent invocation of Question Relay includes all required parameters (tracking issue #, source agent name, questions, poll config)

## Decisions

- `--refine-spec` is opt-in (not auto-detected)
- Poll interval: 60 seconds, max 10 attempts (~10 minutes) — passed to the Question Relay agent as parameters
- Questions posted as issue comments via `mcp_github_add_issue_comment`; answers detected from subsequent issue comments via `get_comments`
- No `clear_questions` needed — comments are a natural conversation thread
- Question Relay is a standalone agent (not part of the Delivery Manager) — owns the full post-and-poll lifecycle, reusable by any agent
- Spec Writer always writes the spec — the Orchestrator never writes spec.md directly. The Orchestrator passes a `refine` setting (from `--refine-spec` flag) and the Spec Writer decides whether to invoke Q&A based on that setting
- The Planner receives the spec.md path and uses it as primary input
- Spec format is a simplified PRD — non-technical, industry-standard

## Further Considerations

1. **Planner integration**: When the Planner needs Q&A later, it invokes the Question Relay subagent directly with its own questions and tracking issue #. Same pattern as the Spec Writer — the calling agent owns the lifecycle, not the Orchestrator.
2. **Poll timeout**: 10 minutes may be too short for complex specs. Consider making this configurable via the flag (e.g., `--refine-spec:20` for 20 min). Recommendation: keep simple for now (fixed 10 min), extend later.
3. **Multiple Q&A rounds**: Current design supports one round of questions. If the refined spec still has questions, a second round could be added later. Recommendation: one round only for v1.
