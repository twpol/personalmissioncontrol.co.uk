using System.Collections.Generic;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Email
{
    public class IndexModel : MicrosoftPageModel
    {
        public IEnumerable<DisplayFolder> Folders = null!;

        readonly GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            Folders = await GetMailFolders(await Graph.Me.MailFolders.Request().GetAsync());
        }

        async Task<IEnumerable<DisplayFolder>> GetMailFolders(IEnumerable<MailFolder> folders)
        {
            var displayFolders = new List<DisplayFolder>();
            foreach (var folder in folders)
            {
                displayFolders.Add(new DisplayFolder(folder.Id, folder.DisplayName, await GetMailFolders(folder)));
            }
            return displayFolders;
        }

        async Task<IEnumerable<DisplayFolder>> GetMailFolders(MailFolder folder)
        {
            if (folder.ChildFolderCount == 0) return System.Array.Empty<DisplayFolder>();
            return await GetMailFolders(await Graph.Me.MailFolders[folder.Id].ChildFolders.Request().GetAsync());
        }

        public record DisplayFolder(string Id, string Name, IEnumerable<DisplayFolder> Children);
    }
}
