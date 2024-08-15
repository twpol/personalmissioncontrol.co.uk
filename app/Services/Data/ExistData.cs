using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace app.Services.Data
{
    public static class ExistDataExtensions
    {
        public static IServiceCollection AddExistData(this IServiceCollection services)
        {
            services.AddScoped<ExistData>();
            services.AddScoped<IHabitProvider, ExistData>(s => s.GetRequiredService<ExistData>());
            return services;
        }
    }

    public class ExistData : IHabitProvider
    {
        static readonly Regex HabitPrefix = new("^(?:[a-z0-9] )?habit (?<flags>(?:[0-9]+p[0-9]+|[0-9]+r|d[0-9-]+) )+(?<name>.*)$");
        static readonly TextInfo TextInfo = new CultureInfo("en-GB").TextInfo;

        readonly ILogger<ExistData> Logger;
        readonly HttpClient? Channel;
        readonly string AccountId;
        readonly IModelStore<HabitModel> Habits;

        public ExistData(ILogger<ExistData> logger, OAuthProvider provider, IModelStore<HabitModel> habits)
        {
            Logger = logger;
            provider.TryGet("Exist", out Channel, out AccountId);
            Habits = habits;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($".ctor({AccountId})");
        }

        public async IAsyncEnumerable<HabitModel> GetHabits()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"GetHabits({AccountId})");
            await foreach (var habit in Habits.GetCollectionAsync(AccountId, ""))
            {
                yield return habit;
            }
            // Do update in the background
            _ = UpdateHabits();
        }

        public async Task UpdateHabits()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"UpdateHabits({AccountId})");
            await Habits.UpdateCollectionAsync(AccountId, "", UpdateCollectionHabits);
        }

        async IAsyncEnumerable<HabitModel> UpdateCollectionHabits()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"UpdateCollectionHabits({AccountId})");
            if (Channel == null) yield break;
            var tags = await ExecutePages<ApiTag>("https://exist.io/api/2/attributes/?groups=custom&limit=100");
            foreach (var tag in tags.Select(tag => FromApi(tag)).Where(tag => tag != null).Cast<HabitModel>())
            {
                yield return tag;
            }
        }

        async Task<IList<T>> ExecutePages<T>(string initialEndpoint)
        {
            string? endpoint = initialEndpoint;
            var results = new List<T>();
            do
            {
                var data = await Execute<ApiPages<T>>(endpoint);
                results.AddRange(data.results);
                endpoint = data.next;
            } while (endpoint != null);
            return results;
        }

        async Task<T> Execute<T>(string endpoint)
        {
            if (Channel == null) throw new InvalidOperationException("Cannot execute without channel");
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            var response = await Channel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Execute({0}) = {1}", endpoint, response.StatusCode);
            response.EnsureSuccessStatusCode();
            var data = JsonSerializer.Deserialize<T>(response.Content.ReadAsStream()) ?? throw new InvalidDataException("Failed to parse response");
            return data;
        }

        HabitModel? FromApi(ApiTag tag)
        {
            var match = HabitPrefix.Match(tag.label);
            if (!match.Success) return null;
            return new HabitModel(AccountId, "", tag.name, TextInfo.ToTitleCase(match.Groups["name"].Value));
        }

        record ApiPages<T>(int count, string? next, string? previous, T[] results);
        record ApiTag(string name, string label, bool active);
    }
}
