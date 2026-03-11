using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class CreateNoteInput
    {
        [Description("The text label for the note")]
        public string Text { get; set; } = string.Empty;

        [Description("The note type: Event, Command, Aggregate, User, Policy, ReadModel, ExternalSystem, or Concern")]
        public NoteType Type { get; set; }

        [Description("X coordinate for placement on the canvas")]
        public double X { get; set; }

        [Description("Y coordinate for placement on the canvas")]
        public double Y { get; set; }
    }
}
