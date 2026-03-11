using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Hubs
{
    public sealed class BoardsHub : Hub
    {
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, BoardUserDto>> BoardUsers = new();

        private readonly IBoardStateService _boardStateService;
        private readonly IBoardEventLog _boardEventLog;

        public BoardsHub(IBoardStateService boardStateService, IBoardEventLog boardEventLog)
        {
            _boardStateService = boardStateService;
            _boardEventLog = boardEventLog;
        }

        private string? GetUserName(Guid boardId)
        {
            if (BoardUsers.TryGetValue(boardId, out var boardConnections) &&
                boardConnections.TryGetValue(Context.ConnectionId, out var user))
            {
                return user.UserName;
            }
            return null;
        }

        public async Task JoinBoard(Guid boardId, string userName)
        {
            var connectionId = Context.ConnectionId;

            var boardConnections = BoardUsers.GetOrAdd(boardId, _ => new ConcurrentDictionary<string, BoardUserDto>());
            boardConnections[connectionId] = new BoardUserDto()
            {
                BoardId = boardId,
                ConnectionId = connectionId,
                UserName = userName
            };

            var connectedUsers = boardConnections.Values.ToList();
            await Clients.Caller.SendAsync("ConnectedUsers", connectedUsers);

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
            foreach (var board in BoardUsers.ToArray())
            {
                if (board.Value.ContainsKey(connectionId))
                {
                    await RemoveUserFromBoardAsync(board.Key, connectionId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task RemoveUserFromBoardAsync(Guid boardId, string connectionId)
        {
            if (BoardUsers.TryGetValue(boardId, out var boardConnections))
            {
                boardConnections.TryRemove(connectionId, out _);

                if (boardConnections.IsEmpty)
                {
                    BoardUsers.TryRemove(boardId, out _);
                }
            }
            await Clients.Group(boardId.ToString()).SendAsync("UserLeftBoard", new UserLeftBoardEvent()
            {
                BoardId = boardId,
                ConnectionId = connectionId
            });
        }

        public async Task BroadcastBoardNameUpdated(BoardNameUpdatedEvent @event)
        {
            _boardStateService.ApplyBoardNameUpdated(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardNameUpdated", @event);
        }

        public async Task BroadcastNoteCreated(NoteCreatedEvent @event)
        {
            _boardStateService.ApplyNoteCreated(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteCreated", @event);
        }

        public async Task BroadcastNotesMoved(NotesMovedEvent @event)
        {
            _boardStateService.ApplyNotesMoved(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesMoved", @event);
        }

        public async Task BroadcastNoteResized(NoteResizedEvent @event)
        {
            _boardStateService.ApplyNoteResized(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteResized", @event);
        }

        public async Task BroadcastNotesDeleted(NotesDeletedEvent @event)
        {
            _boardStateService.ApplyNotesDeleted(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesDeleted", @event);
        }

        public async Task BroadcastConnectionCreated(ConnectionCreatedEvent @event)
        {
            _boardStateService.ApplyConnectionCreated(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("ConnectionCreated", @event);
        }

        public async Task BroadcastNoteTextEdited(NoteTextEditedEvent @event)
        {
            _boardStateService.ApplyNoteTextEdited(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteTextEdited", @event);
        }

        public async Task BroadcastPasted(PastedEvent @event)
        {
            _boardStateService.ApplyPasted(@event);
            _boardEventLog.Append(@event.BoardId, @event, GetUserName(@event.BoardId));
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("Pasted", @event);
        }

        public async Task BroadcastCursorPositionUpdated(CursorPositionUpdatedEvent @event)
        {
            if (!double.IsFinite(@event.X) || !double.IsFinite(@event.Y))
            {
                return;
            }

            @event.ConnectionId = Context.ConnectionId;
            if (BoardUsers.TryGetValue(@event.BoardId, out var boardConnections) &&
                boardConnections.TryGetValue(Context.ConnectionId, out var user))
            {
                @event.UserName = user.UserName;
            }

            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("CursorPositionUpdated", @event);
        }
    }
}
