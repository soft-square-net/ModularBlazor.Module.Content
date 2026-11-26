using Microsoft.Extensions.Logging;
using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Events;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Validation;

/// <summary>
/// DynamicFields' content checks, over its API (<c>POST /api/dynamicfields/content/validate-node</c> and
/// <c>.../content/validate</c>) with the caller's token and tenant. Nothing is stored there.
/// </summary>
public interface IContentValidator
{
    /// <summary>
    /// One node's values and style against its field version (<c>validate-node</c>), without its place in a tree. Paths start at
    /// <c>/content</c> or <c>/style</c>. A field version DynamicFields does not publish is one problem at <c>/field</c>.
    /// Throws <see cref="ContentValidatorUnavailableException"/> when DynamicFields cannot answer.
    /// </summary>
    Task<IReadOnlyList<ContentProblem>> ValidateNodeAsync(PostNode node, CancellationToken ct);

    /// <summary>
    /// A post's whole tree (<c>validate</c>): the root and every nested node. Paths are JSON Pointers into the tree
    /// (<c>/children/1/content/caption</c>). Throws <see cref="ContentValidatorUnavailableException"/> when DynamicFields
    /// cannot answer.
    /// </summary>
    Task<IReadOnlyList<ContentProblem>> ValidateTreeAsync(PostDocument document, CancellationToken ct);
}

/// <summary>DynamicFields could not be reached, or refused the call: the content is not known to be valid, so the step waits.</summary>
public sealed class ContentValidatorUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The guards of wave 4 (C3):
/// <list type="bullet">
/// <item>a content action into a <see cref="StageKind.Review"/> or <see cref="StageKind.Accepted"/> stage first checks the
/// content (its node and every nested node, <c>validate-node</c>);</item>
/// <item>a post's approve, schedule and publish first check the whole post (<c>validate</c>; a legacy root, which is no
/// DynamicFields field, has its contents checked one by one).</item>
/// </list>
/// Problems refuse the step with 422 (<see cref="ProblemErrors"/>) and are recorded, in their own save, as the feed row
/// <c>content.validation-failed</c> and the integration event <c>content.validation-failed.v1</c>; nothing else changes.
/// DynamicFields unreachable: 503, and the step waits (it is never passed unchecked).
/// </summary>
public sealed class ContentValidation(IContentValidator validator, IContentStore store, ContentEventPublisher events, PostAccess access,
    ILoggerFactory loggers)
{
    public const int MaxProblems = 100;
    private readonly ILogger _log = loggers.CreateLogger("soft.square.net.WebApi.Modules.Content.UseCases.Validation");

    /// <summary>Whether taking this action needs the content check: it leads to a review or an accepted stage.</summary>
    public static bool Guards(PipelineDefinition pipeline, PipelineAction action) =>
        pipeline.FindStage(action.To)?.Kind is StageKind.Review or StageKind.Accepted;

    /// <summary>Checks a content before <paramref name="action"/>; success when it may go on.</summary>
    public async Task<Result> CheckContentAsync(ContentItem content, PipelineDefinition pipeline, PipelineAction action, CancellationToken ct)
    {
        if (!Guards(pipeline, action)) return Result.Success();
        var problems = await TryAsync(() => NodesAsync(content.Node, "", ct));
        if (problems is null) return Unavailable();
        if (problems.Count == 0) return Result.Success();
        return await FailAsync(new ContentValidationFailed(ContentValidationFailed.NodeCheck, action.Key, content.Id, null, content.Version,
            problems, access.Me, access.Now), content.Revision, content.Stage, null, content.UnitPath, ct);
    }

    /// <summary>Checks a post's working document before <paramref name="step"/> (approve, schedule, publish).</summary>
    public async Task<Result> CheckPostAsync(Post post, string step, CancellationToken ct)
    {
        var document = post.Working;
        var problems = await TryAsync(async () =>
        {
            if (document.SchemaId != PostDocument.LegacySchemaId) return WithNodes(await validator.ValidateTreeAsync(document, ct), document);
            var found = new List<ContentProblem>();
            var children = document.Children ?? [];
            for (var i = 0; i < children.Count; i++) found.AddRange(await NodesAsync(children[i], $"/children/{i}", ct));
            return found;
        });
        if (problems is null) return Unavailable();
        if (problems.Count == 0) return Result.Success();
        return await FailAsync(new ContentValidationFailed(ContentValidationFailed.PostCheck, step, null, post.Id, post.Version, problems,
            access.Me, access.Now), null, null, post.State.ToString(), post.UnitPath, ct);
    }

    /// <summary>A node and its nested nodes, each with <c>validate-node</c>; paths are prefixed with the node's place.</summary>
    private async Task<IReadOnlyList<ContentProblem>> NodesAsync(PostNode node, string path, CancellationToken ct)
    {
        var problems = (await validator.ValidateNodeAsync(node, ct))
            .Select(p => new ContentProblem(path + p.Path, p.Message, p.NodeId ?? Id(node.NodeId))).ToList();
        var children = node.Children ?? [];
        for (var i = 0; i < children.Count && problems.Count < MaxProblems; i++)
            problems.AddRange(await NodesAsync(children[i], $"{path}/children/{i}", ct));
        return problems;
    }

    /// <summary>Names the node each problem of a tree check is in (the deepest <c>/children/{i}</c> its path walks to).</summary>
    public static IReadOnlyList<ContentProblem> WithNodes(IReadOnlyList<ContentProblem> problems, PostDocument document) =>
        [.. problems.Select(p => p.NodeId is not null ? p : p with { NodeId = NodeAt(document, p.Path) })];

    private static string? NodeAt(PostDocument document, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<PostNode> children = document.Children ?? [];
        string? nodeId = null;
        for (var i = 0; i + 1 < segments.Length && segments[i] == "children"; i += 2)
        {
            if (!int.TryParse(segments[i + 1], out var index) || index < 0 || index >= children.Count) break;
            nodeId = Id(children[index].NodeId);
            children = children[index].Children ?? [];
        }
        return nodeId;
    }

    private async Task<IReadOnlyList<ContentProblem>?> TryAsync(Func<Task<IReadOnlyList<ContentProblem>>> check)
    {
        try
        {
            return await check();
        }
        catch (ContentValidatorUnavailableException e)
        {
            _log.LogWarning(e, "DynamicFields could not check the content: {Reason}", e.Message);
            _reason = e.Message;
            return null;
        }
    }

    private string? _reason;

    private Result Unavailable() =>
        Result.Unavailable($"DynamicFields could not check the content ({_reason}); nothing was changed, try again later.");

    private async Task<Result> FailAsync(ContentValidationFailed failure, int? revision, string? stage, string? postState, string? unitPath,
        CancellationToken ct)
    {
        var problems = failure.Problems.Take(MaxProblems).ToList();
        failure = failure with { Problems = problems };
        var summary = $"{failure.Action}: " + string.Join("; ", problems.Take(3).Select(p => $"{p.Path} {p.Message}"));
        store.Add(FeedEntry.From(new FeedEvent(FeedEventTypes.ValidationFailed, failure.PostId, failure.ContentId, failure.Version, revision, stage,
            postState, failure.By, failure.At, summary)));
        events.ValidationFailed(failure, unitPath);
        await store.SaveChangesAsync(ct); // only the record of the refusal: the step itself changed nothing yet
        return ProblemErrors.Invalid(problems);
    }

    private static string? Id(string? nodeId) => string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
}

/// <summary>
/// DynamicFields' problems as an Ardalis <c>Invalid</c> result, which the endpoints answer with 422 and a <c>problems</c>
/// list (not 400): each is a <see cref="ValidationError"/> whose <c>Identifier</c> is the path and whose <c>ErrorCode</c> is
/// <see cref="Code"/>, or <c>Code:nodeId</c> when the problem is in a node.
/// </summary>
public static class ProblemErrors
{
    public const string Code = "content.validation-failed";

    public static Result Invalid(IEnumerable<ContentProblem> problems) =>
        Result.Invalid([.. problems.Select(p => new ValidationError(p.Path, p.Message, p.NodeId is null ? Code : $"{Code}:{p.NodeId}", ValidationSeverity.Error))]);

    /// <summary>The problems of a result made by <see cref="Invalid"/>; empty for any other result.</summary>
    public static IReadOnlyList<ContentProblem> Of(Ardalis.Result.IResult result) =>
        [.. result.ValidationErrors.Where(e => e.ErrorCode == Code || (e.ErrorCode?.StartsWith(Code + ":", StringComparison.Ordinal) ?? false))
            .Select(e => new ContentProblem(e.Identifier ?? "", e.ErrorMessage, e.ErrorCode!.Length > Code.Length ? e.ErrorCode[(Code.Length + 1)..] : null))];
}
