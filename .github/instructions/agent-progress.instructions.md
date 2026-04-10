---
applyTo: "**/.agent-context/tasks/**/progress.md"
---

# Agent Progress File Standards

Progress files track the status of agentic workflow pipeline runs. They are created and maintained by the Orchestrator agent.

## File Structure

```markdown
# Progress: {Task Title}

**Task**: {Brief description of what was requested}
**Started**: {ISO 8601 timestamp}
**Status**: {In Progress | Completed | Failed | Halted}
**Plan**: [plan.md](plan.md)

## Phase Status

| Phase | Name | Implementation | Review | Notes |
|-------|------|----------------|--------|-------|
| 1 | {Phase name} | {Not Started / In Progress / Done / Failed} | {Not Started / In Progress / Passed / Failed} | {Brief notes} |
| 2 | {Phase name} | {status} | {status} | {notes} |

## Pipeline Stages

| Stage | Status | Notes |
|-------|--------|-------|
| Planning | {Completed / Failed} | {notes} |
| Plan Review | {Completed / Failed} | {notes} |
| Plan Approval | {Approved / Pending / Skipped (--auto)} | {notes} |
| Implementation | {In Progress / Completed / Failed} | {notes} |
| Regression Testing | {Passed / Failed / Skipped} | {notes} |
| Regression Fixes | {Completed / Not Needed / Failed} | {notes} |
| Regression Re-verify | {Passed / Failed / Skipped} | {notes} |
| Knowledge Update | {Completed / Skipped / Failed} | {notes} |

## Issues Log

| Timestamp | Phase | Severity | Description | Resolution |
|-----------|-------|----------|-------------|------------|
| {ISO 8601} | {phase} | {Critical / Major / Minor} | {What went wrong} | {How it was resolved or "Pipeline halted"} |

## Knowledge Updates

| File | Action | Summary |
|------|--------|---------|
| {path} | {Created / Updated} | {Brief description of changes} |
```

## Field Definitions

### Task Status Values

- **In Progress** — Pipeline is currently executing
- **Completed** — All stages finished successfully
- **Failed** — A stage failed and the pipeline halted
- **Halted** — Pipeline was stopped due to an unrecoverable error or user intervention

### Phase Implementation Values

- **Not Started** — Implementation has not begun
- **In Progress** — Implementer is currently executing this phase
- **Done** — Implementation finished, awaiting review
- **Failed** — Implementation failed (build/test errors)

### Phase Review Values

- **Not Started** — Review has not begun (implementation may still be in progress)
- **In Progress** — Phase Reviewer is currently reviewing
- **Passed** — Phase Reviewer approved the changes
- **Failed** — Phase Reviewer found unresolvable issues

## Update Rules

- Update the progress file after each stage transition
- Log all errors to the Issues Log, even if subsequently resolved
- Record the final status accurately — do not mark as Completed if any stage failed
- Knowledge Updates section is populated by the Knowledge Keeper at the end of the pipeline
