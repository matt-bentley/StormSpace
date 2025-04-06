namespace EventStormingBoard.Server.Events
{
    public sealed class UserJoinedBoardEvent : BoardEvent
    {
        public required string UserName { get; set; }
        public required string ConnectionId { get; set; }
    }
}
