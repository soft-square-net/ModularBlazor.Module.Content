using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;

public class PostWorkflow
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    [Fact]
    public void CreatesADraftInTheCategorysPress()
    {
        var post = PostBuilder.Draft(Now);

        post.State.ShouldBe(PostState.Draft);
        post.OwnerId.ShouldBe(PostBuilder.Owner);
        post.PressId.ShouldBe("daily");
        post.Scopes.ShouldBe(["press:daily", "category:politics"]);
        post.Version.ShouldBe(0);
        post.History.Single().Action.ShouldBe("created");
    }

    [Fact]
    public void RejectsAnEmptyTitle() =>
        Should.Throw<DomainException>(() => Post.Create(PostBuilder.Owner, PostBuilder.Category(), " ", "body", Now));

    [Fact]
    public void EveryChangeBumpsTheVersionAndRecordsHistory()
    {
        var post = PostBuilder.Approved(Now);

        post.Version.ShouldBe(4); // submit, accept, assign, approve
        post.History.Select(e => e.Action).ShouldBe(["created", "submitted", "accepted", "assigned", "approved"]);
        post.ApprovedVersion.ShouldBe(4);
    }

    [Fact]
    public void OnlyTheOwnerSubmits() =>
        Should.Throw<DomainException>(() => PostBuilder.Draft(Now).Submit("someone-else", Now));

    [Fact]
    public void OwnerMayEditASubmissionOnlyWithinTheWindow()
    {
        var post = PostBuilder.Submitted(Now);
        var window = TimeSpan.FromHours(24);

        post.OwnerMayEdit(Now.AddHours(23), window).ShouldBeTrue();
        post.OwnerMayEdit(Now.AddHours(25), window).ShouldBeFalse();
        Should.Throw<DomainException>(() => post.EditByOwner(PostBuilder.Owner, "New", "body", Now.AddHours(25), window));
    }

    [Fact]
    public void AcceptingLocksTheOwnerOut()
    {
        var post = PostBuilder.Submitted(Now);
        post.Accept(PostBuilder.Manager, PostBuilder.Shift, Now);

        post.OwnerMayEdit(Now, TimeSpan.FromHours(24)).ShouldBeFalse();
    }

    [Fact]
    public void OnlyTheAssignedReviewerApproves() =>
        Should.Throw<DomainException>(() => PostBuilder.InReview(Now).Approve("another-reviewer", Now));

    [Fact]
    public void ADeskEditCancelsTheScheduleAndNeedsReapproval()
    {
        var post = PostBuilder.Approved(Now);
        post.Schedule(PostBuilder.Desk, Now.AddHours(2), Now);

        post.EditByDesk(PostBuilder.Desk, "Retitled", "body", Now);

        post.State.ShouldBe(PostState.AwaitingDeskReapproval);
        post.ScheduledFor.ShouldBeNull();
    }

    [Fact]
    public void SchedulingNeedsAFutureTime() =>
        Should.Throw<DomainException>(() => PostBuilder.Approved(Now).Schedule(PostBuilder.Desk, Now, Now));

    [Fact]
    public void PublishIfDuePublishesTheScheduledVersionWhenItsTimeComes()
    {
        var post = PostBuilder.Approved(Now);
        post.Schedule(PostBuilder.Desk, Now.AddHours(1), Now);

        post.PublishIfDue("workflow", Now.AddMinutes(30)).ShouldBeFalse();
        post.PublishIfDue("workflow", Now.AddHours(1)).ShouldBeTrue();

        post.State.ShouldBe(PostState.Published);
        post.PublishedBy.ShouldBe(PostBuilder.Desk);
        post.PublishedTitle.ShouldBe("A title");
    }

    [Fact]
    public void TransferSendsReviewWorkBackToTheQueue()
    {
        var post = PostBuilder.InReview(Now);

        post.Transfer(PostBuilder.Manager, null, Now);

        post.State.ShouldBe(PostState.Accepted);
        post.AssigneeId.ShouldBeNull();
        post.TransferredFromShift.ShouldBe(PostBuilder.Shift);
    }

    [Fact]
    public void ADraftCannotBePublished() =>
        Should.Throw<DomainException>(() => PostBuilder.Draft(Now).Publish(PostBuilder.Desk, Now));
}
