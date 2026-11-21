using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Translate;

/// <summary>
/// Who may translate a Post now: the people who may edit it (the same rules as <c>EditPostHandler</c>), so a translation
/// never reaches a Post its text is locked for. Translating does not move the Post through the workflow.
/// </summary>
public sealed class PostTranslators(PostAccess access, IOptions<ContentOptions> options)
{
    public string Me => access.Me;
    public DateTimeOffset Now => access.Now;

    public async Task<Result> MayTranslateAsync(Post post, CancellationToken ct)
    {
        var me = access.Me;
        if (post.OwnerId == me && post.OwnerMayEdit(access.Now, options.Value.OwnerEditWindow)) return Result.Success();
        if (post.State == PostState.InReview && post.AssigneeId == me)
            return await access.RequireAsync(ContentPermissions.Review, post.Scopes, "review in this category now (is your shift over?)", ct);
        if (post.State is PostState.Approved or PostState.Scheduled or PostState.AwaitingDeskReapproval or PostState.Published)
            return await access.RequireAsync(ContentPermissions.Publish, post.Scopes, "translate as Desk in this category", ct);
        return Result.Forbidden($"You cannot translate this Post while it is {post.State}.");
    }
}
