using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Tasks
{
    public class IndexModel : MicrosoftPageModel
    {
        GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }
    }
}
