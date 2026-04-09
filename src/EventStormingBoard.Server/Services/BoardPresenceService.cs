using EventStormingBoard.Server.Models;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public interface IBoardPresenceService
    {
        List<BoardUserDto> JoinBoard(Guid boardId, string connectionId, string userName);
        bool LeaveBoard(Guid boardId, string connectionId);
        IReadOnlyList<Guid> GetBoardsForConnection(string connectionId);
        string? GetUserName(Guid boardId, string connectionId);
        bool HasActiveUsers(Guid boardId);
        void ClearBoard(Guid boardId);
    }

    public sealed class BoardPresenceService : IBoardPresenceService
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, BoardUserDto>> _boardUsers = new();

        public List<BoardUserDto> JoinBoard(Guid boardId, string connectionId, string userName)
        {
            var boardConnections = _boardUsers.GetOrAdd(boardId, _ => new ConcurrentDictionary<string, BoardUserDto>());
            boardConnections[connectionId] = new BoardUserDto
            {
                BoardId = boardId,
                ConnectionId = connectionId,
                UserName = userName
            };

            return boardConnections.Values.ToList();
        }

        public bool LeaveBoard(Guid boardId, string connectionId)
        {
            if (!_boardUsers.TryGetValue(boardId, out var boardConnections))
            {
                return false;
            }

            var removed = boardConnections.TryRemove(connectionId, out _);
            if (boardConnections.IsEmpty)
            {
                _boardUsers.TryRemove(boardId, out _);
            }

            return removed;
        }

        public IReadOnlyList<Guid> GetBoardsForConnection(string connectionId)
        {
            return _boardUsers
                .Where(entry => entry.Value.ContainsKey(connectionId))
                .Select(entry => entry.Key)
                .ToList();
        }

        public string? GetUserName(Guid boardId, string connectionId)
        {
            if (_boardUsers.TryGetValue(boardId, out var boardConnections) &&
                boardConnections.TryGetValue(connectionId, out var user))
            {
                return user.UserName;
            }

            return null;
        }

        public bool HasActiveUsers(Guid boardId)
        {
            return _boardUsers.TryGetValue(boardId, out var boardConnections) && !boardConnections.IsEmpty;
        }

        public void ClearBoard(Guid boardId)
        {
            _boardUsers.TryRemove(boardId, out _);
        }
    }
}