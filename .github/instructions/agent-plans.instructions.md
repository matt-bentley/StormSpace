---
applyTo: "**/.agent-context/plans/**"
---

# Agent Plan File Standards

Plan files define phased implementation strategies created by the Planner agent. They consist of a master plan and per-phase detail files.

## Master Plan Structure (`plan.md`)

```markdown
# Plan: {Task Title}

**Objective**: {What the task achieves}
**Phases**: {N}
**Estimated complexity**: {Low / Medium / High}

## Context

{Brief background on why this task is needed and any relevant domain context.}

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | {Name} | {Brief description} | {Key files} | {How to verify} |
| 2 | {Name} | {Brief description} | {Key files} | {How to verify} |

## Dependencies

{Any prerequisites, packages, or setup needed before implementation.}

## Risks

{Known risks, edge cases, or areas requiring careful attention.}
```

## Phase Detail Structure (`phase-{N}-{name}.md`)

```markdown
# Phase {N}: {Phase Name}

**Objective**: {What this phase achieves}
**Files to modify**:
- `{path/to/file1}` — {what changes}
- `{path/to/file2}` — {what changes}

## Context

{Why this phase exists and how it fits into the overall plan.}

## Knowledge, Instructions & Skills

{References to relevant project knowledge, instructions and skills that apply to this phase.}

- `.agent-context/knowledge/{knowledge}.md` — {why it's relevant}
- `.github/instructions/{relevant}.instructions.md` — {why it's relevant}
- `.github/skills/{skill}/SKILL.md` — {why it's relevant}

## Implementation Steps

### Step 1: {Description}

{Detailed explanation of what to do.}

**Code example:**
```{language}
{Actual code to write or pattern to follow, based on codebase research.}
`` `

**Reference**: `{path/to/analogous/implementation}` — {what pattern to follow}

### Step 2: {Description}

{Continue with detailed steps...}

## Verification Criteria

{Specific commands and checks to verify this phase is complete:}

- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] {Specific behavioral verification}
```

## Content Guidelines

- **Code examples must be concrete** — show actual code based on codebase research, not pseudocode
- **File paths must be accurate** — verify paths exist before referencing them
- **Reference analogous implementations** — link to existing code that follows the same pattern
- **Include relevant knowledge/instructions/skills** — so the Implementer loads the right conventions
- **Verification criteria must be actionable** — specific commands or checks, not vague descriptions
- **Each phase should be independently verifiable** — the build/tests should pass after each phase

## Avoid

- Vague instructions like "implement the feature" without specifics
- Referencing files that don't exist
- Phases that can't be independently verified
- Combining unrelated changes in a single phase
- Phases that depend on uncommitted changes from a future phase
