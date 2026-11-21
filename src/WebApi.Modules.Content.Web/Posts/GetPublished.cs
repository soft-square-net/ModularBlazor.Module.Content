using soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>GET /public/{publicId}: the permanent Post URL. Anonymous; the tenant still comes from the host name or X-Tenant.
/// <c>?culture=ar-EG</c>: the text in that language where the Post is translated (then ar, then its own).</summary>
public sealed class GetPublished : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/public/{publicId}", HandleAsync)
            .AllowAnonymous() // readers are not signed in; the tenant still comes from the host name or the X-Tenant header
            .WithName("Content_GetPublishedPost")
            .WithSummary("Resolve the permanent Post URL; only the immutable published snapshot is returned.");

    private static async Task<Results<Ok<PublishedPost>, ProblemHttpResult>> HandleAsync(string publicId, string? culture, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetPublishedPostQuery(publicId, culture), ct)).ToOkResult();
}
