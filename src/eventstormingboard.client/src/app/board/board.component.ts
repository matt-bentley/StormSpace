import { Component, OnDestroy, OnInit } from '@angular/core';
import { Note, getNoteColor } from '../_shared/models/note.model';
import { CreateConnectionCommand, CreateNoteCommand, DeleteNotesCommand, EditNoteTextCommand, MoveNotesCommand, PasteCommand, ResizeNoteCommand, UpdateBoardContextCommand, UpdateBoardNameCommand } from './board.commands';
import { v4 as uuid } from 'uuid';
import { BoardsSignalRService } from '../_shared/services/boards-signalr.service';
import { Subject, takeUntil } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { BoardsService } from '../_shared/services/boards.service';
import { BoardUser } from '../_shared/models/board-user.model';
import { BoardCanvasComponent } from './board-canvas/board-canvas.component';
import { BoardCanvasService } from './board-canvas/board-canvas.service';
import { MatDialog } from '@angular/material/dialog';
import { KeyboardShortcutsModalComponent } from './keyboard-shortcuts-modal/keyboard-shortcuts-modal.component';
import { CursorPositionUpdatedEvent } from '../_shared/models/board-events.model';
import { AiChatPanelComponent } from './ai-chat-panel/ai-chat-panel.component';
import { BoardContextModalComponent, BoardContextData } from './board-context-modal/board-context-modal.component';

@Component({
    selector: 'app-board',
    imports: [
        MatButtonModule,
        MatIconModule,
        MatTooltipModule,
        CommonModule,
        FormsModule,
        BoardCanvasComponent,
        AiChatPanelComponent
      ],
      providers: [BoardCanvasService],
    templateUrl: './board.component.html',
    styleUrls: ['./board.component.scss']
})
export class BoardComponent implements OnInit, OnDestroy {

  private static readonly CURSOR_STALE_TIMEOUT_MS = 15000;
  private destroy$ = new Subject<void>();
  private id!: string;
  public userName: string;
  private previousName: string;

  constructor(
    private boardsHub: BoardsSignalRService,
    public canvasService: BoardCanvasService,
    private activatedRoute: ActivatedRoute,
    private boardsService: BoardsService,
    private dialog: MatDialog
  ) {
    this.canvasService.boardState = {
      name: '',
      connections: [],
      notes: []
    };
    this.id = this.activatedRoute.snapshot.paramMap.get('id') || '';
    this.userName = localStorage.getItem('userName') ?? 'Anonymous'
    this.previousName = this.canvasService.boardState.name;
  }

  public connectedUsers: BoardUser[] = [];
  public isConnectedUsersHovered = false;
  public isChatOpen = false;
  public hasUnreadMessages = false;

  public exportBoardAsJSON(): void {
    const boardState = {
      boardName: this.canvasService.boardState.name,
      domain: this.canvasService.boardState.domain,
      sessionScope: this.canvasService.boardState.sessionScope,
      agentInstructions: this.canvasService.boardState.agentInstructions,
      phase: this.canvasService.boardState.phase,
      notes: this.canvasService.boardState.notes,
      connections: this.canvasService.boardState.connections
    };

    const json = JSON.stringify(boardState, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${this.canvasService.boardState.name || 'board'}.json`;
    link.click();
  }

  public importBoardFromJSON(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
      return;
    }

    const file = input.files[0];
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const boardState = JSON.parse(reader.result as string);
        this.canvasService.boardState.name = boardState.boardName || 'Untitled Board';
        this.canvasService.boardState.domain = boardState.domain;
        this.canvasService.boardState.sessionScope = boardState.sessionScope;
        this.canvasService.boardState.agentInstructions = boardState.agentInstructions;
        this.canvasService.boardState.phase = boardState.phase;
        this.canvasService.boardState.notes = boardState.notes || [];
        this.canvasService.boardState.connections = boardState.connections || [];
        this.canvasService.drawCanvas(); // Redraw the canvas with the imported state
      } catch (error) {
        console.error('Invalid JSON file:', error);
      }
    };
    reader.readAsText(file);
  }

  public onBoardNameUpdated(): void {
    if (this.canvasService.boardState.name) {
      const command = new UpdateBoardNameCommand(this.canvasService.boardState.name, this.previousName);
      this.canvasService.executeCommand(command);
      this.previousName = this.canvasService.boardState.name;
    }
  }

  public getNoteColor(type: string): string {
    return getNoteColor(type);
  }

  public toggleSelectMode(): void {
    this.canvasService.reset()
    this.canvasService.isSelectMode = true;
    this.canvasService.drawCanvas();
  }

  public togglePanMode(): void {
    this.canvasService.reset()
    this.canvasService.isPanningMode = true;
    this.canvasService.drawCanvas();
  }

  public toggleConnectionMode(): void {
    this.canvasService.reset()
    this.canvasService.isDrawingConnection = true;
    this.canvasService.drawCanvas();
  }

  public openShortcutsGuide(): void {
    this.dialog.open(KeyboardShortcutsModalComponent, {
      width: '560px',
      maxWidth: '95vw',
      autoFocus: false
    });
  }

  public openBoardContext(): void {
    const dialogRef = this.dialog.open(BoardContextModalComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: {
        domain: this.canvasService.boardState.domain || '',
        sessionScope: this.canvasService.boardState.sessionScope || '',
        agentInstructions: this.canvasService.boardState.agentInstructions || '',
        phase: this.canvasService.boardState.phase || ''
      } as BoardContextData
    });

    dialogRef.afterClosed().subscribe((result: BoardContextData | undefined) => {
      if (result) {
        const command = new UpdateBoardContextCommand(
          result.domain || undefined,
          this.canvasService.boardState.domain,
          result.sessionScope || undefined,
          this.canvasService.boardState.sessionScope,
          result.agentInstructions || undefined,
          this.canvasService.boardState.agentInstructions,
          result.phase || undefined,
          this.canvasService.boardState.phase
        );
        this.canvasService.executeCommand(command);
      }
    });
  }

  public addNote(type: string): void {
    const x = (-this.canvasService.originX) / this.canvasService.scale + 140;
    const y = (-this.canvasService.originY + 100) / this.canvasService.scale;

    const noteWidth = type === 'user' ? 60 : 120;
    const noteHeight = type === 'user' ? 60 : 120;

    let offsetX = 0;
    let offsetY = 0;
    const offsetStep = 10;

    while (this.canvasService.boardState.notes.some(n =>
      Math.abs(n.x - (x + offsetX)) < 5 &&
      Math.abs(n.y - (y + offsetY)) < 5
    )) {
      offsetX += offsetStep;
      offsetY += offsetStep;
    }

    const note: Note = {
      id: uuid(),
      x: x + offsetX,
      y: y + offsetY,
      width: noteWidth,
      height: noteHeight,
      text: `${type.charAt(0).toUpperCase() + type.slice(1)}`,
      type: type as Note['type']
    };

    const command = new CreateNoteCommand(note);
    this.canvasService.executeCommand(command);
  }

  public ngOnInit(): void {

    this.toggleSelectMode();

    this.subscribeToEvents();

    this.boardsService.getById(this.id)
      .subscribe(board => {
        this.canvasService.id = this.id;
        this.canvasService.boardState.name = board.name;
        this.canvasService.boardState.domain = board.domain;
        this.canvasService.boardState.sessionScope = board.sessionScope;
        this.canvasService.boardState.agentInstructions = board.agentInstructions;
        this.canvasService.boardState.phase = board.phase;
        // Map NoteDto[] to Note[]
        this.canvasService.boardState.notes = board.notes.map(n => ({
          ...n,
          type: n.type as Note['type'],
          selected: false // default, or preserve if needed
        }));
        this.canvasService.boardState.connections = board.connections;
        this.canvasService.drawCanvas();
      });

    this.startPruningCursors();
    this.boardsHub.joinBoard(this.id, this.userName);
  }

  public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
    this.canvasService.remoteCursors.clear();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private subscribeToEvents(): void {
    this.boardsHub.connectedUsers$
      .pipe(takeUntil(this.destroy$))
      .subscribe(users => {
        this.connectedUsers = users.filter(user => user.boardId === this.id)
          .map(user => new BoardUser(user.boardId, user.userName, user.connectionId));
      });
    this.boardsHub.userJoinedBoard$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.connectedUsers.push(new BoardUser(event.boardId, event.userName, event.connectionId));
      });
    this.boardsHub.userLeftBoard$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.connectedUsers = this.connectedUsers.filter(user => user.connectionId !== event.connectionId);
        this.canvasService.remoteCursors.delete(event.connectionId);
        this.canvasService.drawCanvas();
      });

    this.boardsHub.cursorPositionUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((event: CursorPositionUpdatedEvent) => {
        if (event.boardId !== this.id || !this.isValidCursorEvent(event)) {
          return;
        }

        this.canvasService.remoteCursors.set(event.connectionId, {
          boardId: event.boardId,
          connectionId: event.connectionId,
          userName: event.userName,
          x: event.x,
          y: event.y,
          updatedAt: Date.now()
        });
        this.canvasService.drawCanvas();
      });
    this.boardsHub.boardNameUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new UpdateBoardNameCommand(event.newName, event.oldName), true, event.isUndo);
      });

    this.boardsHub.boardContextUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(
          new UpdateBoardContextCommand(
            event.newDomain, event.oldDomain,
            event.newSessionScope, event.oldSessionScope,
            event.newAgentInstructions, event.oldAgentInstructions,
            event.newPhase, event.oldPhase
          ), true, event.isUndo);
      });

    this.boardsHub.noteAdded$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new CreateNoteCommand(event.note), true, event.isUndo);
      });

    this.boardsHub.notesMoved$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new MoveNotesCommand(event.from, event.to), true, event.isUndo);
      });

    this.boardsHub.noteResized$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new ResizeNoteCommand(event.noteId, event.from, event.to), true, event.isUndo);
      });

    this.boardsHub.notesDeleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new DeleteNotesCommand(event.notes, event.connections), true, event.isUndo);
      });

    this.boardsHub.connectionCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new CreateConnectionCommand(event.connection), true, event.isUndo);
      });

    this.boardsHub.noteTextEdited$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new EditNoteTextCommand(event.noteId, event.toText, event.fromText), true, event.isUndo);
      });

    this.boardsHub.pasted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new PasteCommand(event.notes, event.connections), true, event.isUndo);
      });

    this.boardsHub.agentUserMessage$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (!this.isChatOpen && event.userName !== this.userName) {
          this.hasUnreadMessages = true;
        }
      });

    this.boardsHub.agentResponse$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (!this.isChatOpen) this.hasUnreadMessages = true;
      });
  }

  private startPruningCursors(): void {
    const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
    this.destroy$.subscribe(() => clearInterval(intervalId));
  }

  private pruneStaleRemoteCursors(): void {
    const now = Date.now();
    let removedAny = false;

    for (const [connectionId, cursor] of this.canvasService.remoteCursors.entries()) {
      if (now - cursor.updatedAt > BoardComponent.CURSOR_STALE_TIMEOUT_MS) {
        this.canvasService.remoteCursors.delete(connectionId);
        removedAny = true;
      }
    }

    if (removedAny) {
      this.canvasService.drawCanvas();
    }
  }

  private isValidCursorEvent(event: CursorPositionUpdatedEvent): boolean {
    return !!event.connectionId &&
      !!event.userName &&
      Number.isFinite(event.x) &&
      Number.isFinite(event.y);
  }

}
