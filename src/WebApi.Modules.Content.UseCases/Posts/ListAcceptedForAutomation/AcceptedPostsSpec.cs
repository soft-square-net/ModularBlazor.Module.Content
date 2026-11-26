using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.ListAcceptedForAutomation;

/// <summary>Accepted Posts waiting for a reviewer, oldest acceptance first.</summary>
public sealed class AcceptedPostsSpec : Specification<Post, PostSummary>
{
    public AcceptedPostsSpec() => Query.Where(a => a.State == PostState.Accepted && a.AcceptedAt != null)
        .OrderBy(a => a.AcceptedAt).Take(200).AsNoTracking()
        .Select(a => new PostSummary(a.Id, a.PostKind, a.PressId, a.PrimaryCategoryId, a.Title, a.OwnerId, a.State, a.Version,
            a.AcceptedAt, a.AcceptedShiftId, a.AssigneeId, a.SubmittedAt, a.ScheduledFor, a.TransferredFromShift, a.UpdatedAt, a.UnitId));
}
