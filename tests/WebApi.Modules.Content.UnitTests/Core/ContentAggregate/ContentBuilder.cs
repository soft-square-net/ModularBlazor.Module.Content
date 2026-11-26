using System.Reflection;
using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate;

/// <summary>Nodes, documents and contents for the shared-content tests.</summary>
public static class ContentBuilder
{
    public const string Section = "section-1";
    public static PipelineDefinition Pipeline => PipelineDefinition.Default;
    public static PipelineDefinition Default => PipelineDefinition.Default;

    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static PostNode Paragraph(string id, string text) =>
        new(id, PostKinds.Article, "block.paragraph", 1, Json($$"""{ "text": "{{text}}" }"""), Json("{}"), []);

    public static PostDocument Document(params PostNode[] children) =>
        new("post.news", 1, Json("""{ "headline": "Final day" }"""), Json("{}"), children);

    public static ContentItem Item(string text = "One", string creator = Core.PostAggregate.PostBuilder.Owner, string? origin = null) =>
        ContentItem.Create(creator, "daily", "politics", origin, Paragraph("", text), Pipeline.StartStage, TestClock.Start);

    public static PipelineStage Stage(ContentItem content) => Pipeline.Stage(content.Stage);

    /// <summary>A stored post's id (set by the database in the application).</summary>
    public static T WithId<T>(T entity, object id)
    {
        typeof(T).BaseType!.BaseType!.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }
}
