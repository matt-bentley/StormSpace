# Tasks

This directory holds all artefacts for agentic workflow pipeline runs — plans, phase details, and progress tracking — grouped per task.

## Purpose

When the Orchestrator runs an agentic workflow, it creates a task folder here containing the implementation plan, per-phase detail files, and a progress tracker. This keeps all artefacts for a single task co-located for easy reference.

## Format

- Plan files follow the format defined in `.github/instructions/agent-plans.instructions.md`
- Progress files follow the format defined in `.github/instructions/agent-progress.instructions.md`

## Directory Structure

Each task gets its own subdirectory:

```
tasks/
  {task-slug}/
    plan.md              # Master plan with overview and phase summary
    phase-1-{name}.md    # Detailed plan for phase 1
    phase-2-{name}.md    # Detailed plan for phase 2
    ...
    progress.md          # Pipeline progress tracking
```

## Naming Convention

- Directory: `{task-slug}/` — kebab-case summary of the task (max 50 characters)
- Master plan: `plan.md`
- Phase files: `phase-{N}-{name}.md` where `{name}` is a kebab-case phase title
- Progress: `progress.md`

## Lifecycle

- **Created** by the Orchestrator at pipeline start (progress file) and by the Planner (plan + phase files)
- **Reviewed** by the Refiner before implementation begins
- **Updated** after each phase completes or fails
- **Final state** reflects the outcome of the entire pipeline run
