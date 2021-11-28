using System.Linq;
using app.Auth;

namespace app.Pages.Microsoft
{
    public class IndexModel : MicrosoftPageModel
    {
        public string UserName { get; set; }
        public string UserEmail { get; set; }

        public IndexModel()
        {
        }

        public void OnGet()
        {
            UserName = HttpContext.User.Claims.FirstOrDefault(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
            UserEmail = HttpContext.User.Claims.FirstOrDefault(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        }
    }
}
