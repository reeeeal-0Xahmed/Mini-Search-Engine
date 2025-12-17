using Microsoft.AspNetCore.Mvc;
using SearchEngine.Core;

namespace SearchEngine.Api.Controllers
{
    [ApiController]
    [Route("search")]
    public class SearchController : ControllerBase
    {
        private readonly QueryEngine _engine;

        public SearchController(QueryEngine engine)
        {
            _engine = engine;
        }

        [HttpGet]
        public IActionResult Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("Query is empty");

            var results = _engine.Search(q);
            return Ok(results);
        }
    }
}
