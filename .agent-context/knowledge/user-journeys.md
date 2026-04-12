# User Journeys

User journeys for StormSpace, documented for automated browser testing by AI agents using `playwright-cli` (see `.github/skills/playwright-cli/SKILL.md`). All interactions use `playwright-cli` commands via terminal execution.

## Routing & Navigation

| Route | Component | Description |
|-------|-----------|-------------|
| `/` | `SplashComponent` | Landing page — create or join a board |
| `/boards/:id` | `BoardComponent` | Main board view — canvas, toolbar, AI chat |
| `**` | Redirect to `/` | Fallback |

Direct URL navigation to `/boards/{id}` works and auto-joins the board. Users can share board links directly.

The **STORMSPACE logo** in the nav bar is clickable and navigates back to the splash page (`/`) from any board page.

## Page Reference: Splash Page (`/`)

### Layout

| Area | Description | Key Selector |
|------|-------------|--------------|
| Nav bar | "STORMSPACE" logo (left), settings gear + user avatar (right) | `navigation` role |
| Hero | Title, description, NEW BOARD + JOIN BOARD buttons | `button "NEW BOARD"`, `button "JOIN BOARD"` |
| Features | Three feature cards (AI Facilitation, Real-Time Collaboration, Structured Phases) | — |
| Display name | Text input for setting username | `textbox "Enter your name"` |
| Stats | 5 Phases, 8 Note Types, 4 AI Agents, Real-Time Collaboration | — |

### Key Elements

| Element | Selector Strategy | Notes |
|---------|-------------------|-------|
| NEW BOARD button | `button "NEW BOARD"` | Opens Create Board dialog |
| JOIN BOARD button | `button "JOIN BOARD"` | Opens Select Board dialog |
| Display name input | `textbox "Enter your name"` | Pre-filled if previously set; persisted locally |
| Settings gear | Icon `settings` in nav bar | Opens appearance popover |
| User avatar | Circular element with initial letter in nav bar | — |

---

## Journey 1: Create a Board

**Precondition**: On splash page (`/`)

> **⚠ Display name required**: `createNewBoard()` is gated by `if (this.userName)`. If the display name field is empty, clicking NEW BOARD does nothing — no error, no disabled state, no toast. Always fill the display name first. Skip step 0 if the name is already pre-filled (persisted from a previous session — the user avatar will show an initial letter instead of a `person` icon).

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------||
| 0 | Fill display name | `playwright-cli fill <ref of "Enter your name" textbox> "Tester"` then `playwright-cli press Tab` | User avatar in nav bar shows initial letter (e.g. "T") instead of `person` icon |
| 1 | Click NEW BOARD | `playwright-cli click <ref of "NEW BOARD" button>` | "Create Storm Board" dialog opens |
| 2 | Type board name | `playwright-cli fill <ref of "Board Name" textbox> "My Board"` | Text appears in input; Create button becomes enabled |
| 3 | *(Optional)* Expand board context | `playwright-cli click <ref of "Optional Board Context for AI">` | Domain and Session Scope fields appear |
| 4 | *(Optional)* Fill Domain | `playwright-cli fill <ref of "Domain" textbox> "eCommerce marketplace"` | Domain text entered |
| 5 | *(Optional)* Fill Session Scope | `playwright-cli fill <ref of "Session Scope" textbox> "Order fulfillment flow"` | Session scope text entered |
| 6 | Click Create | `playwright-cli click <ref of "Create" button>` | Navigates to `/boards/{id}`, board page loads |

### Verification

After navigating, run `playwright-cli snapshot` and verify:
- URL pattern: `/boards/{guid}`
- Board name input in top bar shows entered name: `textbox "Enter board name"`
- Status bar shows "0 NOTES" and "0 CONNECTIONS"
- "LIVE" indicator visible in bottom-right status bar
- Phase stepper visible at bottom with 5 phases

### Dialog Structure

```
dialog "Create Storm Board"
├── heading "Create Storm Board"
├── textbox "Board Name" (required)
├── group: "Optional Board Context for AI" (expandable)
│   ├── textbox "Domain" (placeholder: business domain description)
│   └── textbox "Session Scope" (placeholder: session focus)
├── Warning text: "Warning! Storm boards self-destruct in 1 hour if not used."
├── button "Cancel"
└── button "Create" (disabled until name entered)
```

**Note**: When the Board Context section is expanded, an additional paragraph appears: *"Default AI agents will be configured automatically. You can customize them from the board."*

---

## Journey 2: Join an Existing Board

**Precondition**: On splash page (`/`), at least one board exists

> **⚠ Display name required**: Same as Journey 1 — `selectExistingBoard()` is also gated by `if (this.userName)`. Fill display name first if not already set.
>
> **⚠ JOIN BOARD disabled when no boards exist**: The JOIN BOARD button is `[disabled]` when there are no existing boards. Ensure at least one board has been created first (e.g. run Journey 1 beforehand).

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------||
| 0 | Fill display name (if not pre-filled) | `playwright-cli fill <ref of "Enter your name" textbox> "Tester"` then `playwright-cli press Tab` | User avatar shows initial letter |
| 1 | Click JOIN BOARD | `playwright-cli click <ref of "JOIN BOARD" button>` | "Select a Board" dialog opens |
| 2 | Click Board Name dropdown | `playwright-cli click <ref of "Board Name" combobox>` | Dropdown expands showing available boards |
| 3 | Select a board | `playwright-cli click <ref of board option>` | Board selected; Select button becomes enabled |
| 4 | Click Select | `playwright-cli click <ref of "Select" button>` | Navigates to `/boards/{id}` |

### Alternative: Direct URL

Navigate directly via `playwright-cli goto https://localhost:51710/boards/{board-id}` — auto-joins and loads the board.

### Dialog Structure

```
dialog "Select a Board"
├── heading "Select a Board"
├── combobox "Board Name" (mat-select dropdown)
│   └── listbox → option items (one per existing board)
├── button "Cancel"
└── button "Select" (disabled until board selected)
```

---

## Page Reference: Board Page (`/boards/:id`)

### Layout Zones

| Zone | Position | Description |
|------|----------|-------------|
| **Top nav** | Top | STORMSPACE logo, settings gear, user avatar |
| **Top bar** | Below nav, floating | Board name, control buttons, user presence, agent presence |
| **Left toolbar** | Left side, floating vertical | Tool selection (SELECT, DRAW, PAN) + note creation (EVENT, CMD, etc.) + HELP/AI |
| **Canvas** | Centre, full area | HTML Canvas for board drawing (`canvas.storming-canvas`) |
| **Minimap** | Bottom-right of canvas | Small canvas showing board overview (`canvas.minimap`) |
| **Phase stepper** | Bottom-left, floating | 5-step progress indicator |
| **Bottom controls** | Bottom-right, floating | Undo/redo + zoom controls |
| **Status bar** | Very bottom | Note/connection counts (left), LIVE indicator (right) |
| **AI Chat panel** | Right side (toggleable) | Chat with AI assistant |

### Top Bar Elements

| Element | Icon | Selector Strategy | Action |
|---------|------|-------------------|--------|
| Board name | — | `textbox "Enter board name"` | Editable; type to rename |
| Board Context | `psychology` | Icon button with `psychology` icon | Opens Board Context dialog |
| Agent Config | `smart_toy` | Icon button with `smart_toy` icon | Opens Agent Config dialog |
| Export as Image | `image` | Icon button with `image` icon | Downloads canvas as PNG |
| Export as JSON | `download` | Icon button with `download` icon | Downloads board JSON |
| Import from JSON | `upload` | Icon button with `upload` icon | File input trigger |
| User circles | — | Circular elements with initial letters | Shows connected users |
| Agent circles | — | Circular elements with bot badges | Shows active AI agents |

#### Agent Avatar Icons

| Agent | Icon | Badge |
|-------|------|-------|
| Facilitator | `psychology` | `smart_toy` |
| EventExplorer | `explore` | `smart_toy` |
| TriggerMapper | `account_tree` | `smart_toy` |
| DomainDesigner | `architecture` | `smart_toy` |
| Organiser | `auto_fix_high` | `smart_toy` |
| DomainExpert | `school` | `smart_toy` |

### Left Toolbar Buttons

| Button | Label | Icon | Creates/Action |
|--------|-------|------|----------------|
| SELECT | `SELECT` | `arrow_selector_tool` (SVG) | Enter selection mode |
| DRAW | `DRAW` | `trending_flat` | Enter connection drawing mode |
| PAN | `PAN` | `pan_tool` | Enter pan mode |
| EVENT | `EVENT` | `bolt` | Creates Event note (orange) |
| CMD | `CMD` | `send` | Creates Command note (blue) |
| AGGR | `AGGR` | `database` (SVG) | Creates Aggregate note (yellow) |
| USER | `USER` | `person` | Creates User note (small, yellow) |
| POLICY | `POLICY` | `policy` | Creates Policy note (lilac) |
| READ | `READ` | `book` | Creates ReadModel note (green) |
| EXT | `EXT` | `cloud` | Creates ExternalSystem note (pink) |
| CONC | `CONC` | `warning` | Creates Concern note (red) |
| CONTEXT | `CONTEXT` | `crop_free` | Toggles bounded context drawing mode |
| HELP | `HELP` | `help_outline` | Opens keyboard shortcuts dialog |
| AI | `AI` | `smart_toy` | Toggles AI chat panel |

Toolbar buttons use CSS class `.tool-btn`. Active state: `.active` class.

### Phase Stepper

| Phase | Number | Label | Value |
|-------|--------|-------|-------|
| 1 | `"1"` | Set the Context | `SetContext` |
| 2 | `"2"` | Identify Events | `IdentifyEvents` |
| 3 | `"3"` | Add Commands & Policies | `AddCommandsAndPolicies` |
| 4 | `"4"` | Define Aggregates | `DefineAggregates` |
| 5 | `"5"` | Break It Down | `BreakItDown` |

Phase steps are clickable — clicking a step changes the board's active workshop phase (uses `UpdateBoardContextCommand` → SignalR broadcast). Clicking the already-active step is a no-op. Steps are keyboard-accessible (Enter/Space) with `:focus-visible` outline styling.

Container: `group "Event Storming Phase"`. Individual steps have `role="button"`, `tabindex="0"`, class `.step` with `.active` for the current phase.

| Attribute | Value |
|-----------|-------|
| `role` | `button` |
| `aria-label` | `Go to phase: {label}` (e.g., "Go to phase: Identify Events") |
| `aria-current` | `step` (on active step only) |
| `aria-disabled` | `true` (on active step only) |

### Bottom Controls

| Element | Icon | Selector Strategy |
|---------|------|-------------------|
| Undo | `undo` | Button with `undo` icon |
| Redo | `redo` | Button with `redo` icon |
| Zoom In | `zoom_in` | Button with `zoom_in` icon |
| Zoom Out | `zoom_out` | Button with `zoom_out` icon |
| Zoom level | — | `spinbutton` showing percentage (default: "100") |

---

## Journey 3: Create Sticky Notes

**Precondition**: On board page

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click note type button (e.g. EVENT) | `playwright-cli click <ref of "EVENT" button>` | Note created at top-left of canvas viewport |
| 2 | Verify note count | `playwright-cli snapshot` and read status bar | Note count increments (e.g. "1 NOTES") |

Note creation places the note at the top-left of the current canvas viewport. Each toolbar button press creates one note.

**Note**: A toast notification briefly appears confirming the action (e.g., "Add Event", "Add Command"). The clicked toolbar button shows `[active]` state.

### Edit Note Text

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Double-click a note on canvas | `playwright-cli dblclick <ref of canvas.storming-canvas>` | Note text edit modal opens |
| 2 | Type new text | `playwright-cli type "Order Placed"` | Text entered in modal |
| 3 | Confirm edit | Click Save/OK or `playwright-cli press Enter` | Modal closes, note displays new text |

### Select and Delete Notes

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Ensure SELECT mode active | `playwright-cli click <ref of "SELECT" button>` | SELECT button shows `.active` |
| 2 | Click a note on canvas | `playwright-cli click <ref of canvas.storming-canvas>` | Note selected (visual highlight) |
| 3 | Press Delete | `playwright-cli press Delete` | Note removed; count decrements |

### Move Notes (SELECT Mode)

**Precondition**: On board page with at least 1 note, SELECT mode active

To move a note, use `playwright-cli eval` and mouse commands to perform a mousedown→drag→mouseup on the canvas. You must first retrieve the note's world coordinates to calculate correct viewport positions.

#### Step 1: Get Note Positions

> **⚠ IIFE syntax fails in `playwright-cli eval`**: Wrapping code in `(() => { ... })()` causes a `TypeError: result is not a function` error. Access properties individually or use simple expressions instead.

```bash
# Get canvas transform and note count
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.scale"
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.originX"
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.originY"
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.boardState.notes.length"
# Get individual note coords (index 0, 1, etc.)
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.boardState.notes[0].x"
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.boardState.notes[0].y"
playwright-cli eval "window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.boardState.notes[0].width + '/' + window.ng.getComponent(document.querySelector('app-board-canvas')).canvasService.boardState.notes[0].height"
# Get canvas bounding box (note: canvases uses x:0, y:48 in default viewport)
playwright-cli eval "JSON.stringify(document.querySelector('canvas.storming-canvas').getBoundingClientRect())"
```

Viewport formula: `vpX = canvasBox.x + (worldX * scale) + originX`, `vpY = canvasBox.y + (worldY * scale) + originY`.

#### Step 2: Drag the Note

Convert world-coords to viewport: `vpX = canvasBox.x + (worldX * scale) + originX`, `vpY = canvasBox.y + (worldY * scale) + originY`.

Use `playwright-cli mousemove`, `playwright-cli mousedown`, and `playwright-cli mouseup` for the drag:

```bash
# Move to note center, mousedown, drag in steps, mouseup
playwright-cli mousemove <fromVpX> <fromVpY>
playwright-cli mousedown
# Move in increments to the destination
playwright-cli mousemove <toVpX> <toVpY>
playwright-cli mouseup
```

For smoother drags (required for canvas to register movement), use `playwright-cli eval`:

```bash
playwright-cli eval "async (page) => { const canvas = document.querySelector('canvas.storming-canvas'); const box = canvas.getBoundingClientRect(); /* calculate fromVpX/Y and toVpX/Y from world coords */ }"
```

**Important**: The note must be clicked first (single click in SELECT mode) to select it before dragging. A cyan selection border appears when selected. If the note is already under the cursor at mousedown, the drag will move it.

---

## Journey 4: Create Connections (DRAW Mode)

**Precondition**: On board page with at least 2 notes

> **⚠ Notes spawn close together**: Sequential note creation places notes at nearly the same world position (e.g. (140,100) and (150,110)), causing them to overlap. **Move the second note at least 300px to the right before drawing a connection**, otherwise the mousedown and mouseup land inside the same note and no connection is created.

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 0 | Get note positions | Use individual property evals (see Step 1 below) | Know world coords of both notes |
| 0b | Switch to SELECT mode | `playwright-cli click <ref of "SELECT" button>` | SELECT button active |
| 0c | Move second note right | mousedown on note[1] center → drag 300+ px right → mouseup (see Move Notes in Journey 3) | Notes are now clearly separated |
| 1 | Click DRAW button | `playwright-cli click <ref of "DRAW" button>` | DRAW button active (`[active]` state); toast "Draw Connection" appears |
| 2 | mousedown on source note → drag → mouseup on target note | `playwright-cli mousemove`/`mousedown`/`mouseup` (see below) | Connection arrow drawn between notes |
| 3 | Verify connection count | `playwright-cli snapshot` and read status bar | Connection count increments (e.g. "1 CONNECTIONS") |

### Drawing a Connection with Playwright CLI

Connection drawing requires coordinate-based mouse commands because notes are on an HTML Canvas, not DOM elements. You must mousedown **inside** the source note and mouseup **inside** the target note — start dragging immediately from mousedown.

**Do NOT double-click** — double-clicking a note opens the "Edit Note Text" dialog, even in DRAW mode.

#### Step 1: Get Note Positions (same as Move Notes above — use individual property evals, not IIFE)

#### Step 2: Draw the Connection

```bash
# Get canvas bounding box and note positions first via eval
# Then draw connection from source to target note centers:
playwright-cli mousemove <sourceVpX> <sourceVpY>
playwright-cli mousedown
playwright-cli mousemove <targetVpX> <targetVpY>
playwright-cli mouseup
```

For smoother drags with incremental mouse moves, use `playwright-cli eval`:

```bash
playwright-cli eval "async () => { const canvas = document.querySelector('canvas.storming-canvas'); const box = canvas.getBoundingClientRect(); /* calculate source/target viewport coords, then use mouse.move/down/up */ }"
```

**Key points**:
- Must be in DRAW mode (`click_element(element="DRAW button")`) before dragging
- mousedown must land **inside** the source note bounding box (`x ≤ px ≤ x+width`, `y ≤ py ≤ y+height`)
- mouseup must land **inside** the target note bounding box
- Use note centers for reliability: `centerX = note.x + note.width/2`, `centerY = note.y + note.height/2`
- Connections are directional: the arrow points from source → target
- After drawing, DRAW mode remains active for additional connections

---

## Journey 5: Use AI Chat

**Precondition**: On board page

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click AI button in toolbar | `playwright-cli click <ref of "AI" button>` | AI chat panel opens on right side |
| 2 | Type message | `playwright-cli fill <ref of "Ask the AI assistant..." textbox> "Add an OrderPlaced event"` | Text appears in input |
| 3 | Send message | `playwright-cli press Enter` or click send button ref | Message sent; AI response streams in |
| 4 | Close panel | Click close button (X icon) ref or click AI button again | Panel closes |

### AI Chat Panel Structure

```
generic (chat container)
├── header:
│   ├── img: smart_toy
│   ├── generic: "AI Assistant"
│   ├── generic: "Autonomy off" (or "Autonomy on" when enabled)
├── buttons: delete_outline (clear history), close (X icon)
├── message area:
│   ├── img: chat_bubble_outline
│   ├── paragraph: "Ask the AI to help with your Event Storming session."
│   ├── paragraph: "Try: 'Add an OrderPlaced event' or 'What events do we have?'"
│   └── messages (user + agent responses, streamed)
└── input area:
    ├── textbox "Ask the AI assistant..."
    └── button with img: send (disabled when input empty, enabled when text present)
```

### Verification

- Agent responses appear as chat messages
- Tool calls are shown as step updates
- Notes created by AI appear on canvas (note count updates)

---

## Journey 6: Configure Board Context

**Precondition**: On board page

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click psychology icon (Board Context) | `playwright-cli click <ref of psychology icon button>` | "Board Context for AI" dialog opens |
| 2 | Fill Domain | `playwright-cli fill <ref of "Domain" textbox> "eCommerce marketplace"` | Domain entered |
| 3 | Fill Session Scope | `playwright-cli fill <ref of "Session Scope" textbox> "Order fulfillment"` | Scope entered |
| 4 | Select Workshop Phase | `playwright-cli click <ref of "Workshop Phase" combobox>` then click phase option ref | Phase selected |
| 5 | *(Optional)* Toggle autonomous mode | `playwright-cli click <ref of "Run the AI facilitator autonomously" switch>` | Toggle switches on/off |
| 6 | Click Save | `playwright-cli click <ref of "Save" button>` | Dialog closes; changes applied |

### Dialog Structure

```
dialog "Board Context for AI"
├── heading "Board Context for AI"
├── paragraph: explanatory text
├── textbox "Domain" (textarea with placeholder)
├── textbox "Session Scope" (textarea with placeholder)
├── combobox "Workshop Phase" (mat-select dropdown)
│   ├── option "None (unset)" [default/selected]
│   ├── option "Set the Context"
│   ├── option "Identify Events"
│   ├── option "Add Commands & Policies"
│   ├── option "Define Aggregates"
│   └── option "Break It Down"
├── switch "Run the AI facilitator autonomously"
│   └── paragraph: explanation of autonomous mode
├── button "Cancel"
└── button "Save"
```

---

## Journey 7: Configure AI Agents

**Precondition**: On board page

### Steps

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click smart_toy icon (Agent Config) | `playwright-cli click <ref of smart_toy icon button>` | "Configure AI Agents" dialog opens |
| 2 | View agent list | `playwright-cli snapshot` | Lists: Facilitator, EventExplorer, TriggerMapper, DomainDesigner, Organiser, DomainExpert |
| 3 | Expand an agent | `playwright-cli click <ref of "EventExplorer" agent row>` | Agent config form expands |
| 4 | Edit agent fields | Modify Name, Model, System Prompt, etc. via `fill`/`click`/`select` | Fields update |
| 5 | Switch to interaction diagram | `playwright-cli click <ref of hub icon tab>` | Diagram shows agent communication graph |
| 6 | Click Save | `playwright-cli click <ref of "Save" button>` | Dialog closes; agent configs saved |

### Dialog Structure — Config Tab

```
dialog "Configure AI Agents"
├── tab: tune icon (config list) — ACTIVE by default
├── tab: hub icon (interaction diagram)
├── paragraph: "Configure the AI agents available for this board. The facilitator agent is always present."
├── agent list (accordion):
│   ├── "Facilitator" (badge: Facilitator, icon: psychology) — cannot be deleted or removed
│   ├── "EventExplorer" (icon: explore, expandable)
│   │   ├── textbox "Name"
│   │   ├── combobox "Model" (e.g. "GPT 5.2")
│   │   ├── combobox "Reasoning Effort" (e.g. "Low")
│   │   ├── icon picker grid (20 icon options)
│   │   ├── color picker grid (15 color options)
│   │   ├── textbox "System Prompt" (textarea)
│   │   ├── "Active Phases" — checkboxes: All phases, Set the Context, Identify Events, Add Commands & Policies, Define Aggregates, Break It Down
│   │   ├── "Allowed Tools" — checkboxes for each available tool (e.g. GetBoardState, CreateNotes, MoveNotes, DeleteNotes, AskAgentQuestion, etc.)
│   │   ├── "Can Ask Questions To" — checkboxes: All agents, Facilitator, TriggerMapper, DomainDesigner, Organiser, DomainExpert
│   │   └── button "Remove Agent" (delete icon) — removes this agent from the board
│   ├── "TriggerMapper" (icon: account_tree) ...
│   ├── "DomainDesigner" (icon: architecture) ...
│   ├── "Organiser" (icon: auto_fix_high) ...
│   └── "DomainExpert" (icon: school) ...
├── button "Add Agent" (add icon)
├── button "Cancel"
└── button "Save"
```

### Dialog Structure — Interaction Diagram Tab

```
dialog "Configure AI Agents"
├── tab: tune icon
├── tab: hub icon — ACTIVE
├── SVG diagram:
│   ├── Agent nodes (circles with icons): Facilitator (centre), EventExplorer, TriggerMapper, DomainDesigner, Organiser, DomainExpert
│   └── Connection arrows: Delegate (solid), Review (dashed), Ask (dotted)
├── legend: Link Protocols (Delegate, Review, Ask)
├── button "Cancel"
└── button "Save"
```

---

## Journey 8: Export and Import Board

**⚠ Export buttons trigger OS-level file dialogs that cannot be dismissed by browser automation.** Do NOT click the export buttons during automated regression testing — this will leave a Save-As dialog open and block all subsequent journeys.

### Automated Testing Strategy

For regression testing, **verify only that the export buttons exist and are enabled** — do not click them:

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Verify Export as JSON button exists | `playwright-cli snapshot` and check for `download` icon button | Button is present and not disabled |
| 2 | Verify Export as Image button exists | Check snapshot for `image` icon button | Button is present and not disabled |
| 3 | Verify Import from JSON button exists | Check snapshot for `upload` icon button | Button is present and not disabled |

Mark this journey as **Pass (presence only)** in the regression report and add to "Manual Verification Needed" that actual export/import file operations need manual testing.

### Manual Testing Reference

These steps are for **manual testing only** — do not execute in automated runs:

#### Export as JSON

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Click download icon (`download` icon) | Browser downloads `.json` file |

#### Export as Image

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Click image icon (`image` icon) | Browser downloads `.png` file |

#### Import from JSON

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Click upload icon (`upload` icon) | File chooser dialog opens |
| 2 | Select JSON file | Board state loaded from file |

---

## Journey 9: Board Name Editing

**Precondition**: On board page

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click board name input | `playwright-cli click <ref of "Enter board name" textbox>` | Input becomes focused |
| 2 | Clear and type new name | `playwright-cli press Control+a` then `playwright-cli type "New Board Name"` | Name updated |
| 3 | Click away or press Enter | `playwright-cli press Enter` or click elsewhere | Name saved and broadcast |

---

## Journey 10: Theme Toggle

**Precondition**: On any page

| # | Action | Command | Expected Result |
|---|--------|---------|-----------------|
| 1 | Click settings gear in nav bar | `playwright-cli click <ref of settings icon>` | Appearance popover opens |
| 2 | Toggle light/dark switch | `playwright-cli click <ref of "Toggle light mode" switch>` | Theme changes; popover shows "Light"/"Dark" |
| 3 | Click outside popover | `playwright-cli eval "document.querySelector('.settings-menu-backdrop').click()"` | Popover closes |

**Note**: The backdrop must be clicked using a JS eval with CSS selector (`.settings-menu-backdrop`), not a snapshot ref. The theme indicator shows icon `dark_mode` + "Dark" or `light_mode` + "Light" depending on current theme.

---

## Canvas Interaction Notes

The board canvas is an HTML Canvas element — notes and connections are **not** DOM elements. This affects testing:

| Concern | Detail |
|---------|--------|
| **Note selection** | Notes are rendered imperatively on canvas; cannot be targeted by DOM selectors |
| **Note positioning** | Requires coordinate-based mouse events rather than element clicks |
| **Canvas offset** | The canvas bounding box is `{ x: 0, y: 48 }` in the default viewport (top nav is 48px tall). Always add this offset when converting world coords to viewport coords. |
| **IIFE in eval fails** | `playwright-cli eval "(() => { ... })()"` causes `TypeError: result is not a function`. Use individual property access expressions instead. |
| **Note overlap on creation** | Sequential note creation places notes at nearby positions (e.g. (140,100) and (150,110)). For connection testing, create notes in separate steps and verify positions before drawing. |
| **Double-click to edit** | Double-click on canvas at note coordinates opens the Note Text Modal (DOM dialog) |
| **Bounded context drawing** | In CONTEXT mode, click-and-drag on canvas creates a rectangle |
| **Connection drawing** | In DRAW mode, drag from source note to target note on canvas |
| **Minimap** | Separate canvas element (`canvas.minimap`) showing board overview; clickable for navigation |

**Recommended testing strategy**: Use the AI chat panel to create and manipulate notes (e.g., "Add an OrderPlaced event") rather than trying to interact with the canvas directly. This avoids coordinate-based canvas interactions and validates the AI agent pipeline end-to-end.

---

## Key File Paths

| Component | Path |
|-----------|------|
| Angular routes | `src/eventstormingboard.client/src/app/app.routes.ts` |
| Splash page | `src/eventstormingboard.client/src/app/splash/splash.component.ts` |
| Create board dialog | `src/eventstormingboard.client/src/app/splash/create-board-modal/create-board-modal.component.ts` |
| Select board dialog | `src/eventstormingboard.client/src/app/splash/select-board-modal/select-board-modal.component.ts` |
| Board component | `src/eventstormingboard.client/src/app/board/board.component.ts` |
| Board template | `src/eventstormingboard.client/src/app/board/board.component.html` |
| Board canvas | `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` |
| AI chat panel | `src/eventstormingboard.client/src/app/board/ai-chat-panel/ai-chat-panel.component.ts` |
| Agent config modal | `src/eventstormingboard.client/src/app/board/agent-config-modal/agent-config-modal.component.ts` |
| Board context modal | `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.ts` |
| Keyboard shortcuts modal | `src/eventstormingboard.client/src/app/board/keyboard-shortcuts-modal/keyboard-shortcuts-modal.component.ts` |
| Note text edit modal | `src/eventstormingboard.client/src/app/board/board-canvas/note-text-modal/note-text-modal.component.ts` |
| Agent interaction diagram | `src/eventstormingboard.client/src/app/board/agent-interaction-diagram/agent-interaction-diagram.component.ts` |
