using System;
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
        public string Classes => "task " + (IsCompleted ? "task--completed text-black-50" : "task--uncompleted") + " " + (IsImportant ? "task--important" : "task--unimportant");

        [JsonIgnore]
        public string TitleHtml => Markdown.ToHtml(Title, MarkdownPipeline);

        [JsonIgnore]
        public bool IsCompleted => Completed.HasValue;

        [JsonIgnore]
        public string SortKey => $"{(IsCompleted ? 2 : 1)}{(IsImportant ? 1 : 2)} {Title}";

        [JsonIgnore]
        public string? NestedTag => Title.StartsWith("#") ? Title.Split(' ')[0][1..] : null;

        [JsonIgnore]
        public string NestedUrl => $"/Tasks/Children?hashtag={NestedTag}&layout=nested";
    }
}
