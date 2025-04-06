namespace EventStormingBoard.Server.Entities
{
    public sealed class Board
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public List<Note> Notes { get; set; } = new();
        public List<Connection> Connections { get; set; } = new();
    }
}
