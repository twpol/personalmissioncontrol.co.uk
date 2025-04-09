using Newtonsoft.Json;

namespace app.Models
{
    public enum TaskListSpecial
    {
        None,
        Default,
        Emails,
    }

    public record TaskListModel(string AccountId, string ParentId, string ItemId, string Emoji, string Name, TaskListSpecial Special) : BaseModel(AccountId, ParentId, ItemId)
    {
        [JsonIgnore]
        public string SortKey
        {
            get
            {
                if (Special == TaskListSpecial.Default) return "01 ";
                if (Special == TaskListSpecial.Emails) return "02 ";
                return "99 " + Name;
            }
        }
    }
}