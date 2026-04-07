# Design Tokens — Kinetic Ionization

## Color Palette

### Surface Tokens (Tonal Layering)

| Token | Hex | Usage |
|-------|-----|-------|
| `--sys-surface` | `#10131c` | Base void / infinite background |
| `--sys-surface-dim` | `#0d0f16` | Recessed areas, deep backgrounds |
| `--sys-surface-bright` | `#3a3e47` | Floating tool palettes, popovers |
| `--sys-surface-container-lowest` | `#12151e` | Sunken inner panels (one tier below parent) |
| `--sys-surface-container-low` | `#181c24` | Sidebar nav, secondary panels |
| `--sys-surface-container` | `#1e2229` | Default container |
| `--sys-surface-container-high` | `#282c34` | Hover states, elevated cards |
| `--sys-surface-container-highest` | `#31353e` | Active workspace panels, focused areas |
| `--sys-surface-variant` | `#31353e` | Glass overlays (use at 40% opacity) |

### Primary Tokens (Cyan / Electric Discharge)

| Token | Hex | Usage |
|-------|-----|-------|
| `--sys-primary` | `#dbfcff` | Primary text on dark, light end of gradients |
| `--sys-primary-container` | `#00f0ff` | Neon cyan, gradient end, accent glow |
| `--sys-on-primary` | `#10131c` | Text on primary surfaces |
| `--sys-on-primary-container` | `#dbfcff` | Text on primary-container surfaces |
| `--sys-primary-fixed-dim` | `#00c8d6` | Icons — active energy source appearance |

### Secondary Tokens (Electric Pink-Purple / Fuchsia Discharge)

| Token | Hex | Usage |
|-------|-----|-------|
| `--sys-secondary` | `#f0b4ff` | Secondary accent text, labels |
| `--sys-secondary-container` | `#e040fb` | Pink-purple accent surfaces, overlapping glass |
| `--sys-on-secondary` | `#10131c` | Text on secondary surfaces |
| `--sys-on-secondary-container` | `#f8e0ff` | Text on secondary-container |

### Tertiary Tokens (Hot Pink / Signal)

| Token | Hex | Usage |
|-------|-----|-------|
| `--sys-tertiary` | `#ff4081` | Hot pink accents, collaborative cursors |
| `--sys-tertiary-container` | `#ff80ab` | Tertiary surfaces |
| `--sys-on-tertiary` | `#10131c` | Text on tertiary surfaces |

### Text & Icon Tokens

| Token | Hex | Usage |
|-------|-----|-------|
| `--sys-on-surface` | `#e2e2e6` | Primary body text |
| `--sys-on-surface-variant` | `#a0a4ad` | Secondary/metadata text |
| `--sys-outline` | `#8a8e96` | High-contrast outlines (accessibility) |
| `--sys-outline-variant` | `#44474e` | Ghost borders (use at 15% opacity) |

### Semantic States

| State | Token | Note |
|-------|-------|------|
| Success | `--sys-primary-container` (`#00f0ff`) | **No green.** Cyan = positive. |
| Error | `--sys-tertiary` (`#ff4081`) | Hot pink for destructive / error |
| Warning | `#ffab40` | Amber-orange spark |
| Info | `--sys-secondary` (`#f0b4ff`) | Soft pink-purple |

---

## Typography

### Font Stack

```scss
// Display & Headlines
--font-display: 'Space Grotesk', system-ui, sans-serif;

// Body & UI
--font-body: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
```

### Type Scale

| Role | Font | Size | Weight | Letter Spacing | Line Height |
|------|------|------|--------|----------------|-------------|
| `display-lg` | Space Grotesk | 57px | 400 | -0.04em | 64px |
| `display-md` | Space Grotesk | 45px | 400 | -0.04em | 52px |
| `display-sm` | Space Grotesk | 36px | 400 | -0.04em | 44px |
| `headline-lg` | Space Grotesk | 32px | 500 | -0.02em | 40px |
| `headline-md` | Space Grotesk | 28px | 500 | -0.02em | 36px |
| `headline-sm` | Space Grotesk | 24px | 600 | -0.02em | 32px |
| `title-lg` | Space Grotesk | 22px | 500 | 0em | 28px |
| `title-md` | Inter | 16px | 600 | 0.01em | 24px |
| `title-sm` | Inter | 14px | 600 | 0.01em | 20px |
| `body-lg` | Inter | 16px | 400 | 0.03em | 24px |
| `body-md` | Inter | 14px | 400 | 0.02em | 20px |
| `body-sm` | Inter | 12px | 400 | 0.03em | 16px |
| `label-lg` | Space Grotesk | 14px | 700 | 0.1em | 20px |
| `label-md` | Space Grotesk | 12px | 700 | 0.1em | 16px |
| `label-sm` | Space Grotesk | 11px | 700 | 0.1em | 16px |

**Labels are always `text-transform: uppercase`.**

---

## Spacing

Use an 4px base grid. Common spacing values:

| Token | Value | Usage |
|-------|-------|-------|
| `--space-1` | 4px | Tight inline gaps |
| `--space-2` | 8px | List item separation, chip gaps |
| `--space-3` | 12px | List item vertical whitespace (replaces dividers) |
| `--space-4` | 16px | Component internal padding |
| `--space-5` | 20px | Section gutters |
| `--space-6` | 24px | Panel padding |
| `--space-8` | 32px | Major section separation |
| `--space-10` | 40px | Extreme whitespace blocks |

---

## Elevation & Shadows

### Glow Shadows (replace traditional box-shadow)

```scss
// Primary glow — emissive light source effect
--shadow-glow-primary: 0 0 30px rgba(0, 240, 255, 0.10);

// Secondary glow
--shadow-glow-secondary: 0 0 30px rgba(224, 64, 251, 0.10);

// Tertiary glow
--shadow-glow-tertiary: 0 0 30px rgba(255, 64, 129, 0.10);

// Elevated glow (stronger, for focused/active elements)
--shadow-glow-primary-elevated: 0 0 40px rgba(0, 240, 255, 0.18);
```

**Forbidden:** `box-shadow` with gray, black, or any non-token color.

### Glass Surfaces

```scss
// Glass overlay for modals, HUDs, floating panels
.glass {
  background: rgba(49, 53, 62, 0.40); // surface-variant at 40%
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
}
```

### Ghost Borders

```scss
// Ghost border — faint wireframe feel, for input fields and accessibility
.ghost-border {
  border: 1px solid rgba(68, 71, 78, 0.15); // outline-variant at 15%
}
```

---

## Gradients

### Signature Neon Gradient (Primary Actions)

```scss
// "Neon filament" — solid primary for main CTAs
background: var(--sys-primary-container);
color: var(--sys-on-primary);
// Resolves to: background: #00f0ff
```

### Secondary Gradient

```scss
background: linear-gradient(135deg, var(--sys-secondary) 0%, var(--sys-secondary-container) 100%);
```

---

## CSS Custom Properties Block

Paste this into `styles.scss` or a dedicated `_tokens.scss` partial:

```scss
:root {
  // Surfaces
  --sys-surface: #10131c;
  --sys-surface-dim: #0d0f16;
  --sys-surface-bright: #3a3e47;
  --sys-surface-container-lowest: #12151e;
  --sys-surface-container-low: #181c24;
  --sys-surface-container: #1e2229;
  --sys-surface-container-high: #282c34;
  --sys-surface-container-highest: #31353e;
  --sys-surface-variant: #31353e;

  // Primary
  --sys-primary: #dbfcff;
  --sys-primary-container: #00f0ff;
  --sys-on-primary: #10131c;
  --sys-on-primary-container: #dbfcff;
  --sys-primary-fixed-dim: #00c8d6;

  // Secondary
  --sys-secondary: #f0b4ff;
  --sys-secondary-container: #e040fb;
  --sys-on-secondary: #10131c;
  --sys-on-secondary-container: #f8e0ff;

  // Tertiary
  --sys-tertiary: #ff4081;
  --sys-tertiary-container: #ff80ab;
  --sys-on-tertiary: #10131c;

  // Text
  --sys-on-surface: #e2e2e6;
  --sys-on-surface-variant: #a0a4ad;
  --sys-outline: #8a8e96;
  --sys-outline-variant: #44474e;

  // Typography
  --font-display: 'Space Grotesk', system-ui, sans-serif;
  --font-body: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;

  // Spacing
  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 20px;
  --space-6: 24px;
  --space-8: 32px;
  --space-10: 40px;

  // Shadows
  --shadow-glow-primary: 0 0 30px rgba(0, 240, 255, 0.10);
  --shadow-glow-secondary: 0 0 30px rgba(224, 64, 251, 0.10);
  --shadow-glow-tertiary: 0 0 30px rgba(255, 64, 129, 0.10);
  --shadow-glow-primary-elevated: 0 0 40px rgba(0, 240, 255, 0.18);
}
```
