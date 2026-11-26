using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>Today's whole-document editor and workflow over shared contents, through the handlers.</summary>
public class SharedContentHandlers
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Post> CreateAsync(params PostNode[] children)
    {
        var categories = Substitute.For<IReadRepository<Category>>();
        categories.GetByIdAsync("politics", Arg.Any<CancellationToken>()).Returns(PostBuilder.Category());
        _fixture.Allow(ContentPermissions.Author);
        Post? saved = null;
        _fixture.Posts.AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = WithId(call.Arg<Post>(), 1);
            _fixture.Stored(saved);
            return saved;
        });
        var handler = new CreatePostHandler(categories, _fixture.Posts, _fixture.Store, _fixture.Split, _fixture.PostAccess, _fixture.Details, _fixture.Kinds);

        var result = await handler.Handle(new CreatePostCommand("politics", "A title", "", PostKinds.News, Document(children)), Ct);

        result.IsSuccess.ShouldBeTrue();
        return saved!;
    }

    private Task<Result<PostDetail>> EditAsync(Post post, params PostNode[] children) =>
        new EditPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Editors, _fixture.Split)
            .Handle(new EditPostCommand(post.Id, post.Version, post.Title, post.Body, Document(children)), Ct).AsTask();

    private ContentItem Content(string nodeId) => _fixture.Store.Contents.Single(c => c.NodeId == nodeId);

    private static void MoveToReview(Post post)
    {
        post.Submit(PostBuilder.Owner, TestClock.Start);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, TestClock.Start);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, PostBuilder.Shift, TestClock.Start);
    }

    private async Task<Post> ApprovedAsync()
    {
        var post = await CreateAsync(Paragraph("p1", "One"), Paragraph("p2", "Two"));
        MoveToReview(post);
        _fixture.SignIn(PostBuilder.Reviewer);
        _fixture.Allow(ContentPermissions.Review);
        var approved = await new ApprovePostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.PostContents, _fixture.Pipelines)
            .Handle(new ApprovePostCommand(post.Id, post.Version), Ct);
        approved.IsSuccess.ShouldBeTrue();
        return post;
    }

    private Task<Result<PostDetail>> PublishAsync(Post post)
    {
        _fixture.SignIn(PostBuilder.Desk);
        _fixture.Allow(ContentPermissions.Publish);
        return new PublishPostHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Desk, _fixture.Kinds).Handle(new PublishPostCommand(post.Id, post.Version), Ct).AsTask();
    }

    [Fact]
    public async Task CreatingAPostMakesAContentOfEachChild()
    {
        var post = await CreateAsync(Paragraph("p1", "One"), Paragraph("p2", "Two"));

        _fixture.Store.Contents.Select(c => c.NodeId).ShouldBe(["p1", "p2"]);
        _fixture.Store.Contents.ShouldAllBe(c => c.OriginPostId == post.PublicId && c.Stage == "draft");
        post.Contents.Select(c => c.ContentId).ShouldBe(_fixture.Store.Contents.Select(c => c.Id));
        JsonSerializer.Deserialize<PostDocument>(post.DocumentJson, Web)!.Children.Select(c => c.NodeId).ShouldBe(["p1", "p2"]);
    }

    [Fact]
    public async Task TheDetailListsTheContentsWithTheirStage()
    {
        await CreateAsync(Paragraph("p1", "One"));

        var detail = await _fixture.Details.GetAsync(1, Ct);

        detail.Value.Contents.ShouldHaveSingleItem().NodeId.ShouldBe("p1");
        detail.Value.Contents[0].Stage.ShouldBe("draft");
        detail.Value.Document.Children.ShouldHaveSingleItem().NodeId.ShouldBe("p1");
    }

    [Fact]
    public async Task AWholeDocumentEditRevisesAddsAndDetaches()
    {
        var post = await CreateAsync(Paragraph("p1", "One"), Paragraph("p2", "Two"));
        var p2 = Content("p2");

        var result = await EditAsync(post, Paragraph("p3", "Three"), Paragraph("p1", "One, edited"));

        result.IsSuccess.ShouldBeTrue();
        Content("p1").Revision.ShouldBe(2);
        Content("p3").Revision.ShouldBe(1);
        post.Contents.Select(c => c.ContentId).ShouldBe([Content("p3").Id, Content("p1").Id]);
        _fixture.Store.Contents.ShouldContain(p2); // detached, still in the library
        result.Value.Document.Children.Select(c => c.NodeId).ShouldBe(["p3", "p1"]);
    }

    [Fact]
    public async Task AWholeDocumentEditCannotChangeASharedContent()
    {
        var post = await CreateAsync(Paragraph("p1", "One"));
        var shared = Item("Shared", origin: "another-post");
        _fixture.Store.Contents.Add(shared);
        post.Edit(PostEditor.Owner, PostBuilder.Owner, TestClock.Start, TimeSpan.FromHours(1), new PostChange(
            Document: PostComposition.Working(post.Root, [Content("p1").Id, shared.Id], _fixture.Store.Contents.ToDictionary(c => c.Id)),
            Contents: [new ContentRef(Content("p1").Id), new ContentRef(shared.Id)]));

        await Should.ThrowAsync<DomainException>(() => EditAsync(post, Paragraph("p1", "One"), Paragraph(shared.NodeId, "Changed")));
    }

    [Fact]
    public async Task ApprovingSendsThePostsContentsToTheDesk()
    {
        await ApprovedAsync();

        _fixture.Store.Contents.ShouldAllBe(c => c.Stage == "desk");
    }

    [Fact]
    public async Task DesksPublishAcceptsWaitingContentsAndPinsThem()
    {
        var post = await ApprovedAsync();

        var result = await PublishAsync(post);

        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(PostState.Published);
        _fixture.Store.Contents.ShouldAllBe(c => c.Stage == "accepted" && c.AcceptedRevision == 1);
        post.Contents.ShouldAllBe(c => c.PinnedRevision == 1);
        JsonSerializer.Deserialize<PostDocument>(post.PublishedDocumentJson!, Web)!.Children.Select(c => c.NodeId).ShouldBe(["p1", "p2"]);
    }

    [Fact]
    public async Task ARejectedRequiredContentBlocksThePublication()
    {
        var post = await ApprovedAsync();
        Content("p2").Act(PostBuilder.Desk, PipelineDefinition.Default.Action("reject"), "Needs a source", TestClock.Start);

        await Should.ThrowAsync<DomainException>(() => PublishAsync(post));
        post.State.ShouldBe(PostState.Approved);
    }

    [Fact]
    public async Task OnlyWhomThePipelineNamesMayAct()
    {
        await CreateAsync(Paragraph("p1", "One"));
        var content = Content("p1");
        var act = new ActOnContentHandler(_fixture.ContentChanges, _fixture.ContentAccess, _fixture.Pipelines);

        _fixture.SignIn("someone-else");
        _fixture.Allow(ContentPermissions.Editor); // sees it, but is not its assignee
        (await act.Handle(new ActOnContentCommand(content.Id, "submit", 0, null), Ct)).Status.ShouldBe(ResultStatus.Forbidden);

        _fixture.SignIn(PostBuilder.Owner);
        var submitted = await act.Handle(new ActOnContentCommand(content.Id, "submit", 0, null), Ct);
        submitted.IsSuccess.ShouldBeTrue();
        submitted.Value.Stage.ShouldBe("desk");

        (await act.Handle(new ActOnContentCommand(content.Id, "accept", 0, null), Ct)).Status.ShouldBe(ResultStatus.Conflict); // stale version
        (await act.Handle(new ActOnContentCommand(content.Id, "launch", 1, null), Ct)).Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task TheDetailListsTheActionsTheCallerMayTake()
    {
        await CreateAsync(Paragraph("p1", "One"));

        var detail = await _fixture.ContentDetails.GetAsync(Content("p1").Id, Ct);

        detail.Value.Actions.ShouldBe(["submit"]);
        detail.Value.CanEdit.ShouldBeTrue();
    }

    [Fact]
    public async Task AReferencedContentCannotBeDeleted()
    {
        await CreateAsync(Paragraph("p1", "One"));
        var content = Content("p1");
        var delete = new DeleteContentHandler(_fixture.Store, _fixture.ContentAccess);

        (await delete.Handle(new DeleteContentCommand(content.Id, 0), Ct)).Status.ShouldBe(ResultStatus.Conflict);

        var unused = Item("Unused");
        _fixture.Store.Contents.Add(unused);
        (await delete.Handle(new DeleteContentCommand(unused.Id, 0), Ct)).IsSuccess.ShouldBeTrue();
        _fixture.Store.Contents.ShouldNotContain(unused);
    }

    [Fact]
    public async Task PublishingAPipelineNeedsTheRightTenantWide()
    {
        var publish = new PublishPipelineHandler(_fixture.Store, _fixture.PostAccess);

        (await publish.Handle(new PublishPipelineCommand(PipelineDefinition.Default), Ct)).Status.ShouldBe(ResultStatus.Forbidden);

        _fixture.Access.ScopesAsync(ContentPermissions.Pipeline, Arg.Any<CancellationToken>()).Returns([AccessScope.Tenant]);
        var published = await publish.Handle(new PublishPipelineCommand(PipelineDefinition.Default with { Key = "custom" }), Ct);
        published.IsSuccess.ShouldBeTrue();
        published.Value.Version.ShouldBe(1);
        (await _fixture.Pipelines.CurrentAsync(Ct)).Key.ShouldBe("custom");
    }
}
