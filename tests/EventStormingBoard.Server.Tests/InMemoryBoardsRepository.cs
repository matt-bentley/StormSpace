using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Repositories;

namespace EventStormingBoard.Server.Tests;

/// <summary>
/// Simple in-memory repository for testing without IMemoryCache dependency.
/// </summary>
public sealed class InMemoryBoardsRepository : IBoardsRepository
{
    private readonly List<Board> _boards = new();

    public List<Board> GetAll() => _boards;

    public Board? GetById(Guid id) => _boards.FirstOrDefault(b => b.Id == id);

    public void Add(Board board) => _boards.Add(board);

    public bool Update(Guid id, Board boardUpdate)
    {
        var board = GetById(id);
        if (board == null) return false;
        board.Name = boardUpdate.Name;
        board.Domain = boardUpdate.Domain;
        board.SessionScope = boardUpdate.SessionScope;
        board.AgentInstructions = boardUpdate.AgentInstructions;
        board.Phase = boardUpdate.Phase;
        board.AutonomousEnabled = boardUpdate.AutonomousEnabled;
        board.Notes = boardUpdate.Notes;
        board.Connections = boardUpdate.Connections;
        return true;
    }

    public bool Delete(Guid id) => _boards.RemoveAll(b => b.Id == id) > 0;
}
