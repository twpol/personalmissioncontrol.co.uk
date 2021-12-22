using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Email
{
    public class FolderModel : MicrosoftPageModel
    {
        public MailFolder Folder;

        GraphServiceClient Graph;

        public FolderModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet(string folder)
        {
            Folder = await Graph.Me.MailFolders[folder].Request().GetAsync();
        }
    }
}
