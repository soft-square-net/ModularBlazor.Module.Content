using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases;

/// <summary>
/// DynamicFields' content checks in memory: everything is valid unless a node's field is in <see cref="InvalidFields"/>
/// (one problem at <c>/content/text</c>); <see cref="Down"/> fails every call.
/// </summary>
public sealed class FakeContentValidator : IContentValidator
{
    public HashSet<string> InvalidFields { get; } = new(StringComparer.Ordinal);
    public bool Down { get; set; }
    public List<string> NodeCalls { get; } = [];
    public List<PostDocument> TreeCalls { get; } = [];

    public Task<IReadOnlyList<ContentProblem>> ValidateNodeAsync(PostNode node, CancellationToken ct)
    {
        if (Down) throw new ContentValidatorUnavailableException("DynamicFields answered 503 Service Unavailable");
        NodeCalls.Add(node.NodeId);
        return Task.FromResult<IReadOnlyList<ContentProblem>>(
            InvalidFields.Contains(node.SchemaId) ? [new ContentProblem("/content/text", "Text is required.", null)] : []);
    }

    public Task<IReadOnlyList<ContentProblem>> ValidateTreeAsync(PostDocument document, CancellationToken ct)
    {
        if (Down) throw new ContentValidatorUnavailableException("DynamicFields answered 503 Service Unavailable");
        TreeCalls.Add(document);
        var problems = new List<ContentProblem>();
        if (InvalidFields.Contains(document.SchemaId)) problems.Add(new ContentProblem("/content/title", "Title is required.", null));
        for (var i = 0; i < document.Children.Count; i++)
            if (InvalidFields.Contains(document.Children[i].SchemaId))
                problems.Add(new ContentProblem($"/children/{i}/content/text", "Text is required.", null));
        return Task.FromResult<IReadOnlyList<ContentProblem>>(problems);
    }
}
