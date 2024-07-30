using Newtonsoft.Json;

namespace app.Models
{
    public record HabitModel(string AccountId, string ParentId, string ItemId, string Title) : BaseModel(AccountId, ParentId, ItemId)
    {
        [JsonIgnore]
        public string Classes => "habit";
    }
}
