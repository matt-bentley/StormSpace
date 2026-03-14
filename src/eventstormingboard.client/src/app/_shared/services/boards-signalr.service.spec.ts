import { BoardsSignalRService } from './boards-signalr.service';

describe('BoardsSignalRService', () => {
  const prototype = BoardsSignalRService.prototype as unknown as { startConnection: () => Promise<void> };
  const originalStartConnection = prototype.startConnection;

  beforeEach(() => {
    prototype.startConnection = jasmine.createSpy().and.returnValue(Promise.resolve());
  });

  afterEach(() => {
    prototype.startConnection = originalStartConnection;
  });

  it('maps multi-agent metadata from SignalR payloads', () => {
    const service = new BoardsSignalRService();
    const message = (service as never as { mapAgentChatMessage: (raw: unknown) => Record<string, unknown> }).mapAgentChatMessage({
      Role: 'assistant',
      AgentId: 'planner',
      AgentName: 'Planner Agent',
      MessageKind: 'plan',
      ExecutionId: 'exec-123',
      Content: 'Plan the next step.',
      ToolCalls: [
        {
          Name: 'GetBoardState',
          Arguments: 'count: 20'
        }
      ]
    });

    expect(message['agentId']).toBe('planner');
    expect(message['agentName']).toBe('Planner Agent');
    expect(message['messageKind']).toBe('plan');
    expect(message['executionId']).toBe('exec-123');
    expect(message['toolCalls']).toEqual([
      {
        name: 'GetBoardState',
        arguments: 'count: 20'
      }
    ]);
  });
});