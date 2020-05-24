using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Cdn;
using Pulumi.Azure.Cdn.Inputs;
using Pulumi.Azure.Core;
using Pulumi.Azure.KeyVault;
using Pulumi.Azure.KeyVault.Inputs;
using Pulumi.Azure.Storage;

class Program
{
    static Task<int> Main()
    {
        return Deployment.RunAsync(() =>
        {
            var config = new Config();

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

            // Create an Azure Key Vault
            var vault = new KeyVault("pmc", new KeyVaultArgs
            {
                ResourceGroupName = resourceGroup.Name,
                SkuName = "standard",
                TenantId = config.Require("TenantId"),
                AccessPolicies = new[]
                {
                    new KeyVaultAccessPolicyArgs
                    {
                        TenantId = config.Require("TenantId"),
                        ObjectId = config.Require("PrincipalId"),
                        SecretPermissions = new []
                        {
                            "delete",
                            "get",
                            "list",
                            "set",
                        }
                    },
                }
            });

            // Create an Azure Secret for Microsoft OAuth
            var microsoftOAuthClientSecret = new Secret("pmcmsoauthcs", new SecretArgs
            {
                KeyVaultId = vault.Id,
                Value = config.RequireSecret("Authentication.Microsoft.ClientSecret"),
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
                    { "Authentication__Microsoft__ClientId", config.Require("Authentication.Microsoft.ClientId") },
                    { "Authentication__Microsoft__ClientSecret", GetAppSecret(vault, microsoftOAuthClientSecret) },
                },
                Identity = new AppServiceIdentityArgs
                {
                    Type = "SystemAssigned",
                },
                HttpsOnly = true,
            });

            // Grant App Service access to Key Vault secrets
            new AccessPolicy("pmc", new AccessPolicyArgs
            {
                KeyVaultId = vault.Id,
                TenantId = config.Require("TenantId"),
                ObjectId = appService.Identity.Apply(id => id.PrincipalId),
                SecretPermissions = new[]
                {
                    "get",
                },
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
                OriginHostHeader = appService.DefaultSiteHostname,
                Origins =
                {
                    new EndpointOriginArgs
                    {
                        Name = "pmc",
                        HostName = appService.DefaultSiteHostname,
                    },
                },
                QuerystringCachingBehaviour = "UseQueryString",
                DeliveryRules = new[]
                {
                    new EndpointDeliveryRuleArgs
                    {
                        Name = "dynamic",
                        Order = 1,
                        UrlFileExtensionConditions = new[]
                        {
                            new EndpointDeliveryRuleUrlFileExtensionConditionArgs
                            {
                                Operator = "Equal",
                                NegateCondition = true,
                                MatchValues = new[]
                                {
                                    "css",
                                    "js",
                                },
                            },
                        },
                        CacheExpirationAction = new EndpointDeliveryRuleCacheExpirationActionArgs
                        {
                            Behavior = "BypassCache",
                        },
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

    static Output<string> GetAppSecret(KeyVault vault, Secret secret)
    {
        return Output.Format($"@Microsoft.KeyVault(SecretUri={vault.VaultUri}secrets/{secret.Name}/{secret.Version})");
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
                "apac-jp-kaw-edge",
                "emea-fr-pra-edge",
                "latam-br-gru-edge",
                "us-fl-mia-edge",
            },
            Configuration = Output.Format($"<WebTest xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Items><Request Method=\"GET\" Version=\"1.1\" Url=\"{url}\" /></Items></WebTest>"),
        });
    }
}
