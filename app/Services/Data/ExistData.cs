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
using Microsoft.Extensions.Logging;

namespace app.Services.Data
{
    public class ExistData
    {
        static readonly Regex HabitPrefix = new Regex("^(?:[a-z0-9] )?habit (?<flags>(?:[0-9]+p[0-9]+|[0-9]+r|d[0-9-]+) )+(?<name>.*)$");
        static readonly TextInfo TextInfo = new CultureInfo("en-GB").TextInfo;

        readonly ILogger<ExistData> Logger;
        readonly HttpClient? Channel;
        readonly IModelCache<IList<HabitModel>> HabitCache;

        public ExistData(ILogger<ExistData> logger, OAuthProvider provider, IModelCache<IList<HabitModel>> habitCache)
        {
            Logger = logger;
            Channel = provider.GetChannel("Exist");
            HabitCache = habitCache;
        }

        public async Task<IList<HabitModel>> GetHabits()
        {
            if (Channel == null) return new HabitModel[0];
            return await GetOrCreateAsync<IList<HabitModel>>(HabitCache, "habits", async () =>
            {
                var tags = await ExecutePages<ApiTag>("https://exist.io/api/2/attributes/?groups=custom&limit=100");
                return tags.Select(tag => FromApi(tag)).Where(tag => tag != null).Cast<HabitModel>().OrderBy(tag => tag.Title).ToList();
            });
        }

        async Task<T> GetOrCreateAsync<T>(IModelCache<T> cache, string subKey, Func<Task<T>> asyncFactory) where T : class
        {
            var key = $"{nameof(ExistData)}:{subKey}";
            return (await cache.GetAsync(key)) ?? (await SetAsync(cache, key, asyncFactory));
        }

        async Task<T> SetAsync<T>(IModelCache<T> cache, string key, Func<Task<T>> asyncFactory) where T : class
        {
            var obj = await asyncFactory();
            await cache.SetAsync(key, obj);
            return obj;
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
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Execute({0}) = {1}", endpoint, response.StatusCode);
            response.EnsureSuccessStatusCode();
            var data = JsonSerializer.Deserialize<T>(response.Content.ReadAsStream());
            if (data == null) throw new InvalidDataException("Failed to parse response");
            return data;
        }

        HabitModel? FromApi(ApiTag tag)
        {
            var match = HabitPrefix.Match(tag.label);
            if (!match.Success) return null;
            return new HabitModel(tag.name, TextInfo.ToTitleCase(match.Groups["name"].Value));
        }

        record ApiPages<T>(int count, string? next, string? previous, T[] results);
        record ApiTag(string name, string label, bool active);
    }
}
