using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using soft.square.net.WebApi.Contracts.Live;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.Web;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Live;

/// <summary>The live topic: the manifest, the scope resolver and the fan-out of committed feed rows.</summary>
public sealed class LivePostPublisherTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeContentStore _store = new();
    private readonly FakeLiveNotifier _live = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static T WithId<T, TId>(T entity, TId id) where T : EntityBase<TId> where TId : notnull
    {
        typeof(EntityBase<TId>).GetProperty(nameof(EntityBase<TId>.Id))!.SetValue(entity, id);
        return entity;
    }

    private static FeedEntry Row(long id, string type, int? postId, Guid? contentId = null) =>
        WithId(FeedEntry.From(new FeedEvent(type, postId, contentId, 1, null, null, null, "user-1", Now)), id);

    /// <summary>A stored post (id <paramref name="id"/>) in press daily, category politics, that uses these contents.</summary>
    private Post StorePost(int id, params Guid[] contents)
    {
        var category = Category.Create("politics", "daily", "Politics");
        var post = Post.Create("owner-1", category, $"Post {id}", "", Now, contents: [.. contents.Select(c => new ContentRef(c))]);
        _store.Posts.Add(WithId<Post, int>(post, id));
        return post;
    }

    private LivePostPublisher Publisher(ILiveNotifier? notifier)
    {
        var services = new ServiceCollection();
        if (notifier is not null) services.AddSingleton(notifier);
        return new LivePostPublisher(_store, services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public void TheManifestDeclaresThePostTopicOnce()
    {
        var manifest = new ContentModule().Manifest;

        var topic = manifest.LiveTopics.ShouldHaveSingleItem();
        topic.ShouldBeSameAs(PostLive.Topic);
        topic.Kind.ShouldBe("post");
        manifest.Permissions.Select(p => p.Name).ShouldContain(p => topic.Permissions.Contains(p));
        topic.Permissions.ShouldBeSubsetOf(manifest.Permissions.Select(p => p.Name));
        PostLive.TopicOf(42).ShouldBe("content:post:42");
    }

    /// <summary>Any one of them is enough: the authors', and every permission that shows other people's posts.</summary>
    [Fact]
    public void ThePostTopicTakesTheAuthorRightOrAnySeeingRight()
    {
        PostLive.Topic.Permissions.ShouldBe([ContentPermissions.Author, .. PostAccess.SeeingPermissions]);
        PostLive.Topic.Permissions.ShouldContain(ContentPermissions.Publish); // the Desk
        PostLive.Topic.Permissions.ShouldContain(ContentPermissions.Return);  // the shift manager and the Desk
        PostLive.Topic.Permissions.ShouldContain(ContentPermissions.Editor);
        PostLive.Topic.Permissions.Distinct().Count().ShouldBe(PostLive.Topic.Permissions.Count);
    }

    [Fact]
    public async Task APostRowNudgesItsPostWithoutASequence()
    {
        await Publisher(_live).CommittedAsync([Row(7, FeedEventTypes.PostChanged, 42)], Ct);

        var sent = _live.Messages.ShouldHaveSingleItem();
        sent.Topic.ShouldBe("content:post:42");
        sent.Type.ShouldBe("post.changed");
        sent.Sequence.ShouldBeNull();
        sent.Payload!.Value.GetProperty("eventId").GetInt64().ShouldBe(7);
        sent.Payload.Value.GetProperty("type").GetString().ShouldBe(FeedEventTypes.PostChanged);
        sent.Payload.Value.TryGetProperty("contentId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AContentRowFansOutToEveryPostThatUsesIt()
    {
        var shared = Guid.NewGuid();
        var other = Guid.NewGuid();
        StorePost(1, shared);
        StorePost(2, shared, other);
        StorePost(3, other);

        await Publisher(_live).CommittedAsync([Row(10, FeedEventTypes.ContentChanged, null, shared)], Ct);

        _live.Messages.Select(m => m.Topic).OrderBy(t => t).ShouldBe(["content:post:1", "content:post:2"]);
        _live.Messages.ShouldAllBe(m => m.Payload!.Value.GetProperty("contentId").GetGuid() == shared);
        _live.Messages.ShouldAllBe(m => m.Payload!.Value.GetProperty("eventId").GetInt64() == 10);
    }

    [Fact]
    public async Task ARowWithBothIdsGoesToItsPostOnly()
    {
        var shared = Guid.NewGuid();
        StorePost(1, shared);
        StorePost(2, shared);

        await Publisher(_live).CommittedAsync([Row(11, FeedEventTypes.ContentAttached, 2, shared)], Ct);

        _live.Messages.ShouldHaveSingleItem().Topic.ShouldBe("content:post:2");
    }

    [Fact]
    public async Task AContentNoPostUsesSendsNothing()
    {
        await Publisher(_live).CommittedAsync([Row(12, FeedEventTypes.ContentCreated, null, Guid.NewGuid())], Ct);

        _live.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutALiveNotifierNothingIsSent()
    {
        await Should.NotThrowAsync(() => Publisher(null).CommittedAsync([Row(13, FeedEventTypes.PostChanged, 42)], Ct));
    }

    [Fact]
    public async Task AFailedPublishIsSwallowedAndTheOthersAreStillSent()
    {
        _live.FailOn = "content:post:1";

        await Should.NotThrowAsync(() => Publisher(_live).CommittedAsync(
            [Row(14, FeedEventTypes.PostChanged, 1), Row(15, FeedEventTypes.PostChanged, 2)], Ct));

        _live.Messages.ShouldHaveSingleItem().Topic.ShouldBe("content:post:2");
    }

    [Fact]
    public async Task TheResolverGivesThePostsPressAndCategory()
    {
        StorePost(5);
        var resolver = new PostLiveScopeResolver(_store);

        (await resolver.ScopesAsync("post", "5", Ct)).ShouldBe(["press:daily", "category:politics"]);
        (await resolver.ScopesAsync("post", "6", Ct)).ShouldBeEmpty();
        (await resolver.ScopesAsync("post", "five", Ct)).ShouldBeEmpty();
        (await resolver.ScopesAsync("post", "-5", Ct)).ShouldBeEmpty();
        (await resolver.ScopesAsync("unit", "5", Ct)).ShouldBeEmpty();
    }
}
