---
name: frontend-design
description: "Apply the Kinetic Ionization design system when building or restyling Angular components. Use when: creating new UI components, redesigning existing views, writing SCSS styles, configuring Angular Material themes, choosing colors, typography, elevation, or layout patterns. Keywords: design, style, CSS, SCSS, theme, color, typography, component, UI, layout, button, card, input, modal."
argument-hint: "Describe the component or page to design/restyle"
---

# Kinetic Ionization — Frontend Design Skill

## Creative North Star: "The Neon Brutalist"

A high-fidelity, high-energy environment built for power users. Sharp architectural precision over friendly SaaS softness. Intentional asymmetry and tonal depth. This is a digital cockpit, not a template.

## When to Use

- Creating new Angular components with styling
- Restyling or redesigning existing components
- Editing SCSS files or CSS custom properties
- Configuring Angular Material theme tokens
- Choosing colors, spacing, typography, or elevation
- Building modals, HUDs, toolbars, panels, or cards

## Procedure

### 1. Identify the Component Context

Determine what you're building:
- **Structural surface** (panel, sidebar, workspace) → Use tonal layering from [design tokens](./references/design-tokens.md)
- **Interactive element** (button, input, chip) → Use [component patterns](./references/component-patterns.md)
- **Floating overlay** (modal, HUD, tooltip) → Use glass rules from [design tokens](./references/design-tokens.md#glass-surfaces)

### 2. Apply the Core Rules

These rules are **non-negotiable**. Violating any of them breaks the design system.

| Rule | Requirement |
|------|------------|
| **Zero Radius** | All `border-radius: 0px`. No exceptions. No 2px, no 4px, nothing. |
| **No-Line Rule** | Never use `1px solid` borders for structural sectioning. Use tonal surface transitions instead. |
| **No Gray Shadows** | Shadows use `primary` or `secondary` token colors at 10% opacity with 30px blur. |
| **No Opaque Borders** | Borders must be ≤15% opacity using `outline-variant` ("Ghost Borders"). |
| **No Border-Radius on Material overrides** | Override Angular Material's default `border-radius` to `0px` on all components. |
| **No Success Green** | Use `primary` (Cyan) for success/positive states. |

### 3. Apply Surface Hierarchy

Surfaces create depth through **tonal layering**, not elevation.

```
Base void:          var(--sys-surface)              #10131c
Sidebar/nav:        var(--sys-surface-container-low)     #181c24
Active workspace:   var(--sys-surface-container-highest) #31353e
Sunken inner panel: var(--sys-surface-container-lowest)  #12151e
Floating elements:  var(--sys-surface-bright)            #3a3e47
```

**Nesting rule:** Inner containers sit one tier *lower* than their parent (sunken effect).

### 4. Apply Typography

- **Display / Headlines:** `'Space Grotesk'` — sharp terminals match 0px corners
- **Body / UI text:** `'Inter'` — extreme legibility at all sizes
- **Labels / Buttons:** `'Space Grotesk'`, ALL CAPS, `letter-spacing: 0.1em`

See full type scale in [design tokens](./references/design-tokens.md#typography).

### 5. Apply Component Patterns

Reference [component patterns](./references/component-patterns.md) for specific element styling:
- Buttons (primary solid cyan, secondary ghost, tertiary text)
- Input fields (bottom-border-only active state)
- Chips & tags (ghost border, no fill)
- Cards & lists (no dividers, whitespace separation, hover glow)
- Modals & HUDs (glass effect, backdrop blur)

### 6. Angular Material Theme Integration

The app uses `@angular/material` v21+ with `mat.define-theme()`. When updating the theme in `styles.scss`:

```scss
@use '@angular/material' as mat;

$theme: mat.define-theme((
  color: (
    theme-type: dark,
    primary: mat.$cyan-palette,
  ),
  typography: (
    brand-family: 'Space Grotesk',
    plain-family: 'Inter',
    bold-weight: 700,
  ),
  density: (
    scale: -1
  )
));
```

Override Material defaults globally to enforce 0px radius and design tokens:

```scss
html {
  @include mat.all-component-themes($theme);

  // Global 0px radius enforcement
  --mdc-filled-button-container-shape: 0px;
  --mdc-outlined-button-container-shape: 0px;
  --mdc-text-button-container-shape: 0px;
  --mat-card-container-shape: 0px;
  --mdc-dialog-container-shape: 0px;
  --mdc-filled-text-field-container-shape: 0px;
  --mdc-outlined-text-field-container-shape: 0px;
  --mat-chip-container-shape-radius: 0px;
  --mat-menu-container-shape: 0px;
  --mat-select-container-shape: 0px;
}
```

### 7. Validate Against Do's and Don'ts

**Do:**
- Use extreme whitespace — let `surface` (#10131c) breathe
- Overlap glass containers for "Electric Storm" depth
- Use `primary-fixed-dim` for icons (active energy sources)

**Don't:**
- Use any corner radius (even 2px breaks the system)
- Use 100% opaque borders (destroys atmospheric feel)
- Use standard green for success states (use Cyan)
- Use `box-shadow` with gray/black (use colored glow shadows)
- Use `<hr>` or divider lines in lists (use 8–12px vertical whitespace)

## File Conventions

| File Type | Convention |
|-----------|-----------|
| Global styles | `src/styles.scss` — theme definition, Material overrides, CSS custom properties |
| Component SCSS | Use `:host` for component-scoped custom properties |
| Design tokens | Reference `var(--sys-*)` tokens; never hardcode hex values in components |
| Font loading | Load Space Grotesk + Inter via `<link>` in `index.html` or `@font-face` |

### 8. Apply Animation Patterns

Use sharp easing curves (`cubic-bezier(0.4, 0, 0.2, 1)`) — no bouncy or elastic timing. Enter transitions are slower than exits (300ms in, 200ms out). Ambient glow animations use opacity/shadow only. See [animation patterns](./references/animation-patterns.md) for Angular animation triggers and CSS transition recipes.

## Reference Files

- [Design Tokens](./references/design-tokens.md) — Full color palette, typography scale, spacing, elevation
- [Component Patterns](./references/component-patterns.md) — Specific component styling recipes
- [Animation Patterns](./references/animation-patterns.md) — Easing curves, Angular animations, CSS transitions, motion blur
