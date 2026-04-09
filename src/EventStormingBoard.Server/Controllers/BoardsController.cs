using EventStormingBoard.Server.Agents;
using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EventStormingBoard.Server.Controllers
{
    [ApiController]
    [Route("api/boards")]
    public class BoardsController : ControllerBase
    {
        private readonly IBoardsRepository _repository;
        private readonly IBoardEventLog _boardEventLog;
        private readonly IHubContext<BoardsHub> _hubContext;
        private readonly IAgentService _agentService;
        private readonly IAutonomousFacilitatorCoordinator _coordinator;
        private readonly IBoardPresenceService _boardPresenceService;

        public BoardsController(
            IBoardsRepository repository,
            IBoardEventLog boardEventLog,
            IHubContext<BoardsHub> hubContext,
            IAgentService agentService,
            IAutonomousFacilitatorCoordinator coordinator,
            IBoardPresenceService boardPresenceService)
        {
            _repository = repository;
            _boardEventLog = boardEventLog;
            _hubContext = hubContext;
            _agentService = agentService;
            _coordinator = coordinator;
            _boardPresenceService = boardPresenceService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<BoardSummaryDto>), StatusCodes.Status200OK)]
        public IActionResult Get()
        {
            var boards = _repository.GetAll()
                .Select(b => new BoardSummaryDto
                {
                    Id = b.Id,
                    Name = b.Name
                }).ToList();
            return Ok(boards);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BoardDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetById(Guid id)
        {
            var board = _repository.GetById(id);
            if (board == null)
            {
                return NotFound();
            }
            var boardDto = new BoardDto
            {
                Id = board.Id,
                Name = board.Name,
                Domain = board.Domain,
                SessionScope = board.SessionScope,
                Phase = board.Phase,
                AutonomousEnabled = board.AutonomousEnabled,
                Notes = board.Notes.Select(n => new NoteDto
                {
                    Id = n.Id,
                    Text = n.Text,
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width,
                    Height = n.Height,
                    Color = n.Color,
                    Type = n.Type
                }).ToList(),
                Connections = board.Connections.Select(c => new ConnectionDto
                {
                    FromNoteId = c.FromNoteId,
                    ToNoteId = c.ToNoteId
                }).ToList(),
                BoundedContexts = board.BoundedContexts.Select(bc => new BoundedContextDto
                {
                    Id = bc.Id,
                    Name = bc.Name,
                    X = bc.X,
                    Y = bc.Y,
                    Width = bc.Width,
                    Height = bc.Height,
                    Color = bc.Color
                }).ToList(),
                AgentConfigurations = board.AgentConfigurations.Select(MapAgentConfigurationDto).ToList()
            };
            return Ok(boardDto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(BoardDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Post([FromBody] BoardCreateDto boardCreate)
        {
            if (string.IsNullOrWhiteSpace(boardCreate.Name))
            {
                return BadRequest("Board name is required.");
            }

            var board = new Board
            {
                Id = Guid.NewGuid(),
                Name = boardCreate.Name.Trim(),
                Domain = NormalizeOptional(boardCreate.Domain),
                SessionScope = NormalizeOptional(boardCreate.SessionScope),
                Phase = boardCreate.Phase,
                AutonomousEnabled = boardCreate.AutonomousEnabled,
                Notes = new List<Note>(),
                Connections = new List<Connection>(),
                AgentConfigurations = DefaultAgentConfigurations.CreateDefaults()
            };
            _repository.Add(board);
            var boardDto = new BoardDto
            {
                Id = board.Id,
                Name = board.Name,
                Domain = board.Domain,
                SessionScope = board.SessionScope,
                Phase = board.Phase,
                AutonomousEnabled = board.AutonomousEnabled,
                Notes = new List<NoteDto>(),
                Connections = new List<ConnectionDto>(),
                BoundedContexts = new List<BoundedContextDto>(),
                AgentConfigurations = board.AgentConfigurations.Select(MapAgentConfigurationDto).ToList()
            };
            return CreatedAtAction(nameof(GetById), new { id = board.Id }, boardDto);
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [HttpGet("{id}/events")]
        [ProducesResponseType(typeof(List<BoardEventEntry>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetEvents(Guid id, [FromQuery] int count = 50)
        {
            var board = _repository.GetById(id);
            if (board == null)
            {
                return NotFound();
            }
            var events = _boardEventLog.GetRecent(id, count);
            return Ok(events);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Delete(Guid id)
        {
            var deleted = _repository.Delete(id);
            if (!deleted)
            {
                return NotFound();
            }

            // Disable autonomous turns first so the worker doesn't start new ones.
            _coordinator.SyncBoardSettings(id, enabled: false, stopReason: "deleted");

            // Clear all board-scoped singleton state.
            _agentService.ClearBoardData(id);
            _coordinator.ClearBoardState(id);
            _boardPresenceService.ClearBoard(id);
            _boardEventLog.ClearLog(id);

            return NoContent();
        }

        // ── Agent Configuration CRUD ────────────────────────────

        [HttpGet("{boardId}/agents")]
        [ProducesResponseType(typeof(List<AgentConfigurationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetAgents(Guid boardId)
        {
            var board = _repository.GetById(boardId);
            if (board == null) return NotFound();

            return Ok(board.AgentConfigurations.Select(MapAgentConfigurationDto).ToList());
        }

        [HttpPost("{boardId}/agents")]
        [ProducesResponseType(typeof(AgentConfigurationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult AddAgent(Guid boardId, [FromBody] AgentConfigurationCreateDto dto)
        {
            var board = _repository.GetById(boardId);
            if (board == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Agent name is required.");

            var config = new AgentConfiguration
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                IsFacilitator = false,
                SystemPrompt = dto.SystemPrompt,
                Icon = dto.Icon,
                Color = dto.Color,
                ActivePhases = dto.ActivePhases,
                AllowedTools = dto.AllowedTools,
                CanAskAgents = dto.CanAskAgents,
                Order = dto.Order,
                ModelType = dto.ModelType,
                Temperature = dto.Temperature,
                ReasoningEffort = dto.ReasoningEffort
            };

            board.AgentConfigurations.Add(config);
            _repository.Update(boardId, board);
            BroadcastAgentConfigurationsUpdated(boardId, board);

            return CreatedAtAction(nameof(GetAgents), new { boardId }, MapAgentConfigurationDto(config));
        }

        [HttpPut("{boardId}/agents/{agentId}")]
        [ProducesResponseType(typeof(AgentConfigurationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult UpdateAgent(Guid boardId, Guid agentId, [FromBody] AgentConfigurationUpdateDto dto)
        {
            var board = _repository.GetById(boardId);
            if (board == null) return NotFound();

            var config = board.AgentConfigurations.FirstOrDefault(a => a.Id == agentId);
            if (config == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Agent name is required.");

            config.Name = dto.Name.Trim();
            config.SystemPrompt = dto.SystemPrompt;
            config.Icon = dto.Icon;
            config.Color = dto.Color;
            config.ActivePhases = dto.ActivePhases;
            config.AllowedTools = dto.AllowedTools;
            config.CanAskAgents = dto.CanAskAgents;
            config.Order = dto.Order;
            config.ModelType = dto.ModelType;
            config.Temperature = dto.Temperature;
            config.ReasoningEffort = dto.ReasoningEffort;

            _repository.Update(boardId, board);
            BroadcastAgentConfigurationsUpdated(boardId, board);

            return Ok(MapAgentConfigurationDto(config));
        }

        [HttpDelete("{boardId}/agents/{agentId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteAgent(Guid boardId, Guid agentId)
        {
            var board = _repository.GetById(boardId);
            if (board == null) return NotFound();

            var config = board.AgentConfigurations.FirstOrDefault(a => a.Id == agentId);
            if (config == null) return NotFound();

            if (config.IsFacilitator)
                return BadRequest("The facilitator agent cannot be removed.");

            board.AgentConfigurations.Remove(config);
            _repository.Update(boardId, board);
            BroadcastAgentConfigurationsUpdated(boardId, board);

            return NoContent();
        }

        [HttpGet("{boardId}/agents/available-tools")]
        [ProducesResponseType(typeof(List<ToolDefinitionDto>), StatusCodes.Status200OK)]
        public IActionResult GetAvailableTools(Guid boardId)
        {
            var tools = BoardAgentFactory.GetAllToolDefinitions();
            return Ok(tools);
        }

        // ── Helpers ─────────────────────────────────────────────

        private static AgentConfigurationDto MapAgentConfigurationDto(AgentConfiguration config)
        {
            return new AgentConfigurationDto
            {
                Id = config.Id,
                Name = config.Name,
                IsFacilitator = config.IsFacilitator,
                SystemPrompt = config.SystemPrompt,
                Icon = config.Icon,
                Color = config.Color,
                ActivePhases = config.ActivePhases,
                AllowedTools = config.AllowedTools,
                CanAskAgents = config.CanAskAgents,
                Order = config.Order,
                ModelType = config.ModelType,
                Temperature = config.Temperature,
                ReasoningEffort = config.ReasoningEffort
            };
        }

        private void BroadcastAgentConfigurationsUpdated(Guid boardId, Board board)
        {
            var agents = board.AgentConfigurations.Select(MapAgentConfigurationDto).ToList();
            _hubContext.Clients.Group(boardId.ToString())
                .SendAsync("AgentConfigurationsUpdated", new { BoardId = boardId, Agents = agents });
        }
    }
}
