using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using app.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace app.Services
{
    public static class BackgroundDataUpdateExtensions
    {
        public static IServiceCollection AddBackgroundDataUpdate(this IServiceCollection services) => services.AddHostedService<BackgroundDataUpdate>();
    }

    public class BackgroundDataUpdate : BackgroundService
    {
        readonly ILogger<BackgroundDataUpdate> Logger;
        readonly IModelStore<AccountModel> Accounts;
        readonly IServiceScopeFactory ServiceScopeFactory;

        public BackgroundDataUpdate(ILogger<BackgroundDataUpdate> logger, IModelStore<AccountModel> accounts, IServiceScopeFactory serviceScopeFactory)
        {
            Logger = logger;
            Accounts = accounts;
            ServiceScopeFactory = serviceScopeFactory;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor()");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                await Update();
            }
        }

        async Task Update()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Update()");
            await foreach (var account in Accounts.GetCollectionsAsync(null, null)) await Update(account);
        }

        async Task Update(AccountModel account)
        {
            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Update({AccountId})", account.AccountId);

            try
            {
                using var scope = ServiceScopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AuthenticationContext>().AddAccount(account);
                foreach (var provider in scope.ServiceProvider.GetRequiredService<IEnumerable<IDataProvider>>())
                {
                    await provider.UpdateData();
                }
            }
            catch (Exception error)
            {
                if (Logger.IsEnabled(LogLevel.Error)) Logger.LogError("Update({AccountId}) {Error}", account.AccountId, error);
            }

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Update({AccountId}) {Duration:F3} s", account.AccountId, (float)(stopTime - startTime) / Stopwatch.Frequency);
        }
    }
}
