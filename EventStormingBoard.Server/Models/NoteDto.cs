namespace EventStormingBoard.Server.Models
{
    public sealed class NoteDto
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? Color { get; set; }
        public string? Type { get; set; } // event, command, aggregate, user, policy, readModel, externalSystem, concern
    }
}
