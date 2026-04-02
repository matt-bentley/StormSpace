using System.ComponentModel;

namespace EventStormingBoard.Server.Agents
{
    public sealed class DelegationPlugin
    {
        private readonly Func<SpecialistAgent, string, Task<string>> _proposalHandler;
        private readonly Func<SpecialistAgent?, string, Task<string>> _reviewHandler;
        private readonly Func<string, Task<string>> _organisationHandler;

        public DelegationPlugin(
            Func<SpecialistAgent, string, Task<string>> proposalHandler,
            Func<SpecialistAgent?, string, Task<string>> reviewHandler,
            Func<string, Task<string>> organisationHandler)
        {
            _proposalHandler = proposalHandler;
            _reviewHandler = reviewHandler;
            _organisationHandler = organisationHandler;
        }

        [Description(
            "Request a specialist agent to propose and execute board changes. " +
            "The specialist will propose changes and the Wall Scribe will execute them on the board. " +
            "Use this whenever the session requires creating, moving, editing, or connecting notes.")]
        public Task<string> RequestSpecialistProposal(
            [Description(
                "Which specialist to consult: " +
                "EventExplorer (brainstorm events during SetContext/IdentifyEvents), " +
                "TriggerMapper (commands/policies during AddCommandsAndPolicies), " +
                "or DomainDesigner (aggregates/boundaries during DefineAggregates/BreakItDown)")]
            SpecialistAgent specialist,
            [Description(
                "Detailed instructions for the specialist. Include: what to propose, " +
                "relevant domain context, the current board state summary, " +
                "positioning hints, and any user preferences from the conversation.")]
            string instructions)
        {
            return _proposalHandler(specialist, instructions);
        }

        [Description(
            "Request an advisory review of the current board state. " +
            "A specialist will analyse the board and provide feedback on quality, correctness, " +
            "and suggestions for improvement. This is non-blocking — no board changes are made. " +
            "Use when the user asks for a review, or when a phase has been running for a while.")]
        public Task<string> RequestBoardReview(
            [Description(
                "Optional: which specialist should review. " +
                "EventExplorer (review events), TriggerMapper (review commands/policies), " +
                "DomainDesigner (review aggregates/boundaries). " +
                "If null, the most appropriate specialist for the current phase is chosen automatically.")]
            SpecialistAgent? specialist,
            [Description(
                "Instructions for the review: what aspects to focus on, " +
                "any specific concerns the user raised, or areas that need attention.")]
            string instructions)
        {
            return _reviewHandler(specialist, instructions);
        }

        [Description(
            "Request the Organiser agent to tidy and reorganise the board layout. " +
            "The Organiser moves existing notes to match positioning rules and can add Concern notes for layout ambiguities. " +
            "Use when the user asks to organise, tidy, or rearrange the board, or when notes look cramped or misaligned.")]
        public Task<string> RequestBoardOrganisation(
            [Description(
                "Instructions for the Organiser: what to focus on, " +
                "any specific areas that need attention, or user preferences about layout.")]
            string instructions)
        {
            return _organisationHandler(instructions);
        }
    }
}
