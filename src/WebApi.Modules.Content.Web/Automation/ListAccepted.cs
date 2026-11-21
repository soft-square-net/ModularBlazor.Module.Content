using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.ListAcceptedForAutomation;

namespace soft.square.net.WebApi.Modules.Content.Web.Automation;

/// <summary>GET /automation/accepted: the accepted works the workflow (Elsa) polls.</summary>
public sealed class ListAccepted : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/automation/accepted", HandleAsync)
            .RequirePermission(ContentPermissions.Assign)
            .WithName("Content_ListAcceptedForAutomation")
            .WithSummary("Accepted works available for Elsa's durable assignment-delay workflow.");

    private static async Task<Results<Ok<IReadOnlyList<PostSummary>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListAcceptedForAutomationQuery(), ct)).ToOkResult();
}
