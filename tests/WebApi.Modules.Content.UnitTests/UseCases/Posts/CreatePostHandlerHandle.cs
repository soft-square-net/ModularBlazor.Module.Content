using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

public class CreatePostHandlerHandle
{
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private readonly IReadRepository<Category> _categories = Substitute.For<IReadRepository<Category>>();
    private readonly CreatePostHandler _handler;

    public CreatePostHandlerHandle()
    {
        _handler = new CreatePostHandler(_categories, _fixture.Posts, _fixture.Store, _fixture.Split, _fixture.PostAccess, _fixture.Details);
        _categories.GetByIdAsync("politics", Arg.Any<CancellationToken>()).Returns(PostBuilder.Category());
    }

    private static CreatePostCommand Command(string category = "politics") => new(category, "A title", "A body", PostKind.Article, null);

    [Fact]
    public async Task ConflictsOnAnUnknownCategory()
    {
        var result = await _handler.Handle(Command("nowhere"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
    }

    [Fact]
    public async Task ForbidsAnAuthorWithoutTheRightInTheCategory()
    {
        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Forbidden);
        await _fixture.Posts.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatesADraftOwnedByTheCaller()
    {
        _fixture.Allow(ContentPermissions.Author);
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _fixture.Stored(call.Arg<Post>());
            return call.Arg<Post>();
        });

        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OwnerId.ShouldBe(PostBuilder.Owner);
        result.Value.State.ShouldBe(PostState.Draft);
        result.Value.CreatedAt.ShouldBe(TestClock.Start);
        await _fixture.Access.Received().HasPermissionAsync(ContentPermissions.Author,
            Arg.Is<IReadOnlyCollection<string>>(s => s.Contains("category:politics")), Arg.Any<CancellationToken>());
    }
}
