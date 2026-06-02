using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Middleware;

namespace MGold.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Consumes("application/json")]
[ServiceFilter(typeof(AuditLogActionFilter))]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
}
