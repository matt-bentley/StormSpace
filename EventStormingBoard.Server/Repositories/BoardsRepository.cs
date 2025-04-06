using EventStormingBoard.Server.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace EventStormingBoard.Server.Repositories
{
    public interface IBoardsRepository
    {
        List<Board> GetAll();
        Board? GetById(Guid id);
        void Add(Board board);
        bool Update(Guid id, Board boardUpdate);
    }

    public sealed class BoardsRepository : IBoardsRepository
    {
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public BoardsRepository(IMemoryCache cache)
        {
            _cache = cache;
            _cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1)
            };
        }

        public List<Board> GetAll()
        {
            var boards = _cache.GetOrCreate("boards", entry =>
            {
                entry.SetOptions(_cacheOptions);
                return new List<Board>();
            });
            return boards ?? [];
        }

        public Board? GetById(Guid id)
        {
            _cache.TryGetValue(id, out Board? board);
            return board;
        }

        public void Add(Board board)
        {
            _cache.Set(board.Id, board, _cacheOptions);
            var boards = GetAll();
            boards.Add(board);
            _cache.Set("boards", boards, _cacheOptions);
        }

        public bool Update(Guid id, Board boardUpdate)
        {
            if (_cache.TryGetValue(id, out Board? board) && board != null)
            {
                board.Name = boardUpdate.Name;
                board.Notes = boardUpdate.Notes;
                board.Connections = boardUpdate.Connections;
                _cache.Set(id, board, _cacheOptions);
                return true;
            }
            return false;
        }
    }
}
