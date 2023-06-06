using Markdig;
using Markdig.Parsers;

namespace app
{
    static class MarkdownPipelineExtensions
    {
        public static MarkdownPipelineBuilder Clear(this MarkdownPipelineBuilder builder)
        {
            builder.BlockParsers.Clear();
            builder.InlineParsers.Clear();
            builder.Extensions.Clear();
            return builder;
        }

        public static MarkdownPipelineBuilder UseBlock<TExtension>(this MarkdownPipelineBuilder pipeline) where TExtension : BlockParser, new()
        {
            pipeline.BlockParsers.Add(new TExtension());
            return pipeline;
        }

        public static MarkdownPipelineBuilder UseInline<TExtension>(this MarkdownPipelineBuilder pipeline) where TExtension : InlineParser, new()
        {
            pipeline.InlineParsers.Add(new TExtension());
            return pipeline;
        }
    }
}
