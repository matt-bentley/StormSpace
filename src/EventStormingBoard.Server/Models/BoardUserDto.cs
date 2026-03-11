namespace EventStormingBoard.Server.Models
{
    public sealed class BoardUserDto
    {
        public Guid BoardId { get; set; }
        public required string ConnectionId { get; set; }
        public required string UserName { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is BoardUserDto other)
            {
                return ConnectionId == other.ConnectionId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ConnectionId.GetHashCode();
        }
    }
}
