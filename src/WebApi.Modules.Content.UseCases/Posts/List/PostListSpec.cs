using System.Linq.Expressions;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.List;

/// <summary>The newest 200 Posts that match every filter, projected to <see cref="PostSummary"/>.</summary>
public sealed class PostListSpec : Specification<Post, PostSummary>
{
    /// <param name="filters">Combined with AND.</param>
    public PostListSpec(params Expression<Func<Post, bool>>[] filters)
    {
        foreach (var filter in filters) Query.Where(filter);
        Query.OrderByDescending(a => a.UpdatedAt).Take(200).AsNoTracking();
        Query.Select(a => new PostSummary(a.Id, a.PostKind, a.PressId, a.PrimaryCategoryId, a.Title, a.OwnerId, a.State, a.Version,
            a.AcceptedAt, a.AcceptedShiftId, a.AssigneeId,
            a.SubmittedAt, a.ScheduledFor, a.TransferredFromShift, a.UpdatedAt, a.UnitId));
    }
}
