# Animation Patterns — Kinetic Ionization

Angular animations and CSS transitions that reinforce the "Neon Brutalist" energy. All motion should feel precise and electric — sharp easing curves, no bouncy or playful timing.

---

## Easing Curves

```scss
:root {
  // Primary easing — sharp precision for UI transitions
  --ease-sharp: cubic-bezier(0.4, 0, 0.2, 1);

  // Enter — elements materialize quickly
  --ease-enter: cubic-bezier(0, 0, 0.2, 1);

  // Exit — elements dismiss crisply
  --ease-exit: cubic-bezier(0.4, 0, 1, 1);

  // Glow pulse — slow breathe for ambient effects
  --ease-glow: cubic-bezier(0.45, 0, 0.55, 1);
}
```

**Forbidden:** `ease-in-out` (too soft), `spring` or bounce effects (breaks Brutalist precision).

---

## Duration Scale

| Token | Value | Usage |
|-------|-------|-------|
| `--duration-instant` | 100ms | Hover color shifts, focus states |
| `--duration-fast` | 150ms | Button press, chip toggle |
| `--duration-normal` | 200ms | Panel transitions, list hover |
| `--duration-slow` | 300ms | Modal enter/exit, sidebar expand |
| `--duration-ambient` | 2000ms | Glow pulse, background ambience |

---

## CSS Transitions

### Hover Glow (Buttons & Cards)

```scss
.btn-primary {
  transition: box-shadow var(--duration-instant) var(--ease-sharp);

  &:hover {
    box-shadow: var(--shadow-glow-primary-elevated);
  }
}
```

### Surface Illumination (List Items)

```scss
.list-item {
  transition: background-color var(--duration-normal) var(--ease-sharp),
              color var(--duration-normal) var(--ease-sharp);

  &:hover {
    background-color: var(--sys-surface-container-high);
  }
}
```

### Ghost Border Activation (Inputs)

```scss
.input-field {
  transition: border-color var(--duration-fast) var(--ease-enter);

  &:focus {
    border-bottom-color: var(--sys-primary-container);
  }
}
```

---

## Angular Animations

Import these in component `animations` arrays.

### Fade In (Modal / Panel Enter)

```typescript
import { trigger, transition, style, animate } from '@angular/animations';

export const fadeIn = trigger('fadeIn', [
  transition(':enter', [
    style({ opacity: 0 }),
    animate('300ms cubic-bezier(0, 0, 0.2, 1)', style({ opacity: 1 })),
  ]),
  transition(':leave', [
    animate('200ms cubic-bezier(0.4, 0, 1, 1)', style({ opacity: 0 })),
  ]),
]);
```

### Slide Up (Toast / Notification)

```typescript
export const slideUp = trigger('slideUp', [
  transition(':enter', [
    style({ transform: 'translateY(16px)', opacity: 0 }),
    animate('300ms cubic-bezier(0, 0, 0.2, 1)', style({
      transform: 'translateY(0)',
      opacity: 1,
    })),
  ]),
  transition(':leave', [
    animate('200ms cubic-bezier(0.4, 0, 1, 1)', style({
      transform: 'translateY(-8px)',
      opacity: 0,
    })),
  ]),
]);
```

### Scale Materialize (Card / HUD Appear)

```typescript
export const scaleMaterialize = trigger('scaleMaterialize', [
  transition(':enter', [
    style({ transform: 'scale(0.95)', opacity: 0 }),
    animate('200ms cubic-bezier(0, 0, 0.2, 1)', style({
      transform: 'scale(1)',
      opacity: 1,
    })),
  ]),
  transition(':leave', [
    animate('150ms cubic-bezier(0.4, 0, 1, 1)', style({
      transform: 'scale(0.97)',
      opacity: 0,
    })),
  ]),
]);
```

### Glow Pulse (Ambient Indicator)

```typescript
export const glowPulse = trigger('glowPulse', [
  transition('off => on', [
    animate('2000ms cubic-bezier(0.45, 0, 0.55, 1)'),
  ]),
]);
```

CSS companion for ambient glow:

```scss
@keyframes glow-breathe {
  0%, 100% {
    box-shadow: 0 0 20px rgba(0, 240, 255, 0.08);
  }
  50% {
    box-shadow: 0 0 40px rgba(0, 240, 255, 0.18);
  }
}

.ambient-glow {
  animation: glow-breathe 3s var(--ease-glow) infinite;
}
```

---

## Collaborative Cursor Motion Blur

```scss
.cursor-indicator {
  transition: transform 50ms linear; // real-time tracking stays snappy
  filter: drop-shadow(0 0 10px rgba(255, 64, 129, 0.6));

  // Motion blur trail via pseudo-element
  &::after {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    border-left: 6px solid transparent;
    border-right: 6px solid transparent;
    border-bottom: 12px solid var(--sys-tertiary);
    opacity: 0.3;
    filter: blur(4px);
    transform: translate(-2px, -2px);
    pointer-events: none;
  }
}
```

---

## Toolbar Reveal (Floating HUD)

```scss
.floating-hud {
  transition: opacity var(--duration-slow) var(--ease-enter),
              transform var(--duration-slow) var(--ease-enter);

  &.hidden {
    opacity: 0;
    transform: translateY(4px);
    pointer-events: none;
  }

  &.visible {
    opacity: 1;
    transform: translateY(0);
  }
}
```

---

## Rules

1. **No bouncy easing** — cubic-bezier only, no spring/elastic.
2. **Enter is slower than exit** — 300ms in, 200ms out. Users need to see what appears; departures should be swift.
3. **Ambient animations must be subtle** — opacity and shadow only, never size or position.
4. **Cursor animations stay under 50ms** — real-time collaboration demands near-instant feedback.
5. **Prefer CSS transitions over Angular animations** for simple state changes (hover, focus). Reserve `@angular/animations` for enter/leave and multi-step sequences.
