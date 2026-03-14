import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AiChatPanelComponent } from './ai-chat-panel.component';
import { AgentChatMessage, AgentToolCallStartedEvent, AutonomousFacilitatorStatus, BoardsSignalRService } from '../../_shared/services/boards-signalr.service';

class FakeBoardsSignalRService {
  public agentUserMessage$ = new Subject<AgentChatMessage>();
  public agentResponse$ = new Subject<AgentChatMessage>();
  public agentToolCallStarted$ = new Subject<AgentToolCallStartedEvent>();
  public agentHistoryCleared$ = new Subject<string>();
  public agentChatHistory: AgentChatMessage[] = [];

  public sendAgentMessage = jasmine.createSpy().and.returnValue(Promise.resolve());
  public getAgentHistory = jasmine.createSpy().and.returnValue(Promise.resolve());
  public clearAgentHistory = jasmine.createSpy().and.returnValue(Promise.resolve());
}

describe('AiChatPanelComponent', () => {
  let fixture: ComponentFixture<AiChatPanelComponent>;
  let component: AiChatPanelComponent;
  let signalRService: FakeBoardsSignalRService;

  beforeEach(async () => {
    signalRService = new FakeBoardsSignalRService();
    signalRService.agentChatHistory = [
      {
        role: 'assistant',
        agentId: 'planner',
        agentName: 'Planner Agent',
        messageKind: 'plan',
        executionId: 'exec-plan',
        content: 'I will inspect the board first.'
      },
      {
        role: 'assistant',
        agentId: 'board-analyst',
        agentName: 'Board Analyst',
        messageKind: 'response',
        executionId: 'exec-analyst',
        content: 'The board is missing a starter event.'
      }
    ];

    await TestBed.configureTestingModule({
      imports: [AiChatPanelComponent],
      providers: [
        {
          provide: BoardsSignalRService,
          useValue: signalRService
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AiChatPanelComponent);
    component = fixture.componentInstance;
    component.boardId = 'board-1';
    component.userName = 'Mabel';
    fixture.detectChanges();
  });

  it('renders distinct planner and specialist labels in a single timeline', () => {
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Planner Agent');
    expect(text).toContain('Board Analyst');
    expect(text).toContain('Plan');
    expect(component.messages.length).toBe(2);
  });

  it('shows and clears pending tool activity for the matching agent execution', () => {
    signalRService.agentToolCallStarted$.next({
      boardId: 'board-1',
      executionId: 'exec-editor',
      agentId: 'board-editor',
      agentName: 'Board Editor',
      toolName: 'CreateNotes',
      arguments: {
        count: '1'
      }
    });
    fixture.detectChanges();

    expect(component.pendingMessages.length).toBe(1);
    expect((fixture.nativeElement.textContent as string)).toContain('Board Editor');
    expect((fixture.nativeElement.textContent as string)).toContain('CreateNotes');

    signalRService.agentResponse$.next({
      role: 'assistant',
      agentId: 'board-editor',
      agentName: 'Board Editor',
      messageKind: 'response',
      executionId: 'exec-editor',
      content: 'I added the starter event.'
    });
    fixture.detectChanges();

    expect(component.pendingMessages.length).toBe(0);
    expect((fixture.nativeElement.textContent as string)).toContain('I added the starter event.');
  });
});