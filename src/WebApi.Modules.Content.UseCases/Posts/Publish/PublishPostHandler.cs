using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;

/// <summary>
/// Desk publishes now: the post's contents waiting for the Desk are accepted first, then every required content must be
/// accepted; the published document is assembled from the accepted revisions, which the post pins. The post's kind must be
/// a published DynamicFields root post field: its URL prefix is kept with the live version. DynamicFields first checks the
/// whole post (<see cref="ContentValidation.CheckPostAsync"/>).
/// </summary>
public sealed class PublishPostHandler(PostChanges changes, PostAccess access, DeskPublishing desk, PostKindRules kinds,
    ContentValidation validation) : ICommandHandler<PublishPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(PublishPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            var kind = await kinds.ForPublishingAsync(post, ct); // D3: no published root field, no URL, no publication
            if (!kind.IsSuccess) return kind.AsResult();
            if (DeskPublishing.MayPublish(post)) // otherwise the aggregate refuses the state (409) before anything is checked
            {
                var valid = await validation.CheckPostAsync(post, "publish", ct);
                if (!valid.IsSuccess) return valid;
            }
            post.Publish(access.Me, access.Now, await desk.PlanAsync(post, ct), kind.Value.UrlPrefix);
            return Result.Success();
        }, ct);
}
