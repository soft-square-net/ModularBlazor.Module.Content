using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;

public sealed class EditPostHandler(PostChanges changes, PostAccess access, IOptions<ContentOptions> options)
    : ICommandHandler<EditPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(EditPostCommand edit, CancellationToken ct) =>
        await changes.ChangeAsync(edit.Id, edit.Version, async post =>
        {
            var me = access.Me;
            var now = access.Now;
            var window = options.Value.OwnerEditWindow;
            if (post.OwnerId == me && post.OwnerMayEdit(now, window))
                post.EditByOwner(me, edit.Title, edit.Body, now, window, edit.Document);
            else if (post.State == PostState.InReview && post.AssigneeId == me)
            {
                var reviewer = await access.RequireAsync(ContentPermissions.Review, post.Scopes, "review in this category now (is your shift over?)", ct);
                if (!reviewer.IsSuccess) return reviewer;
                post.EditInReview(me, edit.Title, edit.Body, now, edit.Document);
            }
            else if (post.State is PostState.Approved or PostState.Scheduled or PostState.AwaitingDeskReapproval or PostState.Published)
            {
                var desk = await access.RequireAsync(ContentPermissions.Publish, post.Scopes, "edit as Desk in this category", ct);
                if (!desk.IsSuccess) return desk;
                post.EditByDesk(me, edit.Title, edit.Body, now, edit.Document);
            }
            else if (post.OwnerId == me)
                post.EditByOwner(me, edit.Title, edit.Body, now, window, edit.Document); // throws, and explains why not
            else
                return Result.Forbidden($"You cannot edit this Post while it is {post.State}.");
            return Result.Success();
        }, ct);
}
