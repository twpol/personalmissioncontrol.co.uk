using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Newtonsoft.Json;

namespace app.Models
{
    public record TaskModel(string AccountId, string ParentId, string ItemId, string Title, bool IsImportant, DateTimeOffset Created, DateTimeOffset? Completed) : BaseModel(AccountId, ParentId, ItemId)
    {
        static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .Clear()
            .UseBlock<ParagraphBlockParser>()
            .UseInline<LinkInlineParser>()
            .Build();

        // TODO: Remove these temporary properties when migration complete
        public string Name => Title;

        [JsonIgnore]
        public DateTimeOffset EarliestDate => Completed != null && Completed < Created ? Completed.Value : Created;

        [JsonIgnore]
        public string Classes => "task " + (IsCompleted ? "task--completed text-black-50" : "task--uncompleted") + " " + (IsImportant ? "task--important" : "task--unimportant");

        [JsonIgnore]
        public string NameHtml => Markdown.ToHtml(Name, MarkdownPipeline);

        [JsonIgnore]
        public bool IsCompleted => Completed.HasValue;

        [JsonIgnore]
        public string SortKey => $"{(IsCompleted ? 2 : 1)}{(IsImportant ? 1 : 2)} {Name}";

        [JsonIgnore]
        IEnumerable<string> NameWords => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        [JsonIgnore]
        public string? Tag => NameWords.Take(1).Where(tag => tag.StartsWith("#")).FirstOrDefault();

        [JsonIgnore]
        public List<string> Tags => NameWords.Skip(1).Where(tag => tag.StartsWith("#")).ToList();

        [JsonIgnore]
        public List<TaskModel> Children = new();
    }
}
