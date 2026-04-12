# Specification: Clickable Phase Stepper

## Overview
The board's phase stepper UI currently displays the five Event Storming workshop phases (Set the Context → Identify Events → Add Commands & Policies → Define Aggregates → Break It Down) as a read-only progress indicator. Users can only change phases through the board context settings dialog or via AI agents. This feature adds click-to-navigate behaviour to each step in the stepper, allowing users to change the active workshop phase directly by clicking on it.

## Goals
- Allow users to change the board's active phase by clicking on a step in the phase stepper
- Reduce friction in phase navigation — clicking a step should be a single-action shortcut instead of opening the settings dialog
- Broadcast the phase change to all connected clients in real time via SignalR

## User Stories
- As a workshop facilitator, I want to click on a phase in the stepper so that I can quickly jump to any phase without opening the settings dialog
- As a board participant, I want to see the active phase update in real time when another user clicks a phase step

## Functional Requirements
| ID | Requirement | Priority |
|----|------------|----------|
| FR-1 | Each step in the phase stepper must be clickable | Must |
| FR-2 | Clicking a step sets that phase as the active board phase | Must |
| FR-3 | The phase change must broadcast to all connected clients via the existing `BoardContextUpdated` SignalR event | Must |
| FR-4 | The phase change must go through the existing `UpdateBoardContextCommand` and event pipeline so it supports undo/redo | Must |
| FR-5 | Clicking the currently active phase should be a no-op (no duplicate event broadcast) | Must |
| FR-6 | The clicked step should visually update to the active state immediately for the clicking user | Must |

## Non-Functional Requirements
- The click interaction should feel instant — no perceptible delay before the active step updates
- Accessibility: steps should be focusable and activatable via keyboard (Enter/Space)

## Constraints & Assumptions
- The existing `UpdateBoardContextCommand` already supports phase changes alongside domain, session scope, and autonomous mode — the click handler should only change the phase, preserving all other board context values
- The existing `BoardContextUpdatedEvent` and SignalR hub method (`BroadcastBoardContextUpdated`) handle propagation to other clients — no new backend endpoints are needed
- The phase stepper already has `cursor: pointer` CSS and `:hover` styles, so the visual affordance for clickability is partially in place

## Out of Scope
- Adding confirmation dialogs or guards before phase changes
- Restricting which phases can be navigated to (e.g., preventing skipping ahead)
- Changing the visual design of the stepper beyond what is needed for click interaction
- Backend validation of phase transitions

## Open Questions
- Should users be able to jump to any phase freely, or should navigation be restricted (e.g., only move forward, or only to already-visited phases)?
- Should clicking a phase step be available to all board participants, or only certain roles (e.g., board owner/facilitator)?
- When an AI autonomous session is active, should manual phase clicking be disabled to avoid conflicts with agent-driven phase progression?
