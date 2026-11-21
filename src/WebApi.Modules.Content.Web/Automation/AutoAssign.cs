using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.AutoAssign;

namespace soft.square.net.WebApi.Modules.Content.Web.Automation;

/// <summary>POST /automation/accepted/{id}/assign: the workflow assigns an accepted work to an editor on shift.</summary>
public sealed class AutoAssign : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/automation/accepted/{id:int}/assign", HandleAsync)
            .RequirePermission(ContentPermissions.Assign)
            .WithName("Content_AutoAssignPost")
            .WithSummary("Assign the accepted work to an eligible editor on an active shift; concurrency conflicts preserve a manager's assignment.");

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AutoAssignPostCommand(id), ct)).ToOkResult();
}
