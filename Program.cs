using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;

class Program
{
    static Task<int> Main()
    {
        return Deployment.RunAsync(() =>
        {

            // Create an Azure Resource Group
            var resourceGroup = new ResourceGroup($"{Deployment.Instance.ProjectName}-{Deployment.Instance.StackName}-");

            // Create an Azure Storage Account
            var storageAccount = new Account("storage", new AccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
                EnableHttpsTrafficOnly = true,
            });

            // Export the connection string for the storage account
            return new Dictionary<string, object?>
            {
                { "connectionString", storageAccount.PrimaryConnectionString },
            };
        });
    }
}
