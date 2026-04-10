# User Journeys

User journeys for StormSpace, documented for automated browser testing by AI agents using VS Code integrated browser tools (`read_page`, `click_element`, `type_in_page`, `screenshot_page`, etc.).

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

### Steps

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click NEW BOARD | `click_element(element="NEW BOARD button")` | "Create Storm Board" dialog opens |
| 2 | Type board name | `type_in_page(element="Board Name input", ref=<textbox "Board Name">, text="My Board")` | Text appears in input; Create button becomes enabled |
| 3 | *(Optional)* Expand board context | `click_element(element="Optional Board Context for AI")` | Domain and Session Scope fields appear |
| 4 | *(Optional)* Fill Domain | `type_in_page(ref=<textbox "Domain">, text="eCommerce marketplace")` | Domain text entered |
| 5 | *(Optional)* Fill Session Scope | `type_in_page(ref=<textbox "Session Scope">, text="Order fulfillment flow")` | Session scope text entered |
| 6 | Click Create | `click_element(element="Create button")` | Navigates to `/boards/{id}`, board page loads |

### Verification

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

### Steps

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click JOIN BOARD | `click_element(element="JOIN BOARD button")` | "Select a Board" dialog opens |
| 2 | Click Board Name dropdown | `click_element(ref=<combobox "Board Name">)` | Dropdown expands showing available boards |
| 3 | Select a board | `click_element(ref=<option "Board Name">)` | Board selected; Select button becomes enabled |
| 4 | Click Select | `click_element(element="Select button")` | Navigates to `/boards/{id}` |

### Alternative: Direct URL

Navigate directly to `https://localhost:51710/boards/{board-id}` — auto-joins and loads the board.

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

Phase steps are clickable. Container: `progressbar "Event Storming Phase"`. Individual steps have class `.step` with `.active` for the current phase.

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

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click note type button (e.g. EVENT) | `click_element(element="EVENT button")` | Note created at top-left of canvas viewport |
| 2 | Verify note count | Read status bar | Note count increments (e.g. "1 NOTES") |

Note creation places the note at the top-left of the current canvas viewport. Each toolbar button press creates one note.

**Note**: A toast notification briefly appears confirming the action (e.g., "Add Event", "Add Command"). The clicked toolbar button shows `[active]` state.

### Edit Note Text

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Double-click a note on canvas | `click_element(selector="canvas.storming-canvas", dblClick=true)` | Note text edit modal opens |
| 2 | Type new text | `type_in_page(text="Order Placed")` | Text entered in modal |
| 3 | Confirm edit | Click Save/OK or press Enter | Modal closes, note displays new text |

### Select and Delete Notes

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Ensure SELECT mode active | `click_element(element="SELECT button")` | SELECT button shows `.active` |
| 2 | Click a note on canvas | `click_element(selector="canvas.storming-canvas")` | Note selected (visual highlight) |
| 3 | Press Delete | `type_in_page(key="Delete")` | Note removed; count decrements |

### Move Notes (SELECT Mode)

**Precondition**: On board page with at least 1 note, SELECT mode active

To move a note, use `run_playwright_code` to perform a mousedown→drag→mouseup on the canvas. You must first retrieve the note's world coordinates to calculate correct viewport positions.

#### Step 1: Get Note Positions

```js
// run_playwright_code: retrieve note world coords and canvas transform
const result = await page.evaluate(`
  (function() {
    var ng = window.ng;
    var bc = document.querySelector('app-board-canvas');
    var comp = ng.getComponent(bc);
    var svc = comp.canvasService;
    var notes = svc.boardState.notes;
    return JSON.stringify({
      originX: svc.originX, originY: svc.originY, scale: svc.scale,
      notes: notes.map(function(n) {
        return { type: n.type, text: n.text, x: n.x, y: n.y, w: n.width, h: n.height };
      })
    });
  })()
`);
return result;
```

#### Step 2: Drag the Note

Convert world-coords to viewport: `vpX = canvasBox.x + (worldX * scale) + originX`, `vpY = canvasBox.y + (worldY * scale) + originY`.

```js
// run_playwright_code: drag note from current center to new position
const canvas = page.locator('canvas.storming-canvas');
const box = await canvas.boundingBox();
// Example: move note from world center (200,160) by 300px right
const fromVpX = box.x + 200;
const fromVpY = box.y + 160;
const toVpX = fromVpX + 300;
const toVpY = fromVpY;
await page.mouse.move(fromVpX, fromVpY);
await page.mouse.down();
for (let i = 1; i <= 20; i++) {
  await page.mouse.move(
    fromVpX + (toVpX - fromVpX) * (i / 20),
    fromVpY + (toVpY - fromVpY) * (i / 20)
  );
}
await page.mouse.up();
```

**Important**: The note must be clicked first (single click in SELECT mode) to select it before dragging. A cyan selection border appears when selected. If the note is already under the cursor at mousedown, the drag will move it.

---

## Journey 4: Create Connections (DRAW Mode)

**Precondition**: On board page with at least 2 notes

### Steps

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click DRAW button | `click_element(element="DRAW button")` | DRAW button active (`[active]` state); toast "Draw Connection" appears |
| 2 | mousedown on source note → drag → mouseup on target note | `run_playwright_code` (see below) | Connection arrow drawn between notes |
| 3 | Verify connection count | Read status bar | Connection count increments (e.g. "1 CONNECTIONS") |

### Drawing a Connection with Playwright

Connection drawing requires `run_playwright_code` because notes are on an HTML Canvas, not DOM elements. You must mousedown **inside** the source note and mouseup **inside** the target note — start dragging immediately from mousedown.

**Do NOT double-click** — double-clicking a note opens the "Edit Note Text" dialog, even in DRAW mode.

#### Step 1: Get Note Positions (same as Move Notes above)

#### Step 2: Draw the Connection

```js
// run_playwright_code: draw connection from Event note to Command note
const canvas = page.locator('canvas.storming-canvas');
const box = await canvas.boundingBox();
// World coords → viewport: vpX = box.x + worldX, vpY = box.y + worldY (when scale=1, origin=0,0)
// Source note center (e.g. Event at world 200,160)
const fromVpX = box.x + 200;
const fromVpY = box.y + 160;
// Target note center (e.g. Command at world 510,171)
const toVpX = box.x + 510;
const toVpY = box.y + 171;
// Immediate mousedown → drag → mouseup
await page.mouse.move(fromVpX, fromVpY);
await page.mouse.down();
for (let i = 1; i <= 20; i++) {
  await page.mouse.move(
    fromVpX + (toVpX - fromVpX) * (i / 20),
    fromVpY + (toVpY - fromVpY) * (i / 20)
  );
}
await page.mouse.up();
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

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click AI button in toolbar | `click_element(element="AI button")` | AI chat panel opens on right side |
| 2 | Type message | `type_in_page(ref=<textbox "Ask the AI assistant...">, text="Add an OrderPlaced event")` | Text appears in input |
| 3 | Send message | `type_in_page(key="Enter")` or click send button | Message sent; AI response streams in |
| 4 | Close panel | Click close button (X icon) or click AI button again | Panel closes |

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

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click psychology icon (Board Context) | `click_element(element="Board Context button")` with `psychology` icon | "Board Context for AI" dialog opens |
| 2 | Fill Domain | `type_in_page(ref=<textbox "Domain">, text="eCommerce marketplace")` | Domain entered |
| 3 | Fill Session Scope | `type_in_page(ref=<textbox "Session Scope">, text="Order fulfillment")` | Scope entered |
| 4 | Select Workshop Phase | `click_element(ref=<combobox "Workshop Phase">)` then select phase option | Phase selected |
| 5 | *(Optional)* Toggle autonomous mode | `click_element(ref=<switch "Run the AI facilitator autonomously">)` | Toggle switches on/off |
| 6 | Click Save | `click_element(element="Save button")` | Dialog closes; changes applied |

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

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click smart_toy icon (Agent Config) | `click_element(element="Agent Config button")` with `smart_toy` icon | "Configure AI Agents" dialog opens |
| 2 | View agent list | Read dialog | Lists: Facilitator, EventExplorer, TriggerMapper, DomainDesigner, Organiser, DomainExpert |
| 3 | Expand an agent | `click_element(element="EventExplorer agent row")` | Agent config form expands |
| 4 | Edit agent fields | Modify Name, Model, System Prompt, etc. | Fields update |
| 5 | Switch to interaction diagram | Click hub icon tab button | Diagram shows agent communication graph |
| 6 | Click Save | `click_element(element="Save button")` | Dialog closes; agent configs saved |

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

### Export as JSON

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click download icon | `click_element(element="Export as JSON button")` with `download` icon | Browser downloads `.json` file |

### Export as Image

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click image icon | `click_element(element="Export as Image button")` with `image` icon | Browser downloads `.png` file |

### Import from JSON

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click upload icon | `click_element(element="Import from JSON button")` with `upload` icon | File chooser dialog opens |
| 2 | Select JSON file | `handle_dialog(selectFiles=["/path/to/board.json"])` | Board state loaded from file |

---

## Journey 9: Board Name Editing

**Precondition**: On board page

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click board name input | `click_element(ref=<textbox "Enter board name">)` | Input becomes focused |
| 2 | Clear and type new name | `type_in_page(key="Control+a")` then `type_in_page(text="New Board Name")` | Name updated |
| 3 | Click away or press Enter | `type_in_page(key="Enter")` or click elsewhere | Name saved and broadcast |

---

## Journey 10: Theme Toggle

**Precondition**: On any page

| # | Action | Tool Call | Expected Result |
|---|--------|-----------|-----------------|
| 1 | Click settings gear in nav bar | `click_element(element="settings gear icon")` | Appearance popover opens |
| 2 | Toggle light/dark switch | `click_element(ref=<switch "Toggle light mode">)` | Theme changes; popover shows "Light"/"Dark" |
| 3 | Click outside popover | `click_element(selector=".settings-menu-backdrop")` | Popover closes |

**Note**: The backdrop must be clicked using a CSS selector (`.settings-menu-backdrop`), not an aria ref. The theme indicator shows icon `dark_mode` + "Dark" or `light_mode` + "Light" depending on current theme.

---

## Canvas Interaction Notes

The board canvas is an HTML Canvas element — notes and connections are **not** DOM elements. This affects testing:

| Concern | Detail |
|---------|--------|
| **Note selection** | Notes are rendered imperatively on canvas; cannot be targeted by DOM selectors |
| **Note positioning** | Requires coordinate-based mouse events rather than element clicks |
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
