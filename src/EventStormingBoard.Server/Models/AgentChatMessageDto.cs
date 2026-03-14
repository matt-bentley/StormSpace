namespace EventStormingBoard.Server.Models
{
    public static class AgentMessageKinds
    {
        public const string User = "user";
        public const string Plan = "plan";
        public const string Handoff = "handoff";
        public const string Response = "response";
    }

    public sealed class AgentChatMessageDto
    {
        public required string Role { get; set; }
        public string? UserName { get; set; }
        public string? AgentId { get; set; }
        public string? AgentName { get; set; }
        public string? MessageKind { get; set; }
        public string? ExecutionId { get; set; }
        public string? Content { get; set; }
        public List<AgentToolCallDto>? ToolCalls { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class AgentToolCallDto
    {
        public required string Name { get; set; }
        public required string Arguments { get; set; }
    }

    public sealed class AgentToolCallStartedDto
    {
        public required string BoardId { get; set; }
        public required string ExecutionId { get; set; }
        public required string AgentId { get; set; }
        public required string AgentName { get; set; }
        public required string ToolName { get; set; }
        public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed record AgentExecutionScope(Guid BoardId, string ExecutionId, string AgentId, string AgentName);
}
