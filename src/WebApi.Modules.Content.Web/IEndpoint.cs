using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace soft.square.net.WebApi.Modules.Content.Web;

/// <summary>
/// One HTTP endpoint (REPR: its request type, the endpoint, its response). <see cref="ContentRoutes"/> maps it.
/// The endpoint sets its route, permission, name (the OpenAPI operation id, <c>Content_&lt;Operation&gt;</c>) and
/// summary, and sends one command or query.
/// </summary>
public interface IEndpoint
{
    static abstract RouteHandlerBuilder Map(IEndpointRouteBuilder app);
}

/// <summary>
/// A Post endpoint. Mapped three times, under <c>/posts</c> and the compatibility segments <c>/articles</c> and
/// <c>/works</c>; only the <c>/posts</c> copy is named and documented (names are unique across the host), so it does
/// not name itself: <see cref="Name"/> is applied by <see cref="ContentRoutes"/>.
/// </summary>
public interface IPostEndpoint
{
    /// <summary>The operation, without the module prefix: <c>ListPosts</c> becomes <c>Content_ListPosts</c>.</summary>
    static abstract string Name { get; }

    /// <param name="posts">The group of one resource segment.</param>
    /// <param name="resourceSegment"><c>posts</c>, <c>articles</c> or <c>works</c>, for Location headers.</param>
    static abstract RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment);
}
