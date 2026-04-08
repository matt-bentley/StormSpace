using EventStormingBoard.Server.Events;

namespace EventStormingBoard.Server.Services
{
    public interface IBoardEventPipeline
    {
        void ApplyAndLog(BoardEvent @event, string? userName);
    }

    public sealed class BoardEventPipeline : IBoardEventPipeline
    {
        private readonly IBoardStateService _boardStateService;
        private readonly IBoardEventLog _boardEventLog;

        public BoardEventPipeline(IBoardStateService boardStateService, IBoardEventLog boardEventLog)
        {
            _boardStateService = boardStateService;
            _boardEventLog = boardEventLog;
        }

        public void ApplyAndLog(BoardEvent @event, string? userName)
        {
            Apply(@event);
            _boardEventLog.Append(@event.BoardId, @event, userName);
        }

        private void Apply(BoardEvent @event)
        {
            switch (@event)
            {
                case BoardNameUpdatedEvent e:
                    _boardStateService.ApplyBoardNameUpdated(e);
                    break;
                case BoardContextUpdatedEvent e:
                    _boardStateService.ApplyBoardContextUpdated(e);
                    break;
                case NoteCreatedEvent e:
                    _boardStateService.ApplyNoteCreated(e);
                    break;
                case NotesMovedEvent e:
                    _boardStateService.ApplyNotesMoved(e);
                    break;
                case NoteResizedEvent e:
                    _boardStateService.ApplyNoteResized(e);
                    break;
                case NotesDeletedEvent e:
                    _boardStateService.ApplyNotesDeleted(e);
                    break;
                case ConnectionCreatedEvent e:
                    _boardStateService.ApplyConnectionCreated(e);
                    break;
                case NoteTextEditedEvent e:
                    _boardStateService.ApplyNoteTextEdited(e);
                    break;
                case PastedEvent e:
                    _boardStateService.ApplyPasted(e);
                    break;
                case BoundedContextCreatedEvent e:
                    _boardStateService.ApplyBoundedContextCreated(e);
                    break;
                case BoundedContextUpdatedEvent e:
                    _boardStateService.ApplyBoundedContextUpdated(e);
                    break;
                case BoundedContextDeletedEvent e:
                    _boardStateService.ApplyBoundedContextDeleted(e);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported board event type: {@event.GetType().Name}");
            }
        }
    }
}
