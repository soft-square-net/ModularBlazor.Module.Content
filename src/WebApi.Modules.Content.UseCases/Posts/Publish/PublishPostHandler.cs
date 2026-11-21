namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;

public sealed class PublishPostHandler(PostChanges changes, PostAccess access) : ICommandHandler<PublishPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(PublishPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            post.Publish(access.Me, access.Now);
            return Result.Success();
        }, ct);
}
