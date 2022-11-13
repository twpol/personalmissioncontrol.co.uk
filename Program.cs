using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.AzureAD;
using Pulumi.AzureAD.Inputs;
using Pulumi.AzureNative.Cdn;
using Pulumi.AzureNative.DocumentDB;
using Pulumi.AzureNative.Insights;
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
            var config = new Pulumi.Config();

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
                Source = new FileArchive("./app/bin/Debug/net6.0/publish"),
            });
            var appBlobUrl = SignedBlobReadUrl(appBlob, storageContainer, storageAccount, resourceGroup);

            // Create an Azure App Service Plan
            var appPlan = new AppServicePlan("pmc", new AppServicePlanArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = "App",
                Sku = new SkuDescriptionArgs
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

            // Create an Azure App Registration
            var appReg = new Application("pmc", new ApplicationArgs
            {
                DisplayName = "Personal Mission Control",
                Api = new ApplicationApiArgs()
                {
                    RequestedAccessTokenVersion = 2,
                },
                RequiredResourceAccesses = new ApplicationRequiredResourceAccessArgs[]
                {
                    new()
                    {
                        ResourceAppId = "00000003-0000-0000-c000-000000000000" /* https://graph.microsoft.com/ */,
                        ResourceAccesses = new ApplicationRequiredResourceAccessResourceAccessArgs[]
                        {
                            new()
                            {
                                Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" /* https://graph.microsoft.com/User.Read */,
                                Type = "Scope",
                            },
                            new()
                            {
                                Id = "87f447af-9fa4-4c32-9dfa-4a57a73d18ce" /* https://graph.microsoft.com/MailboxSettings.Read */,
                                Type = "Scope",
                            },
                            new()
                            {
                                Id = "570282fd-fa5c-430d-a7fd-fc8dc98a9dca" /* https://graph.microsoft.com/Mail.Read */,
                                Type = "Scope",
                            },
                            new()
                            {
                                Id = "7427e0e9-2fba-42fe-b0c0-848c9e6a8182" /* https://graph.microsoft.com/offline_access */,
                                Type = "Scope",
                            },
                        },
                    },
                },
                SignInAudience = "AzureADandPersonalMicrosoftAccount",
                Web = new ApplicationWebArgs()
                {
                    HomepageUrl = $"https://{config.Get("domain")}",
                    RedirectUris = new[]
                    {
                        "https://localhost/signin-microsoft",
                        $"https://{config.Get("domain")}/signin-microsoft",
                    },
                },
            });

            // Create an Azure App Registration Secret
            var appRegSecret = new ApplicationPassword("pmc", new ApplicationPasswordArgs()
            {
                ApplicationObjectId = appReg.Id,
                DisplayName = "pulumi",
                EndDateRelative = "2160h" /* 90 days */,
                RotateWhenChanged = new()
                {
                    { "CurrentDate", DateTimeOffset.Now.Date.ToString() }
                },
            });

            // Create an Azure Cosmos DB Account
            var cosmosAccount = new DatabaseAccount("pmc", new()
            {
                ResourceGroupName = resourceGroup.Name,
                DatabaseAccountOfferType = DatabaseAccountOfferType.Standard,
                Kind = DatabaseAccountKind.GlobalDocumentDB,
                Capabilities = new[]
                {
                    new Pulumi.AzureNative.DocumentDB.Inputs.CapabilityArgs
                    {
                        Name = "EnableServerless",
                    },
                },
                Locations = new[]
                {
                    new Pulumi.AzureNative.DocumentDB.Inputs.LocationArgs
                    {
                        LocationName = resourceGroup.Location,
                    },
                },
            });

            // Get Azure Cosmos DB Account Keys
            var cosmosAccountKeys = ListDatabaseAccountKeys.Invoke(new ListDatabaseAccountKeysInvokeArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = cosmosAccount.Name,
            });

            // Create an Azure App Service
            var appService = new WebApp("pmc", new WebAppArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ServerFarmId = appPlan.Id,
                SiteConfig = new SiteConfigArgs
                {
                    NetFrameworkVersion = "5",
                    FtpsState = FtpsState.Disabled,
                    AppSettings = new NameValuePairArgs[]
                    {
                        new() { Name = "WEBSITE_RUN_FROM_PACKAGE", Value = appBlobUrl },
                        new() { Name = "APPINSIGHTS_INSTRUMENTATIONKEY", Value = appInsight.InstrumentationKey },
                        new() { Name = "Authentication__Exist__ClientId", Value = config.Require("exist-client-id") },
                        new() { Name = "Authentication__Exist__ClientSecret", Value = config.RequireSecret("exist-client-secret") },
                        new() { Name = "Authentication__Microsoft__ClientId", Value = appReg.ApplicationId },
                        new() { Name = "Authentication__Microsoft__ClientSecret", Value = appRegSecret.Value },
                        new() { Name = "Instrumentation__Honeycomb__ApiKey", Value = config.RequireSecret("honeycomb-apikey") },
                        new() { Name = "Storage__Cosmos__Endpoint", Value = cosmosAccount.DocumentEndpoint },
                        new() { Name = "Storage__Cosmos__Key", Value = cosmosAccountKeys.Apply(k => k.PrimaryMasterKey) },
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

            // Create an Azure CDN Endpoint Custom Domain
            var cdnCustomDomain = new CustomDomain("pmc", new CustomDomainArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ProfileName = cdnProfile.Name,
                EndpointName = cdnEndpoint.Name,
                HostName = config.Require("domain"),
            });

            // NOTE: Manually enable HTTPS certificate provisioning

            // Export the endpoints
            return new Dictionary<string, object?>
            {
                { "app-endpoint", Output.Format($"https://{appService.DefaultHostName}") },
                { "cdn-endpoint", Output.Format($"https://{cdnEndpoint.HostName}") },
                { "custom-endpoint", Output.Format($"https://{config.Get("domain")}") },
            };
        });
    }

    static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account, ResourceGroup resourceGroup)
    {
        var serviceSasToken = ListStorageAccountServiceSAS.Invoke(new ListStorageAccountServiceSASInvokeArgs
        {
            AccountName = account.Name,
            Protocols = HttpProtocol.Https,
            SharedAccessStartTime = "2021-01-01",
            SharedAccessExpiryTime = "2030-01-01",
            Resource = SignedResource.C,
            ResourceGroupName = resourceGroup.Name,
            Permissions = Permissions.R,
            CanonicalizedResource = Output.Format($"/blob/{account.Name}/{container.Name}"),
            ContentType = "application/json",
            CacheControl = "max-age=5",
            ContentDisposition = "inline",
            ContentEncoding = "deflate",
        }).Apply(blobSAS => blobSAS.ServiceSasToken);

        // Add blob contents hash to force App Service to pick up updates
        return Output.Format($"https://{account.Name}.blob.core.windows.net/{container.Name}/{blob.Name}?{serviceSasToken}&src-hash={blob.ContentMd5}");
    }
}
