using Microsoft.AspNetCore.Mvc;
using iVault.Api.Models;
using iVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace iVault.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecordDefinitionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RecordDefinitionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/RecordDefinitions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecordDefinition>>> GetDefinitions()
        {
            return await _context.RecordDefinitions.ToListAsync();
        }

        // GET: api/RecordDefinitions/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<RecordDefinition>> GetDefinition(Guid id)
        {
            var definition = await _context.RecordDefinitions.FindAsync(id);

            if (definition == null) return NotFound();

            return definition;
        }
    }
}