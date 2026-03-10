using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EventStormingBoard.Server.Controllers
{
    [ApiController]
    [Route("api/boards")]
    public class BoardsController : ControllerBase
    {
        private readonly IBoardsRepository _repository;

        public BoardsController(IBoardsRepository repository)
        {
            _repository = repository;
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
                }).ToList()
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
                Notes = new List<Note>(),
                Connections = new List<Connection>()
            };
            _repository.Add(board);
            var boardDto = new BoardDto
            {
                Id = board.Id,
                Name = board.Name,
                Notes = new List<NoteDto>(),
                Connections = new List<ConnectionDto>()
            };
            return CreatedAtAction(nameof(GetById), new { id = board.Id }, boardDto);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Put(Guid id, [FromBody] BoardUpdateDto boardUpdate)
        {
            var board = _repository.GetById(id);
            if (board == null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(boardUpdate.Name))
            {
                return BadRequest("Board name is required.");
            }
            board.Name = boardUpdate.Name.Trim();
            board.Notes = boardUpdate.Notes.Select(n => new Note
            {
                Id = n.Id,
                Text = n.Text,
                X = n.X,
                Y = n.Y,
                Width = n.Width,
                Height = n.Height,
                Color = n.Color,
                Type = n.Type
            }).ToList();
            board.Connections = boardUpdate.Connections.Select(c => new Connection
            {
                FromNoteId = c.FromNoteId,
                ToNoteId = c.ToNoteId
            }).ToList();

            _repository.Update(id, board);
            return NoContent();
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

            return NoContent();
        }
    }
}
