using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Tasks
{
    public class ListModel : MicrosoftPageModel
    {
        public TodoTaskList List;

        GraphServiceClient Graph;

        public ListModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet(string list)
        {
            List = await Graph.Me.Todo.Lists[list].Request().GetAsync();
        }
    }
}
