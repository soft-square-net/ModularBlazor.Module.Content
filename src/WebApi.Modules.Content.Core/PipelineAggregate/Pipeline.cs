using System.Text.Json;

namespace soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;

/// <summary>
/// The tenant's published content pipeline (one row; none means <see cref="PipelineDefinition.Default"/>). Each publication
/// bumps <see cref="Version"/>. Contents keep their stage key: a stage that a new definition drops leaves its contents
/// where they are until an action of the new definition moves them (none can, so an admin re-publishes or Elsa moves them).
/// </summary>
public sealed class Pipeline : AggregateRoot<int>, ITenantOwned
{
    private Pipeline() { } // EF Core

    public string TenantId { get; set; } = "";
    public string Key { get; private set; } = "";
    public int Version { get; private set; }
    public string DefinitionJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public PipelineDefinition Definition =>
        JsonSerializer.Deserialize<PipelineDefinition>(DefinitionJson, PipelineDefinition.Json) ?? PipelineDefinition.Default;

    public static Pipeline Create(PipelineDefinition definition, string actor, DateTimeOffset now)
    {
        var pipeline = new Pipeline();
        pipeline.Publish(definition, actor, now);
        return pipeline;
    }

    public void Publish(PipelineDefinition definition, string actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        Key = definition.Key;
        DefinitionJson = JsonSerializer.Serialize(definition, PipelineDefinition.Json);
        UpdatedAt = now;
        UpdatedBy = actor;
        Version++;
    }
}
