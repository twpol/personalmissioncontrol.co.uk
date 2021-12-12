using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace app.Auth
{
    [Authorize("Microsoft")]
    public class MicrosoftPageModel : PageModel
    {
    }
}
