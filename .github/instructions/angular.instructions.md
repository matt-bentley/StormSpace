---
description: "Use when creating or modifying Angular components, services, templates, styles, models, or routing in the frontend. Covers standalone component architecture, RxJS state patterns, Material 21, SCSS theming, and unsubscription."
applyTo: "src/eventstormingboard.client/**"
---

# Angular Code Conventions

Follow the Angular style guide unless explicitly overridden below.

## Language & Project Settings

- Target: Angular 21 with TypeScript strict mode
- Zone.js change detection with event coalescing — not zoneless
- Standalone components only — no NgModules
- Full Angular signal adoption — use `input()`, `output()`, `model()`, `signal()`, `computed()`, `effect()`

## Component Structure

- Always `@Component({ imports: [...] })` — declare every dependency explicitly
- Use `templateUrl` and `styleUrl` for external template/style files
- Default change detection strategy (not OnPush)
- Use `viewChild()` / `viewChildren()` signal queries instead of `@ViewChild` / `@ViewChildren` decorators
- Lifecycle hooks: `OnInit` for initialisation, `OnDestroy` for cleanup, `AfterViewInit` for DOM access

```typescript
@Component({
  selector: 'app-feature',
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule],
  templateUrl: './feature.component.html',
  styleUrl: './feature.component.scss'
})
export class FeatureComponent {
  private boardsService = inject(BoardsService);

  readonly notes = input.required<NoteDto[]>();
  readonly noteSelected = output<string>();

  readonly filteredNotes = computed(() =>
    this.notes().filter(n => n.type === 'event')
  );
}
```

## Dependency Injection

- Use the `inject()` function for all dependency injection — do not use constructor injection
- Inject tokens directly: `inject(MAT_DIALOG_DATA)`, `inject(ActivatedRoute)`

```typescript
export class BoardComponent {
  private boardsService = inject(BoardsService);
  private route = inject(ActivatedRoute);
  private dialogData = inject<BoardDto>(MAT_DIALOG_DATA);
}
```

## Services & State Management

- `@Injectable({ providedIn: 'root' })` for all services
- RxJS Subject-based event streaming for SignalR events (`Subject`, `ReplaySubject`)
- Use signals (`signal()`, `computed()`) for local component state and derived values
- Expose observables via `$`-suffixed getters returning `.asObservable()` where RxJS is still used
- Convert observables to signals with `toSignal()` in components when practical

```typescript
@Injectable({ providedIn: 'root' })
export class UserService {
  private userName$ = new BehaviorSubject<string>('');
  get displayName$() { return this.userName$.asObservable(); }
}

// In a component — convert to signal for template use
export class HeaderComponent {
  private userService = inject(UserService);
  readonly displayName = toSignal(this.userService.displayName$, { initialValue: '' });
}
```

## Unsubscription

- Prefer `toSignal()` (auto-unsubscribes) or the `async` pipe in templates — both handle cleanup automatically
- When manual subscriptions are necessary, use the `DestroyRef` / `takeUntilDestroyed()` pattern instead of a hand-rolled `destroy$` subject
- Legacy code uses `destroy$` + `takeUntil()` — refactor to `takeUntilDestroyed()` when touching those components

```typescript
// Preferred: toSignal (auto-cleanup)
readonly notes = toSignal(this.boardsService.notes$, { initialValue: [] });

// Preferred: async pipe in template
// <div>{{ userName$ | async }}</div>

// When manual subscription is needed:
private destroyRef = inject(DestroyRef);

ngOnInit(): void {
  this.boardsService.notes$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(notes => this.processNotes(notes));
}
```

## Templates

- Use built-in control flow: `@if`, `@for`, `@switch` — not structural directives (`*ngIf`, `*ngFor`)
- Always include `track` in `@for` loops — use `track $index` when items may have duplicate values
- Use `[(ngModel)]` for two-way binding on form inputs
- Dynamic styles via `[style.property]` or `[ngStyle]`

```html
@if (isLoading()) {
  <mat-spinner />
} @else {
  @for (note of notes(); track note.id) {
    <app-note [note]="note" />
  }
}
```

## Inputs, Outputs & Model Signals

- Use `input()` / `input.required()` instead of `@Input()` decorator
- Use `output()` instead of `@Output()` + `EventEmitter`
- Use `model()` for two-way-bound signals
- Use `computed()` for derived state, `effect()` for side effects

```typescript
// Inputs
readonly boardId = input.required<string>();
readonly title = input<string>('Untitled');

// Outputs
readonly noteSelected = output<string>();

// Two-way binding
readonly isOpen = model(false);  // parent: <child [(isOpen)]="panelOpen" />

// Derived state
readonly noteCount = computed(() => this.notes().length);
```

## Models & Types

- Define interfaces and types in `_shared/models/{name}.model.ts`
- Use `interface` for data shapes, `type` for unions and aliases
- Name DTOs as `{Entity}Dto`, creation inputs as `{Entity}CreateDto`
- Helper functions live alongside their model file (e.g., `getNoteColor()` in `note.model.ts`)

```typescript
export type NoteType = 'event' | 'command' | 'aggregate' | 'user' | 'policy';

export interface NoteDto {
  id: string;
  text: string;
  type: NoteType;
}
```

## Pipes

- Custom pipes use `{Name}Pipe` class naming and `standalone: true`
- One pipe per file, named `{name}.pipe.ts`

## Routing

- Define routes in `app.routes.ts` as a flat `Routes` array
- Use `canActivate` guards for auth-protected routes
- Wildcard route `**` redirects to root

## Styling (SCSS)

- One `.scss` file per component
- Use CSS custom properties from the Kinetic Ionisation design system (`--sys-surface`, `--sys-primary`, etc.)
- 4px spacing grid: `--space-1` through `--space-8`
- Typography: `--font-display` (Space Grotesk) for headings, `--font-body` (Inter) for body text
- BEM-like class naming: `.block`, `.block-element`, `.block.modifier`
- Glassmorphism via `backdrop-filter: blur(16px)` with `-webkit-` prefix
- Dark/light mode via `data-theme` attribute on `<html>`

## Angular Material 21

- Import standalone Material modules per component (e.g., `MatButtonModule`, `MatIconModule`)
- Use Material components: buttons, icons, tooltips, dialogs, form fields, inputs, selects
- Register custom SVG icons via `MatIconRegistry`
- Dialog data injected with `inject(MAT_DIALOG_DATA)`

## Import Ordering

Group imports in this order, separated by blank lines:

1. Angular core (`@angular/core`, `@angular/router`, etc.)
2. RxJS (`rxjs`, `rxjs/operators`)
3. Angular Material (`@angular/material/*`)
4. Third-party libraries (`uuid`, `@azure/msal-angular`)
5. App services and models (`../_shared/services/`, `../_shared/models/`)
6. Local feature components

## File Naming

| Type | Pattern | Example |
|------|---------|---------|
| Component | `{name}.component.ts` | `board.component.ts` |
| Service | `{name}.service.ts` | `boards.service.ts` |
| Model | `{name}.model.ts` | `board.model.ts` |
| Pipe | `{name}.pipe.ts` | `markdown.pipe.ts` |
| Commands | `{name}.commands.ts` | `board.commands.ts` |
| Config | `app.config.ts`, `app.routes.ts` | — |

Component selectors use `app-{feature-name}` prefix.

## Testing

Tests are not created for Angular components by default in this project.