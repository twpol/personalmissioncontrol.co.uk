using app.Services;
using Microsoft.AspNetCore.Mvc;

namespace app.Controllers;

[ApiController]
[Route("api/accounts")]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    readonly AuthenticationContext AuthenticationContext;

    public AccountsController(AuthenticationContext authenticationContext)
    {
        AuthenticationContext = authenticationContext;
    }

    [HttpGet("")]
    public IActionResult List()
    {
        return Ok(AuthenticationContext.AccountModels.Keys);
    }
}
