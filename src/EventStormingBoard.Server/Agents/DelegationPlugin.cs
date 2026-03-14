using System.ComponentModel;

namespace EventStormingBoard.Server.Agents
{
    public sealed class DelegationPlugin
    {
        private readonly Func<SpecialistAgent, string, Task<string>> _handler;

        public DelegationPlugin(Func<SpecialistAgent, string, Task<string>> handler)
        {
            _handler = handler;
        }

        [Description(
            "Request a specialist agent to propose and execute board changes. " +
            "The specialist will propose changes, a Challenger will review them, " +
            "and if approved, the Wall Scribe will execute them on the board. " +
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
            return _handler(specialist, instructions);
        }
    }
}
