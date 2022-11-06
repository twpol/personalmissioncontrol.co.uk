using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft
{
    public class IndexModel : MicrosoftPageModel
    {
        public string UserName { get; set; } = null!;
        public string UserEmail { get; set; } = null!;

        GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            var user = await Graph.Me.Request().GetAsync();
            UserName = user.DisplayName;
            UserEmail = user.UserPrincipalName;
        }
    }
}
