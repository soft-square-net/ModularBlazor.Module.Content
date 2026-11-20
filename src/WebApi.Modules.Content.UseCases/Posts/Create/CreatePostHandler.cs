using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

public sealed class CreatePostHandler(IReadRepository<Category> categories, IRepository<Post> posts, PostAccess access, PostDetails details)
    : ICommandHandler<CreatePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(CreatePostCommand command, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null) return Result<PostDetail>.Conflict($"Unknown category '{command.CategoryId}'.");

        var scopes = new[] { AccessScope.Press(category.PressId), AccessScope.Category(category.Id) };
        var allowed = await access.RequireAsync(ContentPermissions.Author, scopes, "write in this category", ct);
        if (!allowed.IsSuccess) return allowed.As<PostDetail>();

        var post = Post.Create(access.Me, category, command.Title, command.Body, access.Now, command.PostKind, command.Document);
        await posts.AddAsync(post, ct);
        return await details.GetAsync(post.Id, ct);
    }
}
