using soft.square.net.WebApi.Modules.Content.UseCases.Categories;
using soft.square.net.WebApi.Modules.Content.UseCases.Categories.List;

namespace soft.square.net.WebApi.Modules.Content.Web.Categories;

/// <summary>GET /categories?pressId=: the categories of one press, or of all.</summary>
public sealed class List : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/categories", HandleAsync)
            .WithName("Content_ListCategories");

    private static async Task<Results<Ok<IReadOnlyList<CategoryDto>>, ProblemHttpResult>> HandleAsync(string? pressId, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListCategoriesQuery(pressId), ct)).ToOkResult();
}
