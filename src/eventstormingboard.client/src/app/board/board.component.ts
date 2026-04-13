import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Note, getNoteColor } from '../_shared/models/note.model';
import { CreateConnectionCommand, CreateBoundedContextCommand, CreateNoteCommand, DeleteBoundedContextCommand, DeleteNotesCommand, EditNoteTextCommand, MoveBoundedContextCommand, MoveNotesCommand, PasteCommand, ResizeBoundedContextCommand, ResizeNoteCommand, UpdateBoardContextCommand, UpdateBoardNameCommand, UpdateBoundedContextCommand } from './board.commands';
import { v4 as uuid } from 'uuid';
import { AutonomousFacilitatorStatus, BoardsSignalRService } from '../_shared/services/boards-signalr.service';
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
import { AgentConfigModalComponent, AgentConfigModalData } from './agent-config-modal/agent-config-modal.component';
import { ConfirmModalComponent, ConfirmModalData } from '../_shared/components/confirm-modal/confirm-modal.component';
import { AgentConfiguration } from '../_shared/models/agent-configuration.model';
import { EVENT_STORMING_PHASES, EventStormingPhase } from '../_shared/models/board.model';
import { UserService } from '../_shared/services/user.service';
import { Connection } from '../_shared/models/connection.model';
import { BoundedContext } from '../_shared/models/bounded-context.model';

interface ImportedBoardState {
  boardName?: unknown;
  domain?: unknown;
  sessionScope?: unknown;
  phase?: unknown;
  autonomousEnabled?: unknown;
  notes?: unknown;
  connections?: unknown;
  boundedContexts?: unknown;
  agentConfigurations?: unknown;
}

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
  private boardsHub = inject(BoardsSignalRService);
  public canvasService = inject(BoardCanvasService);
  private activatedRoute = inject(ActivatedRoute);
  private boardsService = inject(BoardsService);
  private dialog = inject(MatDialog);
  private userService = inject(UserService);
  private destroyRef = inject(DestroyRef);
  private id!: string;
  public userName: string;
  private previousName: string;

  constructor() {
    this.canvasService.boardState = {
      name: '',
      autonomousEnabled: false,
      connections: [],
      notes: [],
      boundedContexts: []
    };
    this.id = this.activatedRoute.snapshot.paramMap.get('id') || '';
    this.userName = this.userService.displayName || 'Anonymous';
    this.previousName = this.canvasService.boardState.name;
  }

  public connectedUsers: BoardUser[] = [];
  public isConnectedUsersHovered = false;
  public isChatOpen = false;
  public hasUnreadMessages = false;
  public phases = EVENT_STORMING_PHASES;
  public autonomousStatus?: AutonomousFacilitatorStatus;
  public agentConfigurations: AgentConfiguration[] = [];

  get activeAgents(): AgentConfiguration[] {
    const currentPhase = this.canvasService.boardState.phase;
    return this.agentConfigurations.filter(a => {
      if (!a.activePhases) return true;
      if (!currentPhase) return true;
      return a.activePhases.includes(currentPhase as any);
    });
  }

  public isPhaseActive(phase: string): boolean {
    return this.canvasService.boardState.phase === phase;
  }

  public onPhaseClick(phase: { value: EventStormingPhase; label: string }): void {
    if (this.isPhaseActive(phase.value)) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmModalComponent, {
      width: '400px',
      maxWidth: '95vw',
      data: {
        title: 'Change Phase',
        message: `Are you sure you want to change the workshop phase to "${phase.label}"?`
      } as ConfirmModalData
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean | undefined) => {
      if (confirmed) {
        const state = this.canvasService.boardState;
        const command = new UpdateBoardContextCommand(
          state.domain,
          state.domain,
          state.sessionScope,
          state.sessionScope,
          phase.value,
          state.phase,
          state.autonomousEnabled,
          state.autonomousEnabled
        );
        this.canvasService.executeCommand(command);
      }
    });
  }

  public exportBoardAsJSON(): void {
    const boardState = {
      boardName: this.canvasService.boardState.name,
      domain: this.canvasService.boardState.domain,
      sessionScope: this.canvasService.boardState.sessionScope,
      phase: this.canvasService.boardState.phase,
      autonomousEnabled: this.canvasService.boardState.autonomousEnabled,
      notes: this.canvasService.boardState.notes,
      connections: this.canvasService.boardState.connections,
      boundedContexts: this.canvasService.boardState.boundedContexts,
      agentConfigurations: this.agentConfigurations
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
        const parsed = JSON.parse(reader.result as string) as unknown;
        if (!this.isRecord(parsed)) {
          throw new Error('Invalid board JSON shape.');
        }

        const boardState = parsed as ImportedBoardState;
        const importedNotes = this.normalizeImportedNotes(boardState.notes);
        const importedNoteIds = new Set(importedNotes.map(note => note.id));
        const importedConnections = this.normalizeImportedConnections(boardState.connections, importedNoteIds);

        const oldName = this.canvasService.boardState.name;
        const importedName = typeof boardState.boardName === 'string' && boardState.boardName.trim().length > 0
          ? boardState.boardName.trim()
          : 'Untitled Board';

        if (importedName !== oldName) {
          this.canvasService.executeCommand(new UpdateBoardNameCommand(importedName, oldName));
        }
        this.previousName = importedName;

        const oldDomain = this.canvasService.boardState.domain;
        const oldSessionScope = this.canvasService.boardState.sessionScope;
        const oldPhase = this.canvasService.boardState.phase;
        const oldAutonomousEnabled = this.canvasService.boardState.autonomousEnabled;

        const newDomain = typeof boardState.domain === 'string' && boardState.domain.trim().length > 0
          ? boardState.domain.trim()
          : undefined;
        const newSessionScope = typeof boardState.sessionScope === 'string' && boardState.sessionScope.trim().length > 0
          ? boardState.sessionScope.trim()
          : undefined;
        const newPhase = typeof boardState.phase === 'string' && boardState.phase.trim().length > 0
          ? boardState.phase.trim()
          : undefined;
        const newAutonomousEnabled = boardState.autonomousEnabled === true;

        if (
          oldDomain !== newDomain ||
          oldSessionScope !== newSessionScope ||
          oldPhase !== newPhase ||
          oldAutonomousEnabled !== newAutonomousEnabled
        ) {
          this.canvasService.executeCommand(new UpdateBoardContextCommand(
            newDomain,
            oldDomain,
            newSessionScope,
            oldSessionScope,
            newPhase,
            oldPhase,
            newAutonomousEnabled,
            oldAutonomousEnabled
          ));
        }

        if (this.canvasService.boardState.notes.length > 0 || this.canvasService.boardState.connections.length > 0) {
          this.canvasService.executeCommand(new DeleteNotesCommand(
            [...this.canvasService.boardState.notes],
            [...this.canvasService.boardState.connections]
          ));
        }

        // Delete existing bounded contexts
        for (const bc of [...this.canvasService.boardState.boundedContexts]) {
          this.canvasService.executeCommand(new DeleteBoundedContextCommand(bc));
        }

        if (importedNotes.length > 0 || importedConnections.length > 0) {
          this.canvasService.executeCommand(new PasteCommand(importedNotes, importedConnections));
          // Keep imported content deselected; selection state is local-only UI state.
          this.canvasService.boardState.notes.forEach(note => note.selected = false);
          this.canvasService.boardState.connections.forEach(connection => connection.selected = false);
          this.canvasService.drawCanvas();
        }

        // Restore imported bounded contexts
        const importedBCs = this.normalizeImportedBoundedContexts(boardState.boundedContexts);
        for (const bc of importedBCs) {
          this.canvasService.executeCommand(new CreateBoundedContextCommand(bc));
        }
        // Deselect imported BCs
        this.canvasService.boardState.boundedContexts.forEach(bc => bc.selected = false);
        this.canvasService.drawCanvas();

        const importedAgents = this.normalizeImportedAgentConfigurations(boardState.agentConfigurations);
        if (importedAgents) {
          this.saveAgentConfigurations(importedAgents);
        }
      } catch (error) {
        console.error('Invalid JSON file:', error);
      } finally {
        input.value = '';
      }
    };

    reader.onerror = () => {
      console.error('Failed to read JSON file.');
      input.value = '';
    };

    reader.readAsText(file);
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
  }

  private normalizeImportedNotes(value: unknown): Note[] {
    if (!Array.isArray(value)) {
      return [];
    }

    const notes: Note[] = [];
    for (const entry of value) {
      if (!this.isRecord(entry)) {
        continue;
      }

      const id = entry['id'];
      const text = entry['text'];
      const type = entry['type'];
      const x = entry['x'];
      const y = entry['y'];
      const width = entry['width'];
      const height = entry['height'];

      if (
        typeof id !== 'string' ||
        typeof text !== 'string' ||
        typeof type !== 'string' ||
        typeof x !== 'number' ||
        typeof y !== 'number' ||
        typeof width !== 'number' ||
        typeof height !== 'number' ||
        !Number.isFinite(x) ||
        !Number.isFinite(y) ||
        !Number.isFinite(width) ||
        !Number.isFinite(height)
      ) {
        continue;
      }

      notes.push({
        id,
        text,
        type: type as Note['type'],
        x,
        y,
        width,
        height,
        selected: false
      });
    }

    return notes;
  }

  private normalizeImportedConnections(value: unknown, noteIds: Set<string>): Connection[] {
    if (!Array.isArray(value)) {
      return [];
    }

    const connections: Connection[] = [];
    for (const entry of value) {
      if (!this.isRecord(entry)) {
        continue;
      }

      const fromNoteId = entry['fromNoteId'];
      const toNoteId = entry['toNoteId'];
      if (typeof fromNoteId !== 'string' || typeof toNoteId !== 'string') {
        continue;
      }
      if (!noteIds.has(fromNoteId) || !noteIds.has(toNoteId)) {
        continue;
      }

      connections.push({
        fromNoteId,
        toNoteId,
        selected: false
      });
    }

    return connections;
  }

  private normalizeImportedBoundedContexts(value: unknown): BoundedContext[] {
    if (!Array.isArray(value)) {
      return [];
    }

    const boundedContexts: BoundedContext[] = [];
    for (const entry of value) {
      if (!this.isRecord(entry)) {
        continue;
      }

      const id = entry['id'];
      const name = entry['name'];
      const x = entry['x'];
      const y = entry['y'];
      const width = entry['width'];
      const height = entry['height'];

      if (
        typeof id !== 'string' ||
        typeof name !== 'string' ||
        typeof x !== 'number' ||
        typeof y !== 'number' ||
        typeof width !== 'number' ||
        typeof height !== 'number' ||
        !Number.isFinite(x) ||
        !Number.isFinite(y) ||
        !Number.isFinite(width) ||
        !Number.isFinite(height)
      ) {
        continue;
      }

      boundedContexts.push({
        id,
        name,
        x,
        y,
        width,
        height,
        selected: false
      });
    }

    return boundedContexts;
  }

  private normalizeImportedAgentConfigurations(value: unknown): AgentConfiguration[] | undefined {
    if (value === undefined) {
      return undefined;
    }
    if (!Array.isArray(value)) {
      return undefined;
    }

    const agents: AgentConfiguration[] = [];
    for (const entry of value) {
      if (!this.isRecord(entry)) {
        continue;
      }

      const id = typeof entry['id'] === 'string' ? entry['id'] : uuid();
      const name = typeof entry['name'] === 'string' ? entry['name'] : 'Agent';
      const isFacilitator = entry['isFacilitator'] === true;
      const systemPrompt = typeof entry['systemPrompt'] === 'string' ? entry['systemPrompt'] : '';
      const icon = typeof entry['icon'] === 'string' ? entry['icon'] : 'smart_toy';
      const color = typeof entry['color'] === 'string' ? entry['color'] : '#999999';
      const allowedTools = Array.isArray(entry['allowedTools'])
        ? entry['allowedTools'].filter(tool => typeof tool === 'string')
        : [];
      const order = typeof entry['order'] === 'number' && Number.isFinite(entry['order'])
        ? entry['order']
        : 0;
      const modelType = typeof entry['modelType'] === 'string' ? entry['modelType'] : 'gpt-4.1-mini';
      const activePhases = Array.isArray(entry['activePhases'])
        ? entry['activePhases'].filter(phase => typeof phase === 'string') as AgentConfiguration['activePhases']
        : undefined;
      const temperature = typeof entry['temperature'] === 'number' && Number.isFinite(entry['temperature'])
        ? entry['temperature']
        : undefined;
      const reasoningEffort = typeof entry['reasoningEffort'] === 'string'
        ? entry['reasoningEffort']
        : undefined;

      agents.push({
        id,
        name,
        isFacilitator,
        systemPrompt,
        icon,
        color,
        activePhases,
        allowedTools,
        order,
        modelType,
        temperature,
        reasoningEffort
      });
    }

    return agents;
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

  public toggleBoundedContextMode(): void {
    this.canvasService.reset();
    this.canvasService.isDrawingBoundedContext = true;
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
        phase: this.canvasService.boardState.phase || '',
        autonomousEnabled: this.canvasService.boardState.autonomousEnabled
      } as BoardContextData
    });

    dialogRef.afterClosed().subscribe((result: BoardContextData | undefined) => {
      if (result) {
        const command = new UpdateBoardContextCommand(
          result.domain || undefined,
          this.canvasService.boardState.domain,
          result.sessionScope || undefined,
          this.canvasService.boardState.sessionScope,
          result.phase || undefined,
          this.canvasService.boardState.phase,
          result.autonomousEnabled,
          this.canvasService.boardState.autonomousEnabled
        );
        this.canvasService.executeCommand(command);
      }
    });
  }

  public openAgentConfig(): void {
    const dialogRef = this.dialog.open(AgentConfigModalComponent, {
      width: '720px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: {
        boardId: this.id,
        agents: this.agentConfigurations
      } as AgentConfigModalData
    });

    dialogRef.afterClosed().subscribe((result: AgentConfiguration[] | undefined) => {
      if (result) {
        this.saveAgentConfigurations(result);
      }
    });
  }

  private saveAgentConfigurations(agents: AgentConfiguration[]): void {
    const normalizedAgents = this.normalizeAgentsForSave(agents);
    const existingIds = new Set(this.agentConfigurations.map(a => a.id));
    const newIds = new Set(normalizedAgents.map(a => a.id));

    // Delete removed agents
    const toDelete = this.agentConfigurations.filter(a => !newIds.has(a.id) && !a.isFacilitator);
    // Add new agents
    const toAdd = normalizedAgents.filter(a => !a.isFacilitator && !existingIds.has(a.id));
    // Update existing agents
    const toUpdate = normalizedAgents.filter(a => existingIds.has(a.id));

    const ops: Promise<void>[] = [];

    for (const agent of toDelete) {
      ops.push(new Promise<void>((resolve, reject) => {
        this.boardsService.deleteAgent(this.id, agent.id).subscribe({ next: () => resolve(), error: reject });
      }));
    }

    for (const agent of toAdd) {
      ops.push(new Promise<void>((resolve, reject) => {
        this.boardsService.addAgent(this.id, {
          name: agent.name,
          systemPrompt: agent.systemPrompt,
          icon: agent.icon,
          color: agent.color,
          activePhases: agent.activePhases,
          allowedTools: agent.allowedTools,
          order: agent.order,
          modelType: agent.modelType,
          temperature: agent.temperature,
          reasoningEffort: agent.reasoningEffort
        }).subscribe({ next: () => resolve(), error: reject });
      }));
    }

    for (const agent of toUpdate) {
      ops.push(new Promise<void>((resolve, reject) => {
        this.boardsService.updateAgent(this.id, agent.id, {
          name: agent.name,
          systemPrompt: agent.systemPrompt,
          icon: agent.icon,
          color: agent.color,
          activePhases: agent.activePhases,
          allowedTools: agent.allowedTools,
          order: agent.order,
          modelType: agent.modelType,
          temperature: agent.temperature,
          reasoningEffort: agent.reasoningEffort
        }).subscribe({ next: () => resolve(), error: reject });
      }));
    }

    Promise.all(ops).then(() => {
      // Refresh agent configs from server
      this.boardsService.getAgents(this.id).subscribe({
        next: configs => this.agentConfigurations = configs,
        error: err => console.error('Failed to refresh agent configs:', err)
      });
    }).catch(err => console.error('Failed to save agent configurations:', err));
  }

  private normalizeAgentsForSave(agents: AgentConfiguration[]): AgentConfiguration[] {
    const existingFacilitator = this.agentConfigurations.find(agent => agent.isFacilitator);
    const normalized: AgentConfiguration[] = [];
    let facilitatorSeen = false;

    for (const agent of agents) {
      if (!agent.isFacilitator) {
        normalized.push(agent);
        continue;
      }

      if (facilitatorSeen) {
        continue;
      }

      facilitatorSeen = true;
      if (existingFacilitator) {
        normalized.push({
          ...agent,
          id: existingFacilitator.id,
          isFacilitator: true
        });
      } else {
        normalized.push(agent);
      }
    }

    return normalized;
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

  public disableAutonomousMode(): void {
    if (!this.canvasService.boardState.autonomousEnabled) {
      return;
    }

    const command = new UpdateBoardContextCommand(
      this.canvasService.boardState.domain,
      this.canvasService.boardState.domain,
      this.canvasService.boardState.sessionScope,
      this.canvasService.boardState.sessionScope,
      this.canvasService.boardState.phase,
      this.canvasService.boardState.phase,
      false,
      this.canvasService.boardState.autonomousEnabled
    );
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
        this.canvasService.boardState.phase = board.phase;
        this.canvasService.boardState.autonomousEnabled = board.autonomousEnabled;
        this.agentConfigurations = board.agentConfigurations ?? [];
        // Map NoteDto[] to Note[]
        this.canvasService.boardState.notes = board.notes.map(n => ({
          ...n,
          type: n.type as Note['type'],
          selected: false // default, or preserve if needed
        }));
        this.canvasService.boardState.connections = board.connections;
        this.canvasService.boardState.boundedContexts = (board.boundedContexts ?? []).map(bc => ({
          ...bc,
          selected: false
        }));
        this.canvasService.drawCanvas();
      });

    this.startPruningCursors();
    this.boardsHub.joinBoard(this.id, this.userName);
  }

  public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
    this.canvasService.remoteCursors.clear();
  }

  private subscribeToEvents(): void {
    this.boardsHub.connectedUsers$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(users => {
        this.connectedUsers = users.filter(user => user.boardId === this.id)
          .map(user => new BoardUser(user.boardId, user.userName, user.connectionId));
      });
    this.boardsHub.userJoinedBoard$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.connectedUsers.push(new BoardUser(event.boardId, event.userName, event.connectionId));
      });
    this.boardsHub.userLeftBoard$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.connectedUsers = this.connectedUsers.filter(user => user.connectionId !== event.connectionId);
        this.canvasService.remoteCursors.delete(event.connectionId);
        this.canvasService.drawCanvas();
      });

    this.boardsHub.cursorPositionUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
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
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new UpdateBoardNameCommand(event.newName, event.oldName), true, event.isUndo);
      });

    this.boardsHub.boardContextUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(
          new UpdateBoardContextCommand(
            event.newDomain, event.oldDomain,
            event.newSessionScope, event.oldSessionScope,
            event.newPhase, event.oldPhase,
            event.newAutonomousEnabled, event.oldAutonomousEnabled
          ), true, event.isUndo);
      });

    this.boardsHub.noteAdded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new CreateNoteCommand(event.note), true, event.isUndo);
      });

    this.boardsHub.notesMoved$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new MoveNotesCommand(event.from, event.to), true, event.isUndo);
      });

    this.boardsHub.noteResized$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new ResizeNoteCommand(event.noteId, event.from, event.to), true, event.isUndo);
      });

    this.boardsHub.notesDeleted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new DeleteNotesCommand(event.notes, event.connections), true, event.isUndo);
      });

    this.boardsHub.connectionCreated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new CreateConnectionCommand(event.connection), true, event.isUndo);
      });

    this.boardsHub.noteTextEdited$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new EditNoteTextCommand(event.noteId, event.toText, event.fromText), true, event.isUndo);
      });

    this.boardsHub.pasted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new PasteCommand(event.notes, event.connections), true, event.isUndo);
      });

    this.boardsHub.boundedContextCreated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new CreateBoundedContextCommand(event.boundedContext), true, event.isUndo);
      });

    this.boardsHub.boundedContextUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(
          new UpdateBoundedContextCommand(
            event.boundedContextId,
            event.oldName, event.newName,
            event.oldX, event.newX,
            event.oldY, event.newY,
            event.oldWidth, event.newWidth,
            event.oldHeight, event.newHeight
          ), true, event.isUndo);
      });

    this.boardsHub.boundedContextDeleted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        this.canvasService.executeCommand(new DeleteBoundedContextCommand(event.boundedContext), true, event.isUndo);
      });

    this.boardsHub.agentUserMessage$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        if (!this.isChatOpen && event.userName !== this.userName) {
          this.hasUnreadMessages = true;
        }
      });

    this.boardsHub.agentStepUpdate$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (!this.isChatOpen) this.hasUnreadMessages = true;
      });

    this.boardsHub.autonomousStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => {
        if (status.boardId === this.id) {
          this.autonomousStatus = status;
          // Fallback sync: if the server disabled autonomy but the BoardContextUpdated
          // broadcast was missed (e.g. brief disconnect), keep the flag in sync.
          if (this.canvasService.boardState.autonomousEnabled !== status.isEnabled) {
            this.canvasService.boardState.autonomousEnabled = status.isEnabled;
          }
        }
      });

    this.boardsHub.agentConfigurationsUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        if (event.boardId === this.id) {
          this.agentConfigurations = event.agents;
        }
      });
  }

  private startPruningCursors(): void {
    const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
    this.destroyRef.onDestroy(() => clearInterval(intervalId));
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
