import { Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, Pipe, PipeTransform, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BoardsSignalRService, AgentChatMessage, AgentToolCallStartedEvent, AutonomousFacilitatorStatus } from '../../_shared/services/boards-signalr.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subject, takeUntil } from 'rxjs';
import { marked } from 'marked';

marked.setOptions({ breaks: true, gfm: true });

@Pipe({ name: 'markdown' })
export class MarkdownPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) { }

  transform(value: string): SafeHtml {
    const html = marked.parse(value, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  userName?: string;
  agentName?: string;
  text: string;
  toolCalls?: { name: string; arguments: string }[];
}

const AGENT_CONFIG: Record<string, { icon: string; color: string; cssClass: string }> = {
  'Facilitator': { icon: 'school', color: '#3f51b5', cssClass: 'agent-facilitator' },
  'Board Builder': { icon: 'construction', color: '#009688', cssClass: 'agent-builder' },
  'Board Reviewer': { icon: 'checklist', color: '#ff8f00', cssClass: 'agent-reviewer' },
};

@Component({
  selector: 'app-ai-chat-panel',
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatTooltipModule, MarkdownPipe],
  templateUrl: './ai-chat-panel.component.html',
  styleUrls: ['./ai-chat-panel.component.scss']
})
export class AiChatPanelComponent implements OnInit, OnDestroy {
  @Input() boardId!: string;
  @Input() userName!: string;
  @Input() autonomousEnabled = false;
  @Input() autonomousStatus?: AutonomousFacilitatorStatus;
  @Output() closed = new EventEmitter<void>();
  @Output() disableAutonomousRequested = new EventEmitter<void>();
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;

  private destroy$ = new Subject<void>();

  public messages: ChatMessage[] = [];
  public activeToolCalls: { name: string; arguments: string }[] = [];
  public activeAgentName?: string;
  public input = '';
  public loading = false;
  
  public panelWidth = 360;
  public isResizing = false;
  private startX = 0;
  private startWidth = 0;

  constructor(private signalRService: BoardsSignalRService) { }

  ngOnInit(): void {
    // Load existing history from the service cache, or request from server
    if (this.signalRService.agentChatHistory.length > 0) {
      this.messages = this.signalRService.agentChatHistory.map(m => this.mapToDisplayMessage(m));
      this.scrollToBottom();
    } else {
      // Request history from server (response arrives via AgentChatHistory listener)
      this.signalRService.getAgentHistory(this.boardId).then(() => {
        if (this.signalRService.agentChatHistory.length > 0) {
          this.messages = this.signalRService.agentChatHistory.map(m => this.mapToDisplayMessage(m));
          this.scrollToBottom();
        }
      });
    }

    this.signalRService.agentUserMessage$
      .pipe(takeUntil(this.destroy$))
      .subscribe(msg => {
        this.messages.push(this.mapToDisplayMessage(msg));
        this.scrollToBottom();
      });

    this.signalRService.agentResponse$
      .pipe(takeUntil(this.destroy$))
      .subscribe(msg => {
        this.loading = false;
        this.activeToolCalls = [];
        this.messages.push(this.mapToDisplayMessage(msg));
        this.scrollToBottom();
      });

    this.signalRService.agentToolCallStarted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (this.loading) {
          this.activeAgentName = event.agentName;
          const args = Object.entries(event.arguments ?? {})
            .map(([k, v]) => `${k}: ${v}`)
            .join(', ');
          this.activeToolCalls.push({ name: event.toolName, arguments: args });
          this.scrollToBottom();
        }
      });

    this.signalRService.agentHistoryCleared$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.messages = [];
        this.activeToolCalls = [];
        this.loading = false;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  public send(): void {
    const text = this.input.trim();
    if (!text || this.loading) return;

    this.input = '';
    this.loading = true;
    this.activeToolCalls = [];
    this.activeAgentName = undefined;
    this.signalRService.sendAgentMessage(this.boardId, text);
  }

  public clearHistory(): void {
    this.signalRService.clearAgentHistory(this.boardId);
  }

  public onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  public isOwnMessage(msg: ChatMessage): boolean {
    return msg.role === 'user' && msg.userName === this.userName;
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
    if (!this.autonomousEnabled) {
      return 'Autonomy off';
    }

    if (this.autonomousStatus?.isRunning) {
      return 'Autonomy working';
    }

    if (this.autonomousStatus?.stopReason === 'noActiveUsers') {
      return 'Paused: no users';
    }

    if (this.autonomousStatus?.stopReason === 'failureLimitExceeded') {
      return 'Paused after errors';
    }

    return 'Autonomy on';
  }

  public getAgentConfig(agentName?: string): { icon: string; color: string; cssClass: string } {
    return AGENT_CONFIG[agentName ?? ''] ?? AGENT_CONFIG['Facilitator'];
  }

  private mapToDisplayMessage(msg: AgentChatMessage): ChatMessage {
    return {
      role: msg.role as 'user' | 'assistant',
      userName: msg.userName ?? undefined,
      agentName: msg.agentName ?? undefined,
      text: msg.content ?? '',
      toolCalls: msg.toolCalls?.map(tc => ({ name: tc.name, arguments: tc.arguments }))
    };
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
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
