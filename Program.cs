using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Cdn;
using Pulumi.Azure.Cdn.Inputs;
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
            var storageAccount = new Account("pmc", new AccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
                EnableHttpsTrafficOnly = true,
            });

            // Create an Azure Storage Container
            var storageContainer = new Container("pmc", new ContainerArgs
            {
                StorageAccountName = storageAccount.Name,
                ContainerAccessType = "private",
            });

            // Create an Azure Storage Blob
            var appBlob = new Blob("pmc", new BlobArgs
            {
                StorageAccountName = storageAccount.Name,
                StorageContainerName = storageContainer.Name,
                Type = "Block",
                Source = new FileArchive("./app"),
            });
            var appBlobUrl = SharedAccessSignature.SignedBlobReadUrl(appBlob, storageAccount);

            // Create an Azure App Service Plan
            var appPlan = new Plan("pmc", new PlanArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = "Windows",
                Sku = new PlanSkuArgs
                {
                    Tier = "Free",
                    Size = "F1",
                },
            });

            // Create an Azure App Insight
            var appInsight = new Insights("pmc", new InsightsArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ApplicationType = "other",
            });

            // Create an Azure App Service
            var appService = new AppService("pmc", new AppServiceArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AppServicePlanId = appPlan.Id,
                AppSettings = new InputMap<string>
                {
                    { "WEBSITE_RUN_FROM_ZIP", appBlobUrl },
                    { "APPINSIGHTS_INSTRUMENTATIONKEY", appInsight.InstrumentationKey },
                },
                HttpsOnly = true,
            });

            // Create an Azure CDN Profile
            var cdnProfile = new Profile("pmc", new ProfileArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "West Europe",
                Sku = "Standard_Microsoft",
            });

            // Create an Azure CDN Endpoint
            var cdnEndpoint = new Endpoint("pmc", new EndpointArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "West Europe",
                ProfileName = cdnProfile.Name,
                IsHttpAllowed = false,
                IsHttpsAllowed = true,
                OriginHostHeader = appService.DefaultSiteHostname,
                Origins =
                {
                    new EndpointOriginArgs
                    {
                        Name = "pmc",
                        HostName = appService.DefaultSiteHostname,
                    },
                },
            });

            // NOTE: Manually add custom domain and enable CDN managed TLS

            // Export the connection string for the storage account
            return new Dictionary<string, object?>
            {
                { "connectionString", storageAccount.PrimaryConnectionString },
                { "app-endpoint", Output.Format($"https://{appService.DefaultSiteHostname}") },
                { "cdn-endpoint", Output.Format($"https://{cdnEndpoint.HostName}") },
            };
        });
    }
}
