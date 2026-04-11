# Phase 6: Board Canvas Component

**Objective**: Convert the largest component (~1800 lines) to use `inject()`, `viewChild()` signal queries, and `takeUntilDestroyed()`.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` — constructor → `inject()`, `@ViewChild` → `viewChild.required()`, `destroy$` → `takeUntilDestroyed()`

## Context

The board canvas component is the heart of the application — a ~1800-line imperative HTML Canvas rendering and interaction engine. It has:
- 2 `@ViewChild` decorators (canvas, minimap) with `{ static: true }`
- Constructor injection of 5 services
- `destroy$` subject with 3 subscriptions
- 2 `@HostListener` decorators (`document:keydown`, `window:resize`) — these stay, no signal equivalent
- Mouse event handlers (`mousemove`, `mouseup`, `mousedown`) bound via template events, not HostListeners
- 30+ private state properties for drag/resize/pan/zoom interactions

This component has no `@Input()` or `@Output()` decorators. The changes are limited to DI, ViewChild, and cleanup patterns. Due to the file's size, changes must be surgical and precise.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `inject()`, `viewChild()`, `takeUntilDestroyed()` patterns

## Implementation Steps

### Step 1: Convert constructor injection to `inject()`

**Before:**
```typescript
constructor(
  private dialog: MatDialog,
  private canvasService: BoardCanvasService,
  private boardsHub: BoardsSignalRService,
  private themeService: ThemeService,
  private userService: UserService
) { }
```

**After:**
```typescript
private dialog = inject(MatDialog);
private canvasService = inject(BoardCanvasService);
private boardsHub = inject(BoardsSignalRService);
private themeService = inject(ThemeService);
private userService = inject(UserService);
```

Remove the empty constructor.

### Step 2: Convert `@ViewChild` to `viewChild.required()`

**Before:**
```typescript
@ViewChild('canvas', { static: true })
public canvas!: ElementRef<HTMLCanvasElement>;

@ViewChild('minimap', { static: true })
public minimap!: ElementRef<HTMLCanvasElement>;
```

**After:**
```typescript
readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
readonly minimap = viewChild.required<ElementRef<HTMLCanvasElement>>('minimap');
```

**Critical**: Both ViewChildren use `{ static: true }`, meaning they're resolved before `ngOnInit` with the decorator approach. Signal-based `viewChild.required()` does **not** support `{ static: true }` — it always resolves after first change detection (equivalent to `static: false`). However, this component accesses the canvas and minimap via `ngAfterViewInit()` and later event handlers, where the signal will already be resolved. The behavioral change from `static: true` to effectively `static: false` is safe here because no code reads these ViewChildren before `ngAfterViewInit()`.

### Step 3: Update all `this.canvas` and `this.minimap` references

This is the most impactful change in this phase. Every reference to `this.canvas` and `this.minimap` must add `()` to read the signal.

Common patterns to update:
```typescript
// Before
this.canvas.nativeElement
this.minimap.nativeElement
const rect = this.minimap.nativeElement.getBoundingClientRect();
this.ctx = this.canvas.nativeElement.getContext('2d')!;

// After
this.canvas().nativeElement
this.minimap().nativeElement
const rect = this.minimap().nativeElement.getBoundingClientRect();
this.ctx = this.canvas().nativeElement.getContext('2d')!;
```

**Search and replace strategy**: Find all occurrences of `this.canvas.` and `this.minimap.` in the file and replace with `this.canvas().` and `this.minimap().` respectively. Also check for bare `this.canvas` or `this.minimap` usages (e.g., passed as arguments).

Estimated occurrences based on canvas component patterns:
- `this.canvas.nativeElement` — likely 10-20 occurrences (resize, context, getBoundingClientRect, width/height)
- `this.minimap.nativeElement` — likely 5-10 occurrences

### Step 4: Replace `destroy$` with `takeUntilDestroyed()`

**Before:**
```typescript
private destroy$ = new Subject<void>();

ngOnInit(): void {
  this.canvasService.canvasUpdated$.pipe(takeUntil(this.destroy$)).subscribe(() => {
    this.draw();
  });

  this.canvasService.canvasImageDownloaded$.pipe(takeUntil(this.destroy$)).subscribe(() => {
    this.exportBoardAsImage();
  });

  this.themeService.theme$.pipe(takeUntil(this.destroy$)).subscribe(() => {
    this.draw();
  });
}

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}
```

**After:**
```typescript
private destroyRef = inject(DestroyRef);

ngOnInit(): void {
  this.canvasService.canvasUpdated$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(() => {
    this.draw();
  });

  this.canvasService.canvasImageDownloaded$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(() => {
    this.exportBoardAsImage();
  });

  this.themeService.theme$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(() => {
    this.draw();
  });
}
```

Since subscriptions are in `ngOnInit()` (not constructor), use explicit `DestroyRef`. Remove `destroy$`, `ngOnDestroy()`, and `OnDestroy` interface.

**Wait**: Check if `ngOnDestroy()` has any other cleanup logic beyond `destroy$`. If it only has `destroy$.next()` and `destroy$.complete()`, remove the entire method and `OnDestroy` interface. If it has other cleanup, keep the method and interface.

### Step 5: Update imports

```typescript
// Before
import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';

// After
import { AfterViewInit, Component, DestroyRef, ElementRef, HostListener, OnInit, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
```

Remove `OnDestroy` only if `ngOnDestroy()` is fully removed. Remove `ViewChild` (replaced by `viewChild` function). Remove `Subject` if no longer used. Replace `takeUntil` with `takeUntilDestroyed`.

### Step 6: Verify HostListeners are unchanged

The 2 `@HostListener` decorators remain as-is — there is no signal equivalent:
```typescript
@HostListener('document:keydown', ['$event'])
@HostListener('window:resize')
```

Mouse events (`mousemove`, `mouseup`, `mousedown`) are bound via template event bindings, not HostListeners — they also require no changes.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@ViewChild` decorators remain in `board-canvas.component.ts`
- [ ] No `destroy$` subject in `board-canvas.component.ts`
- [ ] No constructor parameters in `BoardCanvasComponent`
- [ ] All `this.canvas.nativeElement` references updated to `this.canvas().nativeElement`
- [ ] All `this.minimap.nativeElement` references updated to `this.minimap().nativeElement`
- [ ] Canvas rendering still works (draw, resize, zoom, pan)
- [ ] Minimap rendering still works
- [ ] 2 `@HostListener` decorators unchanged
