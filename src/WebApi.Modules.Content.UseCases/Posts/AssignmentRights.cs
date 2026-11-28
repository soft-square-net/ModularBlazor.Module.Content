using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// Who may move work between people: a post's accept, assign, transfer and return, and a content's assign. While
/// <see cref="NewsroomOptions.WorkflowOwnsAssignment"/> is off, each step keeps its own rule (the manual desk flow). While
/// it is on:
/// <list type="bullet">
/// <item>work of a post the newsroom workflow is working on (<see cref="Post.IsWorkflowManaged"/>) is the workflow's alone:
/// the caller needs <c>content.unit.assign</c> on the scopes, and that replaces the step's own rule;</item>
/// <item>other work is assigned by hand under the step's own rule, or by a holder of <c>content.unit.assign</c> (the
/// workflow may run before this module has heard of its project).</item>
/// </list>
/// The aggregates' rules apply either way.
/// </summary>
public sealed class AssignmentRights(PostAccess access, IOptions<ContentOptions> options)
{
    /// <summary>The setting: the newsroom workflow assigns the work of its posts.</summary>
    public bool WorkflowOwns => options.Value.Newsroom.WorkflowOwnsAssignment;

    public Task<Result> RequireAsync(Post post, Func<Task<Result>> manual, CancellationToken ct) =>
        RequireAsync(post.IsWorkflowManaged, post.Scopes, manual, ct);

    public async Task<Result> RequireAsync(bool workflowManaged, IReadOnlyCollection<string> scopes, Func<Task<Result>> manual,
        CancellationToken ct)
    {
        if (!WorkflowOwns) return await manual();
        if (workflowManaged)
            return await access.RequireAsync(ContentPermissions.UnitAssign, scopes, "do this: the newsroom workflow assigns the work here", ct);
        return await access.HasAsync(ContentPermissions.UnitAssign, scopes, ct) ? Result.Success() : await manual();
    }
}
