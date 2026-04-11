import { AfterViewInit, Component, DestroyRef, ElementRef, HostListener, OnInit, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Note, getNoteColor } from '../../_shared/models/note.model';
import { Connection } from '../../_shared/models/connection.model';
import { BoundedContext } from '../../_shared/models/bounded-context.model';
import { CreateConnectionCommand, CreateBoundedContextCommand, DeleteNotesCommand, DeleteBoundedContextCommand, EditNoteTextCommand, MoveBoundedContextCommand, MoveNotesCommand, PasteCommand, ResizeBoundedContextCommand, ResizeNoteCommand, UpdateBoundedContextCommand } from '../board.commands';
import { v4 as uuid } from 'uuid';
import { Coordinates } from '../../_shared/models/coordinates.model';
import { NoteSize } from '../../_shared/models/note-size.model';
import { NoteMove } from '../../_shared/models/note-move.model';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { NoteTextModalComponent } from './note-text-modal/note-text-modal.component';
import { BcNameModalComponent } from './bc-name-modal/bc-name-modal.component';
import { BoardCanvasService } from './board-canvas.service';
import { KeyboardShortcutsModalComponent } from '../keyboard-shortcuts-modal/keyboard-shortcuts-modal.component';
import { BoardsSignalRService } from '../../_shared/services/boards-signalr.service';
import { BoardUser } from '../../_shared/models/board-user.model';
import { CursorPositionUpdatedEvent } from '../../_shared/models/board-events.model';
import { ThemeService } from '../../_shared/services/theme.service';
import { UserService } from '../../_shared/services/user.service';

@Component({
    selector: 'app-board-canvas',
    imports: [
        MatDialogModule
    ],
    templateUrl: './board-canvas.component.html',
    styleUrls: ['./board-canvas.component.scss']
})
export class BoardCanvasComponent implements OnInit, AfterViewInit {

  private static readonly CURSOR_BROADCAST_INTERVAL_MS = 50;

  private dialog = inject(MatDialog);
  private canvasService = inject(BoardCanvasService);
  private boardsHub = inject(BoardsSignalRService);
  private themeService = inject(ThemeService);
  private userService = inject(UserService);
  private destroyRef = inject(DestroyRef);

  private ctx!: CanvasRenderingContext2D;
  private minimapCtx!: CanvasRenderingContext2D;

  private currentMousePos: Coordinates = { x: 0, y: 0 };
  private rafPending = false;

  private draggingNote: Note | null = null;
  private resizingNote: Note | null = null;
  private initialResizeState: NoteSize | null = null;
  private initialDragPositions: Map<string, Coordinates> = new Map();
  private resizeCorner: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | null = null;
  private resizeHandleSize = 10;
  private copiedNotes: Note[] = [];

  private arrowStartNote: Note | null = null;
  private copiedConnections: Connection[] = [];
  private hoveredConnectionStartNote: Note | null = null;
  private hoveredConnectionStartPos: { x: number; y: number } | null = null;

  private dragOffsetX = 0;
  private dragOffsetY = 0;

  private panning = false;
  private lastPanX = 0;
  private lastPanY = 0;

  private isSelecting = false;
  private selectionStart: Coordinates = { x: 0, y: 0 };
  private selectionRect = { x: 0, y: 0, width: 0, height: 0 };

  private hoveredNote: Note | null = null;
  private hoveredConnection: Connection | null = null;
  private hoveredBoundedContext: BoundedContext | null = null;
  private lastCursorBroadcastAt = 0;
  private get localUserName(): string { return this.userService.displayName || 'Anonymous'; }

  // Bounded context interaction state
  private bcDrawStart: Coordinates | null = null;
  private bcDrawPreview: { x: number; y: number; width: number; height: number } | null = null;
  private draggingBoundedContext: BoundedContext | null = null;
  private bcDragOffsetX = 0;
  private bcDragOffsetY = 0;
  private resizingBoundedContext: BoundedContext | null = null;
  private bcResizeCorner: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | null = null;
  private bcInitialResizeState: { x: number; y: number; width: number; height: number } | null = null;
  private bcInitialDragPosition: { x: number; y: number } | null = null;

  public canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  public minimap = viewChild.required<ElementRef<HTMLCanvasElement>>('minimap');

  public onMinimapClick(event: MouseEvent): void {
    const rect = this.minimap().nativeElement.getBoundingClientRect();
    const clickX = event.clientX - rect.left;
    const clickY = event.clientY - rect.top;

    const { minX, minY, dynamicScale } = this.getCanvasBoundsAndScale();

    const canvasX = clickX / dynamicScale + minX;
    const canvasY = clickY / dynamicScale + minY;

    const canvasEl = this.canvas().nativeElement;
    this.canvasService.originX = -canvasX * this.canvasService.scale + canvasEl.width / 2;
    this.canvasService.originY = -canvasY * this.canvasService.scale + canvasEl.height / 2;

    this.drawCanvas();
  }

  public onMouseDown(event: MouseEvent): void {
    const { x, y } = this.getMousePos(event);

    if ((this.canvasService.isPanningMode && event.button === 0) || event.button === 1) { // Left mouse button for panning
      this.panning = true;
      this.lastPanX = event.clientX;
      this.lastPanY = event.clientY;
      return;
    }

    let clickedOnNote = false;
    let clickedOnConnection = false;

    // Connection drawing mode takes highest priority  
    if (this.canvasService.isDrawingConnection) {
      for (let note of [...this.canvasService.boardState.notes].reverse()) {
        if (this.isPointInsideNote(note, x, y)) {
          this.arrowStartNote = note;
          clickedOnNote = true;
          break; // Start drawing connection from this note  
        }
      }
      if (clickedOnNote) {
        this.drawCanvas();
        return; // Early return since we're handling connection drawing  
      }
    }

    // Bounded context draw-to-create mode
    if (this.canvasService.isDrawingBoundedContext) {
      this.bcDrawStart = { x, y };
      this.bcDrawPreview = { x, y, width: 0, height: 0 };
      return;
    }

    // Reset selection unless Ctrl is pressed  
    if (!event.ctrlKey) {
      this.canvasService.boardState.notes.forEach(n => n.selected = false);
      this.canvasService.boardState.connections.forEach(c => c.selected = false);
      this.canvasService.boardState.boundedContexts.forEach(bc => bc.selected = false);
    }

    this.resizingNote = null;
    this.resizeCorner = null;

    for (let note of [...this.canvasService.boardState.notes].reverse()) {
      const corner = this.getResizeCorner(note, x, y);
      if (corner) {
        this.resizingNote = note;
        this.initialResizeState = { x: note.x, y: note.y, width: note.width, height: note.height };
        this.resizeCorner = corner;
        note.selected = true;
        clickedOnNote = true;
        break;
      }
    }
    if (clickedOnNote) {
      this.drawCanvas();
      return;
    }

    // Check dragging notes next  
    if (!clickedOnNote) {
      for (let note of [...this.canvasService.boardState.notes].reverse()) {
        if (this.isPointInsideNote(note, x, y)) {
          this.draggingNote = note;
          this.dragOffsetX = x - note.x;
          this.dragOffsetY = y - note.y;
          note.selected = true;
          clickedOnNote = true;
          break;
        }
      }
      this.initialDragPositions = new Map();
      this.canvasService.boardState.notes.filter(n => n.selected).forEach(n => {
        this.initialDragPositions.set(n.id, { x: n.x, y: n.y });
      });
    }

    // Check clicking on connections  
    if (!clickedOnNote) {
      // Check bounded context resize corners first, then drag
      let clickedOnBC = false;
      for (const bc of [...this.canvasService.boardState.boundedContexts].reverse()) {
        const bcCorner = this.getBCResizeCorner(bc, x, y);
        if (bcCorner) {
          this.resizingBoundedContext = bc;
          this.bcResizeCorner = bcCorner;
          this.bcInitialResizeState = { x: bc.x, y: bc.y, width: bc.width, height: bc.height };
          if (!event.ctrlKey) {
            this.canvasService.boardState.boundedContexts.forEach(b => b.selected = false);
          }
          bc.selected = true;
          clickedOnBC = true;
          break;
        }
        if (this.isPointInsideBCBorder(bc, x, y)) {
          this.draggingBoundedContext = bc;
          this.bcDragOffsetX = x - bc.x;
          this.bcDragOffsetY = y - bc.y;
          this.bcInitialDragPosition = { x: bc.x, y: bc.y };
          if (!event.ctrlKey) {
            this.canvasService.boardState.boundedContexts.forEach(b => b.selected = false);
          }
          bc.selected = true;
          clickedOnBC = true;
          break;
        }
      }
      if (clickedOnBC) {
        this.drawCanvas();
        return;
      }
      for (let connection of this.canvasService.boardState.connections) {
        const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
        const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
        if (fromNote && toNote && this.isPointNearArrow(x, y, fromNote, toNote)) {
          connection.selected = true;
          clickedOnConnection = true;
          break;
        }
      }
    }

    // Check if clicking inside a bounded context interior (for selection without drag)
    if (!clickedOnNote && !clickedOnConnection) {
      for (const bc of [...this.canvasService.boardState.boundedContexts].reverse()) {
        if (this.isPointInsideBCInterior(bc, x, y)) {
          if (!event.ctrlKey) {
            this.canvasService.boardState.boundedContexts.forEach(b => b.selected = false);
          }
          bc.selected = true;
          this.drawCanvas();
          return;
        }
      }
    }

    // Default to area selection if nothing else was clicked  
    if (!clickedOnNote && !clickedOnConnection) {
      this.isSelecting = true;
      this.selectionStart = { x, y };
      this.selectionRect = { x, y, width: 0, height: 0 };
    }

    this.drawCanvas();
  }

  public onMouseUp(event: MouseEvent): void {

    if (this.panning) {
      this.panning = false;
      return;
    }

    // Bounded context draw-to-create completion
    if (this.bcDrawStart && this.bcDrawPreview) {
      const preview = this.bcDrawPreview;
      this.bcDrawStart = null;
      this.bcDrawPreview = null;

      if (Math.abs(preview.width) > 40 && Math.abs(preview.height) > 40) {
        const normalizedX = preview.width < 0 ? preview.x + preview.width : preview.x;
        const normalizedY = preview.height < 0 ? preview.y + preview.height : preview.y;
        const normalizedW = Math.abs(preview.width);
        const normalizedH = Math.abs(preview.height);

        this.promptBoundedContextName(normalizedX, normalizedY, normalizedW, normalizedH);
      }

      this.drawCanvas();
      return;
    }

    if (this.isSelecting) {
      this.selectNotesAndConnectionsInRect();
      this.isSelecting = false;
      this.selectionRect = { x: 0, y: 0, width: 0, height: 0 };
    }

    if (this.arrowStartNote && this.canvasService.isDrawingConnection) {
      const { x, y } = this.getMousePos(event);
      for (let note of [...this.canvasService.boardState.notes].reverse()) {
        if (note !== this.arrowStartNote && this.isPointInsideNote(note, x, y)) {
          const command = new CreateConnectionCommand({
            fromNoteId: this.arrowStartNote.id,
            toNoteId: note.id
          });
          this.canvasService.executeCommand(command);
          break;
        }
      }
      this.arrowStartNote = null;
    }

    if (this.draggingNote) {
      const finalDragPositions: NoteMove[] = [];
      this.canvasService.boardState.notes.filter(e => e.selected).forEach(n => {
        finalDragPositions.push({
          noteId: n.id,
          coordinates: { x: n.x, y: n.y }
        });
      });

      const initialDragPositions: NoteMove[] = [];
      this.initialDragPositions.forEach((value, key) => {
        initialDragPositions.push({
          noteId: key,
          coordinates: value
        });
      });

      const hasMoved = finalDragPositions.some(position => {
        const from = this.initialDragPositions.get(position.noteId);
        return !!from && (from.x !== position.coordinates.x || from.y !== position.coordinates.y);
      });

      if (hasMoved) {
        const moveCommand = new MoveNotesCommand(initialDragPositions, finalDragPositions);
        this.canvasService.executeCommand(moveCommand);
      }
    }

    if (this.resizingNote && this.initialResizeState) {
      const to = { x: this.resizingNote.x, y: this.resizingNote.y, width: this.resizingNote.width, height: this.resizingNote.height };
      const command = new ResizeNoteCommand(this.resizingNote.id, this.initialResizeState, to);
      this.canvasService.executeCommand(command);
    }

    if (this.draggingBoundedContext && this.bcInitialDragPosition) {
      const bc = this.draggingBoundedContext;
      const initial = this.bcInitialDragPosition;
      if (bc.x !== initial.x || bc.y !== initial.y) {
        const command = new MoveBoundedContextCommand(
          bc.id,
          initial.x, bc.x,
          initial.y, bc.y
        );
        this.canvasService.executeCommand(command);
      }
      this.draggingBoundedContext = null;
      this.bcInitialDragPosition = null;
    }

    if (this.resizingBoundedContext && this.bcInitialResizeState) {
      const bc = this.resizingBoundedContext;
      const initial = this.bcInitialResizeState;
      if (bc.x !== initial.x || bc.y !== initial.y || bc.width !== initial.width || bc.height !== initial.height) {
        const command = new ResizeBoundedContextCommand(
          bc.id,
          initial.x, bc.x,
          initial.y, bc.y,
          initial.width, bc.width,
          initial.height, bc.height
        );
        this.canvasService.executeCommand(command);
      }
      this.resizingBoundedContext = null;
      this.bcResizeCorner = null;
      this.bcInitialResizeState = null;
    }

    this.draggingNote = null;
    this.resizingNote = null;
    this.resizeCorner = null;

    this.drawCanvas();
  }

  public onDoubleClick(event: MouseEvent): void {
    const { x, y } = this.getMousePos(event);
    for (let note of [...this.canvasService.boardState.notes].reverse()) {
      if (this.isPointInsideNote(note, x, y)) {
        this.editNoteText(note);
        return;
      }
    }
    // Double-click on BC border to edit name
    for (const bc of [...this.canvasService.boardState.boundedContexts].reverse()) {
      if (this.isPointInsideBCBorder(bc, x, y)) {
        this.editBoundedContextName(bc);
        return;
      }
    }
  }

  public onWheel(event: WheelEvent): void {
    if (event.ctrlKey) {
      // Ctrl pressed: Zooming behavior  
      event.preventDefault();

      const rect = this.canvas().nativeElement.getBoundingClientRect();
      const screenX = event.clientX - rect.left;
      const screenY = event.clientY - rect.top;

      const wheel = event.deltaY < 0 ? 1 : -1;
      const zoom = Math.pow(this.canvasService.scaleFactor, wheel);

      const newScale = this.canvasService.scale * zoom;

      // Limit zoom to reasonable levels  
      if (newScale < 0.2 || newScale > 5) return;

      // Adjust origin to zoom towards mouse pointer  
      this.canvasService.originX = screenX - zoom * (screenX - this.canvasService.originX);
      this.canvasService.originY = screenY - zoom * (screenY - this.canvasService.originY);

      this.canvasService.scale = newScale;
    } else {
      // Ctrl not pressed: Vertical canvas panning  
      event.preventDefault(); // Prevent browser default scrolling  

      const panSpeed = 1; // Adjust pan speed as needed  
      const deltaY = event.deltaY * panSpeed;

      // Update originY to move canvas vertically  
      this.canvasService.originY -= deltaY; // Subtract to scroll in the natural direction  

      // Optional: For horizontal scrolling support, you can also adjust originX based on deltaX  
      const deltaX = event.deltaX * panSpeed;
      this.canvasService.originX -= deltaX;
    }

    this.drawCanvas();
  }

  @HostListener('document:keydown', ['$event'])
  public onKeyDown(event: KeyboardEvent): void {

    if (this.dialog.openDialogs.length > 0) {
      return;
    }

    if (this.isEditableTarget(event.target)) {
      return;
    }

    if ((event.key === '?' || (event.shiftKey && event.key === '/')) && !event.ctrlKey && !event.altKey && !event.metaKey) {
      event.preventDefault();
      this.openShortcutsGuide();
      return;
    }

    if (event.ctrlKey && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      this.canvasService.undo();
      return;
    }

    if (event.ctrlKey && (event.key.toLowerCase() === 'y' || (event.shiftKey && event.key.toLowerCase() === 'z'))) {
      event.preventDefault();
      this.canvasService.redo();
      return;
    }

    if ((event.key === 'Delete' || event.key === 'Backspace') && !this.canvasService.isDrawingConnection) {
      // Delete selected bounded contexts
      const selectedBCs = this.canvasService.boardState.boundedContexts.filter(bc => bc.selected);
      for (const bc of selectedBCs) {
        const command = new DeleteBoundedContextCommand({
          id: bc.id,
          name: bc.name,
          x: bc.x,
          y: bc.y,
          width: bc.width,
          height: bc.height
        });
        this.canvasService.executeCommand(command);
      }

      const selectedNoteIds = this.canvasService.boardState.notes.filter(n => n.selected).map(n => n.id);
      const notes = JSON.parse(JSON.stringify(this.canvasService.boardState.notes.filter(n => n.selected))) as Note[];
      const connections = JSON.parse(JSON.stringify(this.canvasService.boardState.connections.filter(c =>
        c.selected ||
        selectedNoteIds.includes(c.fromNoteId) ||
        selectedNoteIds.includes(c.toNoteId)
      ))) as Connection[];
      if (notes.length > 0 || connections.length > 0) {
        const command = new DeleteNotesCommand(notes, connections);
        this.canvasService.executeCommand(command);
      }
    }

    // Handle Ctrl+C (Copy)  
    if (event.ctrlKey && event.key.toLowerCase() === 'c') {
      this.copySelectedNotes();
      event.preventDefault();
    }

    // Handle Ctrl+V (Paste)  
    if (event.ctrlKey && event.key.toLowerCase() === 'v') {
      this.pasteCopiedNotes();
      event.preventDefault();
    }
  }

  public onMouseMove(event: MouseEvent): void {
    this.currentMousePos = this.getMousePos(event);
    this.broadcastCursorPositionIfNeeded();

    if (this.handlePanning(event)) return;

    // BC draw preview
    if (this.bcDrawStart && this.bcDrawPreview) {
      const { x, y } = this.currentMousePos;
      this.bcDrawPreview = {
        x: this.bcDrawStart.x,
        y: this.bcDrawStart.y,
        width: x - this.bcDrawStart.x,
        height: y - this.bcDrawStart.y
      };
      this.drawCanvas();
      return;
    }

    // BC resize
    if (this.resizingBoundedContext && this.bcResizeCorner && this.bcInitialResizeState) {
      const { x, y } = this.currentMousePos;
      this.applyBCResize(this.resizingBoundedContext, this.bcResizeCorner, this.bcInitialResizeState, x, y);
      this.drawCanvas();
      return;
    }

    // BC drag
    if (this.draggingBoundedContext) {
      const { x, y } = this.currentMousePos;
      this.draggingBoundedContext.x = x - this.bcDragOffsetX;
      this.draggingBoundedContext.y = y - this.bcDragOffsetY;
      this.drawCanvas();
      return;
    }

    if (this.handleResize()) return;
    if (this.handleDragging()) return;
    if (this.handleSelection()) return;
    if (this.handleHover()) return;
    if (this.handleTemporaryArrow(event)) return;

    this.updateHoverState();
    this.canvas().nativeElement.style.cursor = this.getCursorStyle();
    this.drawCanvas();
  }

  @HostListener('window:resize')
  public onResize(): void {
    if (!this.ctx) return;
    this.canvas().nativeElement.width = window.innerWidth;
    this.canvas().nativeElement.height = window.innerHeight;
    this.drawCanvas();
  }

  public ngOnInit(): void {
    this.canvasService.canvasUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.drawCanvas();
      });

    this.canvasService.canvasImageDownloaded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.exportBoardAsImage();
      });

    this.themeService.theme$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.ctx) {
          this.drawCanvas();
        }
      });
  }

  public ngAfterViewInit(): void {
    this.generateCanvas();
  }

  private generateCanvas(): void {
    this.ctx = this.canvas().nativeElement.getContext('2d')!;
    this.canvas().nativeElement.width = window.innerWidth;
    this.canvas().nativeElement.height = window.innerHeight;

    this.minimapCtx = this.minimap().nativeElement.getContext('2d')!;
    this.minimap().nativeElement.width = 260;
    this.minimap().nativeElement.height = 185;
    this.drawCanvas();
  }

  private exportBoardAsImage(): void {
    const canvasEl = this.canvas().nativeElement;
    const tempCanvas = document.createElement('canvas');
    const tempCtx = tempCanvas.getContext('2d')!;

    // Set the size of the temporary canvas to match the original canvas
    tempCanvas.width = canvasEl.width;
    tempCanvas.height = canvasEl.height;

    // Fill the temporary canvas with the dark theme background
    tempCtx.fillStyle = this.theme.surface;
    tempCtx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);

    // Draw the original canvas content on top of the white background
    tempCtx.drawImage(canvasEl, 0, 0);

    // Export the temporary canvas as an image
    const link = document.createElement('a');
    link.download = `${this.canvasService.boardState.name || 'board'}.png`;
    link.href = tempCanvas.toDataURL('image/png');
    link.click();
  }

  // ── Kinetic Ionization theme constants for Canvas API ──
  private static readonly DARK_THEME = {
    surface: '#10131c',
    surfaceDim: '#0d0f16',
    surfaceContainerLow: '#181c24',
    surfaceContainerLowest: '#12151e',
    surfaceContainerHigh: '#282c34',
    surfaceContainerHighest: '#31353e',
    primary: '#dbfcff',
    primaryContainer: '#00f0ff',
    onPrimary: '#10131c',
    secondary: '#f0b4ff',
    secondaryContainer: '#e040fb',
    tertiary: '#ff4081',
    onSurface: '#e2e2e6',
    onSurfaceVariant: '#a0a4ad',
    outlineVariant: '#44474e',
    gridLine: 'rgba(0, 240, 255, 0.05)',
    ghostBorder: 'rgba(68, 71, 78, 0.15)',
    noteSurface: '#282c34',
    fontDisplay: "'Space Grotesk', system-ui, sans-serif",
    fontBody: "'Inter', -apple-system, sans-serif",
  };

  private static readonly LIGHT_THEME = {
    surface: '#f5f5f7',
    surfaceDim: '#eaeaed',
    surfaceContainerLow: '#eeeeef',
    surfaceContainerLowest: '#e8e8eb',
    surfaceContainerHigh: '#f5f5f7',
    surfaceContainerHighest: '#fafafa',
    primary: '#004d52',
    primaryContainer: '#00838f',
    onPrimary: '#ffffff',
    secondary: '#7b1fa2',
    secondaryContainer: '#9c27b0',
    tertiary: '#c51162',
    onSurface: '#1a1c1e',
    onSurfaceVariant: '#44474e',
    outlineVariant: '#c4c7ce',
    gridLine: 'rgba(0, 131, 143, 0.08)',
    ghostBorder: 'rgba(0, 0, 0, 0.12)',
    noteSurface: '#ffffff',
    fontDisplay: "'Space Grotesk', system-ui, sans-serif",
    fontBody: "'Inter', -apple-system, sans-serif",
  };

  private get theme() {
    return this.themeService.isDark ? BoardCanvasComponent.DARK_THEME : BoardCanvasComponent.LIGHT_THEME;
  }

  private drawCanvas(): void {
    if (this.rafPending) return;
    this.rafPending = true;
    requestAnimationFrame(() => {
      this.rafPending = false;
      this.drawCanvasFrame();
    });
  }

  private drawCanvasFrame(): void {
    const canvasEl = this.canvas().nativeElement;
    const T = this.theme;
    this.ctx.setTransform(this.canvasService.scale, 0, 0, this.canvasService.scale, this.canvasService.originX, this.canvasService.originY);

    const vx = -this.canvasService.originX / this.canvasService.scale;
    const vy = -this.canvasService.originY / this.canvasService.scale;
    const vw = canvasEl.width / this.canvasService.scale;
    const vh = canvasEl.height / this.canvasService.scale;

    // Dark background
    this.ctx.fillStyle = T.surface;
    this.ctx.fillRect(vx, vy, vw, vh);

    // Subtle cyan grid
    this.ctx.strokeStyle = T.gridLine;
    this.ctx.lineWidth = 1;
    const gridSize = 40;
    const startX = Math.floor(vx / gridSize) * gridSize;
    const startY = Math.floor(vy / gridSize) * gridSize;
    this.ctx.beginPath();
    for (let x = startX; x <= vx + vw; x += gridSize) {
      this.ctx.moveTo(x, vy);
      this.ctx.lineTo(x, vy + vh);
    }
    for (let y = startY; y <= vy + vh; y += gridSize) {
      this.ctx.moveTo(vx, y);
      this.ctx.lineTo(vx + vw, y);
    }
    this.ctx.stroke();

    // Draw bounded contexts BEFORE notes (renders behind)
    this.canvasService.boardState.boundedContexts.forEach(bc => {
      this.drawBoundedContext(bc);
    });

    this.canvasService.boardState.notes.forEach(note => {
      this.drawNote(note);
    });

    this.drawConnections();
    this.drawRemoteCursors();

    if (this.isSelecting) {
      this.ctx.save();
      this.ctx.strokeStyle = T.primaryContainer;
      this.ctx.lineWidth = 1;
      this.ctx.setLineDash([4, 2]);
      this.ctx.strokeRect(this.selectionRect.x, this.selectionRect.y, this.selectionRect.width, this.selectionRect.height);
      this.ctx.restore();
    }

    // Draw bounded context creation preview
    if (this.bcDrawPreview && (Math.abs(this.bcDrawPreview.width) > 5 || Math.abs(this.bcDrawPreview.height) > 5)) {
      this.ctx.save();
      this.ctx.fillStyle = 'rgba(0, 188, 212, 0.08)';
      this.ctx.fillRect(this.bcDrawPreview.x, this.bcDrawPreview.y, this.bcDrawPreview.width, this.bcDrawPreview.height);
      this.ctx.setLineDash([8, 4]);
      this.ctx.lineWidth = 2;
      this.ctx.strokeStyle = T.primary;
      this.ctx.strokeRect(this.bcDrawPreview.x, this.bcDrawPreview.y, this.bcDrawPreview.width, this.bcDrawPreview.height);
      this.ctx.setLineDash([]);
      this.ctx.restore();
    }

    // Indicator square (only when hovering before starting a connection)
    if (this.hoveredConnectionStartNote && this.hoveredConnectionStartPos && this.canvasService.isDrawingConnection && !this.arrowStartNote) {
      this.ctx.save();
      this.ctx.fillStyle = T.primaryContainer;
      const indicatorSize = 8;
      this.ctx.fillRect(
        this.hoveredConnectionStartPos.x - indicatorSize / 2,
        this.hoveredConnectionStartPos.y - indicatorSize / 2,
        indicatorSize,
        indicatorSize
      );
      this.ctx.restore();
    }

    this.drawMinimap();
  }

  private drawRemoteCursors(): void {
    for (const cursor of this.canvasService.remoteCursors.values()) {
      const user = new BoardUser(cursor.boardId, cursor.userName, cursor.connectionId);
      const color = user.getColour();

      this.ctx.save();

      // Shadow for pointer
      this.ctx.shadowColor = 'rgba(0, 0, 0, 0.4)';
      this.ctx.shadowBlur = 8;
      this.ctx.shadowOffsetX = 0;
      this.ctx.shadowOffsetY = 2;

      // Draw cursor pointer (Figma-style arrow)
      this.ctx.beginPath();
      this.ctx.moveTo(cursor.x, cursor.y);
      this.ctx.lineTo(cursor.x + 14, cursor.y + 10);
      this.ctx.lineTo(cursor.x + 8, cursor.y + 11);
      this.ctx.lineTo(cursor.x + 4, cursor.y + 18);
      this.ctx.closePath();

      // Fill with user color
      this.ctx.fillStyle = color;
      this.ctx.fill();

      // Thin border stroke
      this.ctx.strokeStyle = this.theme.surfaceContainerLow;
      this.ctx.lineWidth = 1.5;
      this.ctx.lineJoin = 'round';
      this.ctx.stroke();

      // Remove shadow for text rendering clarity
      this.ctx.shadowColor = 'transparent';

      const T = this.theme;
      this.ctx.font = `700 11px ${T.fontDisplay}`;
      const textWidth = this.ctx.measureText(cursor.userName).width;
      const paddingX = 8;
      const labelWidth = textWidth + paddingX * 2;
      const labelHeight = 20;
      const labelX = cursor.x + 12;
      const labelY = cursor.y + 16;

      // Dark glass label background (0px radius)
      this.ctx.fillStyle = T.surfaceContainerHigh;
      this.ctx.fillRect(labelX, labelY, labelWidth, labelHeight);

      // Ghost border on label
      this.ctx.strokeStyle = T.ghostBorder;
      this.ctx.lineWidth = 1;
      this.ctx.strokeRect(labelX, labelY, labelWidth, labelHeight);

      // Draw label text
      this.ctx.fillStyle = T.primary;
      this.ctx.textAlign = 'left';
      this.ctx.textBaseline = 'middle';
      this.ctx.fillText(cursor.userName, labelX + paddingX, labelY + labelHeight / 2 + 1);

      this.ctx.restore();
    }
  }

  private broadcastCursorPositionIfNeeded(): void {
    if (!this.canvasService.id) {
      return;
    }

    const now = Date.now();
    if (now - this.lastCursorBroadcastAt < BoardCanvasComponent.CURSOR_BROADCAST_INTERVAL_MS) {
      return;
    }

    if (!Number.isFinite(this.currentMousePos.x) || !Number.isFinite(this.currentMousePos.y)) {
      return;
    }

    const cursorEvent: CursorPositionUpdatedEvent = {
      boardId: this.canvasService.id,
      connectionId: '',
      userName: this.localUserName,
      x: this.currentMousePos.x,
      y: this.currentMousePos.y
    };

    this.boardsHub.broadcastCursorPositionUpdated(cursorEvent);
    this.lastCursorBroadcastAt = now;
  }

  private drawBoundedContext(bc: BoundedContext): void {
    const T = this.theme;
    this.ctx.save();

    // Semi-transparent fill (~8% opacity)
    this.ctx.fillStyle = 'rgba(0, 188, 212, 0.08)';
    this.ctx.fillRect(bc.x, bc.y, bc.width, bc.height);

    // Dashed border
    this.ctx.setLineDash([8, 4]);

    if (bc.selected) {
      // Selected: thicker border + cyan glow
      this.ctx.lineWidth = 3;
      this.ctx.shadowColor = T.primary;
      this.ctx.shadowBlur = 16;
      this.ctx.strokeStyle = T.primary;
    } else {
      this.ctx.lineWidth = 2;
      this.ctx.strokeStyle = T.onSurfaceVariant;
    }

    this.ctx.strokeRect(bc.x, bc.y, bc.width, bc.height);

    // Reset line dash and shadow
    this.ctx.setLineDash([]);
    this.ctx.shadowColor = 'transparent';
    this.ctx.shadowBlur = 0;

    // Title above top-left corner
    if (bc.name) {
      this.ctx.font = `700 13px ${T.fontDisplay}`;
      this.ctx.letterSpacing = '1.3px';
      this.ctx.fillStyle = bc.selected ? T.primary : T.onSurfaceVariant;
      this.ctx.textAlign = 'left';
      this.ctx.textBaseline = 'bottom';
      this.ctx.fillText(bc.name.toUpperCase(), bc.x + 4, bc.y - 6);
      this.ctx.letterSpacing = '0px';
    }

    // Draw resize handles when selected
    if (bc.selected) {
      const handleSize = 12;
      const half = handleSize / 2;
      const corners = [
        { x: bc.x, y: bc.y },
        { x: bc.x + bc.width, y: bc.y },
        { x: bc.x, y: bc.y + bc.height },
        { x: bc.x + bc.width, y: bc.y + bc.height }
      ];
      for (const c of corners) {
        this.ctx.fillStyle = T.primary;
        this.ctx.fillRect(c.x - half, c.y - half, handleSize, handleSize);
        this.ctx.strokeStyle = T.surface;
        this.ctx.lineWidth = 1.5;
        this.ctx.strokeRect(c.x - half, c.y - half, handleSize, handleSize);
      }
    }

    this.ctx.restore();
  }

  private drawMinimap(): void {
    const T = this.theme;
    const minimapCanvas = this.minimap().nativeElement;
    const ctx = this.minimapCtx;

    // Dark minimap background
    ctx.fillStyle = T.surfaceContainerLowest;
    ctx.fillRect(0, 0, minimapCanvas.width, minimapCanvas.height);

    const { minX, minY, dynamicScale } = this.getCanvasBoundsAndScale();

    this.canvasService.boardState.notes.forEach(note => {
      ctx.fillStyle = getNoteColor(note.type);
      ctx.fillRect(
        (note.x - minX) * dynamicScale,
        (note.y - minY) * dynamicScale,
        note.width * dynamicScale,
        note.height * dynamicScale
      );
    });

    this.canvasService.boardState.connections.forEach(connection => {
      const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
      const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
      if (fromNote && toNote) {
        const from = this.getClosestSideCenter(fromNote, toNote);
        const to = this.getClosestSideCenter(toNote, fromNote);

        ctx.strokeStyle = T.onSurfaceVariant;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo((from.x - minX) * dynamicScale, (from.y - minY) * dynamicScale);
        ctx.lineTo((to.x - minX) * dynamicScale, (to.y - minY) * dynamicScale);
        ctx.stroke();
      }
    });

    // Draw bounded context outlines on minimap
    this.canvasService.boardState.boundedContexts.forEach(bc => {
      ctx.strokeStyle = T.onSurfaceVariant;
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 2]);
      ctx.strokeRect(
        (bc.x - minX) * dynamicScale,
        (bc.y - minY) * dynamicScale,
        bc.width * dynamicScale,
        bc.height * dynamicScale
      );
      ctx.setLineDash([]);
    });

    const viewportRect = {
      x: (-this.canvasService.originX) / this.canvasService.scale,
      y: (-this.canvasService.originY) / this.canvasService.scale,
      width: this.canvas().nativeElement.width / this.canvasService.scale,
      height: this.canvas().nativeElement.height / this.canvasService.scale
    };

    ctx.strokeStyle = T.primaryContainer;
    ctx.lineWidth = 1;
    ctx.strokeRect(
      (viewportRect.x - minX) * dynamicScale,
      (viewportRect.y - minY) * dynamicScale,
      viewportRect.width * dynamicScale,
      viewportRect.height * dynamicScale
    );
  }

  private getCanvasBoundsAndScale() {
    const minimapCanvas = this.minimap().nativeElement;

    if (this.canvasService.boardState.notes.length === 0 && this.canvasService.boardState.boundedContexts.length === 0) {
      return {
        minX: 0, minY: 0, dynamicScale: 1
      };
    }

    const padding = 20;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of this.canvasService.boardState.notes) {
      if (n.x < minX) minX = n.x;
      if (n.y < minY) minY = n.y;
      if (n.x + n.width > maxX) maxX = n.x + n.width;
      if (n.y + n.height > maxY) maxY = n.y + n.height;
    }
    for (const bc of this.canvasService.boardState.boundedContexts) {
      if (bc.x < minX) minX = bc.x;
      if (bc.y - 20 < minY) minY = bc.y - 20; // account for title above
      if (bc.x + bc.width > maxX) maxX = bc.x + bc.width;
      if (bc.y + bc.height > maxY) maxY = bc.y + bc.height;
    }
    minX -= padding;
    minY -= padding;
    maxX += padding;
    maxY += padding;

    const usedWidth = maxX - minX;
    const usedHeight = maxY - minY;

    const scaleX = minimapCanvas.width / usedWidth;
    const scaleY = minimapCanvas.height / usedHeight;

    const dynamicScale = Math.min(scaleX, scaleY);

    return { minX, minY, dynamicScale };
  }

  private drawTemporaryArrow(event: MouseEvent): void {
    this.drawCanvas(); // clear and redraw existing state first  

    const { x: mouseX, y: mouseY } = this.getMousePos(event);

    if (!this.arrowStartNote) return;

    const from = this.getClosestSideCenter(this.arrowStartNote, { x: mouseX, y: mouseY });
    const to = { x: mouseX, y: mouseY };

    this.drawArrow(from.x, from.y, to.x, to.y);
  }

  private getClosestSideCenter(noteA: Note, noteB: { x: number; y: number; width?: number; height?: number }): { x: number; y: number } {
    const centerA = {
      x: noteA.x + noteA.width / 2,
      y: noteA.y + noteA.height / 2
    };
    const centerB = {
      x: noteB.x + (noteB.width || 0) / 2,
      y: noteB.y + (noteB.height || 0) / 2
    };

    const deltaX = centerB.x - centerA.x;
    const deltaY = centerB.y - centerA.y;

    const absDeltaX = Math.abs(deltaX);
    const absDeltaY = Math.abs(deltaY);

    if (absDeltaX > absDeltaY) {
      if (deltaX > 0) {
        return { x: noteA.x + noteA.width, y: centerA.y };
      } else {
        return { x: noteA.x, y: centerA.y };
      }
    } else {
      if (deltaY > 0) {
        return { x: centerA.x, y: noteA.y + noteA.height };
      } else {
        return { x: centerA.x, y: noteA.y };
      }
    }
  }

  private isEditableTarget(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) {
      return false;
    }

    const editableSelector = 'input, textarea, select, [contenteditable="true"], [contenteditable=""]';
    return target.matches(editableSelector) || target.closest(editableSelector) !== null || target.isContentEditable;
  }

  private drawNote(note: Note): void {
    const T = this.theme;
    const typeColor = getNoteColor(note.type);

    // Glow shadow — selected uses cyan, unselected uses type-color glow
    if (note.selected) {
      this.ctx.shadowColor = 'rgba(0, 240, 255, 0.35)';
      this.ctx.shadowBlur = 28;
      this.ctx.shadowOffsetX = 0;
      this.ctx.shadowOffsetY = 0;
    } else {
      this.ctx.shadowColor = this.hexToGlow(typeColor, 0.22);
      this.ctx.shadowBlur = 20;
      this.ctx.shadowOffsetX = 0;
      this.ctx.shadowOffsetY = 2;
    }

    // Note surface fill — elevated from canvas
    this.ctx.beginPath();
    this.ctx.rect(note.x, note.y, note.width, note.height);
    this.ctx.closePath();
    this.ctx.fillStyle = T.noteSurface;
    this.ctx.fill();

    // Type-color tint overlay
    this.ctx.fillStyle = this.hexToGlow(typeColor, 0.05);
    this.ctx.fill();

    this.resetShadow();

    // Left accent bar (draw before border so it sits flush)
    this.ctx.fillStyle = typeColor;
    this.ctx.fillRect(note.x, note.y, 3, note.height);

    // Border — type-colored (or cyan selection border)
    this.ctx.beginPath();
    this.ctx.rect(note.x, note.y, note.width, note.height);
    if (note.selected) {
      this.ctx.strokeStyle = T.primaryContainer;
      this.ctx.lineWidth = 2;
    } else {
      this.ctx.strokeStyle = this.hexToGlow(typeColor, 0.35);
      this.ctx.lineWidth = 1;
    }
    this.ctx.stroke();

    // Type-colored ID chip in top-left
    const chipText = note.type.toUpperCase();
    this.ctx.font = `700 9px ${T.fontDisplay}`;
    const chipWidth = this.ctx.measureText(chipText).width + 12;
    const chipHeight = 18;
    const chipX = note.x + 8;
    const chipY = note.y + 8;

    this.ctx.fillStyle = typeColor;
    this.ctx.fillRect(chipX, chipY, chipWidth, chipHeight);
    this.ctx.fillStyle = T.onPrimary;
    this.ctx.textAlign = 'left';
    this.ctx.textBaseline = 'middle';
    this.ctx.fillText(chipText, chipX + 6, chipY + chipHeight / 2);

    // Draw text
    this.drawNoteText(note);
  }

  private resetShadow(): void {
    this.ctx.shadowBlur = 0;
    this.ctx.shadowOffsetX = 0;
    this.ctx.shadowOffsetY = 0;
  }

  private hexToGlow(hex: string, alpha: number): string {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  private drawNoteText(note: Note): void {
    const T = this.theme;
    const fontSize = note.width < 80 ? 12 : 14;
    this.ctx.fillStyle = T.onSurface;
    this.ctx.font = `600 ${fontSize}px ${T.fontBody}`;
    this.ctx.textAlign = 'center';
    this.ctx.textBaseline = 'middle';

    const topPadding = 32; // Space for chip
    const lines = this.getWrappedTextLines(note.text, note.width - 24);
    const lineHeight = fontSize + 4;
    const textBlockHeight = lines.length * lineHeight;
    let textY = note.y + topPadding + (note.height - topPadding - textBlockHeight) / 2 + lineHeight / 2;

    for (const line of lines) {
      this.ctx.fillText(line, note.x + note.width / 2, textY);
      textY += lineHeight;
    }

    this.ctx.textAlign = 'start';
    this.ctx.textBaseline = 'alphabetic';
  }

  private getWrappedTextLines(text: string, maxWidth: number): string[] {
    const words = text.split(' ');
    let lines: string[] = [];
    let currentLine = words[0];

    for (let i = 1; i < words.length; i++) {
      const word = words[i];
      const width = this.ctx.measureText(currentLine + ' ' + word).width;
      if (width < maxWidth) {
        currentLine += ' ' + word;
      } else {
        lines.push(currentLine);
        currentLine = word;
      }
    }
    lines.push(currentLine);
    return lines;
  }

  private drawConnections(): void {
    const noteMap = new Map<string, Note>();
    for (const n of this.canvasService.boardState.notes) {
      noteMap.set(n.id, n);
    }
    this.canvasService.boardState.connections.forEach(connection => {
      const fromNote = noteMap.get(connection.fromNoteId);
      const toNote = noteMap.get(connection.toNoteId);
      if (fromNote && toNote) {
        const from = this.getClosestSideCenter(fromNote, toNote);
        const to = this.getClosestSideCenter(toNote, fromNote);
        const hovered = connection === this.hoveredConnection;
        this.drawArrow(from.x, from.y, to.x, to.y, connection.selected, hovered);
      }
    });
  }

  private drawArrow(fromX: number, fromY: number, toX: number, toY: number, selected = false, hovered = false): void {
    const headLength = 12;
    const angle = Math.atan2(toY - fromY, toX - fromX);

    const T = this.theme;
    const baseColor = T.onSurfaceVariant;
    const hoverColor = T.primary;
    const selectedColor = T.primaryContainer;

    const color = selected ? selectedColor : (hovered ? hoverColor : baseColor);

    this.ctx.strokeStyle = color;
    this.ctx.fillStyle = color;
    this.ctx.lineWidth = selected ? 3 : 2;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';

    if (hovered || selected) {
      this.ctx.shadowBlur = 10;
      this.ctx.shadowColor = 'rgba(0, 240, 255, 0.25)';
      this.ctx.shadowOffsetX = 0;
      this.ctx.shadowOffsetY = 0;
    } else {
      this.resetShadow();
    }

    // Draw the main line
    this.ctx.beginPath();
    this.ctx.moveTo(fromX, fromY);
    this.ctx.lineTo(toX, toY);
    this.ctx.stroke();

    // Draw a more modern, slightly sleeker arrowhead
    this.ctx.beginPath();
    this.ctx.moveTo(toX, toY);
    this.ctx.lineTo(toX - headLength * Math.cos(angle - Math.PI / 7), toY - headLength * Math.sin(angle - Math.PI / 7));
    // Pull the back of the arrow in slightly for a swept dart look
    this.ctx.lineTo(toX - (headLength - 3) * Math.cos(angle), toY - (headLength - 3) * Math.sin(angle));
    this.ctx.lineTo(toX - headLength * Math.cos(angle + Math.PI / 7), toY - headLength * Math.sin(angle + Math.PI / 7));
    this.ctx.closePath();
    this.ctx.fill();

    this.resetShadow();
  }

  private getMousePos(event: MouseEvent | WheelEvent): Coordinates {
    const rect = this.canvas().nativeElement.getBoundingClientRect();
    return {
      x: (event.clientX - rect.left - this.canvasService.originX) / this.canvasService.scale,
      y: (event.clientY - rect.top - this.canvasService.originY) / this.canvasService.scale
    };
  }

  private getResizeCorner(note: Note, mouseX: number, mouseY: number): 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | null {
    const size = this.resizeHandleSize;
    const corners = {
      'top-left': { x: note.x, y: note.y },
      'top-right': { x: note.x + note.width, y: note.y },
      'bottom-left': { x: note.x, y: note.y + note.height },
      'bottom-right': { x: note.x + note.width, y: note.y + note.height }
    };

    for (const [corner, pos] of Object.entries(corners)) {
      if (
        mouseX >= pos.x - size && mouseX <= pos.x + size &&
        mouseY >= pos.y - size && mouseY <= pos.y + size
      ) {
        return corner as 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
      }
    }
    return null;
  }

  private isPointNearArrow(x: number, y: number, fromNote: Note, toNote: Note): boolean {
    const from = this.getClosestSideCenter(fromNote, toNote);
    const to = this.getClosestSideCenter(toNote, fromNote);

    const distance = this.distancePointToLine(x, y, from.x, from.y, to.x, to.y);
    return distance < 8; // threshold of 8 pixels for selection  
  }

  private distancePointToLine(px: number, py: number, x1: number, y1: number, x2: number, y2: number): number {
    const A = px - x1;
    const B = py - y1;
    const C = x2 - x1;
    const D = y2 - y1;

    const dot = A * C + B * D;
    const len_sq = C * C + D * D;
    let param = -1;
    if (len_sq !== 0) param = dot / len_sq;

    let xx, yy;

    if (param < 0) {
      xx = x1;
      yy = y1;
    } else if (param > 1) {
      xx = x2;
      yy = y2;
    } else {
      xx = x1 + param * C;
      yy = y1 + param * D;
    }

    const dx = px - xx;
    const dy = py - yy;
    return Math.sqrt(dx * dx + dy * dy);
  }

  private isPointInsideNote(note: Note, x: number, y: number): boolean {
    return x >= note.x && x <= note.x + note.width && y >= note.y && y <= note.y + note.height;
  }

  private isPointInsideBCBorder(bc: BoundedContext, x: number, y: number): boolean {
    const borderWidth = 12; // Detection tolerance for the border area
    const titleHeight = 24; // Title label area above the frame
    const inOuter = x >= bc.x - borderWidth && x <= bc.x + bc.width + borderWidth &&
                    y >= bc.y - titleHeight - borderWidth && y <= bc.y + bc.height + borderWidth;
    const inInner = x >= bc.x + borderWidth && x <= bc.x + bc.width - borderWidth &&
                    y >= bc.y + borderWidth && y <= bc.y + bc.height - borderWidth;
    return inOuter && !inInner;
  }

  private isPointInsideBCInterior(bc: BoundedContext, x: number, y: number): boolean {
    return x >= bc.x && x <= bc.x + bc.width && y >= bc.y && y <= bc.y + bc.height;
  }

  private getBCResizeCorner(bc: BoundedContext, x: number, y: number): 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | null {
    const handleSize = 14;
    type CornerName = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
    const corners: { name: CornerName; cx: number; cy: number }[] = [
      { name: 'top-left', cx: bc.x, cy: bc.y },
      { name: 'top-right', cx: bc.x + bc.width, cy: bc.y },
      { name: 'bottom-left', cx: bc.x, cy: bc.y + bc.height },
      { name: 'bottom-right', cx: bc.x + bc.width, cy: bc.y + bc.height }
    ];
    for (const corner of corners) {
      if (Math.abs(x - corner.cx) < handleSize && Math.abs(y - corner.cy) < handleSize) {
        return corner.name;
      }
    }
    return null;
  }

  private applyBCResize(bc: BoundedContext, corner: string, initial: { x: number; y: number; width: number; height: number }, mx: number, my: number): void {
    const minSize = 100;
    switch (corner) {
      case 'top-left':
        bc.width = Math.max(minSize, initial.x + initial.width - mx);
        bc.height = Math.max(minSize, initial.y + initial.height - my);
        bc.x = initial.x + initial.width - bc.width;
        bc.y = initial.y + initial.height - bc.height;
        break;
      case 'top-right':
        bc.width = Math.max(minSize, mx - initial.x);
        bc.height = Math.max(minSize, initial.y + initial.height - my);
        bc.y = initial.y + initial.height - bc.height;
        break;
      case 'bottom-left':
        bc.width = Math.max(minSize, initial.x + initial.width - mx);
        bc.height = Math.max(minSize, my - initial.y);
        bc.x = initial.x + initial.width - bc.width;
        break;
      case 'bottom-right':
        bc.width = Math.max(minSize, mx - initial.x);
        bc.height = Math.max(minSize, my - initial.y);
        break;
    }
  }

  private promptBoundedContextName(x: number, y: number, width: number, height: number): void {
    const dialogRef = this.dialog.open(BcNameModalComponent, {
      width: '400px',
      data: { name: '' }
    });

    dialogRef.afterClosed().subscribe((name: string | undefined) => {
      if (name && name.trim()) {
        const command = new CreateBoundedContextCommand({
          id: uuid(),
          name: name.trim(),
          x, y, width, height
        });
        this.canvasService.executeCommand(command);
      }
    });
    this.canvasService.isDrawingBoundedContext = false;
  }

  private editBoundedContextName(bc: BoundedContext): void {
    const dialogRef = this.dialog.open(BcNameModalComponent, {
      width: '400px',
      data: { name: bc.name }
    });

    dialogRef.afterClosed().subscribe((newName: string | undefined) => {
      if (newName !== undefined && newName.trim() && newName.trim() !== bc.name) {
        const command = new UpdateBoundedContextCommand(
          bc.id, bc.name, newName.trim(),
          undefined, undefined, undefined, undefined,
          undefined, undefined, undefined, undefined
        );
        this.canvasService.executeCommand(command);
      }
    });
  }

  private selectNotesAndConnectionsInRect(): void {
    const rect = this.selectionRect;

    this.canvasService.boardState.notes.forEach(note => {
      note.selected = note.x + note.width >= rect.x &&
        note.x <= rect.x + rect.width &&
        note.y + note.height >= rect.y &&
        note.y <= rect.y + rect.height;
    });

    this.canvasService.boardState.connections.forEach(connection => {
      const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
      const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
      if (fromNote && toNote) {
        connection.selected = fromNote.selected && toNote.selected;
      }
    });
  } 

  private handlePanning(event: MouseEvent): boolean {
    if (this.panning) {
      const deltaX = event.clientX - this.lastPanX;
      const deltaY = event.clientY - this.lastPanY;

      this.canvasService.originX += deltaX;
      this.canvasService.originY += deltaY;

      this.lastPanX = event.clientX;
      this.lastPanY = event.clientY;

      this.canvas().nativeElement.style.cursor = 'grab';
      this.drawCanvas();
      return true;
    }
    return false;
  }

  private handleResize(): boolean {
    if (this.resizingNote && this.resizeCorner) {
      const note = this.resizingNote;

      switch (this.resizeCorner) {
        case 'top-left':
          const newWidthTL = note.x + note.width - this.currentMousePos.x;
          const newHeightTL = note.y + note.height - this.currentMousePos.y;
          if (newWidthTL > 50) {
            note.width = newWidthTL;
            note.x = this.currentMousePos.x;
          }
          if (newHeightTL > 30) {
            note.height = newHeightTL;
            note.y = this.currentMousePos.y;
          }
          break;

        case 'top-right':
          const newWidthTR = this.currentMousePos.x - note.x;
          const newHeightTR = note.y + note.height - this.currentMousePos.y;
          if (newWidthTR > 50) {
            note.width = newWidthTR;
          }
          if (newHeightTR > 30) {
            note.height = newHeightTR;
            note.y = this.currentMousePos.y;
          }
          break;

        case 'bottom-left':
          const newWidthBL = note.x + note.width - this.currentMousePos.x;
          const newHeightBL = this.currentMousePos.y - note.y;
          if (newWidthBL > 50) {
            note.width = newWidthBL;
            note.x = this.currentMousePos.x;
          }
          if (newHeightBL > 30) {
            note.height = newHeightBL;
          }
          break;

        case 'bottom-right':
          const newWidthBR = this.currentMousePos.x - note.x;
          const newHeightBR = this.currentMousePos.y - note.y;
          if (newWidthBR > 50) {
            note.width = newWidthBR;
          }
          if (newHeightBR > 30) {
            note.height = newHeightBR;
          }
          break;
      }

      this.canvas().nativeElement.style.cursor = `${this.resizeCorner}-resize`;
      this.drawCanvas();
      return true;
    }
    return false;
  }

  private handleDragging(): boolean {
    if (this.draggingNote) {
      const deltaX = this.currentMousePos.x - this.dragOffsetX - this.draggingNote.x;
      const deltaY = this.currentMousePos.y - this.dragOffsetY - this.draggingNote.y;

      this.canvasService.boardState.notes.filter(n => n.selected).forEach(note => {
        note.x += deltaX;
        note.y += deltaY;
      });

      this.canvas().nativeElement.style.cursor = 'move';
      this.drawCanvas();
      return true;
    }
    return false;
  }

  private handleSelection(): boolean {
    if (this.isSelecting) {
      this.selectionRect = {
        x: Math.min(this.selectionStart.x, this.currentMousePos.x),
        y: Math.min(this.selectionStart.y, this.currentMousePos.y),
        width: Math.abs(this.currentMousePos.x - this.selectionStart.x),
        height: Math.abs(this.currentMousePos.y - this.selectionStart.y)
      };
      this.canvas().nativeElement.style.cursor = 'crosshair';
      this.drawCanvas();
      return true;
    }
    return false;
  }

  private handleHover(): boolean {
    this.hoveredConnection = null;
    this.hoveredConnectionStartNote = null;
    this.hoveredConnectionStartPos = null;

    if (this.canvasService.isDrawingConnection && !this.arrowStartNote) {
      let foundHover = false;
      for (let note of [...this.canvasService.boardState.notes].reverse()) {
        if (this.currentMousePos.x >= note.x - 10 && this.currentMousePos.x <= note.x + note.width + 10 && this.currentMousePos.y >= note.y - 10 && this.currentMousePos.y <= note.y + note.height + 10) {
          const sideCenter = this.getClosestSideCenter(note, { x: this.currentMousePos.x, y: this.currentMousePos.y });
          this.hoveredConnectionStartNote = note;
          this.hoveredConnectionStartPos = sideCenter;
          foundHover = true;
          break;
        }
      }
      this.canvas().nativeElement.style.cursor = foundHover ? 'crosshair' : 'default';
      this.drawCanvas();
      return true;
    }

    return false;
  }

  private handleTemporaryArrow(event: MouseEvent): boolean {
    if (this.canvasService.isDrawingConnection) {
      this.drawTemporaryArrow(event);
      this.canvas().nativeElement.style.cursor = 'crosshair';
      return true;
    }
    return false;
  }

  private updateHoverState(): void {
    this.hoveredNote = null;
    this.hoveredConnection = null;
    this.hoveredBoundedContext = null;

    for (const note of [...this.canvasService.boardState.notes].reverse()) {
      if (this.getResizeCorner(note, this.currentMousePos.x, this.currentMousePos.y) ||
          this.isPointInsideNote(note, this.currentMousePos.x, this.currentMousePos.y)) {
        this.hoveredNote = note;
        return;
      }
    }

    for (const connection of this.canvasService.boardState.connections) {
      const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
      const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
      if (fromNote && toNote && this.isPointNearArrow(this.currentMousePos.x, this.currentMousePos.y, fromNote, toNote)) {
        this.hoveredConnection = connection;
        return;
      }
    }

    for (const bc of [...this.canvasService.boardState.boundedContexts].reverse()) {
      if (this.getBCResizeCorner(bc, this.currentMousePos.x, this.currentMousePos.y) ||
          this.isPointInsideBCBorder(bc, this.currentMousePos.x, this.currentMousePos.y)) {
        this.hoveredBoundedContext = bc;
        return;
      }
    }
  }

  private getCursorStyle(): string {
    if (this.canvasService.isDrawingBoundedContext) {
      return 'crosshair';
    }
    if (this.hoveredNote) {
      const corner = this.getResizeCorner(this.hoveredNote, this.currentMousePos.x, this.currentMousePos.y);
      if (corner) {
        return (corner === 'top-left' || corner === 'bottom-right') ? 'nwse-resize' : 'nesw-resize';
      }
      return 'move';
    }
    if (this.hoveredConnection) {
      return 'pointer';
    }
    if (this.hoveredBoundedContext) {
      const bcCorner = this.getBCResizeCorner(this.hoveredBoundedContext, this.currentMousePos.x, this.currentMousePos.y);
      if (bcCorner) {
        return (bcCorner === 'top-left' || bcCorner === 'bottom-right') ? 'nwse-resize' : 'nesw-resize';
      }
      return 'move';
    }
    return 'default';
  }

  private editNoteText(note: Note): void {
    const dialogRef = this.dialog.open(NoteTextModalComponent, {
      width: '400px',
      data: { text: note.text }
    });

    dialogRef.afterClosed()
      .subscribe((newText: string | undefined) => {
        if (newText !== undefined && newText !== note.text) {
          const command = new EditNoteTextCommand(note.id, newText, note.text);
          this.canvasService.executeCommand(command);
        }
      });
  }

  private openShortcutsGuide(): void {
    this.dialog.open(KeyboardShortcutsModalComponent, {
      width: '560px',
      maxWidth: '95vw',
      autoFocus: false
    });
  }

  private copySelectedNotes(): void {
    const selectedNotes = this.canvasService.boardState.notes.filter(n => n.selected);
    const selectedIds = selectedNotes.map(n => n.id);

    this.copiedNotes = selectedNotes.map(n => ({ ...n }));

    this.copiedConnections = this.canvasService.boardState.connections.filter(c =>
      selectedIds.includes(c.fromNoteId) && selectedIds.includes(c.toNoteId)
    ).map(c => ({ ...c }));
  }

  private pasteCopiedNotes(): void {
    if (this.copiedNotes.length === 0) return;

    const mouseX = this.currentMousePos.x;
    const mouseY = this.currentMousePos.y;

    const minX = Math.min(...this.copiedNotes.map(n => n.x));
    const minY = Math.min(...this.copiedNotes.map(n => n.y));

    const idMapping = new Map<string, string>();

    const newNotes: Note[] = this.copiedNotes.map(originalNote => {
      const newId = uuid();
      idMapping.set(originalNote.id, newId);
      return {
        ...originalNote,
        id: newId,
        x: mouseX + (originalNote.x - minX),
        y: mouseY + (originalNote.y - minY),
        selected: true,
      };
    });

    // recreate connections with new note ids  
    const newConnections = this.copiedConnections.map(c => ({
      fromNoteId: idMapping.get(c.fromNoteId)!,
      toNoteId: idMapping.get(c.toNoteId)!,
      selected: true
    }));

    this.copiedNotes.forEach(n => n.selected = false);

    const command = new PasteCommand(newNotes, newConnections);
    this.canvasService.executeCommand(command);
  }
}  