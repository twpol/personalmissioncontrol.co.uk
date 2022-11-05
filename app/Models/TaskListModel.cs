using System;

namespace app.Models
{
    public enum TaskListSpecial
    {
        None,
        Default,
        Emails,
    }

    public record TaskListModel(string Id, string Emoji, string Name, TaskListSpecial Special)
    {
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