using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Clock;

/// <summary>
/// Hands unfinished reviews on when a shift ends. This module knows nothing of shifts or of the workflow's desk reviews:
/// it asks whether the assigned reviewer still holds the review right on the post (a shift's grants end with the shift).
/// A reviewer found without it for <see cref="NewsroomOptions.ReviewHandoverGrace"/> loses the assignment and the post goes
/// back to the review queue (Accepted). A desk review that is still deciding moves the post first, within the grace.
/// Rights held through a platform role are not seen for another user, so assignment asks for a grant too.
/// </summary>
public sealed class ReviewHandover(IReadRepository<Post> reads, IRepository<Post> posts, PostAccess access, IOptions<ContentOptions> options)
{
    /// <summary>Who the clock's changes are by, in the post's history and the change feed.</summary>
    public const string ClockActor = "content:clock";
    public const int BatchSize = 200;

    /// <summary>The next batch of posts in review, after this id.</summary>
    public async Task<IReadOnlyList<int>> InReviewAsync(int afterId, CancellationToken ct) =>
        await reads.ListAsync(new InReviewPostIdsSpec(afterId, BatchSize), ct);

    /// <summary>Checks one post's reviewer, and saves what changed.</summary>
    public async Task<ReviewerCheck> CheckAsync(int postId, CancellationToken ct)
    {
        var post = await posts.FirstOrDefaultAsync(new PostByIdSpec(postId), ct);
        if (post is not { State: PostState.InReview, AssigneeId: { } assignee }) return ReviewerCheck.Unchanged;
        var holds = await access.UserHasAsync(assignee, ContentPermissions.Review, post.Scopes, ct);
        var result = post.CheckReviewer(ClockActor, holds, access.Now, options.Value.Newsroom.ReviewHandoverGrace);
        if (result != ReviewerCheck.Unchanged) await posts.UpdateAsync(post, ct);
        return result;
    }
}
