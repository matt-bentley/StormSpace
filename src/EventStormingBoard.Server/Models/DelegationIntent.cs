namespace EventStormingBoard.Server.Models
{
    public sealed class DelegationIntent
    {
        public required AgentType TargetAgent { get; init; }
        public string? Instructions { get; init; }
        public string? FocusArea { get; init; }
    }
}
