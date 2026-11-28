using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;

public class PostTranslations
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>A news document: a headline and a section choice, one image (img1) with a caption and topics.</summary>
    private static PostDocument Document(string headline = "Final day at the venue", string caption = "The venue") => new(
        "post.news", 1,
        Json($$"""{ "headline": "{{headline}}", "section": "sports" }"""), Json("{}"),
        [new PostNode("img1", PostKinds.Article, "block.image", 1,
            Json($$"""{ "asset": "asset_21", "caption": "{{caption}}", "tags": ["final", "venue"] }"""), Json("{}"), [])]);

    private static Dictionary<string, IReadOnlyDictionary<string, TranslatedText>> Arabic(params (string Node, string Key, string Json)[] values) =>
        values.GroupBy(v => v.Node).ToDictionary(g => g.Key,
            g => (IReadOnlyDictionary<string, TranslatedText>)g.ToDictionary(v => v.Key, v => new TranslatedText(Json(v.Json), "hash")));

    private static Post Draft()
    {
        var post = Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "A body", Now, document: Document());
        return post;
    }

    [Fact]
    public void ATranslationIsKeptWithoutChangingTheState()
    {
        var post = Draft();

        post.Translate(PostBuilder.Owner, "en-GB", Arabic(("$root", "headline", "\"Last day\""), ("img1", "tags", "[\"a\", \"b\"]")), Now);

        post.State.ShouldBe(PostState.Draft);
        post.Version.ShouldBe(1);
        post.History.Last().Action.ShouldBe("translated");
        post.Translations.ShouldHaveSingleItem().Language.ShouldBe("en-GB");
        post.Translations[0].Nodes["img1"]["tags"].Value.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void TranslatingAgainReplacesThatLanguage()
    {
        var post = Draft();
        post.Translate(PostBuilder.Owner, "ar", Arabic(("$root", "headline", "\"أ\"")), Now);
        post.Translate(PostBuilder.Owner, "AR", Arabic(("$root", "headline", "\"ب\"")), Now);

        post.Translations.ShouldHaveSingleItem().Nodes["$root"]["headline"].Value.GetString().ShouldBe("ب");
    }

    [Theory]
    [InlineData("not a tag", "$root", "headline", "\"x\"", "BCP 47")]
    [InlineData("ar", "gone", "caption", "\"x\"", "no node")]
    [InlineData("ar", "$root", "section", "1", "not empty")] // a number is not the shape of a text
    [InlineData("ar", "$root", "byline", "\"x\"", "no text value")]
    [InlineData("ar", "$root", "headline", "\"  \"", "not empty")]
    [InlineData("ar", "img1", "tags", "\"one\"", "list of texts")]
    public void AnOverlayMustFitTheDocument(string language, string node, string key, string json, string message)
    {
        var nodes = Arabic((node, key, json));
        var error = Should.Throw<DomainException>(() => Draft().Translate(PostBuilder.Owner, language, nodes, Now));
        error.Message.ShouldContain(message);
    }

    [Fact]
    public void EditingTheDocumentPrunesWhatItNoLongerHas()
    {
        var post = Draft();
        post.Translate(PostBuilder.Owner, "en", Arabic(("$root", "headline", "\"H\""), ("img1", "caption", "\"C\"")), Now);

        var withoutImage = Document("A new headline") with { Children = [] };
        post.EditByOwner(PostBuilder.Owner, "A title", "A body", Now, TimeSpan.FromMinutes(10), withoutImage);

        var en = post.Translations.ShouldHaveSingleItem();
        en.Nodes.Keys.ShouldBe(["$root"]); // the image is gone; the headline's translation stays (stale, by its hash)
    }

    [Fact]
    public void PublishingTakesTheTranslationsAndLaterOnesGoLiveAtOnce()
    {
        var post = Draft();
        post.Translate(PostBuilder.Owner, "en", Arabic(("$root", "headline", "\"Last day\"")), Now);
        post.Submit(PostBuilder.Owner, Now);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, Now);
        post.Assign(PostBuilder.Manager, PostBuilder.Reviewer, Now);
        post.Approve(PostBuilder.Reviewer, Now);
        post.Publish(PostBuilder.Desk, Now);

        post.PublishedTranslations.ShouldHaveSingleItem().Language.ShouldBe("en");

        post.Translate(PostBuilder.Desk, "fr", Arabic(("$root", "headline", "\"Dernier jour\"")), Now);
        post.State.ShouldBe(PostState.Published);
        post.PublishedTranslations.Select(t => t.Language).ShouldBe(["en", "fr"]);

        post.RemoveTranslation(PostBuilder.Desk, "en", Now);
        post.PublishedTranslations.Select(t => t.Language).ShouldBe(["fr"]);
    }

    [Fact]
    public void ApplyServesTheChainThenTheDocument()
    {
        var document = Document();
        var egyptian = new PostTranslation("ar-EG", Arabic(("img1", "caption", "\"الاستاد\"")), Now, "t");
        var arabic = new PostTranslation("ar", Arabic(("$root", "headline", "\"اليوم الأخير\""), ("img1", "caption", "\"الملعب\"")), Now, "t");

        var chain = PostTranslationRules.Chain("ar-EG", ["en", "ar", "ar-EG"]);
        chain.ShouldBe(["ar-EG", "ar"]);
        var merged = PostTranslationRules.Apply(document, [egyptian, arabic]);

        merged.Content.GetProperty("headline").GetString().ShouldBe("اليوم الأخير");
        merged.Content.GetProperty("section").GetString().ShouldBe("sports");
        merged.Children[0].Content.GetProperty("caption").GetString().ShouldBe("الاستاد");
        merged.Children[0].Content.GetProperty("asset").GetString().ShouldBe("asset_21");
        PostTranslationRules.Apply(document, []).ShouldBeSameAs(document);
    }

    [Fact]
    public void RemovingAMissingTranslationIsRefused() =>
        Should.Throw<DomainException>(() => Draft().RemoveTranslation(PostBuilder.Owner, "en", Now));
}
