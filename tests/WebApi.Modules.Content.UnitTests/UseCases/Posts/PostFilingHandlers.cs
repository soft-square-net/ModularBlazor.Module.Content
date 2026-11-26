using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using soft.square.net.Organization.Contracts;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Events;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Filing;
using soft.square.net.WebApi.Modules.Content.UseCases.Units;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>
/// Filing a post: its categories (its editors, all of its press), its unit (the workflow only, a unit that serves one of its
/// categories, <c>*</c> serving all), and the events; the Shifts section remap and Organization's unit moves.
/// </summary>
public class PostFilingHandlers
{
    private static readonly Guid Desk = PostCategoriesAndUnits.Desk;
    private static readonly Guid Multimedia = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly string RootPath = UnitPaths.Root(PostCategoriesAndUnits.Root);
    private static string DeskPath => PostCategoriesAndUnits.DeskPath;
    private static string MultimediaPath => UnitPaths.Child(RootPath, Multimedia);

    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private readonly FakeUnitLookup _units = new();
    private readonly IIntegrationEvents _events = Substitute.For<IIntegrationEvents>();
    private readonly IReadRepository<Category> _categories = Substitute.For<IReadRepository<Category>>();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public PostFilingHandlers()
    {
        _units.Add(new UnitInfo(Desk, DeskPath, "section", "Politics desk", "ديسك السياسة", ["politics"]));
        _units.Add(new UnitInfo(Multimedia, MultimediaPath, "section", "Multimedia", "الوسائط", [UnitCategoryRules.All]));
        foreach (var category in new[] { PostBuilder.Category(), Category.Create("economy", "daily", "Economy"), Category.Create("weekly-sport", "weekly", "Sport") })
            _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
    }

    private UnitRules Rules => new(_units, NullLoggerFactory.Instance);

    private Post Stored(Post post)
    {
        WithId(post, 7);
        _fixture.Stored(post);
        return post;
    }

    private Task<Result<PostDetail>> SetUnitAsync(Post post, Guid? unit) =>
        new SetPostUnitHandler(_fixture.Changes, _fixture.PostAccess, Rules, new ContentEventPublisher(_events))
            .Handle(new SetPostUnitCommand(post.Id, post.Version, unit), Ct).AsTask();

    private Task<Result<PostDetail>> SetCategoriesAsync(Post post, string primary, params string[] others) =>
        new ChangePostCategoriesHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Editors, _categories, Rules, new ContentEventPublisher(_events))
            .Handle(new ChangePostCategoriesCommand(post.Id, post.Version, primary, others), Ct).AsTask();

    [Fact]
    public async Task OnlyTheWorkflowPutsAPostInAUnit()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.Assign);
        _fixture.Allow(ContentPermissions.Author);

        var result = await SetUnitAsync(post, Desk);

        result.Status.ShouldBe(ResultStatus.Forbidden);
        post.UnitId.ShouldBeNull();
        _events.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task TheWorkflowPutsAPostInAUnitThatServesItsCategoryAndPublishesTheChange()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.SignIn("workflow");
        _fixture.Allow(ContentPermissions.UnitAssign);

        var result = await SetUnitAsync(post, Desk);

        result.IsSuccess.ShouldBeTrue(string.Join(" ", result.Errors));
        result.Value.UnitId.ShouldBe(Desk);
        result.Value.UnitPath.ShouldBe(DeskPath);
        post.Scopes.ShouldContain(UnitScopes.Of(PostCategoriesAndUnits.Root));
        _events.Received(1).Publish(ContentEvents.PostUnitChanged,
            Arg.Is<PostUnitChanged>(e => e.PostId == 7 && e.UnitId == Desk && e.UnitPath == DeskPath && e.PreviousUnitId == null
                                         && e.Categories.SequenceEqual(new[] { "politics" })),
            "post:7", DeskPath, null);
    }

    [Fact]
    public async Task AUnitThatServesEveryCategoryTakesAnyPost()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.UnitAssign);

        (await SetUnitAsync(post, Multimedia)).IsSuccess.ShouldBeTrue();

        post.UnitId.ShouldBe(Multimedia);
    }

    [Fact]
    public async Task AUnitThatServesNoneOfThePostsCategoriesIsRefused()
    {
        var other = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
        _units.Add(new UnitInfo(other, UnitPaths.Child(RootPath, other), "section", "Sport", "الرياضة", ["sport"]));
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.UnitAssign);

        var result = await SetUnitAsync(post, other);

        result.Status.ShouldBe(ResultStatus.Conflict);
        result.Errors.Single().ShouldContain("does not serve");
        post.UnitId.ShouldBeNull();
    }

    [Fact]
    public async Task AnUnknownUnitIsInvalidAndADownDirectoryIsUnavailable()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.UnitAssign);

        (await SetUnitAsync(post, Guid.NewGuid())).Status.ShouldBe(ResultStatus.Invalid);
        _units.Down = true;
        (await SetUnitAsync(post, Desk)).Status.ShouldBe(ResultStatus.Unavailable);
    }

    [Fact]
    public async Task TheOwnerFilesADraftInMoreCategoriesOfItsPress()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.Author);

        var result = await SetCategoriesAsync(post, "economy", "politics");

        result.IsSuccess.ShouldBeTrue(string.Join(" ", result.Errors));
        result.Value.CategoryId.ShouldBe("economy");
        result.Value.Categories.ShouldBe(["economy", "politics"]);
        _events.Received(1).Publish(ContentEvents.PostCategoriesChanged,
            Arg.Is<PostCategoriesChanged>(e => e.PrimaryCategoryId == "economy" && e.PreviousCategories.SequenceEqual(new[] { "politics" })),
            "post:7", null, null);
    }

    [Fact]
    public async Task ACategoryOfAnotherPressIsInvalid()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.Allow(ContentPermissions.Author);

        var result = await SetCategoriesAsync(post, "politics", "weekly-sport");

        result.Status.ShouldBe(ResultStatus.Invalid);
        post.Categories.ShouldBe(["politics"]);
    }

    [Fact]
    public async Task APostKeepsItsUnitOnlyWhileTheUnitServesOneOfItsCategories()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        post.ChangeUnit("workflow", Desk, DeskPath, _fixture.Clock.Now);
        _fixture.Allow(ContentPermissions.Author);

        (await SetCategoriesAsync(post, "economy")).Status.ShouldBe(ResultStatus.Conflict);
        (await SetCategoriesAsync(post, "economy", "politics")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SomeoneWhoMayNotEditThePostCannotChangeItsCategories()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        _fixture.SignIn("stranger");
        _fixture.Allow(ContentPermissions.Author);

        (await SetCategoriesAsync(post, "economy")).Status.ShouldBe(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task TheWorkflowGivesAContentToAUnitThatServesItsCategory()
    {
        var content = Content(text: "One");
        _fixture.Store.Add(content);
        _fixture.SignIn("workflow");
        _fixture.Allow(ContentPermissions.UnitAssign);
        var handler = new SetContentUnitHandler(_fixture.ContentChanges, _fixture.ContentAccess, Rules);

        var result = await handler.Handle(new SetContentUnitCommand(content.Id, content.Version, Desk), Ct);

        result.IsSuccess.ShouldBeTrue(string.Join(" ", result.Errors));
        result.Value.UnitId.ShouldBe(Desk);
        content.Scopes.ShouldContain($"section:{UnitPaths.Id(Desk)}");
    }

    [Fact]
    public void TheWildcardCoversEveryCategory()
    {
        UnitCategoryRules.Covers(["*"], "anything").ShouldBeTrue();
        UnitCategoryRules.Covers(["politics"], "politics").ShouldBeTrue();
        UnitCategoryRules.Covers(["politics"], "sport").ShouldBeFalse();
        UnitCategoryRules.Covers([], "sport").ShouldBeFalse();
        UnitCategoryRules.CoversAny(["sport"], ["politics", "sport"]).ShouldBeTrue();
    }

    [Fact]
    public async Task AMovedSectionRemapsItsContentsToTheUnit()
    {
        var inSection = Content(text: "One", sectionId: "3");
        var other = Content(text: "Two", sectionId: "4");
        _fixture.Store.Add(inSection);
        _fixture.Store.Add(other);
        var handler = new SectionMovedHandler(_fixture.Store, _fixture.Clock, NullLogger<SectionMovedHandler>.Instance);

        await handler.HandleAsync(Envelope(SectionMovedHandler.Type, new { sectionId = 3, unitId = Desk }, DeskPath), Ct);

        inSection.UnitId.ShouldBe(Desk);
        inSection.UnitPath.ShouldBe(DeskPath);
        inSection.SectionId.ShouldBeNull();
        other.SectionId.ShouldBe("4");
        _fixture.Store.Saves.ShouldBe(0); // the platform saves with the inbox row
    }

    [Fact]
    public async Task AMovedSectionWithoutTheUnitsPathIsSkipped()
    {
        var content = Content(text: "One", sectionId: "3");
        _fixture.Store.Add(content);
        var handler = new SectionMovedHandler(_fixture.Store, _fixture.Clock, NullLogger<SectionMovedHandler>.Instance);

        await handler.HandleAsync(Envelope(SectionMovedHandler.Type, new { sectionId = 3, unitId = Desk }, null), Ct);

        content.SectionId.ShouldBe("3");
        content.UnitId.ShouldBeNull();
    }

    [Fact]
    public async Task AUnitMoveRebasesThePostsAndContentsBelowItAndForgetsTheUnit()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        post.ChangeUnit("workflow", Desk, DeskPath, _fixture.Clock.Now);
        var content = Content(text: "One");
        content.ChangeUnit("workflow", Desk, DeskPath, _fixture.Clock.Now);
        _fixture.Store.Add(content);
        var newParent = UnitPaths.Root(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));
        var newPath = UnitPaths.Child(newParent, Desk);
        var handler = new OrgUnitEventsHandler(_fixture.Store, _units, NullLogger<OrgUnitEventsHandler>.Instance);

        await handler.HandleAsync(Envelope(OrgEvents.UnitMoved,
            new UnitMoved(Desk, Guid.NewGuid(), PostCategoriesAndUnits.Root, Guid.NewGuid(), DeskPath, newPath, 2), newPath), Ct);

        post.UnitPath.ShouldBe(newPath);
        content.UnitPath.ShouldBe(newPath);
        _units.Forgotten.ShouldContain(Desk);
    }

    [Fact]
    public async Task TheLiveTopicCoversThePostsCategoriesAndUnitTree()
    {
        var post = Stored(PostBuilder.Draft(_fixture.Clock.Now));
        post.ChangeUnit("workflow", Desk, DeskPath, _fixture.Clock.Now);

        var scopes = await new PostLiveScopeResolver(_fixture.Store).ScopesAsync(PostLive.Kind, "7", Ct);

        scopes.ShouldBe(["press:daily", "category:politics", .. UnitScopes.For(DeskPath)]);
    }

    [Fact]
    public async Task AUnitGrantListsThePostsOfThatUnitAndBelow()
    {
        var mine = PostBuilder.Draft(_fixture.Clock.Now);
        mine.ChangeUnit("workflow", Desk, DeskPath, _fixture.Clock.Now);
        var elsewhere = PostBuilder.Draft(_fixture.Clock.Now);
        elsewhere.ChangeUnit("workflow", Multimedia, MultimediaPath, _fixture.Clock.Now);
        var none = PostBuilder.Draft(_fixture.Clock.Now);
        _fixture.Access.ScopesAsync(ContentPermissions.Publish, Arg.Any<CancellationToken>()).Returns([UnitScopes.Of(Desk)]);
        _fixture.Access.ScopesAsync(Arg.Is<string>(p => p != ContentPermissions.Publish), Arg.Any<CancellationToken>()).Returns([]);

        var filter = (await _fixture.PostAccess.ScopeFilterAsync(PostAccess.SeeingPermissions, Ct))!.Compile();

        new[] { mine, elsewhere, none }.Where(filter).ShouldBe([mine]);
    }

    private static ContentItem Content(string text, string? sectionId = null) =>
        ContentItem.Create("owner-1", "daily", "politics", null, Paragraph("", text), Pipeline.StartStage, TestClock.Start, sectionId);

    private static IntegrationEnvelope Envelope(string type, object data, string? unitPath) =>
        new(Guid.NewGuid(), type, "acme", "subject", TestClock.Start, "c", "test",
            JsonSerializer.SerializeToElement(data, IntegrationEnvelope.JsonOptions), UnitPath: unitPath);

    /// <summary>An in-memory unit directory.</summary>
    private sealed class FakeUnitLookup : IUnitLookup
    {
        private readonly Dictionary<Guid, UnitInfo> _units = [];
        public bool Down { get; set; }
        public List<Guid> Forgotten { get; } = [];

        public void Add(UnitInfo unit) => _units[unit.Id] = unit;

        public Task<UnitInfo?> FindAsync(Guid unitId, CancellationToken ct) =>
            Down ? throw new UnitLookupUnavailableException("down") : Task.FromResult(_units.GetValueOrDefault(unitId));

        public void Forget(Guid unitId) => Forgotten.Add(unitId);
    }
}
