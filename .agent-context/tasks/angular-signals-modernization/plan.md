# Plan: Angular Signals Modernization

**Objective**: Modernize the Angular 21 frontend to use signal-based APIs (`input()`, `output()`, `viewChild()`, `computed()`, `toSignal()`, `inject()`, `takeUntilDestroyed()`) replacing legacy decorators and patterns across all 13 components and 7 services.

**Phases**: 6
**Estimated complexity**: High (wide-reaching refactor across entire frontend, ~1800-line canvas component)

## Context

The Angular conventions file (`.github/instructions/angular.instructions.md`) already mandates full signal adoption, but the current codebase uses legacy patterns exclusively: `@Input()`, `@Output()`, `@ViewChild()`, constructor injection, `destroy$` subject pattern, and raw observable subscriptions. This task aligns the codebase with its own documented conventions.

Key constraints:
- Zone.js change detection remains (not zoneless) — signals coexist with zone-based CD
- Services retain RxJS Subjects for SignalR event streaming — only DI modernized in services
- No frontend tests exist — verification is `npm run build` only
- Templates must be updated alongside TS files when inputs become signal functions (read via `()` syntax)

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | Services DI Modernization | Convert constructor injection to `inject()` across all 7 services | 7 service files | `npm run build` |
| 2 | Modal Components | Convert 7 modal/dialog components: constructor → `inject()`, `@Inject()` → `inject()` | 7 component TS files | `npm run build` |
| 3 | App & Splash Components | Convert root and splash components: `inject()`, `destroy$` → `takeUntilDestroyed()`, `toSignal()` for display name | 2 component TS files, 1 template | `npm run build` |
| 4 | AI Chat Panel & Agent Diagram | Full signal conversion: `input()`/`output()`/`viewChild()`, `inject()`, `takeUntilDestroyed()`, `effect()` | 2 component TS files, 2 templates | `npm run build` |
| 5 | Board Component | Convert main orchestrator: `inject()`, `takeUntilDestroyed()` for 20 SignalR subscriptions | 1 component TS file, 1 template | `npm run build` |
| 6 | Board Canvas Component | Convert largest component (~1800 lines): `inject()`, `viewChild()`, `takeUntilDestroyed()` | 1 component TS file | `npm run build` |

## Dependencies

- No package additions required — `@angular/core` and `@angular/core/rxjs-interop` already available in Angular 21
- Imports needed: `inject`, `input`, `output`, `viewChild`, `viewChildren`, `computed`, `signal`, `DestroyRef` from `@angular/core`
- Imports needed: `toSignal`, `takeUntilDestroyed` from `@angular/core/rxjs-interop`

## Risks

1. **Board canvas component size** (~1800 lines) — high risk of merge conflicts if other work is in progress. Phase 6 is isolated to minimize blast radius.
2. **Template signal reads** — converting `@Input() prop` to `input()` requires updating every template reference from `prop` to `prop()`. Missing a template reference causes a runtime error, not a build error if types are loose. Careful template scanning required.
3. **`@Inject(MAT_DIALOG_DATA)` data binding** — dialog data injected via `inject(MAT_DIALOG_DATA)` is used with `[(ngModel)]` bindings (e.g., `data.name`). Since `inject()` returns the same object reference, `[(ngModel)]` bindings continue to work without template changes.
4. **`input` property name collision in AI chat panel** — `AiChatPanelComponent` has a property `public input = '';` which will collide with the `input` signal function import. Must rename property to avoid shadowing.
5. **`OnChanges` removal** — `AiChatPanelComponent` uses `OnChanges` to react to input changes. With signal inputs, this must be replaced with `effect()` or `computed()`.
