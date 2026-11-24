namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;

/// <summary>
/// Desk publishes now: the post's contents waiting for the Desk are accepted first, then every required content must be
/// accepted; the published document is assembled from the accepted revisions, which the post pins.
/// </summary>
public sealed class PublishPostHandler(PostChanges changes, PostAccess access, DeskPublishing desk) : ICommandHandler<PublishPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(PublishPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            post.Publish(access.Me, access.Now, await desk.PlanAsync(post, ct));
            return Result.Success();
        }, ct);
}
