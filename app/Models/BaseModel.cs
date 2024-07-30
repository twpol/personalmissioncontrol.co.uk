using System;
using Newtonsoft.Json;

namespace app.Models
{
    public abstract record BaseModel(string AccountId, string ParentId, string ItemId)
    {
        [JsonProperty("id")]
        public string Id { get => $"{AccountId}~{ParentId}~{ItemId}"; }

        [JsonProperty("_ts")]
        public long TimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }
}
