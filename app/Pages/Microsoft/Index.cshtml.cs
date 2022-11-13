using System.Security.Claims;
using app.Auth;

namespace app.Pages.Microsoft
{
    public class IndexModel : MicrosoftPageModel
    {
        public string UserID { get; set; } = null!;
        public string UserName { get; set; } = null!;

        public IndexModel()
        {
        }

        public void OnGet()
        {
            if (HttpContext.TryGetIdentity("Microsoft", out var identity))
            {
                UserID = identity.GetId();
                UserName = identity.GetName();
            }
        }
    }
}
