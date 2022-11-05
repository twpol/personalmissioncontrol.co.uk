using System;

namespace app.Models
{
    public record TaskModel(string Id, string Title, bool IsImportant, DateTimeOffset? Completed)
    {
        public string Classes => "task " + (IsCompleted ? "task--completed text-black-50" : "task--uncompleted") + " " + (IsImportant ? "task--important" : "task--unimportant");
        public bool IsCompleted => Completed.HasValue;
        public string SortKey => $"{(IsCompleted ? 2 : 1)}{(IsImportant ? 1 : 2)} {Title}";
        public string NestedTag => Title.StartsWith("#") ? Title.Split(' ')[0].Substring(1) : null;
        public string NestedUrl => $"/Microsoft/Tasks/List/Children?hashtag={NestedTag}&layout=nested";
    }
}
