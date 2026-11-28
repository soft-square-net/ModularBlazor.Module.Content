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

        post.Transfer(PostBuilder.Manager, PostBuilder.Shift, Now);

        post.State.ShouldBe(PostState.Accepted);
        post.AssigneeId.ShouldBeNull();
        post.TransferredFromShift.ShouldBe(PostBuilder.Shift);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData(" shift-1 ", "shift-1")]
    public void AcceptingNeedsNoShiftAndRecordsOneThatIsNamed(string? shift, string? recorded)
    {
        var post = PostBuilder.Submitted(Now);

        post.Accept(PostBuilder.Manager, shift, Now);

        post.AcceptedShiftId.ShouldBe(recorded);
    }

    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    [Fact]
    public void AReviewerWhoLosesTheRightIsGivenTheGraceThenLosesTheReview()
    {
        var post = PostBuilder.InReview(Now);
        var version = post.Version;

        post.CheckReviewer("content:clock", holdsReviewRight: false, Now, Grace).ShouldBe(ReviewerCheck.Noted);
        post.CheckReviewer("content:clock", false, Now.AddMinutes(9), Grace).ShouldBe(ReviewerCheck.Unchanged);
        post.ReviewerAwaySince.ShouldBe(Now);
        post.Version.ShouldBe(version); // noting it changes nothing a caller holds

        post.CheckReviewer("content:clock", false, Now.AddMinutes(10), Grace).ShouldBe(ReviewerCheck.HandedOver);

        post.State.ShouldBe(PostState.Accepted);
        post.AssigneeId.ShouldBeNull();
        post.ReviewerAwaySince.ShouldBeNull();
        post.TransferredFromShift.ShouldBeNull();
        post.Version.ShouldBe(version + 1);
        var last = post.History.Last();
        (last.Action, last.Actor).ShouldBe(("handed over", "content:clock"));
    }

    [Fact]
    public void AReviewerWhoGetsTheRightBackKeepsTheReview()
    {
        var post = PostBuilder.InReview(Now);
        post.CheckReviewer("content:clock", false, Now, Grace);

        post.CheckReviewer("content:clock", true, Now.AddMinutes(5), Grace).ShouldBe(ReviewerCheck.Noted);
        post.ReviewerAwaySince.ShouldBeNull();
        post.CheckReviewer("content:clock", false, Now.AddMinutes(20), Grace).ShouldBe(ReviewerCheck.Noted); // the grace starts again
        post.State.ShouldBe(PostState.InReview);
    }

    [Fact]
    public void WithoutAGraceTheReviewIsHandedOverAtOnce() =>
        PostBuilder.InReview(Now).CheckReviewer("content:clock", false, Now, TimeSpan.Zero).ShouldBe(ReviewerCheck.HandedOver);

    [Fact]
    public void OnlyAPostInReviewIsHandedOver() =>
        PostBuilder.Approved(Now).CheckReviewer("content:clock", false, Now, TimeSpan.Zero).ShouldBe(ReviewerCheck.Unchanged);

    [Fact]
    public void LeavingReviewForgetsTheReviewersAbsence()
    {
        var post = PostBuilder.InReview(Now);
        post.CheckReviewer("content:clock", false, Now, Grace);

        post.Transfer(PostBuilder.Manager, null, Now);

        post.ReviewerAwaySince.ShouldBeNull();
    }

    [Fact]
    public void APostFollowsItsProjectUntilItIsFinal()
    {
        var post = PostBuilder.Submitted(Now);
        var project = Guid.NewGuid();
        var version = post.Version;

        post.IsWorkflowManaged.ShouldBeFalse();
        post.FollowWorkflowProject(project, ended: false).ShouldBeTrue();
        post.IsWorkflowManaged.ShouldBeTrue();
        post.FollowWorkflowProject(project, false).ShouldBeFalse();
        post.FollowWorkflowProject(Guid.NewGuid(), ended: true).ShouldBeFalse(); // another project's end
        post.FollowWorkflowProject(Guid.Empty, false).ShouldBeFalse();
        post.IsWorkflowManaged.ShouldBeTrue();

        post.FollowWorkflowProject(project, ended: true).ShouldBeTrue();
        post.FollowWorkflowProject(project, ended: false).ShouldBeFalse(); // events arrive in any order
        post.IsWorkflowManaged.ShouldBeFalse();
        post.Version.ShouldBe(version);

        post.FollowWorkflowProject(Guid.NewGuid(), false).ShouldBeTrue(); // a new project
        post.IsWorkflowManaged.ShouldBeTrue();
    }

    [Fact]
    public void UnschedulingMakesThePostApprovedAgainAtItsNewVersion()
    {
        var post = PostBuilder.Approved(Now);
        post.Schedule(PostBuilder.Desk, Now.AddHours(1), Now);

        post.Unschedule(PostBuilder.Desk, Now);

        post.State.ShouldBe(PostState.Approved);
        post.ScheduledFor.ShouldBeNull();
        post.ApprovedVersion.ShouldBe(post.Version);
        post.History.Last().Action.ShouldBe("unscheduled");
        post.PublishIfDue("workflow", Now.AddHours(2)).ShouldBeFalse();
    }

    [Fact]
    public void OnlyAScheduledPostIsUnscheduled() =>
        Should.Throw<DomainException>(() => PostBuilder.Approved(Now).Unschedule(PostBuilder.Desk, Now));

    [Fact]
    public void ADraftCannotBePublished() =>
        Should.Throw<DomainException>(() => PostBuilder.Draft(Now).Publish(PostBuilder.Desk, Now));
}
