using Ardalis.Specification;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Clock;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Newsroom;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Unschedule;
using soft.square.net.WebApi.Modules.Content.Web;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>
/// What the newsroom cutover added: the create request's idempotency key, a first assignee while the workflow owns
/// assignment, unschedule, the clock's handover of reviews and the NewsRoom project events.
/// </summary>
public class CutoverHandlers
{
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private readonly IReadRepository<Category> _categories = Substitute.For<IReadRepository<Category>>();
    private readonly List<Post> _added = [];
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public CutoverHandlers()
    {
        _categories.GetByIdAsync("politics", Arg.Any<CancellationToken>()).Returns(PostBuilder.Category());
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var post = call.Arg<Post>();
            WithId(post, 40 + _added.Count);
            _added.Add(post);
            _fixture.Stored(post);
            return post;
        });
    }

    private async Task<Ardalis.Result.Result<PostDetail>> CreateAsync(string? key, string category = "politics") =>
        await new CreatePostHandler(_categories, _fixture.Posts, _fixture.Store, _fixture.Split, _fixture.PostAccess, _fixture.Details,
                _fixture.Kinds, _fixture.Publisher)
            .Handle(new CreatePostCommand(category, "A title", "A body", PostKinds.Article, null, null, key), Ct);

    [Fact]
    public async Task ARepeatedCreateWithTheSameKeyReturnsTheFirstPost()
    {
        _fixture.Allow(ContentPermissions.Author);

        var first = await CreateAsync(" newsroom:7 ");
        // Even a request that could not make a post now.
        var repeat = await CreateAsync("newsroom:7", category: "gone");

        first.IsSuccess.ShouldBeTrue();
        repeat.IsSuccess.ShouldBeTrue();
        repeat.Value.Id.ShouldBe(first.Value.Id);
        _added.Count.ShouldBe(1);
        _fixture.Store.Keys.ShouldHaveSingleItem().PostId.ShouldBe(first.Value.Id);
    }

    [Fact]
    public async Task AnotherUsersKeyIsAConflict()
    {
        _fixture.Allow(ContentPermissions.Author);
        await CreateAsync("newsroom:7");
        _fixture.SignIn("someone-else");

        var result = await CreateAsync("newsroom:7");

        result.Status.ShouldBe(ResultStatus.Conflict);
        _added.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ARequestThatLosesTheRaceForTheKeyReturnsTheWinnersPost()
    {
        _fixture.Allow(ContentPermissions.Author);
        _fixture.Store.RacingKey = new PostCreateKey("newsroom:7", PostBuilder.Owner, 99, TestClock.Start);
        var winner = PostBuilder.Draft(TestClock.Start);
        WithId(winner, 99);
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            WithId(call.Arg<Post>(), 40);
            _fixture.Stored(winner); // what is read afterwards
            return call.Arg<Post>();
        });

        var result = await CreateAsync("newsroom:7");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(99);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task WithoutAKeyEveryRequestMakesAPost(string? key)
    {
        _fixture.Allow(ContentPermissions.Author);

        await CreateAsync(key);
        await CreateAsync(key);

        _added.Count.ShouldBe(2);
        _fixture.Store.Keys.ShouldBeEmpty();
    }

    [Fact]
    public async Task ATooLongKeyIsInvalid() =>
        (await CreateAsync(new string('k', PostCreateKey.MaxLength + 1))).Status.ShouldBe(ResultStatus.Invalid);

    [Theory]
    [InlineData(false, false, ResultStatus.Ok)]
    [InlineData(true, false, ResultStatus.Forbidden)]
    [InlineData(true, true, ResultStatus.Ok)]
    public async Task AFirstAssigneeNeedsTheWorkflowWhileItOwnsAssignment(bool workflowOwns, bool workflow, ResultStatus expected)
    {
        _fixture.Allow(ContentPermissions.Author);
        if (workflow) _fixture.Allow(ContentPermissions.UnitAssign);
        var rights = new AssignmentRights(_fixture.PostAccess,
            Microsoft.Extensions.Options.Options.Create(new ContentOptions { Newsroom = new() { WorkflowOwnsAssignment = workflowOwns } }));
        var handler = new CreateContentHandler(_categories, _fixture.Posts, _fixture.Store, _fixture.ContentAccess, _fixture.Pipelines,
            _fixture.Editing, _fixture.ContentDetails, rights);

        var result = await handler.Handle(
            new CreateContentCommand("politics", Paragraph("", "One"), SectionId: Section, AssigneeId: "writer-2"), Ct);

        // Past the check, the in-memory content may not be readable back through the fixture's rights: what matters is the save.
        if (expected == ResultStatus.Forbidden)
        {
            result.Status.ShouldBe(ResultStatus.Forbidden);
            _fixture.Store.Contents.ShouldBeEmpty();
        }
        else _fixture.Store.Contents.ShouldHaveSingleItem().AssigneeId.ShouldBe("writer-2");
    }

    [Fact]
    public async Task TheDeskUnschedulesAScheduledPost()
    {
        var post = PostBuilder.Approved(TestClock.Start);
        post.Schedule(PostBuilder.Desk, TestClock.Start.AddHours(1), TestClock.Start);
        WithId(post, 7);
        _fixture.Stored(post);
        var handler = new UnschedulePostHandler(_fixture.Changes, _fixture.PostAccess);

        (await handler.Handle(new UnschedulePostCommand(7, post.Version), Ct)).Status.ShouldBe(ResultStatus.Forbidden);
        _fixture.Allow(ContentPermissions.Publish);
        _fixture.SignIn(PostBuilder.Desk);
        var result = await handler.Handle(new UnschedulePostCommand(7, post.Version), Ct);

        result.IsSuccess.ShouldBeTrue(string.Join(" ", result.Errors));
        post.State.ShouldBe(PostState.Approved);
    }

    private ReviewHandover Handover(Post post)
    {
        _fixture.Stored(post);
        return new ReviewHandover(_fixture.Reader, _fixture.Posts, _fixture.PostAccess, Microsoft.Extensions.Options.Options.Create(new ContentOptions()));
    }

    [Fact]
    public async Task TheClockHandsBackAReviewOnceTheReviewerHasBeenWithoutTheRightForTheGrace()
    {
        _fixture.Access.UserId.Returns((string?)null); // the clock acts for no user
        var post = PostBuilder.InReview(_fixture.Clock.Now);
        WithId(post, 7);
        var handover = Handover(post);

        (await handover.CheckAsync(7, Ct)).ShouldBe(ReviewerCheck.Noted);
        _fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        (await handover.CheckAsync(7, Ct)).ShouldBe(ReviewerCheck.HandedOver);

        post.State.ShouldBe(PostState.Accepted);
        post.History.Last().Actor.ShouldBe(ReviewHandover.ClockActor);
        await _fixture.Posts.Received(2).UpdateAsync(post, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheClockLeavesAReviewerWhoHoldsTheRight()
    {
        var post = PostBuilder.InReview(_fixture.Clock.Now);
        WithId(post, 7);
        _fixture.Access.UserHasPermissionAsync(PostBuilder.Reviewer, ContentPermissions.Review, Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>()).Returns(true);

        (await Handover(post).CheckAsync(7, Ct)).ShouldBe(ReviewerCheck.Unchanged);

        await _fixture.Posts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    private static IntegrationEnvelope Envelope(string type, object data) =>
        new(Guid.NewGuid(), type, "acme", "newsroom:project:1", TestClock.Start, "trace", "newsroom",
            System.Text.Json.JsonSerializer.SerializeToElement(data, data.GetType(), IntegrationEnvelope.JsonOptions), null, null);

    [Fact]
    public async Task NewsRoomsProjectEventsSayWhichPostsTheWorkflowWorksOn()
    {
        var post = PostBuilder.Submitted(TestClock.Start);
        WithId(post, 7);
        _fixture.Stored(post);
        var handler = new NewsRoomProjectEventsHandler(_fixture.Store, NullLogger<NewsRoomProjectEventsHandler>.Instance);
        var project = Guid.NewGuid();

        // The shapes of NewsRoom's records, with fields this module ignores.
        await handler.HandleAsync(Envelope(NewsRoomProjectEventsHandler.Approved,
            new { ProjectId = project, ProposalId = Guid.NewGuid(), Version = 3, ApprovalNumber = 1, PostId = 7 }), Ct);
        post.IsWorkflowManaged.ShouldBeTrue();

        await handler.HandleAsync(Envelope(NewsRoomProjectEventsHandler.StateChanged,
            new { ProjectId = project, PreviousState = "Approved", State = "InProduction", PostId = (int?)7 }), Ct);
        post.IsWorkflowManaged.ShouldBeTrue();

        await handler.HandleAsync(Envelope(NewsRoomProjectEventsHandler.StateChanged,
            new { ProjectId = project, PreviousState = "InProduction", State = "Killed", PostId = (int?)7 }), Ct);
        post.IsWorkflowManaged.ShouldBeFalse();

        _fixture.Store.Saves.ShouldBe(0); // the inbox saves
    }

    [Theory]
    [InlineData(null)]
    [InlineData(404)]
    public async Task AProjectEventWithoutAKnownPostIsIgnored(int? postId)
    {
        var handler = new NewsRoomProjectEventsHandler(_fixture.Store, NullLogger<NewsRoomProjectEventsHandler>.Instance);

        await handler.HandleAsync(Envelope(NewsRoomProjectEventsHandler.StateChanged,
            new { ProjectId = Guid.NewGuid(), State = "Approved", PostId = postId }), Ct);
    }

    [Fact]
    public void TheModuleFollowsNewsRoomAndSetsTheClocksDefaults()
    {
        var manifest = new ContentModule().Manifest;

        foreach (var type in NewsRoomProjectEventsHandler.Types) manifest.Subscriptions.ShouldContain(type);
        manifest.DefaultSettings["Content:ClockInterval"].ShouldBe("00:00:15");
        manifest.DefaultSettings["Content:Newsroom:ReviewHandoverGrace"].ShouldBe("00:10:00");
    }
}
