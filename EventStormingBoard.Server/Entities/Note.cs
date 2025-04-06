namespace EventStormingBoard.Server.Entities
{
    public class Note
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? Color { get; set; }
    }
}
