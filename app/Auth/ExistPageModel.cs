using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace app.Auth
{
    [Authorize("Exist")]
    public class ExistPageModel : PageModel
    {
    }
}
