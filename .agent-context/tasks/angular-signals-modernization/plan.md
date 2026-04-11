# Plan: Angular Signals Modernization

**Objective**: Migrate the entire Angular 21 frontend from legacy patterns (decorator-based inputs/outputs, constructor injection, `destroy$` subjects) to modern Angular signal APIs (`input()`, `output()`, `viewChild()`, `inject()`, `takeUntilDestroyed()`, `effect()`).

**Phases**: 5
**Estimated complexity**: Medium

## Context

The Angular frontend (37 files, ~6,000 lines) uses zero modern signal patterns despite Angular 21 being fully signal-ready. The `.github/instructions/angular.instructions.md` already mandates full signal adoption. This task performs a systematic, mechanical migration of all legacy patterns to their signal equivalents.

The codebase has 13 components, 7 services, 14 models, and supporting files. The migration is ordered from foundational (services) to complex (large components), ensuring each phase builds cleanly.

### Scope

**In scope** (mechanical transformations):
- Constructor injection → `inject()` function (all 20+ files)
- `@Input()` / `@Output()` decorators → `input()` / `output()` signal functions (2 components)
- `@ViewChild` / `@ViewChildren` → `viewChild()` / `viewChildren()` (3 components)
- `@Inject(TOKEN)` → `inject(TOKEN)` (6 modal components)
- `destroy$` Subject + `takeUntil()` → `DestroyRef` + `takeUntilDestroyed()` (5 components)
- `EventEmitter` → `output()` (1 component)
- `OnChanges` + `SimpleChanges` → `effect()` (where inputs become signals)
- Simple `observable$.subscribe(v => this.prop = v)` — considered for `toSignal()` but not feasible (properties used with `[(ngModel)]` require mutability)

**Out of scope** (would require extensive template and logic changes):
- Converting all plain component properties to `signal()` — too invasive for a mechanical migration
- Restructuring RxJS Subject-based services to signal-based state — services correctly use Subjects for event streams
- Converting `board.commands.ts` constructors — these are plain classes, not Angular DI
- Adding `computed()` for derived state — requires design decisions beyond mechanical transformation

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | Services — inject() | Convert 6 services from constructor injection to inject() (ThemeService has no DI — no changes) | 6 service files | `npm run build` |
| 2 | Simple Components — inject() | Convert 7 modal components from constructor + @Inject to inject() | 7 component files | `npm run build` |
| 3 | Root & Splash — inject() + takeUntilDestroyed() | Convert AppComponent and SplashComponent: inject(), destroy$ → takeUntilDestroyed() | 2 component files | `npm run build` |
| 4 | Signal I/O Components — input/output/viewChild/effect | Convert AiChatPanelComponent and AgentInteractionDiagramComponent: full signal I/O migration + inject() + takeUntilDestroyed() | 2 component files + 1 template file | `npm run build` |
| 5 | Large Components — inject() + viewChild() + takeUntilDestroyed() | Convert BoardCanvasComponent and BoardComponent: inject(), viewChild(), takeUntilDestroyed() | 2 component files | `npm run build` |

## Dependencies

- Angular 21.2.2 already supports all signal APIs — no package changes needed
- `@angular/core/rxjs-interop` provides `takeUntilDestroyed()` and `toSignal()` — already available in Angular 21
- No backend changes required
- No model file changes required

## Risks

| Risk | Mitigation |
|------|------------|
| `viewChild()` has no `{ static: true }` equivalent — BoardCanvasComponent uses `@ViewChild('canvas', { static: true })` for canvas elements accessed in `ngAfterViewInit` | Signal `viewChild.required()` behaves like `{ static: false }` — it resolves after the view initializes, which is fine for `ngAfterViewInit` access. The existing code only accesses canvas in `ngAfterViewInit → generateCanvas()`, so `viewChild.required()` is safe. Verify canvas init still works. |
| `OnChanges` removal — AiChatPanelComponent and AgentInteractionDiagramComponent use `OnChanges` + `SimpleChanges` | Replace with `effect()` that watches the signal inputs. The effect runs whenever the input signal changes. |
| Large file changes — BoardCanvasComponent (1,976 lines) and BoardComponent (995 lines) | Apply minimal changes (inject + takeUntilDestroyed only). No logic restructuring. Do not attempt toSignal or signal() for local state in these files. |
| `public dialogRef` / `public data` accessed in templates | `inject()` produces instance properties that work identically in templates. No template changes needed for dialog components. |
| `inject()` must be called in constructor or field initializer context | All `inject()` calls go in field initializers. Any constructor logic that used injected services moves to field initializer or `ngOnInit`. |
