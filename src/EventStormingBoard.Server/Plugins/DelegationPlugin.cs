using EventStormingBoard.Server.Models;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace EventStormingBoard.Server.Plugins
{
    public sealed class DelegationPlugin
    {
        private readonly ConcurrentBag<DelegationIntent> _intents = new();

        public IReadOnlyList<DelegationIntent> DrainIntents()
        {
            var list = new List<DelegationIntent>();
            while (_intents.TryTake(out var intent))
            {
                list.Add(intent);
            }
            return list;
        }

        [Description("Request the Board Builder specialist to make changes on the board. Describe exactly what notes, connections, or layout changes should be created. The Board Builder will execute your instructions precisely.")]
        public string RequestBoardChanges(
            [Description("Detailed instructions for what the Board Builder should create or modify on the board")] string instructions)
        {
            _intents.Add(new DelegationIntent
            {
                TargetAgent = AgentType.BoardBuilder,
                Instructions = instructions
            });
            return "Board changes will be applied next.";
        }

        [Description("Request the Board Reviewer specialist to review the current board state. The reviewer will check flow rules, naming conventions, and quality, then add Concern notes where needed.")]
        public string RequestBoardReview(
            [Description("Optional area to focus the review on (e.g., 'payment flow', 'naming conventions')")] string? focusArea = null)
        {
            _intents.Add(new DelegationIntent
            {
                TargetAgent = AgentType.BoardReviewer,
                FocusArea = focusArea
            });
            return "Board review will be performed next.";
        }
    }
}
