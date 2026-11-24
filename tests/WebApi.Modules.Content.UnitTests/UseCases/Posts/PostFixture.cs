using Ardalis.Specification;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>
/// The shared Post and content services over substituted platform contracts: posts through substituted repositories,
/// contents in an in-memory <see cref="FakeContentStore"/> (read through a repository that evaluates the specifications).
/// </summary>
public sealed class PostFixture
{
    public IAccessScopes Access { get; } = Substitute.For<IAccessScopes>();
    public IGrantStore Grants { get; } = Substitute.For<IGrantStore>();
    public IRepository<Post> Posts { get; } = Substitute.For<IRepository<Post>>();
    public IReadRepository<Post> Reader { get; } = Substitute.For<IReadRepository<Post>>();
    public IReadRepository<ContentItem> ContentReader { get; } = Substitute.For<IReadRepository<ContentItem>>();
    public FakeContentStore Store { get; } = new();
    public TestClock Clock { get; } = new();
    public IOptions<ContentOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new ContentOptions());

    public PostAccess PostAccess { get; }
    public PostDetails Details { get; }
    public PostChanges Changes { get; }
    public ContentPipelines Pipelines { get; }
    public ContentAccess ContentAccess { get; }
    public PostContents PostContents { get; }
    public PostEditors Editors { get; }
    public PostDocumentSplit Split { get; }
    public DeskPublishing Desk { get; }
    public PostContentEditing Editing { get; }
    public ContentDetails ContentDetails { get; }
    public ContentChanges ContentChanges { get; }

    public PostFixture(string userId)
    {
        Access.UserId.Returns(userId);
        PostAccess = new PostAccess(Access, Grants, Clock);
        Details = new PostDetails(Reader, ContentReader, PostAccess, Options);
        Changes = new PostChanges(Posts, Details);
        Pipelines = new ContentPipelines(Store);
        ContentAccess = new ContentAccess(PostAccess);
        PostContents = new PostContents(Store);
        Editors = new PostEditors(PostAccess, Options);
        Split = new PostDocumentSplit(Store, PostContents, Pipelines);
        Desk = new DeskPublishing(PostContents, Pipelines, ContentAccess);
        Editing = new PostContentEditing(Store, PostContents, Editors, ContentAccess);
        ContentDetails = new ContentDetails(ContentReader, ContentAccess, Pipelines);
        ContentChanges = new ContentChanges(Store, ContentAccess, ContentDetails);

        ContentReader.ListAsync(Arg.Any<ISpecification<ContentItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<ContentItem>>().Evaluate(Store.Contents).ToList());
        ContentReader.FirstOrDefaultAsync(Arg.Any<ISpecification<ContentItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<ContentItem>>().Evaluate(Store.Contents).FirstOrDefault());
    }

    /// <summary>Every single-Post read (by id, and the detail read after a change) returns this Post.</summary>
    public void Stored(Post post)
    {
        Posts.FirstOrDefaultAsync(Arg.Any<ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns(post);
        Reader.FirstOrDefaultAsync(Arg.Any<ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns(post);
        Store.Posts.Add(post);
    }

    public void Allow(string permission) =>
        Access.HasPermissionAsync(permission, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(true);

    /// <summary>Acts as another user from now on.</summary>
    public void SignIn(string userId) => Access.UserId.Returns(userId);
}
