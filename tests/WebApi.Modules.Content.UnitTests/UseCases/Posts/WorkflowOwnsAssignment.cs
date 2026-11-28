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
/// content's assign, keep the manual desk rules; on, only <c>content.unit.assign</c> (the workflow) may take them.
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

    /// <summary>Grants of a running shift: what the shifts module writes from the rota.</summary>
    private void OnShift(string userId, params string[] permissions) =>
        _fixture.Grants.ListAsync(userId, $"shift:{PostBuilder.Shift}", Arg.Any<CancellationToken>())
            .Returns([.. permissions.Select(p => new AccessGrant(Guid.NewGuid(), "acme", userId, p, "category:politics",
                _fixture.Clock.Now.AddHours(-1), _fixture.Clock.Now.AddHours(1), $"shift:{PostBuilder.Shift}", _fixture.Clock.Now, null))]);

    /// <summary>The manual desk: the shift manager holds every right the five steps ask for today.</summary>
    private void SignInTheManager()
    {
        foreach (var permission in new[] { ContentPermissions.Accept, ContentPermissions.Assign, ContentPermissions.Transfer, ContentPermissions.Return })
            _fixture.Allow(permission);
        OnShift(PostBuilder.Manager, ContentPermissions.Accept, ContentPermissions.Assign);
        OnShift(PostBuilder.Reviewer, ContentPermissions.Review);
    }

    /// <summary>The workflow's service client: <c>content.unit.assign</c> and nothing else, on no shift.</summary>
    private void SignInTheWorkflow()
    {
        _fixture.SignIn(Workflow);
        _fixture.Allow(ContentPermissions.UnitAssign);
        OnShift(PostBuilder.Reviewer, ContentPermissions.Review);
    }

    /// <summary>Takes the step on something in the state it starts from; also says whether anything changed.</summary>
    private async Task<(ResultStatus Status, string Errors, bool Changed)> TakeAsync(Step step, bool workflowOwns)
    {
        var now = _fixture.Clock.Now;
        var rights = Rights(workflowOwns);
        if (step == Step.AssignContent)
        {
            var content = Item();
            _fixture.Store.Contents.Add(content);
            var before = content.Version;
            var assigned = await new AssignContentHandler(_fixture.ContentChanges, _fixture.ContentAccess, rights)
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
        _fixture.Stored(post);
        var version = post.Version;
        var result = step switch
        {
            Step.Accept => await new AcceptPostHandler(_fixture.Changes, _fixture.PostAccess, rights)
                .Handle(new AcceptPostCommand(7, version, PostBuilder.Shift), Ct),
            Step.Assign => await new AssignPostHandler(_fixture.Changes, _fixture.PostAccess, rights, _fixture.Grants)
                .Handle(new AssignPostCommand(7, version, PostBuilder.Reviewer, PostBuilder.Shift), Ct),
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
    public async Task OnTheWorkflowStillAssignsOnlyAnEditorOnShift()
    {
        SignInTheWorkflow();
        var post = PostBuilder.Submitted(_fixture.Clock.Now);
        WithId(post, 7);
        _fixture.Stored(post);

        var result = await new AssignPostHandler(_fixture.Changes, _fixture.PostAccess, Rights(true), _fixture.Grants)
            .Handle(new AssignPostCommand(7, post.Version, "someone-off-shift", PostBuilder.Shift), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
        post.AssigneeId.ShouldBeNull();
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
