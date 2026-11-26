using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.Web.Contents;

/// <summary>The body of <see cref="Create"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record NewContent(string? CategoryId, PostNode Values, string? SectionId = null, string? AssigneeId = null,
    int? PostId = null, int? PostVersion = null, int? Index = null, bool Required = true);

/// <summary>The body of <see cref="Edit"/>.</summary>
public sealed record ContentEdit(int Version, PostNode Values);

/// <summary>The body of <see cref="Act"/>: a pipeline action, the version the caller saw, and the note some actions need.</summary>
public sealed record ContentAction(string Action, int Version, string? Note = null);

/// <summary>The body of <see cref="Assign"/>.</summary>
public sealed record ContentAssignment(int Version, string? SectionId, string? AssigneeId);

/// <summary>The body of the steps that need only the version the caller saw (<see cref="Archive"/>).</summary>
public sealed record ContentVersion(int Version);

/// <summary>GET /contents: the library, filtered (section, stage, assignee, field; archived ones with ?archived=true).</summary>
public sealed class List : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/contents", async (string? sectionId, string? stage, string? assigneeId, string? fieldKey, bool? archived, int? skip, int? take,
                Guid? unitId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListContentsQuery(sectionId, stage, assigneeId, fieldKey, archived ?? false, skip ?? 0, take ?? 50, unitId), ct)).ToOkResult())
            .WithName("Content_ListContents")
            .WithSummary("The shared contents the caller may see, newest change first.");
}

/// <summary>GET /contents/{id}: one content with its revisions, history and the actions the caller may take.</summary>
public sealed class Get : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/contents/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetContentQuery(id), ct)).ToOkResult())
            .WithName("Content_GetContent");
}

/// <summary>GET /contents/{id}/revisions/{revision}: one revision with its values.</summary>
public sealed class GetRevision : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/contents/{id:guid}/revisions/{revision:int}", async (Guid id, int revision, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetContentRevisionQuery(id, revision), ct)).ToOkResult())
            .WithName("Content_GetContentRevision");
}

/// <summary>POST /contents: a new content in the library (optionally attached to a post at once); 201 with it.</summary>
public sealed class Create : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/contents", async Task<Results<Created<ContentDetail>, ProblemHttpResult>> (NewContent r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateContentCommand(r.CategoryId, r.Values, r.SectionId, r.AssigneeId, r.PostId, r.PostVersion, r.Index, r.Required), ct))
                .ToCreatedResult(c => $"contents/{c.Id}"))
            .RequirePermission(ContentPermissions.Author)
            .WithName("Content_CreateContent");
}

/// <summary>PUT /contents/{id}: new values (a new revision), by the stage's editors (the section, or the Desk).</summary>
public sealed class Edit : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/contents/{id:guid}", async (Guid id, ContentEdit r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new EditContentCommand(id, r.Version, r.Values), ct)).ToOkResult())
            .WithName("Content_EditContent");
}

/// <summary>POST /contents/{id}/act: a pipeline action (submit, accept, reject with a note, ...), checked against the pipeline.</summary>
public sealed class Act : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/contents/{id:guid}/act", async (Guid id, ContentAction r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ActOnContentCommand(id, r.Action, r.Version, r.Note), ct)).ToOkResult())
            .WithName("Content_ActOnContent")
            .WithSummary("Act(contentId, action, expectedVersion, note?): move a content through the pipeline.");
}

/// <summary>POST /contents/{id}/assign: give it to a section and/or a person (shift Manager).</summary>
public sealed class Assign : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/contents/{id:guid}/assign", async (Guid id, ContentAssignment r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AssignContentCommand(id, r.Version, r.SectionId, r.AssigneeId), ct)).ToOkResult())
            .RequirePermission(ContentPermissions.Assign)
            .WithName("Content_AssignContent");
}

/// <summary>PUT /contents/{id}/unit: give it to an Organization unit, or to none (the workflow only: content.unit.assign).</summary>
public sealed class SetUnit : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/contents/{id:guid}/unit", async (Guid id, Posts.UnitAssignment r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new soft.square.net.WebApi.Modules.Content.UseCases.Posts.Filing.SetContentUnitCommand(id, r.Version, r.UnitId), ct)).ToOkResult())
            .RequirePermission(ContentPermissions.UnitAssign)
            .WithName("Content_SetContentUnit")
            .WithSummary("Give the content to an Organization unit that serves its category (the workflow only).");
}

/// <summary>POST /contents/{id}/archive: out of the library; the posts that use it keep it.</summary>
public sealed class Archive : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/contents/{id:guid}/archive", async (Guid id, ContentVersion r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ArchiveContentCommand(id, r.Version), ct)).ToOkResult())
            .WithName("Content_ArchiveContent");
}

/// <summary>DELETE /contents/{id}?version=: removed for good; 409 while any post uses it.</summary>
public sealed class Delete : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/contents/{id:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid id, int version, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteContentCommand(id, version), ct);
            return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
        })
            .WithName("Content_DeleteContent");
}

/// <summary>GET /pipeline: the content pipeline in force (stages and actions).</summary>
public sealed class GetPipeline : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/pipeline", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetPipelineQuery(), ct)).ToOkResult())
            .WithName("Content_GetPipeline");
}

/// <summary>PUT /pipeline: publish a new pipeline (Elsa, or an admin; <c>content.pipeline</c> tenant-wide).</summary>
public sealed class PublishPipeline : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/pipeline", async (Core.PipelineAggregate.PipelineDefinition definition, ISender sender, CancellationToken ct) =>
            (await sender.Send(new PublishPipelineCommand(definition), ct)).ToOkResult())
            .RequirePermission(ContentPermissions.Pipeline)
            .WithName("Content_PublishPipeline");
}

/// <summary>
/// GET /events?after=: the change feed for workflow services (Elsa): a client-credentials token with <c>content.events</c> in
/// the tenant. Poll with the last id handled.
/// </summary>
public sealed class Events : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/events", async (long? after, int? take, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetEventsQuery(after ?? 0, take), ct)).ToOkResult())
            .RequirePermission(ContentPermissions.Events)
            .WithName("Content_GetEvents")
            .WithSummary("What happened to posts and contents after an event id, oldest first (content.created, content.changed, content.state-changed, content.assigned, content.archived, content.deleted, content.attached, content.detached, post.changed, post.state-changed).");
}
