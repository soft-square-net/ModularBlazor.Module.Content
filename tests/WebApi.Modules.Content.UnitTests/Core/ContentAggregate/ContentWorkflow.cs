using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate;

public class ContentWorkflow
{
    private static readonly DateTimeOffset Now = TestClock.Start;
    private static PipelineAction Action(string key) => Default.Action(key);

    private static IReadOnlyList<FeedEvent> Events(ContentItem content) => ((IHasDomainEvents)content).TakeDomainEvents().OfType<FeedEvent>().ToList();

    [Fact]
    public void ANewContentIsRevisionOneInTheStartStage()
    {
        var content = Item();

        content.Revision.ShouldBe(1);
        content.Version.ShouldBe(0);
        content.Stage.ShouldBe("draft");
        content.FieldKey.ShouldBe("block.paragraph");
        content.NodeId.ShouldStartWith("c-");
        content.Revisions.ShouldHaveSingleItem().Revision.ShouldBe(1);
        Events(content).Single().Type.ShouldBe(FeedEventTypes.ContentCreated);
    }

    [Fact]
    public void AnEditAddsARevisionAndKeepsTheNodeId()
    {
        var content = Item();
        var nodeId = content.NodeId;
        Events(content);

        content.Edit(PostBuilder.Owner, Paragraph("other", "Two"), Stage(content), Now).ShouldBeTrue();

        content.Revision.ShouldBe(2);
        content.Version.ShouldBe(1);
        content.NodeId.ShouldBe(nodeId);
        content.Node.Content.GetProperty("text").GetString().ShouldBe("Two");
        Events(content).Single().Type.ShouldBe(FeedEventTypes.ContentChanged);
    }

    [Fact]
    public void TheSameValuesAreNoEdit()
    {
        var content = Item("One");

        content.Edit(PostBuilder.Owner, Paragraph("x", "One"), Stage(content), Now).ShouldBeFalse();

        content.Revision.ShouldBe(1);
        content.Version.ShouldBe(0);
    }

    [Fact]
    public void AcceptPinsTheCurrentRevision()
    {
        var content = Item();
        content.Act(PostBuilder.Owner, Action("submit"), null, Now);

        content.Act(PostBuilder.Desk, Action("accept"), null, Now);

        content.Stage.ShouldBe("accepted");
        content.AcceptedRevision.ShouldBe(1);
        content.Version.ShouldBe(2);
        content.History.Select(h => h.Action).ShouldBe(["created", "submit", "accept"]);
    }

    [Fact]
    public void RejectNeedsANoteAndKeepsIt()
    {
        var content = Item();
        content.Act(PostBuilder.Owner, Action("submit"), null, Now);

        Should.Throw<DomainException>(() => content.Act(PostBuilder.Desk, Action("reject"), " ", Now));
        content.Act(PostBuilder.Desk, Action("reject"), "Check the quote", Now);

        content.Stage.ShouldBe("rejected");
        content.ReviewNote.ShouldBe("Check the quote");
    }

    [Fact]
    public void AnActionMustStartFromTheCurrentStage() =>
        Should.Throw<DomainException>(() => Item().Act(PostBuilder.Desk, Action("accept"), null, Now));

    [Fact]
    public void EditingAnAcceptedContentSendsItBackToTheDeskAndKeepsThePin()
    {
        var content = Item();
        content.Act(PostBuilder.Owner, Action("submit"), null, Now);
        content.Act(PostBuilder.Desk, Action("accept"), null, Now);

        content.Edit(PostBuilder.Desk, Paragraph("", "Edited by Desk"), Stage(content), Now);

        content.Stage.ShouldBe("desk");
        content.Revision.ShouldBe(2);
        content.AcceptedRevision.ShouldBe(1);
    }

    [Fact]
    public void AnArchivedContentCannotBeEditedOrMoved()
    {
        var content = Item();
        content.Archive(PostBuilder.Desk, Now);

        content.IsArchived.ShouldBeTrue();
        Should.Throw<DomainException>(() => content.Edit(PostBuilder.Owner, Paragraph("", "x"), Stage(content), Now));
        Should.Throw<DomainException>(() => content.Act(PostBuilder.Owner, Action("submit"), null, Now));
    }

    [Fact]
    public void TheAssigneeOrElseTheCreatorMatches()
    {
        var content = Item(creator: "writer");

        content.Matches(PipelineDefinition.Assignee, "writer").ShouldBeTrue();
        content.Assign("manager", ContentBuilder.Section, "photographer", Now);
        content.Matches(PipelineDefinition.Assignee, "writer").ShouldBeFalse();
        content.Matches(PipelineDefinition.Assignee, "photographer").ShouldBeTrue();
        content.Matches(PipelineDefinition.Creator, "writer").ShouldBeTrue();
        content.SectionId.ShouldBe(ContentBuilder.Section);
    }
}

public class PipelineValidation
{
    [Fact]
    public void TheDefaultIsValid() => PipelineDefinition.Default.Validate();

    [Fact]
    public void FindsTheSubmitAndAcceptActions()
    {
        var pipeline = PipelineDefinition.Default;

        pipeline.SubmitFrom("draft")!.Key.ShouldBe("submit");
        pipeline.SubmitFrom("rejected")!.Key.ShouldBe("submit");
        pipeline.AcceptFrom("desk")!.Key.ShouldBe("accept");
        pipeline.AcceptFrom("draft").ShouldBeNull();
    }

    [Fact]
    public void RefusesAnActionToAnUnknownStage() =>
        Should.Throw<DomainException>(() => (PipelineDefinition.Default with
        {
            Actions = [.. PipelineDefinition.Default.Actions, new PipelineAction("send", ["draft"], "nowhere", ["assignee"])]
        }).Validate());

    [Fact]
    public void RefusesAPinThatDoesNotLeadToAnAcceptedStage() =>
        Should.Throw<DomainException>(() => (PipelineDefinition.Default with
        {
            Actions = [new PipelineAction("accept", ["desk"], "draft", ["content.publish"], Pins: true)]
        }).Validate());

    [Fact]
    public void RefusesAStartThatIsNotAWorkStage() =>
        Should.Throw<DomainException>(() => (PipelineDefinition.Default with { StartStage = "desk" }).Validate());

    [Fact]
    public void RefusesAnActionNobodyMayTake() =>
        Should.Throw<DomainException>(() => (PipelineDefinition.Default with
        {
            Actions = [new PipelineAction("submit", ["draft"], "desk", [])]
        }).Validate());
}
