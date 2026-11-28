using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>GET /kinds: the kinds a new post can be of (the published DynamicFields root post fields); 503 when they cannot be read.</summary>
public sealed class ListKinds : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/kinds", HandleAsync)
            .WithName("Content_ListPostKinds")
            .WithSummary("The post kinds: the published DynamicFields root post fields.");

    private static async Task<Results<Ok<IReadOnlyList<PostKindInfo>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListPostKindsQuery(), ct)).ToOkResult();
}
