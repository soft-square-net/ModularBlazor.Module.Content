using Microsoft.Extensions.Options;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// Who may move work between people: a post's accept, assign, transfer and return, and a content's assign. While
/// <see cref="NewsroomOptions.WorkflowOwnsAssignment"/> is off, each step keeps its own rule (the manual desk flow). While
/// it is on, only the workflow may: the caller needs <c>content.unit.assign</c> on the scopes, and that replaces the
/// step's own rule (the workflow's service client is on no shift). The aggregates' rules apply either way.
/// </summary>
public sealed class AssignmentRights(PostAccess access, IOptions<ContentOptions> options)
{
    public Task<Result> RequireAsync(IReadOnlyCollection<string> scopes, Func<Task<Result>> manual, CancellationToken ct) =>
        options.Value.Newsroom.WorkflowOwnsAssignment
            ? access.RequireAsync(ContentPermissions.UnitAssign, scopes, "do this: the newsroom workflow assigns the work here", ct)
            : manual();
}
