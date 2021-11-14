using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.AzureNative.Cdn;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Insights.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

class Program
{
    static Task<int> Main()
    {
        return Pulumi.Deployment.RunAsync(() =>
        {

            // Create an Azure Resource Group
            var resourceGroup = new ResourceGroup($"{Pulumi.Deployment.Instance.ProjectName}-{Pulumi.Deployment.Instance.StackName}-");

            // Create an Azure Storage Account
            var storageAccount = new StorageAccount("pmc", new StorageAccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = Pulumi.AzureNative.Storage.Kind.StorageV2,
                Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs
                {
                    Name = Pulumi.AzureNative.Storage.SkuName.Standard_LRS,
                },
            });

            // Create an Azure Storage Container
            var storageContainer = new BlobContainer("pmc", new BlobContainerArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = storageAccount.Name,
                PublicAccess = PublicAccess.None,
            });

            // Create an Azure Storage Blob
            var appBlob = new Blob("pmc", new BlobArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = storageAccount.Name,
                ContainerName = storageContainer.Name,
                Source = new FileArchive("./app/bin/Debug/net5.0/publish"),
            });
            var appBlobUrl = SignedBlobReadUrl(appBlob, storageContainer, storageAccount, resourceGroup);

            // Create an Azure App Service Plan
            var appPlan = new AppServicePlan("pmc", new AppServicePlanArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = "Windows",
                Sku = new Pulumi.AzureNative.Web.Inputs.SkuDescriptionArgs
                {
                    Name = "F1",
                    Tier = "Free",
                }
            });

            // Create an Azure App Insight
            var appInsight = new Component("pmc", new ComponentArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = "web",
                RetentionInDays = 730,
            });

            // Create an Azure App Service
            var appService = new WebApp("pmc", new WebAppArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ServerFarmId = appPlan.Id,
                SiteConfig = new SiteConfigArgs
                {
                    AppSettings =
                    {
                        new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_ZIP", Value = appBlobUrl },
                        new NameValuePairArgs { Name = "APPINSIGHTS_INSTRUMENTATIONKEY", Value = appInsight.InstrumentationKey },
                    },
                },
                HttpsOnly = true,
            });

            // Create an Azure CDN Profile
            var cdnProfile = new Profile("pmc", new ProfileArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "West Europe",
                Sku = new Pulumi.AzureNative.Cdn.Inputs.SkuArgs
                {
                    Name = Pulumi.AzureNative.Cdn.SkuName.Standard_Microsoft,
                },
            });

            // Create an Azure CDN Endpoint
            var cdnEndpoint = new Endpoint("pmc", new EndpointArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "West Europe",
                ProfileName = cdnProfile.Name,
                IsHttpAllowed = false,
                IsHttpsAllowed = true,
                OriginHostHeader = appService.DefaultHostName,
                Origins =
                {
                    new Pulumi.AzureNative.Cdn.Inputs.DeepCreatedOriginArgs
                    {
                        Name = "pmc",
                        HostName = appService.DefaultHostName,
                    },
                },
                QueryStringCachingBehavior = QueryStringCachingBehavior.UseQueryString,
            });

            // NOTE: Manually add custom domain and enable CDN managed TLS

            // Export the connection string for the storage account
            return new Dictionary<string, object?>
            {
                { "app-endpoint", Output.Format($"https://{appService.DefaultHostName}") },
                { "cdn-endpoint", Output.Format($"https://{cdnEndpoint.HostName}") },
            };
        });
    }

    static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account, ResourceGroup resourceGroup)
    {
        return Output.Tuple<string, string, string, string>(blob.Name, container.Name, account.Name, resourceGroup.Name).Apply(t =>
        {
            (string blobName, string containerName, string accountName, string resourceGroupName) = t;

            var blobSAS = ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
            {
                AccountName = accountName,
                Protocols = HttpProtocol.Https,
                SharedAccessStartTime = "2021-01-01",
                SharedAccessExpiryTime = "2030-01-01",
                Resource = SignedResource.C,
                ResourceGroupName = resourceGroupName,
                Permissions = Permissions.R,
                CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                ContentType = "application/json",
                CacheControl = "max-age=5",
                ContentDisposition = "inline",
                ContentEncoding = "deflate",
            });
            return Output.Format($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
        });
    }
}
