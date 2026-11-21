using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Submit;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

public class PostChangesChangeAsync
{
    private readonly PostFixture _fixture = new(PostBuilder.Owner);

    [Fact]
    public async Task IsNotFoundWhenThereIsNoPost()
    {
        var result = await _fixture.Changes.ChangeAsync(7, 0, _ => Task.FromResult(Result.Success()), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ConflictsOnAStaleVersionWithoutSaving()
    {
        _fixture.Stored(PostBuilder.Submitted(_fixture.Clock.Now)); // version 1

        var result = await _fixture.Changes.ChangeAsync(1, 0, _ => Task.FromResult(Result.Success()), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _fixture.Posts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReturnsAFailedCheckWithoutSaving()
    {
        _fixture.Stored(PostBuilder.Draft(_fixture.Clock.Now));

        var result = await _fixture.Changes.ChangeAsync(1, 0, _ => Task.FromResult(Result.Forbidden("no")), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Forbidden);
        await _fixture.Posts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SubmitSavesAndReturnsTheNewVersion()
    {
        var post = PostBuilder.Draft(_fixture.Clock.Now);
        _fixture.Stored(post);
        var handler = new SubmitPostHandler(_fixture.Changes, _fixture.PostAccess);

        var result = await handler.Handle(new SubmitPostCommand(1, 0), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(PostState.Submitted);
        result.Value.Version.ShouldBe(1);
        result.Value.OwnerMayEdit.ShouldBeTrue();
        await _fixture.Posts.Received(1).UpdateAsync(post, Arg.Any<CancellationToken>());
    }
}
