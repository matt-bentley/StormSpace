using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Entities
{
    public sealed class Board
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? Domain { get; set; }
        public string? SessionScope { get; set; }
        public EventStormingPhase? Phase { get; set; }
        public bool AutonomousEnabled { get; set; }
        public List<Note> Notes { get; set; } = new();
        public List<Connection> Connections { get; set; } = new();
        public List<AgentConfiguration> AgentConfigurations { get; set; } = new();
    }
}
