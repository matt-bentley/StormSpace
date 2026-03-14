using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace EventStormingBoard.Server.Hubs
{
    public sealed class BoardsHub : Hub
    {
        private readonly IBoardEventPipeline _boardEventPipeline;
        private readonly IAgentService _agentService;
        private readonly IBoardPresenceService _boardPresenceService;
        private readonly IAutonomousFacilitatorCoordinator _coordinator;
        private readonly IBoardsRepository _boardsRepository;

        public BoardsHub(
            IBoardEventPipeline boardEventPipeline,
            IAgentService agentService,
            IBoardPresenceService boardPresenceService,
            IAutonomousFacilitatorCoordinator coordinator,
            IBoardsRepository boardsRepository)
        {
            _boardEventPipeline = boardEventPipeline;
            _agentService = agentService;
            _boardPresenceService = boardPresenceService;
            _coordinator = coordinator;
            _boardsRepository = boardsRepository;
        }

        private string? GetUserName(Guid boardId)
        {
            return _boardPresenceService.GetUserName(boardId, Context.ConnectionId);
        }

        public async Task JoinBoard(Guid boardId, string userName)
        {
            var connectionId = Context.ConnectionId;
            var connectedUsers = _boardPresenceService.JoinBoard(boardId, connectionId, userName);
            await Clients.Caller.SendAsync("ConnectedUsers", connectedUsers);

            var board = _boardsRepository.GetById(boardId);
            if (board != null)
            {
                _coordinator.SyncBoardSettings(boardId, board.AutonomousEnabled);
                await Clients.Caller.SendAsync("AutonomousFacilitatorStatusChanged", _coordinator.GetStatus(boardId, board.AutonomousEnabled));
            }

            await Clients.Group(boardId.ToString()).SendAsync("UserJoinedBoard", new UserJoinedBoardEvent()
            {
                BoardId = boardId,
                UserName = userName,
                ConnectionId = connectionId
            });
            await Groups.AddToGroupAsync(connectionId, boardId.ToString());
        }

        public async Task LeaveBoard(Guid boardId)
        {
            var connectionId = Context.ConnectionId;

            await RemoveUserFromBoardAsync(boardId, connectionId);

            await Groups.RemoveFromGroupAsync(connectionId, boardId.ToString());
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            foreach (var boardId in _boardPresenceService.GetBoardsForConnection(connectionId))
            {
                await RemoveUserFromBoardAsync(boardId, connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task RemoveUserFromBoardAsync(Guid boardId, string connectionId)
        {
            _boardPresenceService.LeaveBoard(boardId, connectionId);
            await Clients.Group(boardId.ToString()).SendAsync("UserLeftBoard", new UserLeftBoardEvent()
            {
                BoardId = boardId,
                ConnectionId = connectionId
            });
        }

        public async Task BroadcastBoardNameUpdated(BoardNameUpdatedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardNameUpdated", @event);
        }

        public async Task BroadcastBoardContextUpdated(BoardContextUpdatedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.SyncBoardSettings(@event.BoardId, @event.NewAutonomousEnabled);
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardContextUpdated", @event);
            await Clients.Group(@event.BoardId.ToString()).SendAsync("AutonomousFacilitatorStatusChanged", _coordinator.GetStatus(@event.BoardId, @event.NewAutonomousEnabled));
        }

        public async Task BroadcastNoteCreated(NoteCreatedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteCreated", @event);
        }

        public async Task BroadcastNotesMoved(NotesMovedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesMoved", @event);
        }

        public async Task BroadcastNoteResized(NoteResizedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteResized", @event);
        }

        public async Task BroadcastNotesDeleted(NotesDeletedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesDeleted", @event);
        }

        public async Task BroadcastConnectionCreated(ConnectionCreatedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("ConnectionCreated", @event);
        }

        public async Task BroadcastNoteTextEdited(NoteTextEditedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteTextEdited", @event);
        }

        public async Task BroadcastPasted(PastedEvent @event)
        {
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("Pasted", @event);
        }

        public async Task BroadcastCursorPositionUpdated(CursorPositionUpdatedEvent @event)
        {
            if (!double.IsFinite(@event.X) || !double.IsFinite(@event.Y))
            {
                return;
            }

            @event.ConnectionId = Context.ConnectionId;
            @event.UserName = _boardPresenceService.GetUserName(@event.BoardId, Context.ConnectionId) ?? @event.UserName;

            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("CursorPositionUpdated", @event);
        }

        public async Task SendAgentMessage(Guid boardId, string message)
        {
            var userName = GetUserName(boardId) ?? "Unknown";
            _coordinator.RecordUserActivity(boardId);
            _coordinator.BeginManualAgentResponse(boardId, DateTimeOffset.UtcNow);

            await Clients.Group(boardId.ToString()).SendAsync("AgentUserMessage", new AgentChatMessageDto
            {
                Role = "user",
                UserName = userName,
                Content = message,
                Timestamp = DateTime.UtcNow
            });

            var responses = await _agentService.ChatAsync(boardId, message, userName);
            _coordinator.AcknowledgeManualAgentResponse(boardId, DateTimeOffset.UtcNow);
            foreach (var response in responses)
            {
                await Clients.Group(boardId.ToString()).SendAsync("AgentResponse", response);
            }
        }

        public async Task GetAgentHistory(Guid boardId)
        {
            var history = _agentService.GetHistory(boardId);
            await Clients.Caller.SendAsync("AgentChatHistory", history);
        }

        public async Task ClearAgentHistory(Guid boardId)
        {
            _agentService.ClearHistory(boardId);
            await Clients.Group(boardId.ToString()).SendAsync("AgentHistoryCleared", new { BoardId = boardId });
        }
    }
}
