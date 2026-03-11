namespace EventStormingBoard.Server.Events
{
    public sealed class BoardContextUpdatedEvent : BoardEvent
    {
        public string? NewDomain { get; set; }
        public string? OldDomain { get; set; }
        public string? NewSessionScope { get; set; }
        public string? OldSessionScope { get; set; }
        public string? NewAgentInstructions { get; set; }
        public string? OldAgentInstructions { get; set; }
    }
}
