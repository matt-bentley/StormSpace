import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { BoardNameUpdatedEvent, ConnectionCreatedEvent, NoteCreatedEvent, NoteResizedEvent, NotesDeletedEvent, NotesMovedEvent, NoteTextEditedEvent, PastedEvent, UserJoinedBoardEvent, UserLeftBoardEvent } from '../models/board-events.model';
import { BoardUser } from '../models/board-user.model';

@Injectable({ providedIn: 'root' })
export class BoardsSignalRService {
  
  private hubConnection!: signalR.HubConnection;
  private connectionEstablished: Promise<void>;

  constructor() {
    this.connectionEstablished = this.startConnection();
  }

  public connectedUsers$ = new Subject<BoardUser[]>();
  public userJoinedBoard$ = new Subject<UserJoinedBoardEvent>();
  public userLeftBoard$ = new Subject<UserLeftBoardEvent>();
  public boardNameUpdated$ = new Subject<BoardNameUpdatedEvent>();
  public noteAdded$ = new Subject<NoteCreatedEvent>();
  public notesMoved$ = new Subject<NotesMovedEvent>();
  public noteResized$ = new Subject<NoteResizedEvent>();
  public notesDeleted$ = new Subject<NotesDeletedEvent>();
  public connectionCreated$ = new Subject<ConnectionCreatedEvent>();
  public noteTextEdited$ = new Subject<NoteTextEditedEvent>();
  public pasted$ = new Subject<PastedEvent>();

  private startConnection(): Promise<void> {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ConnectedUsers', (event) => {
      this.connectedUsers$.next(event);
    });
    this.hubConnection.on('UserJoinedBoard', (event) => {
      this.userJoinedBoard$.next(event);
    });
    this.hubConnection.on('UserLeftBoard', (event) => {
      this.userLeftBoard$.next(event);
    });
    this.hubConnection.on('BoardNameUpdated', (event) => {
      this.boardNameUpdated$.next(event);
    });
    this.hubConnection.on('NoteCreated', (event) => {
      this.noteAdded$.next(event);
    });
    this.hubConnection.on('NotesMoved', (event) => {
      this.notesMoved$.next(event);
    });
    this.hubConnection.on('NoteResized', (event) => {
      this.noteResized$.next(event);
    });
    this.hubConnection.on('NotesDeleted', (event) => {
      this.notesDeleted$.next(event);
    });
    this.hubConnection.on('ConnectionCreated', (event) => {
      this.connectionCreated$.next(event);
    });
    this.hubConnection.on('NoteTextEdited', (event) => {
      this.noteTextEdited$.next(event);
    });
    this.hubConnection.on('Pasted', (event) => {
      this.pasted$.next(event);
    });

    return this.hubConnection.start()
      .catch(err => console.error(err));
  }

  public async joinBoard(boardId: string, userName: string): Promise<void> {
    await this.connectionEstablished;
    this.hubConnection.invoke('JoinBoard', boardId, userName)
      .catch(err => console.error('Error joining board group:', err));
  }

  public async leaveBoard(boardId: string): Promise<void> {
    await this.connectionEstablished;
    this.hubConnection.invoke('LeaveBoard', boardId)
      .catch(err => console.error('Error leaving board group:', err));
  }

  public broadcastBoardNameUpdated(event: BoardNameUpdatedEvent) {
    this.hubConnection.invoke('BroadcastBoardNameUpdated', event)
      .catch(err => console.error(err));
  }

  public broadcastNoteCreated(event: NoteCreatedEvent) {
    this.hubConnection.invoke('BroadcastNoteCreated', event)
      .catch(err => console.error(err));
  }

  public broadcastNotesMoved(event: NotesMovedEvent) {
    this.hubConnection.invoke('BroadcastNotesMoved', event)
      .catch(err => console.error(err));
  }

  public broadcastNoteResized(event: NoteResizedEvent) {
    this.hubConnection.invoke('BroadcastNoteResized', event)
      .catch(err => console.error(err));
  }

  public broadcastNotesDeleted(event: NotesDeletedEvent) {
    this.hubConnection.invoke('BroadcastNotesDeleted', event)
      .catch(err => console.error(err));
  }

  public broadcastConnectionCreated(event: ConnectionCreatedEvent) {
    this.hubConnection.invoke('BroadcastConnectionCreated', event)
      .catch(err => console.error(err));
  }

  public broadcastNoteTextEdited(event: NoteTextEditedEvent) {
    this.hubConnection.invoke('BroadcastNoteTextEdited', event)
      .catch(err => console.error(err));
  }

  public broadcastPasted(event: PastedEvent) {
    this.hubConnection.invoke('BroadcastPasted', event)
      .catch(err => console.error(err));
  }
}  
