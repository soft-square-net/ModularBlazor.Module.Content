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

        var others = new List<Category>();
        foreach (var id in (command.CategoryIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id) && id != category.Id).Distinct())
        {
            var other = await categories.GetByIdAsync(id, ct);
            if (other is null) return Result<PostDetail>.Conflict($"Unknown category '{id}'.");
            var author = await access.RequireAsync(ContentPermissions.Author, [AccessScope.Press(other.PressId), AccessScope.Category(other.Id)],
                $"write in category '{other.Id}'", ct);
            if (!author.IsSuccess) return author.As<PostDetail>();
            others.Add(other);
        }

        var kind = await kinds.ForNewPostAsync(command.PostKind, ct);
        if (!kind.IsSuccess) return kind.As<PostDetail>();

        var publicId = Post.NewPublicId();
        var (document, contents, created) = await split.CreateAsync(access.Me, category.PressId, category.Id, publicId, command.Document, access.Now, ct);
        var post = Post.Create(access.Me, category, command.Title, command.Body, access.Now, kind.Value.Key, document, contents, publicId, others);
        foreach (var content in created) store.Add(content);
        await posts.AddAsync(post, ct); // one save: the post and its contents
        return await details.GetAsync(post.Id, ct);
    }
}
