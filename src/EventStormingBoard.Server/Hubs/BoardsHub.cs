using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

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

        public static string? GetAuthenticatedUserName(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var name = user.FindFirstValue("name")
                       ?? user.FindFirstValue("preferred_username");

            if (string.IsNullOrEmpty(name))
            {
                throw new HubException(
                    "Authenticated user has no 'name' or 'preferred_username' claim. " +
                    "Configure optional claims (name, preferred_username) on the access token in your Entra ID app registration.");
            }

            return name;
        }

        private string? GetUserName(Guid boardId)
        {
            return _boardPresenceService.GetUserName(boardId, Context.ConnectionId);
        }

        private void EnsureBoardMember(Guid boardId)
        {
            if (_boardPresenceService.GetUserName(boardId, Context.ConnectionId) == null)
            {
                throw new HubException($"Connection is not a member of board {boardId}. Call JoinBoard first.");
            }
        }

        public async Task JoinBoard(Guid boardId, string userName)
        {
            var connectionId = Context.ConnectionId;

            // When authenticated, use the claim-derived name instead of client-supplied value
            var authName = GetAuthenticatedUserName(Context.User);
            if (authName != null)
            {
                userName = authName;
            }

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
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardNameUpdated", @event);
        }

        public async Task BroadcastBoardContextUpdated(BoardContextUpdatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.SyncBoardSettings(@event.BoardId, @event.NewAutonomousEnabled);
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardContextUpdated", @event);
            await Clients.Group(@event.BoardId.ToString()).SendAsync("AutonomousFacilitatorStatusChanged", _coordinator.GetStatus(@event.BoardId, @event.NewAutonomousEnabled));
        }

        public async Task BroadcastNoteCreated(NoteCreatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteCreated", @event);
        }

        public async Task BroadcastNotesMoved(NotesMovedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesMoved", @event);
        }

        public async Task BroadcastNoteResized(NoteResizedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteResized", @event);
        }

        public async Task BroadcastNotesDeleted(NotesDeletedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesDeleted", @event);
        }

        public async Task BroadcastConnectionCreated(ConnectionCreatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("ConnectionCreated", @event);
        }

        public async Task BroadcastNoteTextEdited(NoteTextEditedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteTextEdited", @event);
        }

        public async Task BroadcastPasted(PastedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("Pasted", @event);
        }

        public async Task BroadcastBoundedContextCreated(BoundedContextCreatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoundedContextCreated", @event);
        }

        public async Task BroadcastBoundedContextUpdated(BoundedContextUpdatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoundedContextUpdated", @event);
        }

        public async Task BroadcastBoundedContextDeleted(BoundedContextDeletedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);
            _boardEventPipeline.ApplyAndLog(@event, GetUserName(@event.BoardId));
            _coordinator.RecordUserActivity(@event.BoardId);
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoundedContextDeleted", @event);
        }

        public async Task BroadcastCursorPositionUpdated(CursorPositionUpdatedEvent @event)
        {
            EnsureBoardMember(@event.BoardId);

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
            EnsureBoardMember(boardId);

            var board = _boardsRepository.GetById(boardId);
            if (board == null)
            {
                await Clients.Caller.SendAsync("AgentChatComplete", new { BoardId = boardId });
                return;
            }

            var userName = GetUserName(boardId) ?? "Unknown";
            var succeeded = false;

            try
            {
                _coordinator.RecordUserActivity(boardId);
                _coordinator.BeginManualAgentResponse(boardId, DateTimeOffset.UtcNow);

                await Clients.Group(boardId.ToString()).SendAsync("AgentUserMessage", new AgentChatMessageDto
                {
                    StepId = Guid.NewGuid(),
                    BoardId = boardId,
                    Role = "user",
                    UserName = userName,
                    Content = message,
                    Timestamp = DateTime.UtcNow
                });

                var responses = await _agentService.ChatAsync(boardId, message, userName);
                _coordinator.AcknowledgeManualAgentResponse(boardId, DateTimeOffset.UtcNow);
                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    _coordinator.CancelManualAgentResponse(boardId);
                }

                // Steps were broadcast in real-time via AgentStepUpdate during pipeline execution.
                // Signal completion so the client clears the loading state.
                await Clients.Group(boardId.ToString()).SendAsync("AgentChatComplete", new { BoardId = boardId });
            }
        }

        public async Task GetAgentHistory(Guid boardId)
        {
            EnsureBoardMember(boardId);

            if (_boardsRepository.GetById(boardId) == null)
            {
                await Clients.Caller.SendAsync("AgentChatHistory", Array.Empty<AgentChatMessageDto>());
                return;
            }

            var history = _agentService.GetHistory(boardId);
            await Clients.Caller.SendAsync("AgentChatHistory", history);
        }

        public async Task ClearAgentHistory(Guid boardId)
        {
            EnsureBoardMember(boardId);
            _agentService.ClearHistory(boardId);
            await Clients.Group(boardId.ToString()).SendAsync("AgentHistoryCleared", new { BoardId = boardId });
        }
    }
}
