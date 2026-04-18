# Specification: Clickable Phase Stepper

## Overview

The phase stepper at the bottom-left of the board canvas currently displays the workshop phase as a read-only visual indicator. Users must open the "Board Context for AI" modal dialog to change the phase. This task makes the phase stepper directly clickable so users can change the workshop phase by clicking on any step in the stepper, with a confirmation prompt before the change is applied.

## Goals

- Allow users to change the workshop phase by clicking directly on a phase step in the stepper
- Provide a confirmation modal before applying the phase change
- Introduce a reusable confirmation modal component following the Kinetic Ionization design system
- Maintain real-time synchronisation of phase changes to all connected board members via SignalR

## User Stories

- As a workshop facilitator, I want to click on a phase in the stepper to change the current phase so that I can quickly navigate the workshop without opening a settings dialog
- As a board member, I want to see a confirmation prompt before the phase changes so that accidental clicks don't disrupt the session

## Functional Requirements

| ID | Requirement | Priority |
|----|------------|----------|
| FR-1 | Clicking a phase step in the stepper opens a confirmation modal asking the user to confirm the phase change | Must |
| FR-2 | The confirmation modal displays the target phase name so the user knows what they are switching to | Must |
| FR-3 | Confirming the modal sets the board phase to the clicked phase | Must |
| FR-4 | Dismissing or cancelling the modal leaves the phase unchanged | Must |
| FR-5 | Clicking the already-active phase does nothing (no modal shown) | Must |
| FR-6 | Users can jump to any phase regardless of the current phase (no sequential restriction) | Must |
| FR-7 | The phase change is broadcast to all connected users in real time via the existing SignalR mechanism | Must |
| FR-8 | The phase change supports undo/redo through the existing command system | Must |
| FR-9 | The confirmation modal is a reusable component that can be used elsewhere in the application | Must |
| FR-10 | The confirmation modal follows the Kinetic Ionization design system styling consistent with other modals | Must |
| FR-11 | The stepper steps visually indicate they are clickable (cursor pointer on hover, which already exists) | Should |

## Non-Functional Requirements

- The confirmation modal must be a standalone Angular component suitable for reuse across the application
- No new backend changes are required — the existing `BoardContextUpdatedEvent` and SignalR broadcast mechanism are reused
- The phase change interaction must feel responsive (modal appears immediately on click)

## Constraints & Assumptions

- The existing `UpdateBoardContextCommand` will be reused, keeping domain, session scope, and autonomous settings unchanged — only the phase field changes
- The existing `BoardsSignalRService.broadcastBoardContextUpdated()` method handles the SignalR broadcast
- Any user on the board can change the phase (no role-based restriction)
- The stepper already has `cursor: pointer` styling on steps, so visual affordance exists
- The reusable confirmation modal accepts configurable title, message, and confirm/cancel button labels

## Out of Scope

- Restricting phase changes to specific user roles
- Sequential-only phase navigation (users can jump to any phase)
- Warning about board state impact when navigating backward
- Changes to the existing Board Context modal dialog

## Open Questions

_None — all questions resolved via GitHub issue #41._
