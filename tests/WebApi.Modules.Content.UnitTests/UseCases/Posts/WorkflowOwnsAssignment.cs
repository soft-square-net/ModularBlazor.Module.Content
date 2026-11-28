using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Accept;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Return;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Transfer;
using soft.square.net.WebApi.Modules.Content.Web;
using ContentRoute = soft.square.net.WebApi.Modules.Content.Web.Contents;
using PostRoutes = soft.square.net.WebApi.Modules.Content.Web.Posts;
using static soft.square.net.WebApi.Modules.Content.UnitTests.Core.ContentAggregate.ContentBuilder;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Posts;

/// <summary>
/// <c>Content:Newsroom:WorkflowOwnsAssignment</c>: off (the default), a post's accept, assign, transfer and return, and a
/// content's assign, keep the manual desk rules; on, those of a post the workflow is working on need
/// <c>content.unit.assign</c> (the workflow), and other work is taken by hand or by the workflow.
/// </summary>
public class WorkflowOwnsAssignment
{
    private const string Workflow = "workflow";

    /// <summary>The five steps.</summary>
    public enum Step { Accept, Assign, Transfer, Return, AssignContent }

    private readonly PostFixture _fixture = new(PostBuilder.Manager);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssignmentRights Rights(bool workflowOwns) =>
        new(_fixture.PostAccess, Microsoft.Extensions.Options.Options.Create(
            new ContentOptions { Newsroom = new() { WorkflowOwnsAssignment = workflowOwns } }));

    /// <summary>The reviewer holds the review right now (an editor on a running shift).</summary>
    private void MayReview(string userId) =>
        _fixture.Access.UserHasPermissionAsync(userId, ContentPermissions.Review, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(true);

    /// <summary>The manual desk: a manager who holds every right the five steps ask for.</summary>
    private void SignInTheManager()
    {
        foreach (var permission in new[] { ContentPermissions.Accept, ContentPermissions.Assign, ContentPermissions.Transfer, ContentPermissions.Return })
            _fixture.Allow(permission);
        MayReview(PostBuilder.Reviewer);
    }

    /// <summary>The workflow's service client: <c>content.unit.assign</c> and nothing else, on no shift.</summary>
    private void SignInTheWorkflow()
    {
        _fixture.SignIn(Workflow);
        _fixture.Allow(ContentPermissions.UnitAssign);
        MayReview(PostBuilder.Reviewer);
    }

    /// <summary>Takes the step on something in the state it starts from; also says whether anything changed.</summary>
    private async Task<(ResultStatus Status, string Errors, bool Changed)> TakeAsync(Step step, bool workflowOwns,
        bool project = true, bool projectEnded = false)
    {
        var now = _fixture.Clock.Now;
        var rights = Rights(workflowOwns);
        if (step == Step.AssignContent)
        {
            var draft = Content.Core.PostAggregate.Post.Create(PostBuilder.Owner, PostBuilder.Category(), "A draft", "", now);
            var content = Item(origin: draft.PublicId); // made for the draft
            _fixture.Store.Contents.Add(content);
            if (project)
            {
                draft.FollowWorkflowProject(Guid.NewGuid(), ended: false);
                if (projectEnded) draft.FollowWorkflowProject(draft.WorkflowProjectId!.Value, ended: true);
                _fixture.Store.Posts.Add(draft);
            }
            var before = content.Version;
            var assigned = await new AssignContentHandler(_fixture.ContentChanges, _fixture.ContentAccess, rights, _fixture.Store)
                .Handle(new AssignContentCommand(content.Id, before, null, "writer-2"), Ct);
            return (assigned.Status, string.Join(" ", assigned.Errors), content.Version != before);
        }

        var post = step switch
        {
            Step.Accept => PostBuilder.Submitted(now),
            Step.Assign => PostBuilder.Submitted(now),
            Step.Transfer => PostBuilder.InReview(now),
            _ => PostBuilder.Submitted(now)
        };
        WithId(post, 7);
        if (project) post.FollowWorkflowProject(Guid.NewGuid(), ended: false);
        if (projectEnded) post.FollowWorkflowProject(post.WorkflowProjectId!.Value, ended: true);
        _fixture.Stored(post);
        var version = post.Version;
        var result = step switch
        {
            Step.Accept => await new AcceptPostHandler(_fixture.Changes, _fixture.PostAccess, rights)
                .Handle(new AcceptPostCommand(7, version, PostBuilder.Shift), Ct),
            Step.Assign => await new AssignPostHandler(_fixture.Changes, _fixture.PostAccess, rights)
                .Handle(new AssignPostCommand(7, version, PostBuilder.Reviewer), Ct),
            Step.Transfer => await new TransferPostHandler(_fixture.Changes, _fixture.PostAccess, rights)
                .Handle(new TransferPostCommand(7, version, PostBuilder.Shift), Ct),
            _ => await new ReturnPostHandler(_fixture.Changes, _fixture.PostAccess, rights)
                .Handle(new ReturnPostCommand(7, version, "Fix the headline."), Ct)
        };
        return (result.Status, string.Join(" ", result.Errors), post.Version != version);
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OffTheManualDeskTakesTheStep(Step step)
    {
        SignInTheManager();

        var (status, errors, changed) = await TakeAsync(step, workflowOwns: false);

        status.ShouldBe(ResultStatus.Ok, errors);
        changed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OffTheWorkflowsPermissionAloneIsNotEnough(Step step)
    {
        SignInTheWorkflow();

        var (status, _, changed) = await TakeAsync(step, workflowOwns: false);

        status.ShouldBe(ResultStatus.Forbidden);
        changed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OnAnOrdinaryUserIsRefusedEvenWithTheDeskRights(Step step)
    {
        SignInTheManager();

        var (status, errors, changed) = await TakeAsync(step, workflowOwns: true);

        status.ShouldBe(ResultStatus.Forbidden);
        errors.ShouldContain(ContentPermissions.UnitAssign);
        changed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OnTheWorkflowTakesTheStep(Step step)
    {
        SignInTheWorkflow();

        var (status, errors, changed) = await TakeAsync(step, workflowOwns: true);

        status.ShouldBe(ResultStatus.Ok, errors);
        changed.ShouldBeTrue();
    }

    [Fact]
    public async Task OnTheWorkflowStillAssignsOnlyAReviewerWhoMayReview()
    {
        SignInTheWorkflow();
        var post = PostBuilder.Submitted(_fixture.Clock.Now);
        WithId(post, 7);
        _fixture.Stored(post);

        var result = await new AssignPostHandler(_fixture.Changes, _fixture.PostAccess, Rights(true))
            .Handle(new AssignPostCommand(7, post.Version, "someone-off-shift"), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
        post.AssigneeId.ShouldBeNull();
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OnWithoutAProjectTheDeskStillTakesTheStepByHand(Step step)
    {
        SignInTheManager();

        var (status, errors, changed) = await TakeAsync(step, workflowOwns: true, project: false);

        status.ShouldBe(ResultStatus.Ok, errors);
        changed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OnOnceTheProjectEndedTheDeskTakesTheStepAgain(Step step)
    {
        SignInTheManager();

        var (status, errors, _) = await TakeAsync(step, workflowOwns: true, projectEnded: true);

        status.ShouldBe(ResultStatus.Ok, errors);
    }

    /// <summary>The workflow may act before NewsRoom's event has reached this module.</summary>
    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Transfer)]
    [InlineData(Step.Return)]
    [InlineData(Step.AssignContent)]
    public async Task OnWithoutAProjectTheWorkflowMayStillTakeTheStep(Step step)
    {
        SignInTheWorkflow();

        var (status, errors, _) = await TakeAsync(step, workflowOwns: true, project: false);

        status.ShouldBe(ResultStatus.Ok, errors);
    }

    [Theory]
    [InlineData(Step.Accept)]
    [InlineData(Step.Assign)]
    [InlineData(Step.Return)]
    public async Task WithoutAnyRightNobodyTakesTheStep(Step step)
    {
        var (status, _, changed) = await TakeAsync(step, workflowOwns: true, project: false);

        status.ShouldBe(ResultStatus.Forbidden);
        changed.ShouldBeFalse();
    }

    [Fact]
    public void ItIsOffByDefaultAndBindsFromTheModulesSettings()
    {
        new ContentOptions().Newsroom.WorkflowOwnsAssignment.ShouldBeFalse();
        var defaults = new ContentModule().Manifest.DefaultSettings;
        defaults["Content:Newsroom:WorkflowOwnsAssignment"].ShouldBe("false");

        ContentOptions Bound(string value) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(defaults) { ["Content:Newsroom:WorkflowOwnsAssignment"] = value })
            .Build().GetSection("Content").Get<ContentOptions>()!;
        Bound("false").Newsroom.WorkflowOwnsAssignment.ShouldBeFalse();
        Bound("true").Newsroom.WorkflowOwnsAssignment.ShouldBeTrue();
    }

    /// <summary>The coarse endpoint check must not stop the workflow, which holds none of the desk's permissions.</summary>
    [Theory]
    [InlineData("/posts/{id:int}/accept", ContentPermissions.Accept)]
    [InlineData("/posts/{id:int}/assign", ContentPermissions.Assign)]
    [InlineData("/posts/{id:int}/transfer", ContentPermissions.Transfer)]
    [InlineData("/posts/{id:int}/return", ContentPermissions.Return)]
    [InlineData("/contents/{id:guid}/assign", ContentPermissions.Assign)]
    public void TheEndpointsLetEitherPermissionIn(string route, string permission)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<Mediator.ISender>());
        var app = builder.Build();
        var posts = app.MapGroup("/posts");
        PostRoutes.Accept.Map(posts, "posts");
        PostRoutes.Assign.Map(posts, "posts");
        PostRoutes.Transfer.Map(posts, "posts");
        PostRoutes.Return.Map(posts, "posts");
        ContentRoute.Assign.Map(app);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == route);

        endpoint.Metadata.OfType<AuthorizationPolicy>().SelectMany(p => p.Requirements).OfType<PermissionRequirement>()
            .ShouldHaveSingleItem().AnyOf.ShouldBe([permission, ContentPermissions.UnitAssign]);
    }
}
