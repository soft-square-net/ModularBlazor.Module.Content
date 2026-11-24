using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate;

public class PostWithContents
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    private static (Post Post, ContentItem A, ContentItem B) ApprovedWithTwo()
    {
        var a = Item("A");
        var b = Item("B");
        var contents = new Dictionary<Guid, ContentItem> { [a.Id] = a, [b.Id] = b };
        var document = PostComposition.Working(Document(), [a.Id, b.Id], contents);
        var post = WithId(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", Now, document: document,
            contents: [new ContentRef(a.Id), new ContentRef(b.Id, Required: false)]), 7);
        post.Submit(PostBuilder.Owner, Now);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, Now);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, PostBuilder.Shift, Now);
        post.Approve(PostBuilder.Reviewer, Now);
        return (post, a, b);
    }

    private static void Accept(ContentItem content)
    {
        content.Act(PostBuilder.Owner, Pipeline.Action("submit"), null, Now);
        content.Act(PostBuilder.Desk, Pipeline.Action("accept"), null, Now);
    }

    private static PublishPlan Plan(Post post, params ContentItem[] contents)
    {
        var items = contents.ToDictionary(c => c.Id);
        var pins = PostComposition.AcceptedPins(post.Contents, items);
        return new PublishPlan(pins, PostComposition.Published(post.Root, post.Contents, pins, (id, revision) => items[id].Revisions.Single(r => r.Revision == revision).Node));
    }

    [Fact]
    public void TheContentsKeepTheirReadingOrder()
    {
        var (post, a, b) = ApprovedWithTwo();

        post.Contents.Select(c => c.ContentId).ShouldBe([a.Id, b.Id]);
        post.Contents[1].Required.ShouldBeFalse();
        JsonSerializer.Deserialize<PostDocument>(post.DocumentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Children.Select(n => n.NodeId).ShouldBe([a.NodeId, b.NodeId]);
    }

    [Fact]
    public void ARequiredContentMustBeAcceptedToPublish()
    {
        var (post, a, b) = ApprovedWithTwo();
        Accept(b);

        Should.Throw<DomainException>(() => post.Publish(PostBuilder.Desk, Now, Plan(post, a, b))).Message.ShouldContain("not accepted");
        Should.Throw<DomainException>(() => post.Publish(PostBuilder.Desk, Now)).Message.ShouldContain("not accepted");
    }

    [Fact]
    public void PublishingPinsTheAcceptedRevisionsAndLeavesOutUnacceptedOptionalOnes()
    {
        var (post, a, b) = ApprovedWithTwo();
        Accept(a);
        a.Edit(PostBuilder.Desk, Paragraph("", "A, later"), Pipeline.Stage(a.Stage), Now); // revision 2, back to the Desk

        post.Publish(PostBuilder.Desk, Now, Plan(post, a, b));

        post.State.ShouldBe(PostState.Published);
        post.Contents[0].PinnedRevision.ShouldBe(1);
        post.Contents[1].PinnedRevision.ShouldBeNull();
        var published = JsonSerializer.Deserialize<PostDocument>(post.PublishedDocumentJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        published.Children.Select(n => n.NodeId).ShouldBe([a.NodeId]);
        published.Children[0].Content.GetProperty("text").GetString().ShouldBe("A");
    }

    [Fact]
    public void SchedulingPinsAndTheDuePublicationUsesThePins()
    {
        var (post, a, b) = ApprovedWithTwo();
        Accept(a);
        var plan = Plan(post, a, b);

        post.Schedule(PostBuilder.Desk, Now.AddHours(1), Now, plan.Pins);
        post.Contents[0].PinnedRevision.ShouldBe(1);

        post.PublishIfDue("workflow", Now.AddHours(2), plan.Document).ShouldBeTrue();
        post.PublishedBy.ShouldBe(PostBuilder.Desk);
    }

    [Fact]
    public void AttachingAndDetachingRaiseFeedEvents()
    {
        var (post, a, b) = ApprovedWithTwo();
        ((IHasDomainEvents)post).TakeDomainEvents();
        var c = Item("C");
        var items = new Dictionary<Guid, ContentItem> { [a.Id] = a, [c.Id] = c };

        post.Edit(PostEditor.Desk, PostBuilder.Desk, Now, TimeSpan.Zero,
            new PostChange(Document: PostComposition.Working(post.Root, [c.Id, a.Id], items), Contents: [new ContentRef(c.Id), new ContentRef(a.Id)]));

        var events = ((IHasDomainEvents)post).TakeDomainEvents().OfType<FeedEvent>().ToList();
        events.Select(e => e.Type).ShouldBe([FeedEventTypes.ContentDetached, FeedEventTypes.ContentAttached, FeedEventTypes.PostChanged,
            FeedEventTypes.PostStateChanged], ignoreOrder: true);
        events.ShouldAllBe(e => e.PostId == 7);
        events.Single(e => e.Type == FeedEventTypes.ContentDetached).ContentId.ShouldBe(b.Id);
        post.Contents.Select(x => x.ContentId).ShouldBe([c.Id, a.Id]);
        post.State.ShouldBe(PostState.AwaitingDeskReapproval);
    }

    [Fact]
    public void APostUsesAContentOnce()
    {
        var (post, a, _) = ApprovedWithTwo();

        Should.Throw<DomainException>(() => post.Edit(PostEditor.Desk, PostBuilder.Desk, Now, TimeSpan.Zero,
            new PostChange(Contents: [new ContentRef(a.Id), new ContentRef(a.Id)])));
    }

    [Fact]
    public void SlugsAreCheckedPerCulture()
    {
        var post = PostBuilder.Draft(Now);

        post.Edit(PostEditor.Owner, PostBuilder.Owner, Now, TimeSpan.FromHours(1),
            new PostChange(Slugs: new Dictionary<string, string> { ["en"] = "final-day", ["ar"] = "اليوم-الأخير" }));

        post.Slugs.Select(s => (s.Culture, s.Slug)).ShouldBe([("en", "final-day"), ("ar", "اليوم-الأخير")], ignoreOrder: true);
        Should.Throw<DomainException>(() => post.Edit(PostEditor.Owner, PostBuilder.Owner, Now, TimeSpan.FromHours(1),
            new PostChange(Slugs: new Dictionary<string, string> { ["en"] = "Final Day" })));
    }

    [Fact]
    public void TwoContentsOfAPostCannotShareANodeId()
    {
        var a = ContentItem.Create(PostBuilder.Owner, "daily", "politics", null, Paragraph("p1", "A"), "draft", Now);
        var b = ContentItem.Create(PostBuilder.Owner, "daily", "politics", null, Paragraph("p1", "B"), "draft", Now);

        Should.Throw<DomainException>(() => PostComposition.RequireUniqueNodes([a, b]));
    }
}

public class PostSplitMigration
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    private static Post Stored(Post post)
    {
        post.MarkUnsplit();
        return WithId(post, 3);
    }

    private static PostDocument Read(Post post) =>
        JsonSerializer.Deserialize<PostDocument>(post.DocumentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static Post WithChildren(Func<DateTimeOffset, Post> state)
    {
        var post = state(Now);
        post.EditByDesk(PostBuilder.Desk, post.Title, post.Body, Now, Document(Paragraph("p1", "One"), Paragraph("p2", "Two")));
        return post;
    }

    [Fact]
    public void ADraftsChildrenBecomeContentsInTheStartStage()
    {
        var post = Stored(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "T", "", Now,
            document: Document(Paragraph("p1", "One"), Paragraph("p2", "Two"))));

        var contents = PostSplit.Migrate(post, Read(post), Pipeline, Now);

        contents.Select(c => c.NodeId).ShouldBe(["p1", "p2"]);
        contents.ShouldAllBe(c => c.Stage == "draft" && c.AcceptedRevision == null && c.OriginPostId == post.StablePublicId);
        contents.ShouldAllBe(c => c.CreatedBy == PostBuilder.Owner && c.Revision == 1);
        post.ContentsSplit.ShouldBeTrue();
        post.Contents.Select(c => c.ContentId).ShouldBe(contents.Select(c => c.Id));
        post.Contents.ShouldAllBe(c => c.PinnedRevision == null && c.Required);
        ((IHasDomainEvents)contents[0]).HasDomainEvents.ShouldBeFalse();
    }

    [Fact]
    public void APublishedPostsContentsAreAcceptedAndPinned()
    {
        var post = WithChildren(PostBuilder.Approved);
        post.Publish(PostBuilder.Desk, Now);
        Stored(post);

        var contents = PostSplit.Migrate(post, Read(post), Pipeline, Now);

        contents.ShouldAllBe(c => c.Stage == "accepted" && c.AcceptedRevision == 1);
        post.Contents.ShouldAllBe(c => c.PinnedRevision == 1);
    }

    [Fact]
    public void AnApprovedPostsContentsWaitForTheDesk()
    {
        var post = Stored(WithChildren(PostBuilder.Approved)); // the Desk edit: awaiting re-approval

        var contents = PostSplit.Migrate(post, Read(post), Pipeline, Now);

        contents.ShouldAllBe(c => c.Stage == "desk" && c.AcceptedRevision == null);
    }

    [Fact]
    public void ASplitPostIsLeftAlone() =>
        PostSplit.Migrate(PostBuilder.Draft(Now), PostDocument.FromLegacy("t", "b"), Pipeline, Now).ShouldBeEmpty();
}
