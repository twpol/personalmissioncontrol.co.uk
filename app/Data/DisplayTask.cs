using System;
using System.Collections.Generic;
using Microsoft.Graph;

namespace app.Data
{
    public record DisplayTask(string Id, string Title, TaskStatus Status, Importance Importance, DateTimeOffset? Completed)
    {
        static Dictionary<TaskStatus, string> StatusSort = new()
            {
                { TaskStatus.NotStarted, "1" },
                { TaskStatus.InProgress, "2" },
                { TaskStatus.Completed, "3" },
                { TaskStatus.WaitingOnOthers, "4" },
                { TaskStatus.Deferred, "5" },
            };

        static Dictionary<Importance, string> ImportanceSort = new()
            {
                { Importance.High, "1" },
                { Importance.Normal, "2" },
                { Importance.Low, "3" },
            };

        public string Classes => "task " + (IsCompleted ? "task--completed text-black-50" : "task--uncompleted") + " " + (IsImportant ? "task--important" : "task--unimportant");
        public bool IsCompleted => Status == TaskStatus.Completed;
        public bool IsImportant => Importance == Importance.High;
        public string SortKey => $"{StatusSort[Status]}{ImportanceSort[Importance]} {Title}";
        public string NestedTag => Title.StartsWith("#") ? Title.Split(' ')[0].Substring(1) : null;
        public string NestedUrl => $"/Microsoft/Tasks/List/Children?hashtag={NestedTag}&layout=nested";
    }
}
