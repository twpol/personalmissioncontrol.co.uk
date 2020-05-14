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
                Source = new FileArchive("./app/bin/Debug/netcoreapp3.1/publish"),
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
                ApplicationType = "web",
                RetentionInDays = 730,
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

            // Create an Azure Availability Test for the app
            AddWebTest(resourceGroup, appInsight, "apphome", Output.Format($"https://{appService.DefaultSiteHostname}/"));

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

            // Create an Azure Availability Test for the CDN
            AddWebTest(resourceGroup, appInsight, "cdnhome", Output.Format($"https://{cdnEndpoint.HostName}/"));

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

    static void AddWebTest(ResourceGroup resourceGroup, Insights appInsight, string name, Output<string> url)
    {
        new WebTest($"pmc{name}", new WebTestArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ApplicationInsightsId = appInsight.Id,
            Kind = "ping",
            Enabled = true,
            GeoLocations = new[]
            {
                "apac-hk-hkn-azr",
                "apac-jp-kaw-edge",
                "apac-sg-sin-azr",
                "emea-au-syd-edge",
                "emea-ch-zrh-edge",
                "emea-fr-pra-edge",
                "emea-gb-db3-azr",
                "emea-nl-ams-azr",
                "emea-ru-msa-edge",
                "emea-se-sto-edge",
                "latam-br-gru-edge",
                "us-ca-sjc-azr",
                "us-fl-mia-edge",
                "us-il-ch1-azr",
                "us-tx-sn1-azr",
                "us-va-ash-azr",
            },
            Configuration = Output.Format($"<WebTest xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Items><Request Method=\"GET\" Version=\"1.1\" Url=\"{url}\" /></Items></WebTest>"),
        });
    }
}
