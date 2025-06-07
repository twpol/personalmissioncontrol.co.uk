using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Filters;
using app.Services;
using Microsoft.AspNetCore.Mvc;

namespace app.Controllers;

[ApiController]
[ApiModelFilter]
[Route("api/email")]
[Produces("application/json")]
public class EmailController : ControllerBase
{
    readonly IList<IEmailDataProvider> EmailProviders;

    public EmailController(IEnumerable<IEmailDataProvider> emailProviders)
    {
        EmailProviders = emailProviders.ToList();
    }

    [HttpGet("folders")]
    public async Task<IActionResult> GetEmailFolders()
    {
        return Ok(await EmailProviders.SelectManyAsync(provider => provider.GetEmailFolders()).ToListAsync());
    }
}
