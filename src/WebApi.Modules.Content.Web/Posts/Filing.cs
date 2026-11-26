using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Filing;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="SetCategories"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record PostCategories(int Version, string PrimaryCategoryId, IReadOnlyList<string>? CategoryIds = null);

/// <summary>The body of <see cref="SetUnit"/> and of <c>PUT /contents/{id}/unit</c>: the unit (null: none). Keep the name stable.</summary>
public sealed record UnitAssignment(int Version, Guid? UnitId);

/// <summary>PUT /posts/{id}/categories: the post's categories, all of its press (its editors, or the workflow).</summary>
public sealed class SetCategories : IPostEndpoint
{
    public static string Name => "SetPostCategories";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/categories", HandleAsync)
            .WithSummary("Set the post's categories (a primary one and others, all of its press).");

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, PostCategories r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ChangePostCategoriesCommand(id, r.Version, r.PrimaryCategoryId, r.CategoryIds ?? []), ct)).ToOkResult();
}

/// <summary>PUT /posts/{id}/unit: put the post in an Organization unit, or in none (the workflow only: content.unit.assign).</summary>
public sealed class SetUnit : IPostEndpoint
{
    public static string Name => "SetPostUnit";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/unit", HandleAsync)
            .RequirePermission(ContentPermissions.UnitAssign)
            .WithSummary("Put the post in an Organization unit that serves one of its categories (the workflow only).");

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, UnitAssignment r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SetPostUnitCommand(id, r.Version, r.UnitId), ct)).ToOkResult();
}
