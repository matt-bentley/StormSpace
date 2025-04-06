namespace EventStormingBoard.Server.Events
{
    public sealed class UserLeftBoardEvent : BoardEvent
    {
        public required string ConnectionId { get; set; }
    }
}
