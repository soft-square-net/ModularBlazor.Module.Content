using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.PublishDue;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>A post's kind must be a published DynamicFields root post field to create it, and to publish it (D3).</summary>
public class PostKindHandlers
{
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Result<PostDetail>> CreateAsync(string kind)
    {
        var categories = Substitute.For<IReadRepository<Category>>();
        categories.GetByIdAsync("politics", Arg.Any<CancellationToken>()).Returns(PostBuilder.Category());
        _fixture.Allow(ContentPermissions.Author);
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var saved = WithId(call.Arg<Post>(), 1);
            _fixture.Stored(saved);
            return saved;
        });
        return new CreatePostHandler(categories, _fixture.Posts, _fixture.Store, _fixture.Split, _fixture.PostAccess, _fixture.Details, _fixture.Kinds, _fixture.Publisher)
            .Handle(new CreatePostCommand("politics", "A title", "", kind, null), Ct).AsTask();
    }

    /// <summary>An approved post of <paramref name="kind"/>, stored, with the Desk signed in.</summary>
    private Post Approved(string kind)
    {
        var now = _fixture.Clock.Now;
        var post = WithId(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", now, kind), 7);
        post.Submit(PostBuilder.Owner, now);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, now);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, PostBuilder.Shift, now);
        post.Approve(PostBuilder.Reviewer, now);
        _fixture.Stored(post);
        _fixture.SignIn(PostBuilder.Desk);
        _fixture.Allow(ContentPermissions.Publish);
        return post;
    }

    private Task<Result<PostDetail>> PublishAsync(Post post) =>
        new PublishPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Desk, _fixture.Kinds, _fixture.Validation)
            .Handle(new PublishPostCommand(post.Id, post.Version), Ct).AsTask();

    [Fact]
    public async Task CreatingAcceptsAnOldEnumNameAndReturnsTheKeyAndItsPrefix()
    {
        var result = await CreateAsync("News");

        result.IsSuccess.ShouldBeTrue();
        result.Value.PostKind.ShouldBe(PostKinds.News);
        result.Value.UrlPrefix.ShouldBe("news");
        result.Value.CanonicalPath.ShouldBeNull();
    }

    [Fact]
    public async Task CreatingAKindWithoutAPublishedRootFieldIsInvalid()
    {
        var result = await CreateAsync("post.video");

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.ShouldHaveSingleItem().ErrorMessage.ShouldContain("not a published DynamicFields root post field");
        await _fixture.Posts.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task CreatingIsUnavailableWhileDynamicFieldsIsDown()
    {
        _fixture.KindCatalog.Down = true;

        (await CreateAsync(PostKinds.Article)).Status.ShouldBe(ResultStatus.Unavailable);
    }

    [Fact]
    public async Task PublishingKeepsTheKindsPrefixAndGivesTheCanonicalPath()
    {
        var post = Approved(PostKinds.News);

        var result = await PublishAsync(post);

        result.IsSuccess.ShouldBeTrue();
        post.PublishedUrlPrefix.ShouldBe("news");
        result.Value.UrlPrefix.ShouldBe("news");
        result.Value.CanonicalPath.ShouldBe($"/news/{post.StablePublicId}");
    }

    [Fact]
    public async Task ALegacyKindWithoutAFieldCanBeEditedButNotPublishedOrScheduled()
    {
        var post = Approved(PostKinds.Video);

        var edited = await new EditPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Editors, _fixture.Split)
            .Handle(new EditPostCommand(post.Id, post.Version, "A new title", "", null), Ct);
        edited.IsSuccess.ShouldBeTrue();
        edited.Value.UrlPrefix.ShouldBeNull();

        var published = await PublishAsync(post);
        published.Status.ShouldBe(ResultStatus.Conflict);
        published.Errors.ShouldHaveSingleItem().ShouldContain("cannot be published yet");
        post.State.ShouldBe(PostState.AwaitingDeskReapproval);

        var scheduled = await new SchedulePostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Desk, _fixture.Kinds, _fixture.Validation)
            .Handle(new SchedulePostCommand(post.Id, post.Version, _fixture.Clock.Now.AddHours(1)), Ct);
        scheduled.Status.ShouldBe(ResultStatus.Conflict);

        _fixture.KindCatalog.Kinds.Add(new(PostKinds.Video, "Video", "videos", "VideoObject"));
        (await PublishAsync(post)).Value.CanonicalPath.ShouldBe($"/videos/{post.StablePublicId}");
    }

    [Fact]
    public async Task PublishingIsUnavailableWhileDynamicFieldsIsDown()
    {
        var post = Approved(PostKinds.News);
        _fixture.KindCatalog.Down = true;

        (await PublishAsync(post)).Status.ShouldBe(ResultStatus.Unavailable);
        post.State.ShouldBe(PostState.Approved);
    }

    [Fact]
    public async Task ADuePostWhoseKindLostItsFieldStaysScheduled()
    {
        var post = Approved(PostKinds.News);
        post.Schedule(PostBuilder.Desk, _fixture.Clock.Now.AddMinutes(5), _fixture.Clock.Now);
        _fixture.Posts.ListAsync(Arg.Any<Ardalis.Specification.ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns([post]);
        _fixture.Access.ScopesAsync(ContentPermissions.Publish, Arg.Any<CancellationToken>()).Returns([AccessScope.Tenant]);
        _fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        var handler = new PublishDuePostsHandler(_fixture.Posts, _fixture.PostAccess, _fixture.PostContents, _fixture.Kinds);
        _fixture.KindCatalog.Kinds.RemoveAll(k => k.Key == PostKinds.News);

        var first = await handler.Handle(new PublishDuePostsCommand(), Ct);

        first.IsSuccess.ShouldBeTrue();
        first.Value.ShouldBeEmpty();
        post.State.ShouldBe(PostState.Scheduled);

        _fixture.KindCatalog.Kinds.Add(new(PostKinds.News, "News", "news", "NewsArticle"));
        (await handler.Handle(new PublishDuePostsCommand(), Ct)).Value.ShouldBe([post.Id]);
        post.PublishedUrlPrefix.ShouldBe("news");
    }
}
