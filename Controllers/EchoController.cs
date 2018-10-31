using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace aspnetcoreaadpodidentity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EchoController : ControllerBase
    {
        private readonly Settings _settings;

        public EchoController(IOptions<Settings> settings)
        {
            _settings = settings.Value;
        }

        [HttpGet]
        public ActionResult<Settings> GetSettings()
        {
            return _settings;
        }
    }
}