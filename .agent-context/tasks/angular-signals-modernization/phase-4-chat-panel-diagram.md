# Phase 4: AI Chat Panel & Agent Interaction Diagram

**Objective**: Full signal modernization of the two components that use `@Input()`, `@Output()`, and `@ViewChild()` decorators.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.ts` — `@Input` → `input()`, `@Output` → `output()`, `@ViewChild` → `viewChild()`, constructor → `inject()`, `destroy$` → `takeUntilDestroyed()`, remove `OnChanges` → `effect()`
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.html` — update template for signal reads (`boardId()`, `autonomousEnabled()`, `autonomousStatus()`, `userName()`)
- `src/eventstormingboard.client/src/app/board/agent-interaction-diagram/agent-interaction-diagram.component.ts` — `@Input` → `input()`, `@ViewChild` → `viewChild.required()`, remove `OnChanges` → `effect()`
- `src/eventstormingboard.client/src/app/board/agent-interaction-diagram/agent-interaction-diagram.component.html` — update template for signal reads if any direct input references exist

## Context

These are the only components with `@Input()`, `@Output()`, and `@ViewChild()` decorators (aside from `board-canvas` which is Phase 6). The AI chat panel is medium complexity (5 inputs, 2 outputs, 1 ViewChild, 6 subscriptions, `OnChanges`). Note: `MarkdownPipe` is defined inline in the same file — ensure `Pipe` and `PipeTransform` imports are preserved when updating the import list. The agent interaction diagram is simpler (392 lines, 1 input, 1 ViewChild, `OnChanges`).

**Critical**: `AiChatPanelComponent` has a property named `public input = '';` which will shadow the `input` signal function import. This property must be renamed (e.g., to `userInput` or `messageInput`) before converting inputs.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `input()`, `output()`, `viewChild()`, `effect()` patterns

## Implementation Steps

### Step 1: Convert `AiChatPanelComponent` — DI and cleanup pattern

**Rename the `input` property** first to avoid shadowing:
```typescript
// Before
public input = '';
// After  
public messageInput = '';
```

Update template references: `input` → `messageInput` (search template for `input` usage in `[(ngModel)]` and method references).

Convert constructor injection:
```typescript
// Before
constructor(private signalRService: BoardsSignalRService) { }

// After
private signalRService = inject(BoardsSignalRService);
```

### Step 2: Convert `AiChatPanelComponent` — Inputs and Outputs

**Before:**
```typescript
@Input() boardId!: string;
@Input() userName!: string;
@Input() autonomousEnabled = false;
@Input() autonomousStatus?: AutonomousFacilitatorStatus;
@Input() agentConfigurations: AgentConfiguration[] = [];
@Output() closed = new EventEmitter<void>();
@Output() disableAutonomousRequested = new EventEmitter<void>();
@ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;
```

**After:**
```typescript
readonly boardId = input.required<string>();
readonly userName = input.required<string>();
readonly autonomousEnabled = input(false);
readonly autonomousStatus = input<AutonomousFacilitatorStatus | undefined>();
readonly agentConfigurations = input<AgentConfiguration[]>([]);
readonly closed = output<void>();
readonly disableAutonomousRequested = output<void>();
private messagesContainer = viewChild.required<ElementRef<HTMLDivElement>>('messagesContainer');
```

**Import changes:**
```typescript
// Remove
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild, HostListener } from '@angular/core';

// Add (keep Pipe, PipeTransform for the inline MarkdownPipe defined in this file)
import { Component, DestroyRef, ElementRef, HostListener, Pipe, PipeTransform, computed, effect, inject, input, output, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
```

### Step 3: Convert `AiChatPanelComponent` — OnChanges → effect()

The component uses `OnChanges` to rebuild the agent display map when `agentConfigurations` changes:

**Before:**
```typescript
ngOnChanges(changes: SimpleChanges): void {
  if (changes['agentConfigurations']) {
    this.rebuildAgentDisplayMap();
  }
}
```

**After:** Replace with `effect()` in the constructor or as a field:
```typescript
constructor() {
  effect(() => {
    // Reading agentConfigurations() registers the signal dependency
    const configs = this.agentConfigurations();
    this.rebuildAgentDisplayMap(configs);
  });
}
```

Update `rebuildAgentDisplayMap` to accept the configs parameter or read from the signal inside.

### Step 4: Convert `AiChatPanelComponent` — destroy$ → takeUntilDestroyed()

**Before:**
```typescript
private destroy$ = new Subject<void>();

ngOnInit(): void {
  // ... 6 subscriptions with takeUntil(this.destroy$)
}

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}
```

**After:** Keep subscriptions in `ngOnInit()` — they read `this.boardId()` which is a required input signal and is not populated until after construction. Use explicit `DestroyRef`:

```typescript
private destroyRef = inject(DestroyRef);

ngOnInit(): void {
  this.signalRService.getAgentHistory(this.boardId()).then(() => {
    const cached = this.signalRService.getHistoryForBoard(this.boardId());
    // ...
  });

  this.signalRService.agentUserMessage$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(msg => {
    if (msg.boardId && msg.boardId !== this.boardId()) return;
    // ...
  });

  // ... repeat for all 6 subscriptions with takeUntilDestroyed(this.destroyRef)
}
```

**Critical**: `takeUntilDestroyed()` without arguments only works in injection context (constructor). Since these subscriptions are in `ngOnInit()` and they read required input signals (`this.boardId()`), they **must** stay in `ngOnInit()` with `inject(DestroyRef)` passed explicitly. Moving them to the constructor would cause a runtime error because required input signals are not populated until after construction.

Remove `OnDestroy`, `ngOnDestroy()`, `destroy$`. Keep `OnInit` and `ngOnInit()`.

### Step 5: Convert `AiChatPanelComponent` — TS signal reads

Update all TypeScript references to read inputs as signals:
- `this.boardId` → `this.boardId()`
- `this.userName` → `this.userName()`
- `this.autonomousEnabled` → `this.autonomousEnabled()`
- `this.autonomousStatus` → `this.autonomousStatus()`
- `this.agentConfigurations` → `this.agentConfigurations()`
- `this.messagesContainer` → `this.messagesContainer()`
- `this.closed.emit()` — stays the same (output's `emit()` API is identical)
- `this.disableAutonomousRequested.emit()` — stays the same

### Step 6: Convert `AiChatPanelComponent` — Template signal reads

Update template references (in `ai-chat-panel.component.html`):
- `autonomousEnabled` → `autonomousEnabled()`
- `autonomousStatus?.isRunning` → `autonomousStatus()?.isRunning`
- `autonomousStatusLabel` → `autonomousStatusLabel` (keep as-is if it's a getter — but convert the getter to `computed()` if it only depends on signals)

Check if `autonomousStatusLabel` is a getter that reads `this.autonomousEnabled` and `this.autonomousStatus` — if so, convert to `computed()`:
```typescript
readonly autonomousStatusLabel = computed(() => {
  if (!this.autonomousEnabled()) return 'Autonomous Off';
  if (this.autonomousStatus()?.isRunning) return 'Running';
  // ... rest of logic
});
```

### Step 7: Convert `AgentInteractionDiagramComponent`

**Before:**
```typescript
@Input() agents: AgentConfiguration[] = [];
@ViewChild('diagramContainer') containerRef!: ElementRef<HTMLDivElement>;
```

**After:**
```typescript
readonly agents = input<AgentConfiguration[]>([]);
readonly containerRef = viewChild.required<ElementRef<HTMLDivElement>>('diagramContainer');
```

**OnChanges → effect():**

The component uses `OnChanges` to rebuild the graph when `agents` changes. Replace with:
```typescript
constructor() {
  effect(() => {
    const agents = this.agents();
    this.buildGraph(agents);
  });
}
```

**TS references:**
- `this.agents` → `this.agents()` throughout the file
- `this.containerRef` → `this.containerRef()` throughout the file

**Important**: The `recalcSize()` method has an `if (this.containerRef)` guard. After conversion to `viewChild.required()`, this must become `if (this.containerRef())` — checking the signal function itself is always truthy, you must invoke it to get the value.

**Template references:** Update any template references to `agents` → `agents()`.

**Import changes:**
```typescript
// Remove
import { Component, Input, OnChanges, SimpleChanges, ElementRef, ViewChild, AfterViewInit } from '@angular/core';

// Add
import { Component, ElementRef, AfterViewInit, effect, input, viewChild } from '@angular/core';
```

Note: Keep `AfterViewInit` if the component uses `ngAfterViewInit()`.

### Step 8: Update parent template bindings

**Parent: `board.component.html`** — The binding syntax `[boardId]="canvasService.id"` etc. stays the same. Signal inputs accept the same template binding syntax.

**Parent: `agent-config-modal.component.html`** — The binding `[agents]="agents"` stays the same.

No parent template changes needed.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@Input()`, `@Output()`, `@ViewChild()` decorators remain in these 2 components
- [ ] No `EventEmitter` imports in `ai-chat-panel.component.ts`
- [ ] No `OnChanges` interface or `ngOnChanges()` method in either component
- [ ] No `destroy$` subject in `ai-chat-panel.component.ts`
- [ ] `input` property renamed to `messageInput` to avoid shadowing
- [ ] Agent display map rebuilds correctly when `agentConfigurations` signal changes
- [ ] Chat panel resize drag still works (HostListener events unchanged)
