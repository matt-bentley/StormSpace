import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { BoardContextUpdatedEvent, BoardNameUpdatedEvent, ConnectionCreatedEvent, CursorPositionUpdatedEvent, NoteCreatedEvent, NoteResizedEvent, NotesDeletedEvent, NotesMovedEvent, NoteTextEditedEvent, PastedEvent, UserJoinedBoardEvent, UserLeftBoardEvent } from '../models/board-events.model';
import { BoardUser } from '../models/board-user.model';

export interface AgentToolCall {
  name: string;
  arguments: string;
}

export interface AgentChatMessage {
  role: string;
  userName?: string;
  agentName?: string;
  content?: string;
  toolCalls?: AgentToolCall[];
  timestamp?: string;
}

export interface AgentToolCallStartedEvent {
  boardId: string;
  toolName: string;
  arguments?: Record<string, string>;
}

export interface AutonomousFacilitatorStatus {
  boardId: string;
  isEnabled: boolean;
  isRunning: boolean;
  state: string;
  lastResultStatus?: string;
  stopReason?: string;
  triggerReason?: string;
  updatedAt?: string;
}

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
  public boardContextUpdated$ = new Subject<BoardContextUpdatedEvent>();
  public cursorPositionUpdated$ = new Subject<CursorPositionUpdatedEvent>();
  public noteAdded$ = new Subject<NoteCreatedEvent>();
  public notesMoved$ = new Subject<NotesMovedEvent>();
  public noteResized$ = new Subject<NoteResizedEvent>();
  public notesDeleted$ = new Subject<NotesDeletedEvent>();
  public connectionCreated$ = new Subject<ConnectionCreatedEvent>();
  public noteTextEdited$ = new Subject<NoteTextEditedEvent>();
  public pasted$ = new Subject<PastedEvent>();
  public agentUserMessage$ = new Subject<AgentChatMessage>();
  public agentResponse$ = new Subject<AgentChatMessage>();
  public agentToolCallStarted$ = new Subject<AgentToolCallStartedEvent>();
  public autonomousStatusChanged$ = new Subject<AutonomousFacilitatorStatus>();
  public agentHistoryCleared$ = new Subject<string>();
  public agentChatHistory: AgentChatMessage[] = [];
  public autonomousStatuses = new Map<string, AutonomousFacilitatorStatus>();

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
    this.hubConnection.on('BoardContextUpdated', (event) => {
      this.boardContextUpdated$.next(event);
    });
    this.hubConnection.on('CursorPositionUpdated', (event) => {
      this.cursorPositionUpdated$.next(event);
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
    this.hubConnection.on('AgentUserMessage', (event) => {
      const message = this.mapAgentChatMessage(event);
      this.agentChatHistory.push(message);
      this.agentUserMessage$.next(message);
    });
    this.hubConnection.on('AgentResponse', (event) => {
      const message = this.mapAgentChatMessage(event);
      this.agentChatHistory.push(message);
      this.agentResponse$.next(message);
    });
    this.hubConnection.on('AgentToolCallStarted', (event) => {
      this.agentToolCallStarted$.next({
        boardId: this.pickValue<string>(event, 'boardId', 'BoardId') ?? '',
        toolName: this.pickValue<string>(event, 'toolName', 'ToolName') ?? '',
        arguments: this.pickValue<Record<string, string>>(event, 'arguments', 'Arguments')
      });
    });
    this.hubConnection.on('AgentChatHistory', (event) => {
      const history = Array.isArray(event) ? event.map((message) => this.mapAgentChatMessage(message)) : [];
      this.agentChatHistory = history;
    });
    this.hubConnection.on('AutonomousFacilitatorStatusChanged', (event) => {
      const status = this.mapAutonomousStatus(event);
      this.autonomousStatuses.set(status.boardId, status);
      this.autonomousStatusChanged$.next(status);
    });
    this.hubConnection.on('AgentHistoryCleared', (event) => {
      const boardId = this.pickValue<string>(event, 'boardId', 'BoardId') ?? '';
      this.agentChatHistory = [];
      this.agentHistoryCleared$.next(boardId);
    });

    return this.hubConnection.start()
      .catch(err => console.error(err));
  }

  public async joinBoard(boardId: string, userName: string): Promise<void> {
    await this.connectionEstablished;
    await this.hubConnection.invoke('JoinBoard', boardId, userName)
      .catch(err => console.error('Error joining board group:', err));
  }

  public async leaveBoard(boardId: string): Promise<void> {
    await this.connectionEstablished;
    await this.hubConnection.invoke('LeaveBoard', boardId)
      .catch(err => console.error('Error leaving board group:', err));
  }

  public broadcastBoardNameUpdated(event: BoardNameUpdatedEvent) {
    this.hubConnection.invoke('BroadcastBoardNameUpdated', event)
      .catch(err => console.error(err));
  }

  public broadcastBoardContextUpdated(event: BoardContextUpdatedEvent) {
    this.hubConnection.invoke('BroadcastBoardContextUpdated', event)
      .catch(err => console.error(err));
  }

  public broadcastCursorPositionUpdated(event: CursorPositionUpdatedEvent) {
    this.hubConnection.invoke('BroadcastCursorPositionUpdated', event)
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

  public async sendAgentMessage(boardId: string, message: string): Promise<void> {
    await this.connectionEstablished;
    try {
      await this.hubConnection.invoke('SendAgentMessage', boardId, message);
    } catch (err) {
      console.error('Error sending agent message:', err);
      throw err;
    }
  }

  public async getAgentHistory(boardId: string): Promise<void> {
    await this.connectionEstablished;
    try {
      await this.hubConnection.invoke('GetAgentHistory', boardId);
    } catch (err) {
      console.error('Error getting agent history:', err);
      throw err;
    }
  }

  public async clearAgentHistory(boardId: string): Promise<void> {
    await this.connectionEstablished;
    try {
      await this.hubConnection.invoke('ClearAgentHistory', boardId);
    } catch (err) {
      console.error('Error clearing agent history:', err);
      throw err;
    }
  }

  private mapAgentChatMessage(raw: unknown): AgentChatMessage {
    const event = (raw ?? {}) as Record<string, unknown>;
    const toolCalls = this.pickValue<unknown[]>(event, 'toolCalls', 'ToolCalls');

    return {
      role: this.pickValue<string>(event, 'role', 'Role') ?? '',
      userName: this.pickValue<string>(event, 'userName', 'UserName'),
      agentName: this.pickValue<string>(event, 'agentName', 'AgentName'),
      content: this.pickValue<string>(event, 'content', 'Content'),
      timestamp: this.pickValue<string>(event, 'timestamp', 'Timestamp'),
      toolCalls: Array.isArray(toolCalls)
        ? toolCalls.map((toolCall) => {
            const call = toolCall as Record<string, unknown>;
            return {
              name: this.pickValue<string>(call, 'name', 'Name') ?? '',
              arguments: this.pickValue<string>(call, 'arguments', 'Arguments') ?? ''
            };
          })
        : undefined
    };
  }

  private mapAutonomousStatus(raw: unknown): AutonomousFacilitatorStatus {
    const event = (raw ?? {}) as Record<string, unknown>;

    return {
      boardId: this.pickValue<string>(event, 'boardId', 'BoardId') ?? '',
      isEnabled: this.pickValue<boolean>(event, 'isEnabled', 'IsEnabled') ?? false,
      isRunning: this.pickValue<boolean>(event, 'isRunning', 'IsRunning') ?? false,
      state: this.pickValue<string>(event, 'state', 'State') ?? 'disabled',
      lastResultStatus: this.pickValue<string>(event, 'lastResultStatus', 'LastResultStatus'),
      stopReason: this.pickValue<string>(event, 'stopReason', 'StopReason'),
      triggerReason: this.pickValue<string>(event, 'triggerReason', 'TriggerReason'),
      updatedAt: this.pickValue<string>(event, 'updatedAt', 'UpdatedAt')
    };
  }

  private pickValue<T>(source: Record<string, unknown>, camelName: string, pascalName: string): T | undefined {
    const value = source[camelName] ?? source[pascalName];
    return value as T | undefined;
  }
}  
