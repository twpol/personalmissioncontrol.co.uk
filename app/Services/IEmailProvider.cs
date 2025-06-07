using System.Collections.Generic;
using app.Models;

namespace app.Services
{
    public interface IEmailDataProvider : IDataProvider
    {
        public IAsyncEnumerable<EmailFolderModel> GetEmailFolders();
    }
}
