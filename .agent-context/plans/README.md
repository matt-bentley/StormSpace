# Implementation Plans

This directory holds phased implementation plans created by the Planner agent during agentic workflow execution.

## Purpose

When the Orchestrator invokes the Planner, it generates a master plan and per-phase detail files here. These plans are then reviewed by the Refiner and executed phase-by-phase by the Implementer.

## Format

Plan files follow the format defined in `.github/instructions/agent-plans.instructions.md`.

## Directory Structure

Each task gets its own subdirectory:

```
plans/
  {task-slug}/
    plan.md              # Master plan with overview and phase summary
    phase-1-{name}.md    # Detailed plan for phase 1
    phase-2-{name}.md    # Detailed plan for phase 2
    ...
```

## Naming Convention

- Directory: `{task-slug}/` — kebab-case summary of the task
- Master plan: `plan.md`
- Phase files: `phase-{N}-{name}.md` where `{name}` is a kebab-case phase title
