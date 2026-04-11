import { inject, Injectable } from '@angular/core';
import { BoardState } from '../../_shared/models/board-state.model';
import { Subject } from 'rxjs';
import { BoardsSignalRService } from '../../_shared/services/boards-signalr.service';
import { Command } from '../../_shared/models/command.model';
import { CreateBoundedContextCommand, CreateConnectionCommand, CreateNoteCommand, DeleteBoundedContextCommand, DeleteNotesCommand, EditNoteTextCommand, MoveBoundedContextCommand, MoveNotesCommand, PasteCommand, ResizeBoundedContextCommand, ResizeNoteCommand, UpdateBoardContextCommand, UpdateBoardNameCommand, UpdateBoundedContextCommand } from '../board.commands';
import { BoardContextUpdatedEvent, BoardEvent, BoardNameUpdatedEvent, BoundedContextCreatedEvent, BoundedContextDeletedEvent, BoundedContextUpdatedEvent, ConnectionCreatedEvent, NoteCreatedEvent, NoteResizedEvent, NotesDeletedEvent, NotesMovedEvent, NoteTextEditedEvent, PastedEvent } from '../../_shared/models/board-events.model';
import { RemoteCursorState } from '../../_shared/models/remote-cursor-state.model';

@Injectable()
export class BoardCanvasService {
  private boardsHub = inject(BoardsSignalRService);

  private undoStack: Command<BoardState>[] = [];
  private redoStack: Command<BoardState>[] = [];

  public boardState: BoardState = {
    notes: [],
    connections: [],
    boundedContexts: [],
    name: 'Untitled Board',
    autonomousEnabled: false
  };

  public remoteCursors = new Map<string, RemoteCursorState>();

  public id!: string;
  public isDrawingConnection = false;
  public isDrawingBoundedContext = false;
  public isPanningMode = false;
  public isSelectMode = true;

  public canvasUpdated$ = new Subject<void>();
  public canvasImageDownloaded$ = new Subject<void>();

  public originX = 0;
  public originY = 0;

  public scale = 1;
  public zoomPercentage = 100;
  public scaleFactor = 1.1;

  public zoomIn(): void {
    this.scale *= this.scaleFactor;
    this.drawCanvas();
    this.updateZoomPercentage();
  }

  public zoomOut(): void {
    this.scale /= this.scaleFactor;
    this.drawCanvas();
    this.updateZoomPercentage();
  }

  public setZoom(): void {
    const newScale = Math.min(5, Math.max(0.2, this.zoomPercentage / 100));
    this.scale = newScale;
    this.updateZoomPercentage();
    this.drawCanvas();
  }

  private updateZoomPercentage(): void {
    this.zoomPercentage = Math.round(this.scale * 100);
  }

  public executeCommand(command: Command<BoardState>, serverInvoked = false, isUndo: boolean = false): void {
    command.initialize(this.boardState);
    if (isUndo) {
      command.undo();
    }
    else {
      command.execute();
    }
    if (!serverInvoked) {
      this.undoStack.push(command);
      this.redoStack = [];
      this.broadcastCommandExecuted(command, false);
    }
    this.drawCanvas();
  }

  private broadcastCommandExecuted(command: Command<BoardState>, isUndo: boolean): void {
    if (command instanceof CreateNoteCommand) {
      this.boardsHub.broadcastNoteCreated(this.toEvent<NoteCreatedEvent>(command, isUndo));
    } else if (command instanceof MoveNotesCommand) {
      this.boardsHub.broadcastNotesMoved(this.toEvent<NotesMovedEvent>(command, isUndo));
    } else if (command instanceof ResizeNoteCommand) {
      this.boardsHub.broadcastNoteResized(this.toEvent<NoteResizedEvent>(command, isUndo));
    } else if (command instanceof DeleteNotesCommand) {
      this.boardsHub.broadcastNotesDeleted(this.toEvent<NotesDeletedEvent>(command, isUndo));
    } else if (command instanceof EditNoteTextCommand) {
      this.boardsHub.broadcastNoteTextEdited(this.toEvent<NoteTextEditedEvent>(command, isUndo));
    } else if (command instanceof PasteCommand) {
      this.boardsHub.broadcastPasted(this.toEvent<PastedEvent>(command, isUndo));
    } else if (command instanceof CreateConnectionCommand) {
      this.boardsHub.broadcastConnectionCreated(this.toEvent<ConnectionCreatedEvent>(command, isUndo));
    } else if (command instanceof UpdateBoardNameCommand) {
      this.boardsHub.broadcastBoardNameUpdated(this.toEvent<BoardNameUpdatedEvent>(command, isUndo));
    } else if (command instanceof UpdateBoardContextCommand) {
      this.boardsHub.broadcastBoardContextUpdated(this.toEvent<BoardContextUpdatedEvent>(command, isUndo));
    } else if (command instanceof CreateBoundedContextCommand) {
      this.boardsHub.broadcastBoundedContextCreated(this.toEvent<BoundedContextCreatedEvent>(command, isUndo));
    } else if (command instanceof UpdateBoundedContextCommand) {
      this.boardsHub.broadcastBoundedContextUpdated(this.toEvent<BoundedContextUpdatedEvent>(command, isUndo));
    } else if (command instanceof DeleteBoundedContextCommand) {
      this.boardsHub.broadcastBoundedContextDeleted(this.toEvent<BoundedContextDeletedEvent>(command, isUndo));
    } else if (command instanceof MoveBoundedContextCommand) {
      this.boardsHub.broadcastBoundedContextUpdated(this.toEvent<BoundedContextUpdatedEvent>(command, isUndo));
    } else if (command instanceof ResizeBoundedContextCommand) {
      this.boardsHub.broadcastBoundedContextUpdated(this.toEvent<BoundedContextUpdatedEvent>(command, isUndo));
    }
  }

  private toEvent<TEvent extends BoardEvent>(command: Command<BoardState>, isUndo: boolean): TEvent {
    const event = command as unknown as TEvent;
    event.boardId = this.id;
    event.isUndo = isUndo;
    return event;
  }

  public undo(): void {
    const command = this.undoStack.pop();
    if (command) {
      command.undo();
      this.redoStack.push(command);
      this.drawCanvas();
      this.broadcastCommandExecuted(command, true);
    }
  }

  public redo(): void {
    const command = this.redoStack.pop();
    if (command) {
      command.execute();
      this.undoStack.push(command);
      this.drawCanvas();
      this.broadcastCommandExecuted(command, false);
    }
  }

  public reset(): void {
    this.isDrawingConnection = false;
    this.isDrawingBoundedContext = false;
    this.isSelectMode = false;
    this.isPanningMode = false;
    this.boardState.connections.forEach(c => c.selected = false);
    this.boardState.notes.forEach(n => n.selected = false);
    this.boardState.boundedContexts.forEach(bc => bc.selected = false);
  }

  public drawCanvas(): void {
    this.canvasUpdated$.next();
  }

  public downloadCanvasImage(): void {
    this.canvasImageDownloaded$.next();
  }
}