---
applyTo: "**/.agent-context/tasks/**/progress.md"
---

# Agent Progress File Standards

Progress files track the status of agentic workflow pipeline runs. They are created and maintained by the Orchestrator agent.

## Timestamps
- Use the `utc-datetime` skill to populate all timestamp fields for consistency.
- Update timestamps at each stage transition and phase milestone.

## File Structure

```markdown
# Progress: {Task Title}

**Task**: {Brief description of what was requested}
**Started**: {utc-datetime}
**Status**: {In Progress | Completed | Failed | Halted}
**Plan**: [plan.md](plan.md)

## Phase Status

| Phase | Name | Started | Completed | Notes |
|-------|------|---------|-----------|-------|
| 1 | {Phase name} | {utc-datetime} | {utc-datetime} | {Brief notes} |
| 2 | {Phase name} | | | {notes} |

## Pipeline Stages

| Stage | Started | Completed | Status | Notes |
|-------|---------|-----------|--------|-------|
| Planning | {utc-datetime} | {utc-datetime} | {Completed / Failed} | {notes} |
| Plan Review | | | {Completed / Failed} | {notes} |
| Plan Approval | | | {Approved / Pending / Skipped (--auto)} | {notes} |
| Implementation | | | {In Progress / Completed / Failed} | {notes} |
| Implementation Review | | | {Passed / Failed / Skipped} | {notes} |
| Regression Testing | | | {Passed / Failed / Skipped} | {notes} |
| Regression Fixes | | | {Completed / Not Needed / Failed} | {notes} |
| Regression Re-verify | | | {Passed / Failed / Skipped} | {notes} |
| Knowledge Update | | | {Completed / Skipped / Failed} | {notes} |

## Issues Log

| Timestamp | Phase | Severity | Description | Resolution |
|-----------|-------|----------|-------------|------------|
| {utc-datetime} | {phase} | {Critical / Major / Minor} | {What went wrong} | {How it was resolved or "Pipeline halted"} |

## Knowledge Updates

| File | Action | Summary |
|------|--------|---------|
| {path} | {Created / Updated} | {Brief description of changes} |

## GitHub Tracking

| Key | Value |
|-----|-------|
| Tracking Issue | #{number} |
| Issue Node ID | {node_id} |
| Branch | task/{task-slug} |
| PR | #{number} or — |
| Project Number | 1 |
| Tracking Item ID | {project_item_id} |
| Status Field ID | {field_id} |
| Backlog Option ID | {option_id} |
| In Progress Option ID | {option_id} |
| In Review Option ID | {option_id} |
| Done Option ID | {option_id} |

### Phase Sub-Issues

| Phase | Name | Issue # | Node ID | Project Item ID |
|-------|------|---------|---------|-----------------|
| 1 | {name} | #{number} | {node_id} | {item_id} |
| 2 | {name} | #{number} | {node_id} | {item_id} |

### Regression Sub-Issue

| Key | Value |
|-----|-------|
| Issue # | #{number} or — |
| Node ID | {node_id} or — |
| Project Item ID | {item_id} or — |
```

## Field Definitions

### Task Status Values

- **In Progress** — Pipeline is currently executing
- **Completed** — All stages finished successfully
- **Failed** — A stage failed and the pipeline halted
- **Halted** — Pipeline was stopped due to an unrecoverable error or user intervention

### Phase Timestamp Rules

- **Implementation Started** — Set when the Implementer begins executing this phase
- **Completed** — Set when the Implementer finishes this phase and the commit is created

## Update Rules

- Update the progress file after each stage transition
- For phases: set **Impl Started** when implementation begins, **Review Started** when review begins, and **Completed** when the phase is fully done (all utc-datetime)
- For stages: set **Started** when the stage begins and **Completed** when it finishes (all utc-datetime)
- Log all errors to the Issues Log, even if subsequently resolved
- Record the final status accurately — do not mark as Completed if any stage failed
- Knowledge Updates section is populated by the Knowledge Keeper at the end of the pipeline

### GitHub Tracking Rules

- The `## GitHub Tracking` section is populated by the Delivery Manager's `init` response and updated as new items (PR, regression sub-issue) are created
- The Orchestrator reads IDs from this section and passes them to the Delivery Manager for every subsequent command — the Delivery Manager must not re-look up IDs
- If the Delivery Manager fails during `init`, this section remains empty and all subsequent Delivery Manager calls are skipped (non-blocking)
