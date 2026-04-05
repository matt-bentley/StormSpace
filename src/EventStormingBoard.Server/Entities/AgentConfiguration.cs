using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Entities
{
    public sealed class AgentConfiguration
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public bool IsFacilitator { get; set; }
        public required string SystemPrompt { get; set; }
        public string Icon { get; set; } = "smart_toy";
        public string Color { get; set; } = "#607d8b";
        public List<EventStormingPhase>? ActivePhases { get; set; }
        public List<string> AllowedTools { get; set; } = new();
        public List<string>? CanAskAgents { get; set; }
        public int Order { get; set; }
    }
}
