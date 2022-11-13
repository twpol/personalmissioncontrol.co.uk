using System.Security.Claims;
using app.Auth;

namespace app.Pages.Exist
{
    public class IndexModel : ExistPageModel
    {
        public string UserID { get; set; } = null!;
        public string UserName { get; set; } = null!;

        public IndexModel()
        {
        }

        public void OnGet()
        {
            if (HttpContext.TryGetIdentity("Exist", out var identity))
            {
                UserID = identity.GetId();
                UserName = identity.GetName();
            }
        }
    }
}
