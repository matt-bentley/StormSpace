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
        bool Delete(Guid id);
    }

    public sealed class BoardsRepository : IBoardsRepository
    {
        private const string BoardsCacheKey = "boards";
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
            var boards = _cache.GetOrCreate(BoardsCacheKey, entry =>
            {
                entry.SetOptions(_cacheOptions);
                return new List<Board>();
            });
            return boards ?? [];
        }

        public Board? GetById(Guid id)
        {
            return GetAll().FirstOrDefault(board => board.Id == id);
        }

        public void Add(Board board)
        {
            var boards = GetAll();
            boards.Add(board);
            _cache.Set(BoardsCacheKey, boards, _cacheOptions);
        }

        public bool Update(Guid id, Board boardUpdate)
        {
            var boards = GetAll();
            var board = boards.FirstOrDefault(b => b.Id == id);
            if (board != null)
            {
                board.Name = boardUpdate.Name;
                board.Domain = boardUpdate.Domain;
                board.SessionScope = boardUpdate.SessionScope;
                board.AgentInstructions = boardUpdate.AgentInstructions;
                board.Notes = boardUpdate.Notes;
                board.Connections = boardUpdate.Connections;
                _cache.Set(BoardsCacheKey, boards, _cacheOptions);
                return true;
            }
            return false;
        }

        public bool Delete(Guid id)
        {
            var boards = GetAll();
            var removed = boards.RemoveAll(board => board.Id == id) > 0;
            if (!removed)
            {
                return false;
            }

            _cache.Set(BoardsCacheKey, boards, _cacheOptions);
            return true;
        }
    }
}
