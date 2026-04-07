using System.ComponentModel;

namespace EventStormingBoard.Server.Agents
{
    public sealed class DelegationPlugin
    {
        private readonly Func<string, string, Task<string>> _delegationHandler;
        private readonly Func<string?, string, Task<string>> _reviewHandler;
        private readonly Func<string, string, Task<string>> _questionHandler;

        public DelegationPlugin(
            Func<string, string, Task<string>> delegationHandler,
            Func<string?, string, Task<string>> reviewHandler,
            Func<string, string, Task<string>> questionHandler)
        {
            _delegationHandler = delegationHandler;
            _reviewHandler = reviewHandler;
            _questionHandler = questionHandler;
        }

        [Description(
            "Delegate a task to another agent by name. The agent will use its own tools to execute the task. " +
            "Use this whenever the session requires creating, moving, editing, or connecting notes, " +
            "or when specialist expertise is needed.")]
        public Task<string> DelegateToAgent(
            [Description(
                "The name of the agent to delegate to. " +
                "Available agents depend on the board configuration and current phase.")]
            string agentName,
            [Description(
                "Detailed instructions for the agent. Include: what to do, " +
                "relevant domain context, the current board state summary, " +
                "positioning hints, and any user preferences from the conversation.")]
            string instructions)
        {
            return _delegationHandler(agentName, instructions);
        }

        [Description(
            "Request an advisory review of the current board state. " +
            "An agent will analyse the board and provide feedback on quality, correctness, " +
            "and suggestions for improvement. This is non-blocking — no board changes are made. " +
            "Use when the user asks for a review, or when a phase has been running for a while.")]
        public Task<string> RequestBoardReview(
            [Description(
                "Optional: name of the agent that should review. " +
                "If null or empty, an appropriate agent for the current phase is chosen automatically.")]
            string? agentName,
            [Description(
                "Instructions for the review: what aspects to focus on, " +
                "any specific concerns the user raised, or areas that need attention.")]
            string instructions)
        {
            return _reviewHandler(agentName, instructions);
        }

        [Description(
            "Ask a question to another agent by name and get their answer back. " +
            "No board changes are made. Use this for domain clarification, " +
            "business rule questions, or when you need specialist knowledge from another agent.")]
        public Task<string> AskAgentQuestion(
            [Description(
                "The name of the agent to ask. " +
                "Available agents depend on the board configuration and your permissions.")]
            string agentName,
            [Description(
                "The question to ask the agent. Be specific and include " +
                "relevant context so the agent can give a useful answer.")]
            string question)
        {
            return _questionHandler(agentName, question);
        }
    }
}
