using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class PastedEvent : BoardEvent
    {
        public List<NoteDto> Notes { get; set; } = new List<NoteDto>();
        public List<ConnectionDto> Connections { get; set; } = new List<ConnectionDto>();
    }
}
