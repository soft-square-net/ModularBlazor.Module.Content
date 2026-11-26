using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

/// <summary>A new draft; each child of its document becomes a shared content of the post (in the pipeline's start stage).</summary>
public sealed class CreatePostHandler(IReadRepository<Category> categories, IRepository<Post> posts, IContentStore store, PostDocumentSplit split,
    PostAccess access, PostDetails details, PostKindRules kinds)
    : ICommandHandler<CreatePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(CreatePostCommand command, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null) return Result<PostDetail>.Conflict($"Unknown category '{command.CategoryId}'.");

        var scopes = new[] { AccessScope.Press(category.PressId), AccessScope.Category(category.Id) };
        var allowed = await access.RequireAsync(ContentPermissions.Author, scopes, "write in this category", ct);
        if (!allowed.IsSuccess) return allowed.As<PostDetail>();

        var kind = await kinds.ForNewPostAsync(command.PostKind, ct);
        if (!kind.IsSuccess) return kind.As<PostDetail>();

        var publicId = Post.NewPublicId();
        var (document, contents, created) = await split.CreateAsync(access.Me, category.PressId, category.Id, publicId, command.Document, access.Now, ct);
        var post = Post.Create(access.Me, category, command.Title, command.Body, access.Now, kind.Value.Key, document, contents, publicId);
        foreach (var content in created) store.Add(content);
        await posts.AddAsync(post, ct); // one save: the post and its contents
        return await details.GetAsync(post.Id, ct);
    }
}
