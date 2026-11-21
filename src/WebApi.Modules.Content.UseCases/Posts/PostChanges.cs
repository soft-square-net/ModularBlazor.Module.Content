using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// The shape of every workflow step: load the Post, check the caller's version, apply the change (rights check, then
/// the aggregate's method), save, and return the Post as the caller now sees it.
/// </summary>
public sealed class PostChanges(IRepository<Post> posts, PostDetails details)
{
    public async Task<Result<PostDetail>> ChangeAsync(int id, int version, Func<Post, Task<Result>> change, CancellationToken ct)
    {
        var post = await posts.FirstOrDefaultAsync(new PostByIdSpec(id), ct);
        if (post is null) return PostDetails.NotFound(id);
        if (post.Version != version)
            return Result<PostDetail>.Conflict($"Post {id} is at version {post.Version}, not {version}; reload it and try again.");
        var changed = await change(post);
        if (!changed.IsSuccess) return changed.As<PostDetail>();
        await posts.UpdateAsync(post, ct); // the version is a concurrency token: a racing change loses with 409
        return await details.GetAsync(id, ct);
    }
}
