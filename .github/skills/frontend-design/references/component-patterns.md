# Component Patterns — Kinetic Ionization

Recipes for styling specific component types. All patterns follow the core rules: 0px radius, no gray shadows, no opaque borders.

---

## Buttons

### Primary Button

Solid neon cyan. Use for main CTAs and confirmations.

```scss
.btn-primary {
  background: var(--sys-primary-container);
  color: var(--sys-on-primary);
  font-family: var(--font-display);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  border: none;
  border-radius: 0px;
  padding: var(--space-3) var(--space-6);
  cursor: pointer;
  box-shadow: var(--shadow-glow-primary);
  transition: box-shadow 0.2s ease;

  &:hover {
    box-shadow: var(--shadow-glow-primary-elevated);
  }
}
```

### Secondary Button

Ghost border on dark surface.

```scss
.btn-secondary {
  background: var(--sys-surface-container-high);
  color: var(--sys-secondary);
  font-family: var(--font-display);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  border: 1px solid rgba(224, 64, 251, 0.15); // secondary ghost border
  border-radius: 0px;
  padding: var(--space-3) var(--space-6);
  cursor: pointer;

  &:hover {
    background: var(--sys-surface-container-highest);
    box-shadow: var(--shadow-glow-secondary);
  }
}
```

### Tertiary Button

Text-only. Underline on hover.

```scss
.btn-tertiary {
  background: transparent;
  color: var(--sys-secondary);
  font-family: var(--font-display);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  border: none;
  border-radius: 0px;
  padding: var(--space-2) var(--space-4);
  cursor: pointer;
  text-decoration: none;

  &:hover {
    text-decoration: underline;
  }
}
```

### Angular Material Button Override

```scss
// Apply to mat-raised-button, mat-flat-button, mat-stroked-button
.mat-mdc-raised-button,
.mat-mdc-unelevated-button {
  --mdc-filled-button-container-shape: 0px;
  font-family: var(--font-display) !important;
  letter-spacing: 0.1em !important;
  text-transform: uppercase;
}

.mat-mdc-outlined-button {
  --mdc-outlined-button-container-shape: 0px;
  border-color: rgba(68, 71, 78, 0.15) !important;
}
```

---

## Input Fields

Bottom-border-only active state. No full box borders.

```scss
.input-field {
  background: var(--sys-surface-container-lowest);
  color: var(--sys-on-surface);
  font-family: var(--font-body);
  font-size: 14px;
  border: none;
  border-bottom: 1px solid rgba(68, 71, 78, 0.15); // ghost border
  border-radius: 0px;
  padding: var(--space-3) var(--space-4);
  outline: none;
  transition: border-color 0.2s ease;

  &:focus {
    border-bottom-color: var(--sys-primary-container); // cyan active state
  }

  &::placeholder {
    color: var(--sys-on-surface-variant);
  }
}
```

### Angular Material Input Override

```scss
.mat-mdc-form-field {
  --mdc-filled-text-field-container-shape: 0px;
  --mdc-filled-text-field-container-color: var(--sys-surface-container-lowest);
  --mdc-filled-text-field-focus-active-indicator-color: var(--sys-primary-container);
  --mdc-filled-text-field-label-text-font: var(--font-body);
  --mdc-filled-text-field-input-text-font: var(--font-body);
}
```

---

## Chips & Tags

No fill. Ghost border only. Technical label typography.

```scss
.chip {
  display: inline-flex;
  align-items: center;
  padding: var(--space-1) var(--space-3);
  border: 1px solid rgba(124, 77, 255, 0.15); // secondary ghost
  border-radius: 0px;
  background: transparent;
  color: var(--sys-secondary);
  font-family: var(--font-display);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

// Tertiary variant
.chip--tertiary {
  border-color: rgba(255, 64, 129, 0.15);
  color: var(--sys-tertiary);
}
```

---

## Cards

No dividers. Tonal separation. Hover glow.

```scss
.card {
  background: var(--sys-surface-container-low);
  padding: var(--space-6);
  border-radius: 0px;
  border: none;

  &:hover {
    background: var(--sys-surface-container-high);
    box-shadow: var(--shadow-glow-primary);
  }
}

// Sunken inner card (sits inside a panel)
.card--sunken {
  background: var(--sys-surface-container-lowest);
}
```

### Angular Material Card Override

```scss
.mat-mdc-card {
  --mat-card-container-shape: 0px;
  --mat-card-container-color: var(--sys-surface-container-low);
  border: none !important;
  box-shadow: none !important;

  &:hover {
    --mat-card-container-color: var(--sys-surface-container-high);
    box-shadow: var(--shadow-glow-primary) !important;
  }
}
```

---

## Lists

No dividers. Whitespace separation. Hover illumination.

```scss
.list-item {
  padding: var(--space-3) var(--space-4);
  margin-bottom: var(--space-2); // 8px gap replaces dividers
  color: var(--sys-on-surface);
  font-family: var(--font-body);
  font-size: 14px;
  transition: background-color 0.15s ease;

  &:hover {
    background-color: var(--sys-surface-container-high); // "lit" effect
  }
}

// Metadata line
.list-item__meta {
  color: var(--sys-on-surface-variant);
  font-size: 12px;
  margin-top: var(--space-1);
}
```

**Forbidden:** `<mat-divider>`, `<hr>`, or `border-bottom` between list items.

---

## Modals & Dialogs (Glass HUD)

```scss
// Angular Material dialog override
.mat-mdc-dialog-surface {
  --mdc-dialog-container-shape: 0px;
  background: rgba(49, 53, 62, 0.40) !important; // surface-variant at 40%
  backdrop-filter: blur(20px) !important;
  -webkit-backdrop-filter: blur(20px) !important;
  border: 1px solid rgba(68, 71, 78, 0.15) !important; // ghost border
  box-shadow: var(--shadow-glow-primary) !important;
}

.mat-mdc-dialog-title {
  font-family: var(--font-display) !important;
  font-weight: 600 !important;
  color: var(--sys-primary) !important;
  letter-spacing: -0.02em !important;
  background: transparent !important; // no gradient header
}

.mat-mdc-dialog-actions {
  border-top: none !important; // no-line rule
  background: transparent !important;
}
```

---

## Sidebar / Navigation Panel

```scss
.sidebar {
  background: var(--sys-surface-container-low);
  width: 260px;
  padding: var(--space-4) 0;
  border-right: none; // no-line rule — tonal transition defines boundary
}

.nav-item {
  padding: var(--space-3) var(--space-6);
  color: var(--sys-on-surface-variant);
  font-family: var(--font-body);
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.15s ease, color 0.15s ease;

  &:hover {
    background: var(--sys-surface-container);
    color: var(--sys-on-surface);
  }

  &.active {
    background: var(--sys-surface-container-highest);
    color: var(--sys-primary);
    box-shadow: inset 2px 0 0 var(--sys-primary-container); // left accent bar
  }
}
```

---

## Toolbar / Floating HUD

```scss
.floating-hud {
  background: rgba(49, 53, 62, 0.40);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  padding: var(--space-2) var(--space-4);
  border: 1px solid rgba(68, 71, 78, 0.15);
  border-radius: 0px;
  box-shadow: var(--shadow-glow-primary);
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
```

---

## Collaborative Cursors (App Specific)

```scss
.cursor-indicator {
  width: 0;
  height: 0;
  border-left: 6px solid transparent;
  border-right: 6px solid transparent;
  border-bottom: 12px solid var(--sys-tertiary); // hot pink
  filter: drop-shadow(0 0 10px rgba(255, 64, 129, 0.6)); // motion blur glow
  position: absolute;
  pointer-events: none;
  z-index: 9999;
}

// Purple variant
.cursor-indicator--secondary {
  border-bottom-color: var(--sys-secondary-container);
  filter: drop-shadow(0 0 10px rgba(224, 64, 251, 0.6));
}
```

---

## Scrollbars

```scss
::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: var(--sys-surface-container-highest);
  border-radius: 0px; // yes, even scrollbars

  &:hover {
    background: var(--sys-on-surface-variant);
  }
}
```
