using System.Text.RegularExpressions;
using soft.square.net.Module.Content;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Client;

/// <summary>The Contents Management pages: how kinds and units are named, and the patterns their inputs use.</summary>
public partial class ContentPagesTests
{
    private static string Arabic(string text) => "ar:" + text;

    [Theory]
    [InlineData("post.review", "Review", "ar:Review")]
    [InlineData("post.blog", "Blog post", "ar:Blog post")]
    [InlineData("post.news", null, "ar:News")] // the kinds could not be read
    [InlineData("post.news", "أخبار عاجلة", "أخبار عاجلة")] // the tenant renamed it
    [InlineData("post.recipe", "Recipe", "Recipe")] // a kind of the tenant's own
    [InlineData("post.recipe", null, "post.recipe")]
    public void AKindIsNamedInTheUiLanguageWhereTheModuleKnowsIt(string kind, string? title, string expected) =>
        ContentNames.Kind(kind, title, Arabic).ShouldBe(expected);

    [Fact]
    public void TheFallbackKindsHaveTheStarterLibrarysEightRoots() =>
        ContentNames.StarterKinds.Select(k => k.Key).ShouldBe(["post.article", "post.news", "post.review", "post.blog",
            "post.video", "post.reportage", "post.photo-story", "post.other"]);

    [Fact]
    public void AUnitShowsItsNameElseTheStartOfItsId()
    {
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        ContentNames.Unit(id, "Politics", "السياسة", "ar").ShouldBe("السياسة");
        ContentNames.Unit(id, "Politics", "السياسة", "en").ShouldBe("Politics");
        ContentNames.Unit(id, "Politics", "", "ar").ShouldBe("Politics");
        ContentNames.Unit(id, null, null, "en").ShouldBe("0f8fad5b");
    }

    [GeneratedRegex("pattern=\"([^\"]*)\"")]
    private static partial Regex PatternAttribute();

    /// <summary>
    /// Browsers compile an input's pattern with the <c>v</c> flag, where an unescaped '-' that ends a character class is a
    /// syntax error: the browser then logs it and validates nothing.
    /// </summary>
    [Fact]
    public void InputPatternsEscapeAHyphenInACharacterClass()
    {
        var pages = Directory.GetFiles(Path.Combine(Root(), "src", "Module.Content", "Pages"), "*.razor");
        var patterns = pages.SelectMany(p => PatternAttribute().Matches(File.ReadAllText(p))).Select(m => m.Groups[1].Value).ToList();

        patterns.ShouldNotBeEmpty();
        foreach (var pattern in patterns) Regex.IsMatch(pattern, @"(?<!\\)-\]").ShouldBeFalse(pattern);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
