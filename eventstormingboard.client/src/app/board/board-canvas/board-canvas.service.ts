import { Injectable } from '@angular/core';
import { BoardState } from '../../_shared/models/board-state.model';
import { Subject } from 'rxjs';
import { BoardsSignalRService } from '../../_shared/services/boards-signalr.service';
import { Command } from '../../_shared/models/command.model';
import { CreateConnectionCommand, CreateNoteCommand, DeleteNotesCommand, EditNoteTextCommand, MoveNotesCommand, PasteCommand, ResizeNoteCommand, UpdateBoardNameCommand } from '../board.commands';
import { BoardEvent, BoardNameUpdatedEvent, ConnectionCreatedEvent, NoteCreatedEvent, NoteResizedEvent, NotesDeletedEvent, NotesMovedEvent, NoteTextEditedEvent, PastedEvent } from '../../_shared/models/board-events.model';

@Injectable({
  providedIn: 'root'
})
export class BoardCanvasService {

  private undoStack: Command<BoardState>[] = [];
  private redoStack: Command<BoardState>[] = [];

  constructor(
    private boardsHub: BoardsSignalRService
  ) {
  }

  public boardState: BoardState = {
    notes: [],
    connections: [],
    name: 'Untitled Board'
  };

  public id!: string;
  public isDrawingConnection = false;
  public isPanningMode = false;
  public isSelectMode = true;

  public hasChanges = false;

  public canvasUpdated$ = new Subject<void>();
  public canvasImageDownloaded$ = new Subject<void>();

  public originX = 0;
  public originY = 0;

  public scale = 1;
  public zoomPercentage = 100;
  public scaleFactor = 1.1;

  public zoomIn(): void {
    const zoomFactor = 1.1;
    this.scale *= zoomFactor;
    this.drawCanvas();
    this.updateZoomPercentage();
  }

  public zoomOut(): void {
    const zoomFactor = 1.1;
    this.scale /= zoomFactor;
    this.drawCanvas();
    this.updateZoomPercentage();
  }

  public setZoom(): void {
    const newScale = this.zoomPercentage / 100;
    if (newScale >= 0.2 && newScale <= 5) {
      this.scale = newScale;
      this.drawCanvas();
    }
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
    }
    this.hasChanges = true;
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
    this.isSelectMode = false;
    this.isPanningMode = false;
    this.boardState.connections.forEach(c => c.selected = false);
    this.boardState.notes.forEach(n => n.selected = false);
  }

  public drawCanvas(): void {
    this.canvasUpdated$.next();
  }

  public downloadCanvasImage(): void {
    this.canvasImageDownloaded$.next();
  }
}