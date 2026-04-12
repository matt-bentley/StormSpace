import { Component, DestroyRef, ElementRef, HostListener, OnInit, Pipe, PipeTransform, effect, inject, input, output, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BoardsSignalRService, AgentChatMessage, AutonomousFacilitatorStatus } from '../../_shared/services/boards-signalr.service';
import { marked } from 'marked';
import DOMPurify from 'dompurify';
import { AgentConfiguration } from '../../_shared/models/agent-configuration.model';

marked.setOptions({ breaks: true, gfm: true });

@Pipe({ name: 'markdown' })
export class MarkdownPipe implements PipeTransform {
  transform(value: string): string {
    const html = marked.parse(value, { async: false }) as string;
    return DOMPurify.sanitize(html);
  }
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  userName?: string;
  agentName?: string;
  text: string;
  prompt?: string;
  stepId?: string;
  toolCalls?: { name: string; arguments: string }[];
}

export interface AgentDisplayInfo {
  label: string;
  icon: string;
  color: string;
}

const DEFAULT_AGENT: AgentDisplayInfo = { label: 'AI Assistant', icon: 'smart_toy', color: '#3f51b5' };

@Component({
  selector: 'app-ai-chat-panel',
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatTooltipModule, MarkdownPipe],
  templateUrl: './ai-chat-panel.component.html',
  styleUrls: ['./ai-chat-panel.component.scss']
})
export class AiChatPanelComponent implements OnInit {
  readonly boardId = input.required<string>();
  readonly userName = input.required<string>();
  readonly autonomousEnabled = input(false);
  readonly autonomousStatus = input<AutonomousFacilitatorStatus | undefined>();
  readonly agentConfigurations = input<AgentConfiguration[]>([]);
  readonly closed = output<void>();
  readonly disableAutonomousRequested = output<void>();
  private messagesContainer = viewChild<ElementRef<HTMLDivElement>>('messagesContainer');

  private signalRService = inject(BoardsSignalRService);
  private destroyRef = inject(DestroyRef);

  public messages: ChatMessage[] = [];
  public activeToolCalls: { name: string; arguments: string }[] = [];
  public input = '';
  public loading = false;
  
  public panelWidth = 360;
  public isResizing = false;
  private startX = 0;
  private startWidth = 0;
  private agentDisplayMap = new Map<string, AgentDisplayInfo>();
  private agentConfigEffect = effect(() => {
    const configs = this.agentConfigurations();
    this.agentDisplayMap.clear();
    for (const config of configs) {
      this.agentDisplayMap.set(config.name, {
        label: config.name,
        icon: config.icon,
        color: config.color
      });
    }
  });

  ngOnInit(): void {
    // Always request fresh history from server for the current board.
    this.signalRService.getAgentHistory(this.boardId()).then(() => {
      const cached = this.signalRService.getHistoryForBoard(this.boardId());
      if (cached.length > 0) {
        this.messages = cached.map(m => this.mapToDisplayMessage(m));
        this.scrollToBottom();
      }
    });

    this.signalRService.agentUserMessage$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(msg => {
        if (msg.boardId && msg.boardId !== this.boardId()) return;
        this.messages.push(this.mapToDisplayMessage(msg));
        this.scrollToBottom();
      });

    this.signalRService.agentResponse$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(msg => {
        if (msg.boardId && msg.boardId !== this.boardId()) return;
        this.loading = false;
        this.activeToolCalls = [];
        this.messages.push(this.mapToDisplayMessage(msg));
        this.scrollToBottom();
      });

    this.signalRService.agentStepUpdate$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(msg => {
        if (msg.boardId && msg.boardId !== this.boardId()) return;
        const display = this.mapToDisplayMessage(msg);
        // Replace-or-append: if a message with this stepId exists, replace it in-place.
        if (display.stepId) {
          const existingIndex = this.messages.findIndex(m => m.stepId === display.stepId);
          if (existingIndex >= 0) {
            this.messages[existingIndex] = display;
            this.scrollToBottom();
            return;
          }
        }
        this.messages.push(display);
        this.scrollToBottom();
      });

    this.signalRService.agentChatComplete$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(boardId => {
        if (boardId && boardId !== this.boardId()) return;
        this.loading = false;
        this.activeToolCalls = [];
      });

    this.signalRService.agentToolCallStarted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => {
        if (event.boardId && event.boardId !== this.boardId()) return;
        if (this.loading) {
          const args = Object.entries(event.arguments ?? {})
            .map(([k, v]) => `${k}: ${v}`)
            .join(', ');
          this.activeToolCalls.push({ name: event.toolName, arguments: args });
          this.scrollToBottom();
        }
      });

    this.signalRService.agentHistoryCleared$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(boardId => {
        if (boardId && boardId !== this.boardId()) return;
        this.messages = [];
        this.activeToolCalls = [];
        this.loading = false;
      });
  }

  public send(): void {
    const text = this.input.trim();
    if (!text || this.loading) return;

    this.input = '';
    this.loading = true;
    this.activeToolCalls = [];
    this.signalRService.sendAgentMessage(this.boardId(), text);
  }

  public clearHistory(): void {
    this.signalRService.clearAgentHistory(this.boardId());
  }

  public onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  public isOwnMessage(msg: ChatMessage): boolean {
    return msg.role === 'user' && msg.userName === this.userName();
  }

  public aggregateToolCalls(toolCalls: { name: string; arguments: string }[]): { name: string; count: number, active: boolean }[] {
    const map = new Map<string, number>();
    for (const tc of toolCalls) {
      map.set(tc.name, (map.get(tc.name) ?? 0) + 1);
    }
    const aggregateToolCalls = Array.from(map, ([name, count]) => ({ name, count, active: false }));
    aggregateToolCalls[aggregateToolCalls.length - 1].active = true;
    return aggregateToolCalls;
  }

  public get autonomousStatusLabel(): string {
    if (!this.autonomousEnabled()) {
      return 'Autonomy off';
    }

    if (this.autonomousStatus()?.isRunning) {
      return 'Autonomy working';
    }

    if (this.autonomousStatus()?.stopReason === 'noActiveUsers') {
      return 'Paused: no users';
    }

    if (this.autonomousStatus()?.stopReason === 'failureLimitExceeded') {
      return 'Paused after errors';
    }

    return 'Autonomy on';
  }

  private mapToDisplayMessage(msg: AgentChatMessage): ChatMessage {
    return {
      role: msg.role as 'user' | 'assistant',
      userName: msg.userName ?? undefined,
      agentName: msg.agentName ?? undefined,
      text: msg.content ?? '',
      prompt: msg.prompt ?? undefined,
      stepId: msg.stepId ?? undefined,
      toolCalls: msg.toolCalls?.map(tc => ({ name: tc.name, arguments: tc.arguments }))
    };
  }

  agentInfo(msg: ChatMessage): AgentDisplayInfo {
    return (msg.agentName && this.agentDisplayMap.get(msg.agentName)) || DEFAULT_AGENT;
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.isResizing) return;
    const delta = this.startX - event.clientX;
    const newWidth = this.startWidth + delta;
    this.panelWidth = Math.max(300, Math.min(newWidth, 800));
  }

  @HostListener('document:mouseup')
  onMouseUp() {
    this.isResizing = false;
  }

  onResizeStart(event: MouseEvent) {
    this.isResizing = true;
    this.startX = event.clientX;
    this.startWidth = this.panelWidth;
    event.preventDefault();
  }
}
