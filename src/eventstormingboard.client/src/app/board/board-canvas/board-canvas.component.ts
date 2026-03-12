import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Note, getNoteColor } from '../../_shared/models/note.model';
import { Connection } from '../../_shared/models/connection.model';
import { CreateConnectionCommand, DeleteNotesCommand, EditNoteTextCommand, MoveNotesCommand, PasteCommand, ResizeNoteCommand } from '../board.commands';
import { v4 as uuid } from 'uuid';
import { Coordinates } from '../../_shared/models/coordinates.model';
import { NoteSize } from '../../_shared/models/note-size.model';
import { NoteMove } from '../../_shared/models/note-move.model';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { NoteTextModalComponent } from './note-text-modal/note-text-modal.component';
import { BoardCanvasService } from './board-canvas.service';
import { Subject, takeUntil } from 'rxjs';
import { KeyboardShortcutsModalComponent } from '../keyboard-shortcuts-modal/keyboard-shortcuts-modal.component';
import { BoardsSignalRService } from '../../_shared/services/boards-signalr.service';
import { BoardUser } from '../../_shared/models/board-user.model';
import { CursorPositionUpdatedEvent } from '../../_shared/models/board-events.model';

@Component({
    selector: 'app-board-canvas',
    imports: [
        MatDialogModule
    ],
    templateUrl: './board-canvas.component.html',
    styleUrls: ['./board-canvas.component.scss']
})
export class BoardCanvasComponent implements OnInit, AfterViewInit, OnDestroy {

  private static readonly CURSOR_BROADCAST_INTERVAL_MS = 50;

  private destroy$ = new Subject<void>();

  private ctx!: CanvasRenderingContext2D;
  private minimapCtx!: CanvasRenderingContext2D;

  private currentMousePos: Coordinates = { x: 0, y: 0 };
  private ctrlPressed = false;

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
  private lastCursorBroadcastAt = 0;
  private readonly localUserName = localStorage.getItem('userName') ?? 'Anonymous';

  @ViewChild('canvas', { static: true })
  public canvas!: ElementRef<HTMLCanvasElement>;

  @ViewChild('minimap', { static: true })
  public minimap!: ElementRef<HTMLCanvasElement>;

  constructor(
    private dialog: MatDialog,
    private canvasService: BoardCanvasService,
    private boardsHub: BoardsSignalRService
  ) {
  }

  public onMinimapClick(event: MouseEvent): void {
    const rect = this.minimap.nativeElement.getBoundingClientRect();
    const clickX = event.clientX - rect.left;
    const clickY = event.clientY - rect.top;

    const { minX, minY, dynamicScale } = this.getCanvasBoundsAndScale();

    const canvasX = clickX / dynamicScale + minX;
    const canvasY = clickY / dynamicScale + minY;

    const canvasEl = this.canvas.nativeElement;
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

    // Reset selection unless Ctrl is pressed  
    if (!this.ctrlPressed) {
      this.canvasService.boardState.notes.forEach(n => n.selected = false);
      this.canvasService.boardState.connections.forEach(c => c.selected = false);
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

    this.draggingNote = null;
    this.resizingNote = null;
    this.resizeCorner = null;
    this.panning = false;

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
  }

  public onWheel(event: WheelEvent): void {
    const mousePos = this.getMousePos(event);

    if (event.ctrlKey) {
      // Ctrl pressed: Zooming behavior  
      event.preventDefault();

      const wheel = event.deltaY < 0 ? 1 : -1;
      const zoom = Math.pow(this.canvasService.scaleFactor, wheel);

      const newScale = this.canvasService.scale * zoom;

      // Limit zoom to reasonable levels  
      if (newScale < 0.2 || newScale > 5) return;

      // Adjust origin to zoom towards mouse pointer  
      this.canvasService.originX = mousePos.x - zoom * (mousePos.x - this.canvasService.originX);
      this.canvasService.originY = mousePos.y - zoom * (mousePos.y - this.canvasService.originY);

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
      const selectedNoteIds = this.canvasService.boardState.notes.filter(n => n.selected).map(n => n.id);
      const notes = JSON.parse(JSON.stringify(this.canvasService.boardState.notes.filter(n => n.selected))) as Note[];
      const connections = JSON.parse(JSON.stringify(this.canvasService.boardState.connections.filter(c =>
        c.selected ||
        selectedNoteIds.includes(c.fromNoteId) ||
        selectedNoteIds.includes(c.toNoteId)
      ))) as Connection[];
      const command = new DeleteNotesCommand(notes, connections);
      this.canvasService.executeCommand(command);
    }

    if (event.key === 'Control') {
      this.ctrlPressed = true;
    }

    // Handle Ctrl+C (Copy)  
    if (this.ctrlPressed && event.key.toLowerCase() === 'c') {
      this.copySelectedNotes();
      event.preventDefault();
    }

    // Handle Ctrl+V (Paste)  
    if (this.ctrlPressed && event.key.toLowerCase() === 'v') {
      this.pasteCopiedNotes();
      event.preventDefault();
    }
  }

  public onMouseMove(event: MouseEvent): void {
    this.currentMousePos = this.getMousePos(event);
    this.broadcastCursorPositionIfNeeded();

    if (this.handlePanning(event)) return;
    if (this.handeResize()) return;
    if (this.handleDragging()) return;
    if (this.handleSelection()) return;
    if (this.handleHover()) return;
    if (this.handleTemporaryArrow(event)) return;

    const cursor = this.getCursorStyle();

    this.canvas.nativeElement.style.cursor = cursor;
    this.drawCanvas();
  }

  @HostListener('document:keyup', ['$event'])
  public onKeyUp(event: KeyboardEvent): void {
    if (event.key === 'Control') {
      this.ctrlPressed = false;
    }
  }

  @HostListener('window:resize')
  public onResize(): void {
    this.canvas.nativeElement.width = window.innerWidth;
    this.canvas.nativeElement.height = window.innerHeight;
    this.drawCanvas();
  }

  public ngOnInit(): void {
    this.canvasService.canvasUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.drawCanvas();
      });

    this.canvasService.canvasImageDownloaded$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.exportBoardAsImage();
      });
  }

  public ngAfterViewInit(): void {
    this.generateCanvas();
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private generateCanvas(): void {
    this.ctx = this.canvas.nativeElement.getContext('2d')!;
    this.canvas.nativeElement.width = window.innerWidth;
    this.canvas.nativeElement.height = window.innerHeight;

    this.minimapCtx = this.minimap.nativeElement.getContext('2d')!;
    this.minimap.nativeElement.width = 260;
    this.minimap.nativeElement.height = 185;
    this.drawCanvas();
  }

  private exportBoardAsImage(): void {
    const canvasEl = this.canvas.nativeElement;
    const tempCanvas = document.createElement('canvas');
    const tempCtx = tempCanvas.getContext('2d')!;

    // Set the size of the temporary canvas to match the original canvas
    tempCanvas.width = canvasEl.width;
    tempCanvas.height = canvasEl.height;

    // Fill the temporary canvas with a white background
    tempCtx.fillStyle = '#ffffff';
    tempCtx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);

    // Draw the original canvas content on top of the white background
    tempCtx.drawImage(canvasEl, 0, 0);

    // Export the temporary canvas as an image
    const link = document.createElement('a');
    link.download = `${this.canvasService.boardState.name || 'board'}.png`;
    link.href = tempCanvas.toDataURL('image/png');
    link.click();
  }

  private drawCanvas(): void {
    const canvasEl = this.canvas.nativeElement;
    this.ctx.setTransform(this.canvasService.scale, 0, 0, this.canvasService.scale, this.canvasService.originX, this.canvasService.originY);
    this.ctx.clearRect(-this.canvasService.originX / this.canvasService.scale, -this.canvasService.originY / this.canvasService.scale, canvasEl.width / this.canvasService.scale, canvasEl.height / this.canvasService.scale);

    this.canvasService.boardState.notes.forEach(note => {
      this.drawNote(note);
    });

    this.drawConnections();
    this.drawRemoteCursors();

    if (this.isSelecting) {
      this.ctx.save();
      this.ctx.strokeStyle = '#3498db';
      this.ctx.lineWidth = 1;
      this.ctx.setLineDash([4, 2]);
      this.ctx.strokeRect(this.selectionRect.x, this.selectionRect.y, this.selectionRect.width, this.selectionRect.height);
      this.ctx.restore();
    }

    // Indicator square (only when hovering before starting a connection)  
    if (this.hoveredConnectionStartNote && this.hoveredConnectionStartPos && this.canvasService.isDrawingConnection && !this.arrowStartNote) {
      this.ctx.save();
      this.ctx.fillStyle = '#000';
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
      this.ctx.shadowColor = 'rgba(0, 0, 0, 0.25)';
      this.ctx.shadowBlur = 6;
      this.ctx.shadowOffsetX = 1;
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

      // White exterior stroke
      this.ctx.strokeStyle = '#ffffff';
      this.ctx.lineWidth = 1.5;
      this.ctx.lineJoin = 'round';
      this.ctx.stroke();

      // Remove shadow for text rendering clarity
      this.ctx.shadowColor = 'transparent';

      this.ctx.font = 'bold 12px Calibri, sans-serif';
      const textWidth = this.ctx.measureText(cursor.userName).width;
      const paddingX = 8;
      const labelWidth = textWidth + paddingX * 2;
      const labelHeight = 22;
      const labelX = cursor.x + 12;
      const labelY = cursor.y + 16;
      const r = 4; // border radius

      // Draw label background with rounded corners
      this.ctx.beginPath();
      this.ctx.moveTo(labelX + r, labelY);
      this.ctx.lineTo(labelX + labelWidth - r, labelY);
      this.ctx.quadraticCurveTo(labelX + labelWidth, labelY, labelX + labelWidth, labelY + r);
      this.ctx.lineTo(labelX + labelWidth, labelY + labelHeight - r);
      this.ctx.quadraticCurveTo(labelX + labelWidth, labelY + labelHeight, labelX + labelWidth - r, labelY + labelHeight);
      this.ctx.lineTo(labelX + r, labelY + labelHeight);
      this.ctx.quadraticCurveTo(labelX, labelY + labelHeight, labelX, labelY + labelHeight - r);
      this.ctx.lineTo(labelX, labelY + r);
      this.ctx.quadraticCurveTo(labelX, labelY, labelX + r, labelY);
      this.ctx.closePath();
      
      this.ctx.fillStyle = color;
      this.ctx.fill();

      // Draw label text
      this.ctx.fillStyle = '#ffffff';
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

  private drawMinimap(): void {
    const minimapCanvas = this.minimap.nativeElement;
    const ctx = this.minimapCtx;

    ctx.clearRect(0, 0, minimapCanvas.width, minimapCanvas.height);

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

        ctx.strokeStyle = '#888';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo((from.x - minX) * dynamicScale, (from.y - minY) * dynamicScale);
        ctx.lineTo((to.x - minX) * dynamicScale, (to.y - minY) * dynamicScale);
        ctx.stroke();
      }
    });

    const viewportRect = {
      x: (-this.canvasService.originX) / this.canvasService.scale,
      y: (-this.canvasService.originY) / this.canvasService.scale,
      width: this.canvas.nativeElement.width / this.canvasService.scale,
      height: this.canvas.nativeElement.height / this.canvasService.scale
    };

    ctx.strokeStyle = '#bfbfbf';
    ctx.lineWidth = 1;
    ctx.strokeRect(
      (viewportRect.x - minX) * dynamicScale,
      (viewportRect.y - minY) * dynamicScale,
      viewportRect.width * dynamicScale,
      viewportRect.height * dynamicScale
    );
  }

  private getCanvasBoundsAndScale() {
    const minimapCanvas = this.minimap.nativeElement;

    if (this.canvasService.boardState.notes.length === 0) {
      return {
        minX: 0, minY: 0, dynamicScale: 1
      };
    }

    const padding = 20;
    const minX = Math.min(...this.canvasService.boardState.notes.map(n => n.x)) - padding;
    const minY = Math.min(...this.canvasService.boardState.notes.map(n => n.y)) - padding;
    const maxX = Math.max(...this.canvasService.boardState.notes.map(n => n.x + n.width)) + padding;
    const maxY = Math.max(...this.canvasService.boardState.notes.map(n => n.y + n.height)) + padding;

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

    // Helper functions for colour manipulation
    const parseColor = (color: string) => {
      let r = 0, g = 0, b = 0;
      if (color.startsWith('#')) {
        let hex = color.slice(1);
        if (hex.length === 3) hex = hex.split('').map(c => c + c).join('');
        r = parseInt(hex.slice(0, 2), 16);
        g = parseInt(hex.slice(2, 4), 16);
        b = parseInt(hex.slice(4, 6), 16);
      } else if (color.startsWith('rgb')) {
        const parts = color.match(/\d+/g);
        if (parts && parts.length >= 3) {
          r = parseInt(parts[0], 10);
          g = parseInt(parts[1], 10);
          b = parseInt(parts[2], 10);
        }
      }
      return { r, g, b };
    };

    const darkenColor = (color: string, amount: number) => {
      const { r, g, b } = parseColor(color);
      const shift = Math.floor(255 * (amount / 100));
      const tr = Math.max(0, r - shift);
      const tg = Math.max(0, g - shift);
      const tb = Math.max(0, b - shift);
      return `rgb(${tr}, ${tg}, ${tb})`;
    };

    const lightenColor = (color: string, amount: number) => {
      const { r, g, b } = parseColor(color);
      const shift = Math.floor(255 * (amount / 100));
      const tr = Math.min(255, r + shift);
      const tg = Math.min(255, g + shift);
      const tb = Math.min(255, b + shift);
      return `rgb(${tr}, ${tg}, ${tb})`;
    };

    // Calculate dynamic gradient colours based on base colour
    const baseColor = getNoteColor(note.type);
    const topColor = lightenColor(baseColor, 15);
    const bottomColor = baseColor;

    // Create a smooth gradient for the main note body
    const gradient = this.ctx.createLinearGradient(note.x, note.y, note.x, note.y + note.height);
    gradient.addColorStop(0, topColor);
    gradient.addColorStop(1, bottomColor);

    // More realistic multiple shadow effect
    this.ctx.shadowColor = 'rgba(0, 0, 0, 0.15)';
    this.ctx.shadowBlur = note.selected ? 12 : 5;
    this.ctx.shadowOffsetX = note.selected ? 4 : 2;
    this.ctx.shadowOffsetY = note.selected ? 6 : 3;

    // Draw main note rectangle
    this.ctx.beginPath();
    this.ctx.rect(note.x, note.y, note.width, note.height);
    this.ctx.closePath();

    // Fill main shape with gradient
    this.ctx.fillStyle = gradient;
    this.ctx.fill();

    // Reset shadow before drawing borders and details
    this.resetShadow();

    // Draw border
    this.ctx.beginPath();
    this.ctx.rect(note.x, note.y, note.width, note.height);
    if (note.selected) {
      // Draw standard selection border on top
      this.ctx.strokeStyle = '#4A85CD';
      this.ctx.lineWidth = 2.5;
    } else {
      // Very faint outline for unselected
      this.ctx.strokeStyle = darkenColor(baseColor, 12);
      this.ctx.lineWidth = 1;
    }
    this.ctx.stroke();

    // Draw text
    this.drawNoteText(note);
  }

  private resetShadow(): void {
    this.ctx.shadowBlur = 0;
    this.ctx.shadowOffsetX = 0;
    this.ctx.shadowOffsetY = 0;
  }

  private drawNoteText(note: Note): void {
    const fontSize = note.width < 80 ? 12 : 16;
    this.ctx.fillStyle = '#000';
    this.ctx.font = `bold ${fontSize}px Calibri`;
    this.ctx.textAlign = 'center';

    const lines = this.getWrappedTextLines(note.text, note.width - 20);
    const textBlockHeight = lines.length * fontSize;
    let textY = note.y + (note.height - textBlockHeight) / 2 + fontSize / 2;

    for (const line of lines) {
      this.ctx.fillText(line, note.x + note.width / 2, textY);
      textY += fontSize;
    }

    // Reset text alignment
    this.ctx.textAlign = 'start';
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
    this.canvasService.boardState.connections.forEach(connection => {
      const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
      const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
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

    // Soften the default connector from harsh black to a smooth slate grey
    const baseColor = '#8b8f94'; // Slate 400
    const hoverColor = '#64748b'; // Slate 500
    const selectedColor = '#4A85CD';

    const color = selected ? selectedColor : (hovered ? hoverColor : baseColor);

    this.ctx.strokeStyle = color;
    this.ctx.fillStyle = color;
    this.ctx.lineWidth = selected ? 3 : 2;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';

    if (hovered || selected) {
      this.ctx.shadowBlur = 6;
      this.ctx.shadowColor = selected ? 'rgba(74, 133, 205, 0.3)' : 'rgba(0,0,0,0.15)';
      this.ctx.shadowOffsetX = 1;
      this.ctx.shadowOffsetY = 2;
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
    const rect = this.canvas.nativeElement.getBoundingClientRect();
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

      this.canvas.nativeElement.style.cursor = 'grab';
      this.drawCanvas();
      return true;
    }
    return false;
  }

  private handeResize(): boolean {
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

      this.canvas.nativeElement.style.cursor = `${this.resizeCorner}-resize`;
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

      this.canvas.nativeElement.style.cursor = 'move';
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
      this.canvas.nativeElement.style.cursor = 'crosshair';
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
      this.canvas.nativeElement.style.cursor = foundHover ? 'crosshair' : 'default';
      this.drawCanvas();
      return true;
    }

    return false;
  }

  private handleTemporaryArrow(event: MouseEvent): boolean {
    if (this.canvasService.isDrawingConnection) {
      this.drawTemporaryArrow(event);
      this.canvas.nativeElement.style.cursor = 'crosshair';
      return true;
    }
    return false;
  }

  private getCursorStyle(): string {
    // Existing logic: Hover logic for resize handles, notes, connections  
    let cursor = 'default';
    let foundHover = false;

    for (let note of [...this.canvasService.boardState.notes].reverse()) {
      const corner = this.getResizeCorner(note, this.currentMousePos.x, this.currentMousePos.y);
      if (corner) {
        switch (corner) {
          case 'top-left':
          case 'bottom-right':
            cursor = 'nwse-resize';
            break;
          case 'top-right':
          case 'bottom-left':
            cursor = 'nesw-resize';
            break;
        }
        foundHover = true;
        this.hoveredNote = note;
        break;
      } else if (this.isPointInsideNote(note, this.currentMousePos.x, this.currentMousePos.y)) {
        cursor = 'move';
        foundHover = true;
        this.hoveredNote = note;
        break;
      }
    }

    if (!foundHover) {
      this.hoveredNote = null;
      for (let connection of this.canvasService.boardState.connections) {
        const fromNote = this.canvasService.boardState.notes.find(n => n.id === connection.fromNoteId);
        const toNote = this.canvasService.boardState.notes.find(n => n.id === connection.toNoteId);
        if (fromNote && toNote && this.isPointNearArrow(this.currentMousePos.x, this.currentMousePos.y, fromNote, toNote)) {
          cursor = 'pointer';
          foundHover = true;
          this.hoveredConnection = connection;
          break;
        }
      }
    }

    return cursor;
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