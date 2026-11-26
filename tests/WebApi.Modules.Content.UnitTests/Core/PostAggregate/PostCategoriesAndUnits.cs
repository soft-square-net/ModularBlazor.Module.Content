using soft.square.net.Organization.Contracts;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.Common;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;

/// <summary>A post's categories (all of its press, D4) and unit, and a content's unit and legacy section.</summary>
public class PostCategoriesAndUnits
{
    private static readonly DateTimeOffset Now = TestClock.Start;
    public static readonly Guid Root = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid Desk = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static string DeskPath => UnitPaths.Child(UnitPaths.Root(Root), Desk);

    private static Category Economy() => Category.Create("economy", "daily", "Economy", "الاقتصاد");
    private static Category OtherPress() => Category.Create("weekly-sport", "weekly", "Sport");

    [Fact]
    public void ANewPostIsInItsPrimaryCategoryAndTheOthersOfItsPress()
    {
        var post = Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "A body", Now, otherCategories: [Economy(), PostBuilder.Category()]);

        post.PrimaryCategoryId.ShouldBe("politics");
        post.Categories.ShouldBe(["politics", "economy"]);
        post.Scopes.ShouldBe(["press:daily", "category:politics", "category:economy"]);
    }

    [Fact]
    public void APostsCategoriesAreAllOfItsPress()
    {
        Should.Throw<DomainException>(() => Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A title", "A body", Now, otherCategories: [OtherPress()]))
            .Message.ShouldContain("all of its press");
        var post = PostBuilder.Draft(Now);
        Should.Throw<DomainException>(() => post.ChangeCategories(PostBuilder.Owner, OtherPress(), [], Now));
    }

    [Fact]
    public void ChangingTheCategoriesIsAVersionThatKeepsTheScheduleValid()
    {
        var post = PostBuilder.Approved(Now);
        post.Schedule(PostBuilder.Desk, Now.AddHours(1), Now);
        var version = post.Version;

        post.ChangeCategories(PostBuilder.Desk, Economy(), [PostBuilder.Category()], Now).ShouldBeTrue();

        post.Version.ShouldBe(version + 1);
        post.PrimaryCategoryId.ShouldBe("economy");
        post.Categories.ShouldBe(["economy", "politics"]);
        post.IsDue(Now.AddHours(2)).ShouldBeTrue();
        post.History[^1].Action.ShouldBe("categories changed");
        post.ChangeCategories(PostBuilder.Desk, Economy(), [PostBuilder.Category()], Now).ShouldBeFalse();
    }

    [Fact]
    public void AUnitAddsItsScopeAndItsAncestorsAsOrganizationWritesThem()
    {
        var post = PostBuilder.Draft(Now);

        post.ChangeUnit("workflow", Desk, DeskPath, Now).ShouldBeTrue();

        post.UnitId.ShouldBe(Desk);
        post.Scopes.ShouldBe(["press:daily", "category:politics", .. UnitScopes.For(DeskPath)]);
        UnitPathRules.Scopes(DeskPath).ShouldBe(UnitScopes.For(DeskPath));
        UnitPathRules.Scope(Desk).ShouldBe(UnitScopes.Of(Desk));
        post.ChangeUnit("workflow", Desk, DeskPath, Now).ShouldBeFalse();
        post.ChangeUnit("workflow", null, null, Now).ShouldBeTrue();
        post.Scopes.ShouldBe(["press:daily", "category:politics"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/not-a-path")]
    [InlineData("/aaaaaaaa000000000000000000000001")] // another unit's path
    public void AUnitNeedsItsOwnPath(string path)
    {
        var post = PostBuilder.Draft(Now);

        Should.Throw<DomainException>(() => post.ChangeUnit("workflow", Desk, path, Now));
    }

    [Fact]
    public void AMoveRebasesTheUnitPathsAtAndBelowTheUnitOnly()
    {
        var post = PostBuilder.Draft(Now);
        post.ChangeUnit("workflow", Desk, DeskPath, Now);
        var version = post.Version;
        var newRoot = UnitPaths.Root(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));
        var moved = UnitPaths.Child(newRoot, Desk);

        post.RebaseUnit(UnitPaths.Root(Guid.NewGuid()), newRoot).ShouldBeFalse();
        post.RebaseUnit(UnitPaths.Root(Root), newRoot).ShouldBeTrue();

        post.UnitPath.ShouldBe(moved);
        post.Version.ShouldBe(version);
    }

    [Fact]
    public void ACategoryWithoutAnArabicNameTakesTheEnglishOne()
    {
        var category = Category.Create("sport", "daily", "Sport");

        category.NameEn.ShouldBe("Sport");
        category.NameAr.ShouldBe("Sport");
        Economy().NameIn("ar-EG").ShouldBe("الاقتصاد");
        Economy().NameIn("en").ShouldBe("Economy");
    }

    [Fact]
    public void AContentOfASectionHasTheSectionScopeUntilItsSectionMoves()
    {
        var content = Content(sectionId: "3");
        content.Scopes.ShouldContain("section:3");

        content.RemapSection("4", Desk, DeskPath, Now).ShouldBeFalse();
        content.RemapSection("3", Desk, DeskPath, Now).ShouldBeTrue();

        content.SectionId.ShouldBeNull();
        content.UnitId.ShouldBe(Desk);
        content.Scopes.ShouldBe(["press:daily", "category:politics", .. UnitScopes.For(DeskPath), $"section:{UnitPaths.Id(Desk)}"]);
        content.RemapSection("3", Desk, DeskPath, Now).ShouldBeFalse();
    }

    [Fact]
    public void AContentOfAUnitDropsALegacySection()
    {
        var content = Content(sectionId: "3");
        content.ChangeUnit("workflow", Desk, DeskPath, Now).ShouldBeTrue();
        content.SectionId.ShouldBeNull();

        content.Assign("manager", "5", "editor-1", Now);

        content.SectionId.ShouldBeNull();
        content.AssigneeId.ShouldBe("editor-1");
    }

    private static ContentItem Content(string? sectionId) =>
        ContentItem.Create("owner-1", "daily", "politics", null, Paragraph("n1", "One"), "assigned", Now, sectionId);
}
