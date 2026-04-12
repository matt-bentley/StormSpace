# Phase 4: Signal I/O Components — input() + output() + viewChild() + effect()

**Objective**: Convert `AiChatPanelComponent` and `AgentInteractionDiagramComponent` to use signal-based inputs, outputs, view queries, and effects. These are the only components with `@Input()`, `@Output()`, `@ViewChild`, and `OnChanges` patterns.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.ts` — @Input → input(), @Output → output(), @ViewChild → viewChild(), destroy$ → takeUntilDestroyed(), constructor → inject(), OnChanges → effect()
- `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.html` — Update input property references to signal call syntax (e.g., `autonomousEnabled` → `autonomousEnabled()`)
- `src/eventstormingboard.client/src/app/board/agent-interaction-diagram/agent-interaction-diagram.component.ts` — @Input → input(), @ViewChild → viewChild(), OnChanges → effect()

## Context

These two components contain the most diverse set of legacy patterns in the codebase. `AiChatPanelComponent` has 5 `@Input()`, 2 `@Output()`, 1 `@ViewChild`, `destroy$` + `takeUntil`, constructor injection, and `OnChanges`. `AgentInteractionDiagramComponent` has 1 `@Input()`, 1 `@ViewChild`, and `OnChanges`.

**Critical considerations**:
- When `@Input()` becomes `input()`, all internal reads of the property change from `this.prop` to `this.prop()` (call the signal)
- `OnChanges` + `SimpleChanges` cannot detect changes on signal inputs — must be replaced with `effect()`
- Parent templates (`board.component.html`) do NOT need changes — `[propName]="value"` binding syntax works identically with signal inputs
- Signal `output()` supports `.emit()` just like `EventEmitter`, and `(eventName)="handler()"` template syntax is unchanged
- `viewChild()` returns a signal that may be `undefined` until the view initializes — use `viewChild<ElementRef>('ref')` and access via `this.ref()?.nativeElement`
- `effect()` runs immediately when created (unlike `OnChanges` which only fires on binding changes). **Placement rule**: declare `effect()` field initializers AFTER all dependent state fields (e.g., `agentDisplayMap`) to ensure the map is initialized before the first effect run. Alternatively, guard the effect body: `if (!this.agentDisplayMap) return;`

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Signal input/output/viewChild patterns, effect() usage

## Implementation Steps

### Step 1: Convert AiChatPanelComponent

This is the most complex conversion. Work through each pattern:

#### 1a. Update imports

**Remove** from `@angular/core`:
- `EventEmitter`, `Input`, `Output`, `ViewChild`, `OnChanges`, `SimpleChanges`

**Add** to `@angular/core`:
- `inject`, `input`, `output`, `viewChild`, `effect`, `DestroyRef`, `ElementRef`

**Add** new import:
- `import { takeUntilDestroyed } from '@angular/core/rxjs-interop';`

**Remove** from `rxjs`:
- `Subject`, `takeUntil`

#### 1b. Convert class declaration

**Remove** `OnChanges` from `implements` clause. Keep `OnInit`, `OnDestroy` → actually remove `OnDestroy` too since we're removing `destroy$`.

**Current:**
```typescript
export class AiChatPanelComponent implements OnInit, OnDestroy, OnChanges {
```

**Target:**
```typescript
export class AiChatPanelComponent implements OnInit {
```

#### 1c. Convert @Input to input()

**Current:**
```typescript
@Input() boardId!: string;
@Input() userName!: string;
@Input() autonomousEnabled = false;
@Input() autonomousStatus?: AutonomousFacilitatorStatus;
@Input() agentConfigurations: AgentConfiguration[] = [];
```

**Target:**
```typescript
readonly boardId = input.required<string>();
readonly userName = input.required<string>();
readonly autonomousEnabled = input(false);
readonly autonomousStatus = input<AutonomousFacilitatorStatus | undefined>();
readonly agentConfigurations = input<AgentConfiguration[]>([]);
```

#### 1d. Convert @Output to output()

**Current:**
```typescript
@Output() closed = new EventEmitter<void>();
@Output() disableAutonomousRequested = new EventEmitter<void>();
```

**Target:**
```typescript
readonly closed = output<void>();
readonly disableAutonomousRequested = output<void>();
```

Template usage like `(closed)="..."` and `closed.emit()` and `disableAutonomousRequested.emit()` remains unchanged.

#### 1e. Convert @ViewChild to viewChild()

**Current:**
```typescript
@ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;
```

**Target:**
```typescript
private messagesContainer = viewChild<ElementRef<HTMLDivElement>>('messagesContainer');
```

Update the `scrollToBottom()` method:
**Current:**
```typescript
private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
}
```

**Target:**
```typescript
private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
}
```

#### 1f. Convert constructor injection to inject()

**Current:**
```typescript
constructor(private signalRService: BoardsSignalRService) { }
```

**Target:**
```typescript
private signalRService = inject(BoardsSignalRService);
private destroyRef = inject(DestroyRef);
```

Remove the constructor.

#### 1g. Replace OnChanges with effect()

**Current:**
```typescript
ngOnChanges(changes: SimpleChanges): void {
    if (changes['agentConfigurations']) {
      this.rebuildAgentDisplayMap();
    }
}
```

**Target** — declare AFTER `agentDisplayMap` and other dependent state fields (effect runs immediately on creation):
```typescript
private agentConfigEffect = effect(() => {
    this.agentConfigurations(); // track the signal
    this.rebuildAgentDisplayMap();
});
```

Note: `effect()` runs whenever any signal read inside it changes. Reading `this.agentConfigurations()` establishes the tracking dependency.

**Important**: The `rebuildAgentDisplayMap()` method reads `this.agentConfigurations` — update it to call the signal:

**Current:**
```typescript
private rebuildAgentDisplayMap(): void {
    this.agentDisplayMap.clear();
    for (const config of this.agentConfigurations) {
```

**Target:**
```typescript
private rebuildAgentDisplayMap(): void {
    this.agentDisplayMap.clear();
    for (const config of this.agentConfigurations()) {
```

#### 1h. Replace destroy$ + takeUntil with takeUntilDestroyed

**Current (in ngOnInit):**
```typescript
this.signalRService.agentUserMessage$
    .pipe(takeUntil(this.destroy$))
    .subscribe(msg => { ... });
```

**Target:**
```typescript
this.signalRService.agentUserMessage$
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(msg => { ... });
```

Apply this to all 6 subscription pipes in `ngOnInit()`. Remove the `ngOnDestroy()` method entirely.

#### 1i. Update all internal references to inputs

Every place that reads an input property must add `()`:

- `this.boardId` → `this.boardId()` (used in ngOnInit for getAgentHistory, sendAgentMessage, clearHistory, and in subscription filters)
- `this.userName` → `this.userName()` (used in `isOwnMessage()` method)
- `this.autonomousEnabled` → `this.autonomousEnabled()` (used in `autonomousStatusLabel` getter)
- `this.autonomousStatus` → `this.autonomousStatus()` (used in `autonomousStatusLabel` getter)
- `this.agentConfigurations` → `this.agentConfigurations()` (used in `rebuildAgentDisplayMap()`)

Search the file exhaustively for all occurrences. Key locations:
- `ngOnInit()`: `this.boardId` appears multiple times in SignalR calls and event filters
- `send()`: `this.signalRService.sendAgentMessage(this.boardId, text)`
- `clearHistory()`: `this.signalRService.clearAgentHistory(this.boardId)`
- `isOwnMessage()`: `msg.userName === this.userName`
- `autonomousStatusLabel` getter: reads `this.autonomousEnabled` and `this.autonomousStatus`

#### 1j. Update template references

Check `ai-chat-panel.component.html` for direct references to input properties. In the template:
- `autonomousEnabled` → `autonomousEnabled()` (in `@if` conditions and bindings)
- `autonomousStatus?.isRunning` → `autonomousStatus()?.isRunning`
- `disableAutonomousRequested.emit()` — this stays as-is (output signals support `.emit()`)
- `closed.emit()` — stays as-is

Search the template file for all usages of the 5 input names and update to signal call syntax.

### Step 2: Convert AgentInteractionDiagramComponent

#### 2a. Update imports

**Remove** from `@angular/core`: `Input`, `OnChanges`, `SimpleChanges`, `ViewChild`
**Add** to `@angular/core`: `input`, `viewChild`, `effect`, `ElementRef` (keep if not already imported)

#### 2b. Convert class declaration

**Current:**
```typescript
export class AgentInteractionDiagramComponent implements OnChanges, AfterViewInit {
```

**Target:**
```typescript
export class AgentInteractionDiagramComponent implements AfterViewInit {
```

#### 2c. Convert @Input to input()

**Current:**
```typescript
@Input() agents: AgentConfiguration[] = [];
```

**Target:**
```typescript
readonly agents = input<AgentConfiguration[]>([]);
```

#### 2d. Convert @ViewChild to viewChild()

**Current:**
```typescript
@ViewChild('diagramContainer') containerRef!: ElementRef<HTMLDivElement>;
```

**Target:**
```typescript
readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('diagramContainer');
```

#### 2e. Replace OnChanges with effect()

**Current:**
```typescript
ngOnChanges(changes: SimpleChanges): void {
    if (changes['agents']) {
      this.buildGraph();
    }
}
```

**Target:**
```typescript
private agentsEffect = effect(() => {
    this.agents(); // track signal
    this.buildGraph();
});
```

#### 2f. Update all internal references

- `this.agents` → `this.agents()` everywhere (in `buildGraph()`, `layoutNodes()`, `deriveEdges()`)
- `this.containerRef` → `this.containerRef()` (in `recalcSize()`, `ngAfterViewInit()`)

Specific locations:
- `recalcSize()`: `if (this.containerRef)` → `const ref = this.containerRef(); if (ref)`
- `ngAfterViewInit()`: no direct reference to containerRef here (calls `recalcSize()`)
- `buildGraph()`: `if (!this.agents || this.agents.length === 0)` → `const agents = this.agents(); if (!agents || agents.length === 0)`
- `layoutNodes()`: `this.agents.find(...)` → `this.agents().find(...)`
- `deriveEdges()`: `this.agents.filter(...)`, `this.agents.map(...)` → `this.agents().filter(...)`, etc.
- `getNode()`, `edgePath()`, etc. — these don't reference `this.agents` directly

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@Input()`, `@Output()`, `@ViewChild` decorators remain in either component
- [ ] No `EventEmitter` in AiChatPanelComponent
- [ ] No `destroy$` Subject or `ngOnDestroy` in AiChatPanelComponent
- [ ] No `OnChanges` or `SimpleChanges` in either component
- [ ] `effect()` used to react to input signal changes in both components
- [ ] Parent template `board.component.html` — property bindings `[boardId]`, `[userName]`, etc. still compile (no changes needed)
- [ ] `agent-config-modal.component.html` — `[agents]` binding on `<app-agent-interaction-diagram>` still compiles
