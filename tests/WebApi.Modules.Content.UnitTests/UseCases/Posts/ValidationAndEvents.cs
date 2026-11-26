using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Contracts.WorkItems;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;
using soft.square.net.WebApi.Modules.Content.Web.Extensions;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>Wave 4 C3: DynamicFields' guards, the work-item events, the feed on the outbox and content.post-created.v1.</summary>
public class ValidationAndEvents
{
    private const string Bad = "block.bad";
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValidationAndEvents() => _fixture.Validator.InvalidFields.Add(Bad);

    private static PostNode Bad_(string id) => Paragraph(id, "x") with { SchemaId = Bad };

    private async Task<Post> CreateAsync(params PostNode[] children)
    {
        var categories = Substitute.For<IReadRepository<Category>>();
        categories.GetByIdAsync("politics", Arg.Any<CancellationToken>()).Returns(PostBuilder.Category());
        _fixture.Allow(ContentPermissions.Author);
        Post? saved = null;
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = WithId(call.Arg<Post>(), 7);
            _fixture.Stored(saved);
            return saved;
        });
        var result = await new CreatePostHandler(categories, _fixture.Posts, _fixture.Store, _fixture.Split, _fixture.PostAccess, _fixture.Details,
            _fixture.Kinds, _fixture.Publisher).Handle(new CreatePostCommand("politics", "A title", "", PostKinds.News, Document(children)), Ct);
        result.IsSuccess.ShouldBeTrue();
        return saved!;
    }

    private ActOnContentHandler Act => new(_fixture.ContentChanges, _fixture.ContentAccess, _fixture.Pipelines, _fixture.Validation);

    private ContentItem Content(string nodeId) => _fixture.Store.Contents.Single(c => c.NodeId == nodeId);

    private Task<Result<PostDetail>> ApproveAsync(Post post)
    {
        post.Submit(PostBuilder.Owner, TestClock.Start);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, TestClock.Start);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, PostBuilder.Shift, TestClock.Start);
        _fixture.SignIn(PostBuilder.Reviewer);
        _fixture.Allow(ContentPermissions.Review);
        return new ApprovePostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.PostContents, _fixture.Pipelines, _fixture.Validation)
            .Handle(new ApprovePostCommand(post.Id, post.Version), Ct).AsTask();
    }

    private IReadOnlyList<(string Type, object Data)> Published() =>
        [.. _fixture.Events.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IIntegrationEvents.Publish))
            .Select(c => ((string)c.GetArguments()[0]!, c.GetArguments()[1]!))];

    [Fact]
    public async Task CreatingAPostPublishesPostCreatedInOneTransaction()
    {
        await CreateAsync(Paragraph("p1", "One"));

        _fixture.Store.Transactions.ShouldBe(1);
        var created = Published().Single(p => p.Type == ContentEventTypes.PostCreated).Data.ShouldBeOfType<PostCreated>();
        created.PostId.ShouldBe(7);
        created.PostKind.ShouldBe(PostKinds.News);
        created.Contents.ShouldBe([Content("p1").Id]);
        _fixture.Events.Received(1).Publish(ContentEventTypes.PostCreated, Arg.Any<object>(), "post:7", null, null);
    }

    [Fact]
    public async Task AnActionIntoReviewIsCheckedAndRefusedWithItsProblems()
    {
        await CreateAsync(Bad_("p1"));
        var content = Content("p1");

        var result = await Act.Handle(new ActOnContentCommand(content.Id, "submit", content.Version, null), Ct);

        result.Status.ShouldBe(ResultStatus.Invalid);
        ProblemErrors.Of(result).ShouldBe([new ContentProblem("/content/text", "Text is required.", "p1")]);
        content.Stage.ShouldBe("assigned");
        var row = _fixture.Store.Feed.Last();
        row.Type.ShouldBe(FeedEventTypes.ValidationFailed);
        row.ContentId.ShouldBe(content.Id);
        row.Detail!.ShouldStartWith("submit: /content/text Text is required.");
        var failure = Published().Single(p => p.Type == ContentEventTypes.ValidationFailed).Data.ShouldBeOfType<ContentValidationFailed>();
        failure.Check.ShouldBe(ContentValidationFailed.NodeCheck);
        failure.Action.ShouldBe("submit");
        failure.Problems.ShouldHaveSingleItem().NodeId.ShouldBe("p1");
    }

    [Fact]
    public async Task AWorkActionIsNotChecked()
    {
        await CreateAsync(Bad_("p1"));
        _fixture.Validator.Down = true;

        var started = await Act.Handle(new ActOnContentCommand(Content("p1").Id, "start", 0, null), Ct);

        started.IsSuccess.ShouldBeTrue();
        started.Value.Stage.ShouldBe("in-progress");
    }

    [Fact]
    public async Task DynamicFieldsDownIs503AndNothingMoves()
    {
        await CreateAsync(Paragraph("p1", "One"));
        _fixture.Validator.Down = true;

        var result = await Act.Handle(new ActOnContentCommand(Content("p1").Id, "submit", 0, null), Ct);

        result.Status.ShouldBe(ResultStatus.Unavailable);
        Content("p1").Stage.ShouldBe("assigned");
        _fixture.Store.Feed.ShouldNotContain(f => f.Type == FeedEventTypes.ValidationFailed);
    }

    [Fact]
    public async Task AValidActionPublishesTheWorkItemStateChange()
    {
        await CreateAsync(Paragraph("p1", "A paragraph"));
        var content = Content("p1");

        var result = await Act.Handle(new ActOnContentCommand(content.Id, "submit", 0, null), Ct);

        result.IsSuccess.ShouldBeTrue();
        _fixture.Validator.NodeCalls.ShouldBe(["p1"]);
        var published = Published();
        var state = published.Single(p => p.Type == ContentEventTypes.WorkItemStateChanged).Data.ShouldBeOfType<WorkItemStateChanged>();
        state.PreviousState.ShouldBe("assigned");
        state.Item.State.ShouldBe("submitted");
        state.Item.Title.ShouldBe("A paragraph");
        published.ShouldContain(p => p.Type == ContentEventTypes.ContentStateChanged);
    }

    [Fact]
    public void TheWorkItemEventsCarryTheSnapshot()
    {
        var unit = Guid.NewGuid();
        var id = Guid.NewGuid();

        _fixture.Publisher.WorkItem(new ContentWorkItemChanged(ContentWorkItemChange.StateChanged, id, "A title", "submitted", "assigned", "writer",
            "writer", unit, "/root/" + unit.ToString("N")));
        _fixture.Publisher.WorkItem(new ContentWorkItemChanged(ContentWorkItemChange.Assigned, id, "A title", "assigned", "assigned", "editor",
            null, null, null));

        var published = Published();
        published.Select(p => p.Type).ShouldBe([ContentEventTypes.WorkItemStateChanged, ContentEventTypes.WorkItemAssigned]);
        var state = published[0].Data.ShouldBeOfType<WorkItemStateChanged>();
        state.PreviousState.ShouldBe("assigned");
        state.Item.Ref.ToString().ShouldBe(ContentWorkItems.RefOf(id));
        state.Item.State.ShouldBe("submitted");
        state.Item.Title.ShouldBe("A title");
        state.Item.Assignee.ShouldBe(WorkItemAssignee.User("writer"));
        state.Item.UnitId.ShouldBe(unit.ToString("N"));
        state.Item.DueAt.ShouldBeNull();
        var assigned = published[1].Data.ShouldBeOfType<WorkItemAssigned>();
        assigned.PreviousAssignee.ShouldBeNull();
        assigned.Item.Assignee.ShouldBe(WorkItemAssignee.User("editor"));
        _fixture.Events.Received(1).Publish(ContentEventTypes.WorkItemStateChanged, Arg.Any<object>(), ContentWorkItems.RefOf(id),
            "/root/" + unit.ToString("N"), null);
    }

    [Fact]
    public void EveryFeedTypeIsMappedOntoTheOutbox()
    {
        foreach (var type in typeof(FeedEventTypes).GetFields().Select(f => (string)f.GetValue(null)!).Where(t => t != FeedEventTypes.ValidationFailed))
            ContentEventTypes.OfFeedType.ShouldContainKey(type);
        ContentEventTypes.All.ShouldBeUnique();
        ContentEventTypes.All.ShouldAllBe(t => IntegrationTypes.IsValidType(t));

        var id = Guid.NewGuid();
        _fixture.Publisher.Feed(new FeedEvent(FeedEventTypes.ContentChanged, null, id, 3, 2, "in-progress", null, "writer", TestClock.Start, "x",
            "/u1"));
        _fixture.Publisher.Feed(new FeedEvent(FeedEventTypes.ValidationFailed, 1, null, 3, null, null, null, "writer", TestClock.Start));

        var data = Published().ShouldHaveSingleItem();
        data.Type.ShouldBe(ContentEventTypes.ContentChanged);
        data.Data.ShouldBe(new ContentFeedEvent(FeedEventTypes.ContentChanged, null, id, 3, 2, "in-progress", null, "writer", TestClock.Start, "x"));
        _fixture.Events.Received(1).Publish(ContentEventTypes.ContentChanged, Arg.Any<object>(), $"content:{id:D}", "/u1", null);
    }

    [Fact]
    public async Task ApprovingChecksTheWholePost()
    {
        var post = await CreateAsync(Paragraph("p1", "One"), Bad_("p2"));

        var result = await ApproveAsync(post);

        result.Status.ShouldBe(ResultStatus.Invalid);
        ProblemErrors.Of(result).ShouldBe([new ContentProblem("/children/1/content/text", "Text is required.", "p2")]);
        post.State.ShouldBe(PostState.InReview);
        _fixture.Store.Contents.ShouldAllBe(c => c.Stage == "assigned");
        var failure = Published().Single(p => p.Type == ContentEventTypes.ValidationFailed).Data.ShouldBeOfType<ContentValidationFailed>();
        failure.Check.ShouldBe(ContentValidationFailed.PostCheck);
        failure.PostId.ShouldBe(post.Id);
        failure.Action.ShouldBe("approve");
    }

    [Fact]
    public async Task AValidPostIsApprovedAndPublished()
    {
        var post = await CreateAsync(Paragraph("p1", "One"));

        (await ApproveAsync(post)).IsSuccess.ShouldBeTrue();
        _fixture.SignIn(PostBuilder.Desk);
        _fixture.Allow(ContentPermissions.Publish);
        var published = await new PublishPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Desk, _fixture.Kinds, _fixture.Validation)
            .Handle(new PublishPostCommand(post.Id, post.Version), Ct);

        published.IsSuccess.ShouldBeTrue();
        _fixture.Validator.TreeCalls.Count.ShouldBe(2); // approve and publish
    }

    [Fact]
    public async Task PublishingWhileDynamicFieldsIsDownIs503()
    {
        var post = await CreateAsync(Paragraph("p1", "One"));
        (await ApproveAsync(post)).IsSuccess.ShouldBeTrue();
        _fixture.Validator.Down = true;
        _fixture.SignIn(PostBuilder.Desk);
        _fixture.Allow(ContentPermissions.Publish);

        var result = await new PublishPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Desk, _fixture.Kinds, _fixture.Validation)
            .Handle(new PublishPostCommand(post.Id, post.Version), Ct);

        result.Status.ShouldBe(ResultStatus.Unavailable);
        post.State.ShouldBe(PostState.Approved);
    }

    [Fact]
    public async Task ALegacyPostHasItsContentsCheckedOneByOne()
    {
        var post = await CreateAsync(); // a title-and-body post: its root is no DynamicFields field
        post.Recompose(PostDocument.FromLegacy("t", "b") with { Children = [Bad_("n1")] });

        var result = await _fixture.Validation.CheckPostAsync(post, "approve", Ct);

        ProblemErrors.Of(result).ShouldBe([new ContentProblem("/children/0/content/text", "Text is required.", "n1")]);
        _fixture.Validator.TreeCalls.ShouldBeEmpty();
    }

    [Fact]
    public void AProblemInANestedNodeNamesThatNode()
    {
        var gallery = Paragraph("g1", "x") with { Children = [Paragraph("i1", "a"), Paragraph("i2", "b")] };
        var document = Document(Paragraph("p1", "x"), gallery);

        ContentValidation.WithNodes([new("/children/1/children/1/content/src", "Missing.", null), new("/content/headline", "Missing.", null)], document)
            .Select(p => p.NodeId).ShouldBe(["i2", null]);
    }

    [Fact]
    public async Task ThePipelineIsPublishedWithTodaysKeys()
    {
        _fixture.Access.ScopesAsync(ContentPermissions.Pipeline, Arg.Any<CancellationToken>()).Returns([AccessScope.Tenant]);
        var old = Default with
        {
            StartStage = "draft",
            Stages = [.. Default.Stages.Select(s => s.Key == "assigned" ? s with { Key = "draft" } : s)],
            Actions = [.. Default.Actions.Select(a => a with { From = [.. a.From.Select(f => f == "assigned" ? "draft" : f)] })]
        };

        var published = await new PublishPipelineHandler(_fixture.Store, _fixture.PostAccess).Handle(new PublishPipelineCommand(old), Ct);

        published.IsSuccess.ShouldBeTrue();
        published.Value.Definition.StartStage.ShouldBe("assigned");
        published.Value.Definition.Action("start").From.ShouldContain("assigned");
    }

    [Fact]
    public void TheProblemsAre422WithTheirList()
    {
        var problem = ProblemErrors.Invalid([new ContentProblem("/content/text", "Text is required.", "p1")]).ToProblem();

        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        var list = problem.ProblemDetails.Extensions["problems"].ShouldBeAssignableTo<IReadOnlyList<ContentProblem>>()!;
        list.ShouldHaveSingleItem().NodeId.ShouldBe("p1");
        JsonSerializer.Serialize(problem.ProblemDetails, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .ShouldContain("\"problems\":[{\"path\":\"/content/text\",\"message\":\"Text is required.\",\"nodeId\":\"p1\"}]");
        Result.Invalid(new ValidationError("x", "y")).ToProblem().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }
}
