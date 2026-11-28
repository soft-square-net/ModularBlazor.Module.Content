using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Root;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>
/// A post's root is its kind's: a new post gets one with its title as the headline, its values can be set afterwards, and a
/// post made from a title and a body only (<c>legacy.basic</c>) is upgraded that way without losing anything.
/// </summary>
public class PostRootHandlers
{
    private readonly PostFixture _fixture = new(PostBuilder.Owner);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private Task<Result<PostDetail>> CreateAsync(string kind, PostDocument? document = null)
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
            .Handle(new CreatePostCommand("politics", "A title", "A body", kind, document), Ct).AsTask();
    }

    private Task<Result<PostDetail>> SetRootAsync(Post post, int fieldVersion, object content) =>
        new ChangePostRootHandler(_fixture.Changes, _fixture.PostAccess, _fixture.Editors, _fixture.Kinds)
            .Handle(new ChangePostRootCommand(post.Id, post.Version, fieldVersion, Json(content), null), Ct).AsTask();

    /// <summary>A stored draft as Content made them before: its root is <c>legacy.basic</c>.</summary>
    private Post Legacy(string body = "A body")
    {
        var post = WithId(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", body, _fixture.Clock.Now, PostKinds.News), 7);
        _fixture.Stored(post);
        return post;
    }

    private static TranslatedText Text(string value) => new(Json(value), "hash");

    private void Translate(Post post, string key, string value) =>
        post.Translate(PostBuilder.Owner, "ar",
            new Dictionary<string, IReadOnlyDictionary<string, TranslatedText>>
            {
                [PostTranslationRules.RootId] = new Dictionary<string, TranslatedText> { [key] = Text(value) }
            }, _fixture.Clock.Now);

    [Fact]
    public async Task ANewPostGetsARootOfItsKindWithTheTitleAsItsHeadline()
    {
        _fixture.KindCatalog.Kinds.Add(new("post.review", "Review", "reviews", "Review", 3));

        var result = await CreateAsync("post.review");

        result.IsSuccess.ShouldBeTrue();
        result.Value.SchemaId.ShouldBe("post.review");
        result.Value.SchemaVersion.ShouldBe(3);
        result.Value.Document.RootText("headline").ShouldBe("A title");
        result.Value.Document.Content.EnumerateObject().Count().ShouldBe(1);
        result.Value.Body.ShouldBe("A body");
    }

    [Fact]
    public async Task AKindWithoutAHeadlineGetsAnEmptyRoot()
    {
        _fixture.KindCatalog.Values[PostKinds.News] = ["name"];

        var result = await CreateAsync(PostKinds.News);

        result.Value.SchemaId.ShouldBe(PostKinds.News);
        result.Value.Document.Content.EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public async Task ADocumentThatWasGivenIsKept()
    {
        var given = new PostDocument(PostKinds.News, 1, Json(new { headline = "Mine" }), Json(new { }), []);

        var result = await CreateAsync(PostKinds.News, given);

        result.Value.Document.RootText("headline").ShouldBe("Mine");
    }

    [Fact]
    public void AHeadlineThatIsTheTitleFollowsIt()
    {
        var post = Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", _fixture.Clock.Now, PostKinds.News,
            PostDocument.NewRoot(PostKinds.News, 1, true, "A title"));

        post.EditByOwner(PostBuilder.Owner, "Another title", "", _fixture.Clock.Now, TimeSpan.FromDays(1));

        post.Root.RootText("headline").ShouldBe("Another title");
    }

    [Fact]
    public void AHeadlineOfItsOwnStays()
    {
        var post = Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", _fixture.Clock.Now, PostKinds.News,
            PostDocument.NewRoot(PostKinds.News, 1, true, "The headline"));

        post.EditByOwner(PostBuilder.Owner, "Another title", "", _fixture.Clock.Now, TimeSpan.FromDays(1));

        post.Root.RootText("headline").ShouldBe("The headline");
    }

    [Fact]
    public async Task SettingTheValuesGivesALegacyPostItsKindsRootAndLosesNothing()
    {
        var post = Legacy();
        Translate(post, "title", "عنوان");

        var result = await SetRootAsync(post, 1, new { headline = "A title", description = "A summary" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.SchemaId.ShouldBe(PostKinds.News);
        result.Value.Document.RootText("description").ShouldBe("A summary");
        result.Value.Title.ShouldBe("A title");
        result.Value.Body.ShouldBe("A body");
        var translated = result.Value.Translations.ShouldHaveSingleItem().Nodes[PostTranslationRules.RootId];
        translated.Keys.ShouldBe(["headline"]);
        translated["headline"].Value.GetString().ShouldBe("عنوان");
    }

    [Fact]
    public async Task ABodyHeldOnlyByTheOldRootMovesToTheBody()
    {
        var legacy = PostDocument.FromLegacy("A title", "Only in the document");
        var post = WithId(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", _fixture.Clock.Now, PostKinds.News, legacy), 7);
        _fixture.Stored(post);

        var result = await SetRootAsync(post, 1, new { headline = "A title" });

        result.Value.Body.ShouldBe("Only in the document");
    }

    [Fact]
    public async Task ATranslationThatWouldBeLostRefusesTheUpgrade()
    {
        var post = Legacy();
        Translate(post, "body", "نص");

        var error = await Should.ThrowAsync<DomainException>(() => SetRootAsync(post, 1, new { headline = "A title" }));

        error.Message.ShouldContain("would be lost");
        post.Root.SchemaId.ShouldBe(PostDocument.LegacySchemaId);
        post.Translations.ShouldHaveSingleItem().Nodes[PostTranslationRules.RootId].Keys.ShouldBe(["body"]);
    }

    [Fact]
    public async Task AVersionTheKindHasNotPublishedIsInvalid()
    {
        var result = await SetRootAsync(Legacy(), 2, new { headline = "A title" });

        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task OnlyWhoMayEditThePostSetsItsValues()
    {
        var post = Legacy();
        _fixture.SignIn("someone-else");

        var result = await SetRootAsync(post, 1, new { headline = "A title" });

        result.Status.ShouldBe(ResultStatus.Forbidden);
        post.Root.SchemaId.ShouldBe(PostDocument.LegacySchemaId);
    }

    [Fact]
    public async Task WithoutDynamicFieldsTheValuesWait()
    {
        var post = Legacy();
        _fixture.KindCatalog.Down = true;

        var result = await SetRootAsync(post, 1, new { headline = "A title" });

        result.Status.ShouldBe(ResultStatus.Unavailable);
    }

    [Fact]
    public async Task ThePublicReadServesTheBodyBesideAKindsRoot()
    {
        var now = _fixture.Clock.Now;
        var post = WithId(Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "The story", now, PostKinds.News,
            PostDocument.NewRoot(PostKinds.News, 1, true, "A title")), 7);
        post.Submit(PostBuilder.Owner, now);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, now);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, PostBuilder.Shift, now);
        post.Approve(PostBuilder.Reviewer, now);
        post.Publish(PostBuilder.Desk, now, urlPrefix: "news");
        _fixture.Reader.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Post>>(), Arg.Any<CancellationToken>()).Returns(post);

        var result = await new GetPublishedPostHandler(_fixture.Reader).Handle(new GetPublishedPostQuery(post.PublicId), Ct);

        result.Value.Body.ShouldBe("The story");
        result.Value.Document.RootText("headline").ShouldBe("A title");
    }

    [Fact]
    public async Task TheKindsAreThePublishedOnesByTitle()
    {
        _fixture.KindCatalog.Kinds.Add(new("post.blog", "Blog post", "blog", "BlogPosting"));
        var handler = new ListPostKindsHandler(_fixture.Kinds, _fixture.PostAccess);

        var result = await handler.Handle(new ListPostKindsQuery(), Ct);

        result.Value.Select(k => k.Key).ShouldBe([PostKinds.Article, "post.blog", PostKinds.News]);

        _fixture.KindCatalog.Down = true;
        (await handler.Handle(new ListPostKindsQuery(), Ct)).Status.ShouldBe(ResultStatus.Unavailable);
    }
}
