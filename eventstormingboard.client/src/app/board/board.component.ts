import { Component, OnDestroy, OnInit } from '@angular/core';
import { Note } from '../_shared/models/note.model';
import { CreateConnectionCommand, CreateNoteCommand, DeleteNotesCommand, EditNoteTextCommand, MoveNotesCommand, PasteCommand, ResizeNoteCommand, UpdateBoardNameCommand } from './board.commands';
import { v4 as uuid } from 'uuid';
import { BoardsSignalRService } from '../_shared/services/boards-signalr.service';
import { interval, Subject, takeUntil } from 'rxjs';
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

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    CommonModule,
    FormsModule,
    BoardCanvasComponent
  ],
  templateUrl: './board.component.html',
  styleUrls: ['./board.component.scss']
})
export class BoardComponent implements OnInit, OnDestroy {

  private destroy$ = new Subject<void>();
  private id!: string;
  private userName: string;
  private previousName: string ;
  private colors: { [key: string]: string } = {
    event: '#fdb634',
    command: '#61c4fd',
    aggregate: '#f8fb1d',
    user: '#ffffc5',
    policy: '#df89df',
    readModel: '#90f179',
    externalSystem: '#f5bee7',
    concern: '#f50532'
  };

  constructor(
    private boardsHub: BoardsSignalRService,
    public canvasService: BoardCanvasService,
    private activatedRoute: ActivatedRoute,
    private boardsService: BoardsService
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

  public exportBoardAsJSON(): void {
    const boardState = {
      boardName: this.canvasService.boardState.name,
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
    return this.colors[type] || '#ffffff'; // Default to white if type not found  
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
      color: this.colors[type]
    };

    const command = new CreateNoteCommand(note);
    this.canvasService.executeCommand(command);
  }

  public ngOnInit(): void {

    this.subscribeToEvents();

    this.boardsService.getById(this.id)
      .subscribe(board => {
        this.canvasService.id = this.id;
        this.canvasService.boardState.name = board.name;
        this.canvasService.boardState.notes = board.notes;
        this.canvasService.boardState.connections = board.connections;
        this.canvasService.drawCanvas();
      });

    this.startCheckingForChanges();
    this.boardsHub.joinBoard(this.id, this.userName);
  }

  public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
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
      });
    this.boardsHub.boardNameUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.canvasService.executeCommand(new UpdateBoardNameCommand(event.newName, event.oldName), true, event.isUndo);
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
  }

  private startCheckingForChanges(): void {
    interval(2000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.canvasService.hasChanges) {
          this.saveBoard();
        }
      });
  }

  private saveBoard(): void {
    const boardUpdateDto = {
      name: this.canvasService.boardState.name,
      notes: this.canvasService.boardState.notes,
      connections: this.canvasService.boardState.connections
    };

    this.boardsService.update(this.id, boardUpdateDto).subscribe(() => {
      this.canvasService.hasChanges = false;
    });
  }
}  