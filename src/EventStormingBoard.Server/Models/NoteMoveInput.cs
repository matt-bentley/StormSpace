using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class NoteMoveInput
    {
        [Description("The ID of the note to move")]
        public string NoteId { get; set; } = string.Empty;

        [Description("The target X coordinate")]
        public double X { get; set; }

        [Description("The target Y coordinate")]
        public double Y { get; set; }
    }
}
