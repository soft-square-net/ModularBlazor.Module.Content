using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;

/// <summary>Post kinds are DynamicFields root field keys; the old enum names are read as their keys.</summary>
public class PostKindsAsKeys
{
    private static readonly DateTimeOffset Now = TestClock.Start;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Article", "post.article")]
    [InlineData("news", "post.news")]
    [InlineData("VIDEO", "post.video")]
    [InlineData("Reportage", "post.reportage")]
    [InlineData("PhotoStory", "post.photo-story")]
    [InlineData("Other", "post.other")]
    [InlineData("2", "post.video")]
    [InlineData(" post.review ", "post.review")]
    [InlineData("post.news", "post.news")]
    public void OldEnumNamesAndNumbersBecomeKeys(string kind, string key) => PostKinds.Normalize(kind).ShouldBe(key);

    [Theory]
    [InlineData("")]
    [InlineData("Post.News")]
    [InlineData("news!")]
    [InlineData("9")]
    public void ARequiredKindMustBeAKey(string kind) =>
        Should.Throw<DomainException>(() => PostKinds.Require(kind)).Message.ShouldContain("not a post kind");

    [Fact]
    public void APostIsCreatedWithTheKeyOfAnOldName()
    {
        var post = Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "", Now, "Reportage");

        post.PostKind.ShouldBe(PostKinds.Reportage);
    }

    [Fact]
    public void AStoredNodeWithAnOldNameIsReadWithItsKey()
    {
        var node = JsonSerializer.Deserialize<PostNode>(
            """{ "nodeId": "n1", "postKind": "PhotoStory", "schemaId": "block.paragraph", "schemaVersion": 1, "content": {}, "style": {}, "children": [] }""", Web)!;

        node.PostKind.ShouldBe(PostKinds.PhotoStory);
        JsonSerializer.Serialize(node, Web).ShouldContain("\"postKind\":\"post.photo-story\"");
    }

    [Fact]
    public void ANodeWithoutAKindIsAnArticleAsBefore()
    {
        var node = JsonSerializer.Deserialize<PostNode>(
            """{ "nodeId": "n1", "schemaId": "block.paragraph", "schemaVersion": 1, "content": {}, "style": {}, "children": [] }""", Web)!;

        node.PostKind.ShouldBe(PostKinds.Article);
    }

    [Fact]
    public void ANodeOfAnyRootFieldKeyIsValid()
    {
        var node = JsonSerializer.Deserialize<PostNode>(
            """{ "nodeId": "n1", "postKind": "post.live-blog", "schemaId": "block.paragraph", "schemaVersion": 1, "content": {}, "style": {}, "children": [] }""", Web)!;

        Should.NotThrow(() => PostDocumentRules.Validate(node));
    }

    [Fact]
    public void APublishedPostKeepsItsPrefixAndHasACanonicalPath()
    {
        var post = PostBuilder.Approved(Now);

        post.Publish(PostBuilder.Desk, Now, urlPrefix: "ar/news");

        post.PublishedUrlPrefix.ShouldBe("ar/news");
        post.CanonicalPath().ShouldBe($"/ar/news/{post.StablePublicId}");
    }

    [Fact]
    public void TheCanonicalPathUsesTheSlugOfTheCulture()
    {
        var post = PostBuilder.Approved(Now);
        post.Edit(PostEditor.Desk, PostBuilder.Desk, Now, TimeSpan.Zero,
            new PostChange(Slugs: new Dictionary<string, string> { ["en"] = "final-day", ["ar"] = "al-yawm" }));
        post.Publish(PostBuilder.Desk, Now, urlPrefix: "news");

        post.CanonicalPath(["en-GB", "en"]).ShouldBe("/news/final-day");
        post.CanonicalPath().ShouldBe("/news/al-yawm"); // the first culture, in ordinal order
    }

    [Fact]
    public void ADraftOrAPostPublishedWithoutAPrefixHasNoCanonicalPath()
    {
        PostBuilder.Approved(Now).CanonicalPath().ShouldBeNull();
        var post = PostBuilder.Approved(Now);
        post.Publish(PostBuilder.Desk, Now);
        post.CanonicalPath().ShouldBeNull();
    }

    [Fact]
    public void ABadPrefixIsRefused() =>
        Should.Throw<DomainException>(() => PostBuilder.Approved(Now).Publish(PostBuilder.Desk, Now, urlPrefix: "/News/"));
}
