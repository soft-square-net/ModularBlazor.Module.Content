using Ardalis.Specification;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>The shared Post services (PostAccess, PostDetails, PostChanges) over substituted platform contracts.</summary>
public sealed class PostFixture
{
    public IAccessScopes Access { get; } = Substitute.For<IAccessScopes>();
    public IGrantStore Grants { get; } = Substitute.For<IGrantStore>();
    public IRepository<Post> Posts { get; } = Substitute.For<IRepository<Post>>();
    public IReadRepository<Post> Reader { get; } = Substitute.For<IReadRepository<Post>>();
    public TestClock Clock { get; } = new();

    public PostAccess PostAccess { get; }
    public PostDetails Details { get; }
    public PostChanges Changes { get; }

    public PostFixture(string userId)
    {
        Access.UserId.Returns(userId);
        PostAccess = new PostAccess(Access, Grants, Clock);
        Details = new PostDetails(Reader, PostAccess, Options.Create(new ContentOptions()));
        Changes = new PostChanges(Posts, Details);
    }

    /// <summary>Every single-Post read (by id, and the detail read after a change) returns this Post.</summary>
    public void Stored(Post post)
    {
        Posts.FirstOrDefaultAsync(Arg.Any<ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns(post);
        Reader.FirstOrDefaultAsync(Arg.Any<ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns(post);
    }

    public void Allow(string permission) =>
        Access.HasPermissionAsync(permission, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(true);
}
