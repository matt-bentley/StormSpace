using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Hubs
{
    public sealed class BoardsHub : Hub
    {
        private static readonly ConcurrentDictionary<Guid, HashSet<BoardUserDto>> BoardUsers = new();

        public async Task JoinBoard(Guid boardId, string userName)
        {
            var connectionId = Context.ConnectionId;

            if (!BoardUsers.ContainsKey(boardId))
            {
                BoardUsers[boardId] = new HashSet<BoardUserDto>();
            }

            BoardUsers[boardId].Add(new BoardUserDto()
            {
                BoardId = boardId,
                ConnectionId = connectionId,
                UserName = userName
            });

            var connectedUsers = BoardUsers[boardId].ToList();
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

            if (BoardUsers.ContainsKey(boardId))
            {
                await RemoveUserFromBoardAsync(boardId, connectionId);
            }

            await Groups.RemoveFromGroupAsync(connectionId, boardId.ToString());
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            foreach (var board in BoardUsers)
            {
                if (board.Value.Any(e => e.ConnectionId == connectionId))
                {
                    await RemoveUserFromBoardAsync(board.Key, connectionId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task RemoveUserFromBoardAsync(Guid boardId, string connectionId)
        {
            if (BoardUsers.ContainsKey(boardId))
            {
                BoardUsers[boardId].RemoveWhere(e => e.ConnectionId == connectionId);

                if (BoardUsers[boardId].Count == 0)
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
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("BoardNameUpdated", @event);
        }        

        public async Task BroadcastNoteCreated(NoteCreatedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteCreated", @event);
        }

        public async Task BroadcastNotesMoved(NotesMovedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesMoved", @event);
        }

        public async Task BroadcastNoteResized(NoteResizedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteResized", @event);
        }

        public async Task BroadcastNotesDeleted(NotesDeletedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NotesDeleted", @event);
        }

        public async Task BroadcastConnectionCreated(ConnectionCreatedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("ConnectionCreated", @event);
        }

        public async Task BroadcastNoteTextEdited(NoteTextEditedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("NoteTextEdited", @event);
        }

        public async Task BroadcastPasted(PastedEvent @event)
        {
            await Clients.OthersInGroup(@event.BoardId.ToString()).SendAsync("Pasted", @event);
        }
    }
}
