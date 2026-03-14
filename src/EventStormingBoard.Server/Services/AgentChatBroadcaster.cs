using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public interface IAgentChatBroadcaster
    {
        Task BroadcastUserMessageAsync(Guid boardId, string userName, string content, CancellationToken cancellationToken = default);
        Task BroadcastAgentMessageAsync(Guid boardId, AgentChatMessageDto message, CancellationToken cancellationToken = default);
        List<AgentChatMessageDto> GetHistory(Guid boardId);
        void ClearHistory(Guid boardId);
    }

    public sealed class AgentChatBroadcaster : IAgentChatBroadcaster
    {
        private readonly IHubContext<BoardsHub> _hubContext;
        private readonly ConcurrentDictionary<Guid, List<AgentChatMessageDto>> _displayHistories = new();

        public AgentChatBroadcaster(IHubContext<BoardsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task BroadcastUserMessageAsync(Guid boardId, string userName, string content, CancellationToken cancellationToken = default)
        {
            var message = new AgentChatMessageDto
            {
                Role = "user",
                UserName = userName,
                Content = content,
                MessageKind = AgentMessageKinds.User,
                Timestamp = DateTime.UtcNow
            };

            Append(boardId, message);
            await _hubContext.Clients.Group(boardId.ToString()).SendAsync("AgentUserMessage", message, cancellationToken);
        }

        public async Task BroadcastAgentMessageAsync(Guid boardId, AgentChatMessageDto message, CancellationToken cancellationToken = default)
        {
            if (message.Timestamp == default)
            {
                message.Timestamp = DateTime.UtcNow;
            }

            Append(boardId, message);
            await _hubContext.Clients.Group(boardId.ToString()).SendAsync("AgentResponse", message, cancellationToken);
        }

        public List<AgentChatMessageDto> GetHistory(Guid boardId)
        {
            if (_displayHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    return new List<AgentChatMessageDto>(history);
                }
            }

            return new List<AgentChatMessageDto>();
        }

        public void ClearHistory(Guid boardId)
        {
            _displayHistories.TryRemove(boardId, out _);
        }

        private void Append(Guid boardId, AgentChatMessageDto message)
        {
            var history = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());
            lock (history)
            {
                history.Add(message);
            }
        }
    }
}