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

        [JsonIgnore]
        public DateTimeOffset EarliestDate => Completed != null && Completed < Created ? Completed.Value : Created;

        [JsonIgnore]
        public string Classes => "task " + (IsCompleted ? "task--completed text-black-50" : "task--uncompleted") + " " + (IsImportant ? "task--important" : "task--unimportant");

        [JsonIgnore]
        public string TitleHtml => Markdown.ToHtml(Title, MarkdownPipeline);

        [JsonIgnore]
        public bool IsCompleted => Completed.HasValue;

        [JsonIgnore]
        public string SortKey => $"{(IsCompleted ? 2 : 1)}{(IsImportant ? 1 : 2)} {Title}";

        [JsonIgnore]
        IEnumerable<string> TitleWords => Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        [JsonIgnore]
        public string? Tag => TitleWords.Take(1).Where(tag => tag.StartsWith("#")).FirstOrDefault();

        [JsonIgnore]
        public List<string> Tags => TitleWords.Skip(1).Where(tag => tag.StartsWith("#")).ToList();

        [JsonIgnore]
        public List<TaskModel> Children = new();
    }
}
