# Phase 1: Reusable Confirmation Modal Component

**Objective**: Create a standalone, reusable confirmation modal component following the Kinetic Ionization design system.
**Files to create** (directory `_shared/components/confirm-modal/` is new — create it):
- `src/eventstormingboard.client/src/app/_shared/components/confirm-modal/confirm-modal.component.ts` — New component
- `src/eventstormingboard.client/src/app/_shared/components/confirm-modal/confirm-modal.component.html` — New template
- `src/eventstormingboard.client/src/app/_shared/components/confirm-modal/confirm-modal.component.scss` — New styles

## Context

The application has several existing modal components (`board-context-modal`, `keyboard-shortcuts-modal`, `agent-config-modal`, etc.) but no reusable confirmation dialog. This phase creates a generic confirmation modal that accepts configurable title, message, and button labels. It will live in `_shared/components/` for cross-feature reuse.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Standalone component conventions, `inject()` pattern, `input()`/`output()` signals, Material imports
- `.github/skills/frontend-design/SKILL.md` — Kinetic Ionization design system rules (0px radius, glass HUD surface, colored glow shadows)
- `.github/skills/frontend-design/references/component-patterns.md` — Modal/dialog glass HUD styling, primary/secondary button recipes

## Implementation Steps

### Step 1: Create the ConfirmModalData interface and component

Create the component file with a data interface for configurable title, message, and button labels.

**Code example:**
```typescript
import { Component, inject } from '@angular/core';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface ConfirmModalData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
}

@Component({
  selector: 'app-confirm-modal',
  imports: [MatDialogModule, MatButtonModule],
  templateUrl: './confirm-modal.component.html',
  styleUrl: './confirm-modal.component.scss' // singular per angular.instructions.md convention (existing modals use plural styleUrls — either works, prefer singular for new code)
})
export class ConfirmModalComponent {
  public dialogRef = inject<MatDialogRef<ConfirmModalComponent>>(MatDialogRef);
  public data = inject<ConfirmModalData>(MAT_DIALOG_DATA);

  public onCancel(): void {
    this.dialogRef.close(false);
  }

  public onConfirm(): void {
    this.dialogRef.close(true);
  }
}
```

**Reference**: `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.ts` — Same `inject(MatDialogRef)` and `inject(MAT_DIALOG_DATA)` pattern.

### Step 2: Create the template

Uses `mat-dialog-title`, `mat-dialog-content`, and `mat-dialog-actions` directives, matching the pattern from existing modals. Button labels default to "Cancel" / "Confirm".

**Code example:**
```html
<h1 mat-dialog-title>{{ data.title }}</h1>
<div mat-dialog-content>
  <p class="confirm-message">{{ data.message }}</p>
</div>
<div mat-dialog-actions align="end">
  <button mat-button type="button" (click)="onCancel()">{{ data.cancelLabel || 'CANCEL' }}</button>
  <button mat-flat-button color="primary" type="button" (click)="onConfirm()" cdkFocusInitial>{{ data.confirmLabel || 'CONFIRM' }}</button>
</div>
```

**Reference**: `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.html` — Same `mat-dialog-*` directive structure with Cancel/action button pair.

### Step 3: Create the SCSS styling

Apply Kinetic Ionization design system rules: 0px radius, design token variables, proper typography. The glass HUD surface styling is already applied globally to `.mat-mdc-dialog-surface` in `styles.scss`, so component SCSS only needs message formatting.

**Code example:**
```scss
.confirm-message {
  font-family: var(--font-body);
  font-size: 14px;
  color: var(--sys-on-surface);
  line-height: 1.6;
  margin: 0;
}
```

**Reference**: `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.scss` — Same minimal component SCSS, relies on global Material dialog overrides for glass surface.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npx ng build` passes with no errors
- [ ] Component files exist at `src/eventstormingboard.client/src/app/_shared/components/confirm-modal/`
- [ ] Component exports `ConfirmModalData` interface and `ConfirmModalComponent` class
- [ ] Component uses `inject()` pattern (no constructor injection)
- [ ] Component uses `mat-dialog-title`, `mat-dialog-content`, `mat-dialog-actions` Material directives
- [ ] SCSS uses design token CSS variables (no hardcoded hex values)
