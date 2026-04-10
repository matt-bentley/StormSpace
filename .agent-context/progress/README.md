# Progress Tracking

This directory holds per-task progress tracking files created by the Orchestrator during pipeline execution.

## Purpose

When the Orchestrator runs an agentic workflow, it creates a progress file here to track the status of each pipeline phase. This provides visibility into what has been completed, what is in progress, and what has failed.

## Format

Progress files follow the format defined in `.github/instructions/agent-progress.instructions.md`.

## Naming Convention

Files are named `{task-slug}.md` where `{task-slug}` is a kebab-case summary of the task (e.g., `add-tooltip-board-name.md`).

## Lifecycle

- **Created** by the Orchestrator at pipeline start
- **Updated** after each phase completes or fails
- **Final state** reflects the outcome of the entire pipeline run
