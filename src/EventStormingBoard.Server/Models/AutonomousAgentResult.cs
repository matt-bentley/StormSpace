namespace EventStormingBoard.Server.Models
{
    public enum AutonomousAgentStatus
    {
        Idle,
        Acted,
        Complete,
        Failed
    }

    public sealed class AutonomousAgentResult
    {
        public AutonomousAgentStatus Status { get; set; }
        public string TriggerReason { get; set; } = string.Empty;
        public string? VisibleMessage { get; set; }
        public List<AgentToolCallDto> ToolCalls { get; set; } = new();
        public string? StopReason { get; set; }
        public string? Diagnostics { get; set; }
    }

    public sealed class AutonomousFacilitatorStatusDto
    {
        public Guid BoardId { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsRunning { get; set; }
        public string State { get; set; } = "disabled";
        public string? LastResultStatus { get; set; }
        public string? StopReason { get; set; }
        public string? TriggerReason { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}