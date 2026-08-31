using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Route("api/security")]
public sealed class SecurityController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        antiforgery.GetAndStoreTokens(HttpContext);
        return NoContent();
    }
}
