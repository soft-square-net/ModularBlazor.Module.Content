using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate;

/// <summary>Wave 4 C3: a content's stage is its work-item state; the first built-in pipeline's keys still work.</summary>
public class WorkItemStages
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    private static List<ContentWorkItemChanged> WorkItems(ContentItem content) =>
        ((IHasDomainEvents)content).TakeDomainEvents().OfType<ContentWorkItemChanged>().ToList();

    [Fact]
    public void TheDefaultPipelineHasTheAgreedStages()
    {
        Default.Stages.Select(s => s.Key).ShouldBe(ContentWorkItems.States.All);
        Default.StartStage.ShouldBe("assigned");
        PipelineStageKeys.Legacy.ShouldBe(ContentWorkItems.States.Legacy);
        Default.Validate();
    }

    [Fact]
    public void TheWholeWayThroughThePipeline()
    {
        var content = Item();
        content.Act(PostBuilder.Owner, Default.Action("start"), null, Now);
        content.Act(PostBuilder.Owner, Default.Action("submit"), null, Now);
        content.Act(PostBuilder.Desk, Default.Action("review"), null, Now);
        content.Act(PostBuilder.Desk, Default.Action("return"), "A source, please", Now);
        content.Act(PostBuilder.Owner, Default.Action("submit"), null, Now);
        content.Act(PostBuilder.Desk, Default.Action("accept"), null, Now);

        content.Stage.ShouldBe("accepted");
        content.AcceptedRevision.ShouldBe(1);
        content.History.Select(h => h.Stage).ShouldBe(["assigned", "in-progress", "submitted", "in-review", "returned", "submitted", "accepted"]);
    }

    [Fact]
    public void EveryMoveIsAWorkItemStateChange()
    {
        var content = Item("A headline");
        WorkItems(content).ShouldBeEmpty(); // nobody assigned at creation

        content.Act(PostBuilder.Owner, Default.Action("submit"), null, Now);

        var change = WorkItems(content).ShouldHaveSingleItem();
        change.Change.ShouldBe(ContentWorkItemChange.StateChanged);
        change.PreviousStage.ShouldBe("assigned");
        change.Stage.ShouldBe("submitted");
        change.Title.ShouldBe("A headline");
        change.ContentId.ShouldBe(content.Id);
    }

    [Fact]
    public void AnAssignmentIsAWorkItemAssignment()
    {
        var content = Item(creator: "writer");
        WorkItems(content);

        content.Assign("manager", Section, "photographer", Now);
        content.Assign("manager", Section, "editor", Now);

        WorkItems(content).Select(c => (c.Change, c.PreviousAssigneeId, c.AssigneeId)).ShouldBe(
        [
            (ContentWorkItemChange.Assigned, (string?)null, (string?)"photographer"),
            (ContentWorkItemChange.Assigned, "photographer", "editor")
        ]);
    }

    [Fact]
    public void AContentMadeForSomeoneIsAssignedAtOnce()
    {
        var content = ContentItem.Create("manager", "daily", "politics", null, Paragraph("", "x"), Default.StartStage, Now, assigneeId: "writer");

        var change = WorkItems(content).ShouldHaveSingleItem();
        change.Change.ShouldBe(ContentWorkItemChange.Assigned);
        change.AssigneeId.ShouldBe("writer");
        change.Stage.ShouldBe("assigned");
    }

    [Fact]
    public void TheTitleComesFromTheValues()
    {
        Item("  Two   words ").Title.ShouldBe("Two words");
        var long_ = Item(new string('x', 300)).Title;
        long_.Length.ShouldBe(ContentItem.TitleLength);
        long_.ShouldEndWith("…");
        ContentItem.Create("w", "daily", "politics", null, Paragraph("n1", "") with { Content = Json("{}") }, "assigned", Now)
            .Title.ShouldBe("block.paragraph n1");
        ContentTitles.Of(Paragraph("n", "") with { Content = Json("""{ "src": "a.jpg", "caption": { "en": "", "ar": "صورة" } }""") },
            "block.image", "n", 50).ShouldBe("صورة");
    }

    [Fact]
    public void AnOldDefinitionGetsTodaysStageKeys()
    {
        var old = new PipelineDefinition("newsroom", "draft",
        [
            new("draft", StageKind.Work, ["assignee"]),
            new("desk", StageKind.Review, ["content.publish"]),
            new("accepted", StageKind.Accepted, ["content.publish"], OnEdit: "desk"),
            new("rejected", StageKind.Work, ["assignee"])
        ],
        [
            new("submit", ["draft", "rejected"], "desk", ["assignee"]),
            new("accept", ["desk"], "accepted", ["content.publish"], Pins: true),
            new("reject", ["desk", "accepted"], "rejected", ["content.publish"], RequiresNote: true)
        ]);

        var renamed = old.WithCurrentStageKeys();

        renamed.StartStage.ShouldBe("assigned");
        renamed.Stages.Select(s => s.Key).ShouldBe(["assigned", "submitted", "accepted", "returned"]);
        renamed.Stage("accepted").OnEdit.ShouldBe("submitted");
        renamed.Action("submit").From.ShouldBe(["assigned", "returned"]);
        renamed.Action("reject").To.ShouldBe("returned"); // its own action keeps its key
        renamed.Validate();
    }

    [Fact]
    public void ALegacyKeyStaysWhenItsNewKeyIsTaken()
    {
        var custom = Default with
        {
            Stages = [.. Default.Stages, new PipelineStage("draft", StageKind.Work, ["assignee"])]
        };

        custom.WithCurrentStageKeys().Stages.Select(s => s.Key).ShouldContain("draft");
        Default.WithCurrentStageKeys().ShouldBeSameAs(Default);
    }

    [Fact]
    public void TheOldRejectFindsReturn()
    {
        Default.FindAction("reject")!.Key.ShouldBe("return");
        Default.FindAction("launch").ShouldBeNull();
    }

    [Fact]
    public void TheWorkItemReferenceRoundTrips()
    {
        var id = Guid.NewGuid();

        ContentWorkItems.RefOf(id).ShouldBe($"content:content:{id:D}");
        ContentWorkItems.ContentIdOf(ContentWorkItems.RefOf(id)).ShouldBe(id);
        ContentWorkItems.ContentIdOf("newsroom:post-project:7").ShouldBeNull();
        ContentLiveTopic.Of(42).ShouldBe("content:post:42");
    }
}
