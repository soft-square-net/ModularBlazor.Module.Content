using soft.square.net.WebApi.Modules.Content.UseCases.Categories;
using soft.square.net.WebApi.Modules.Content.UseCases.Categories.Add;

namespace soft.square.net.WebApi.Modules.Content.Web.Categories;

/// <summary>POST /categories: add a category to a press (content.manage for the whole tenant).</summary>
public sealed class Add : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/categories", HandleAsync)
            .RequirePermission(ContentPermissions.Manage)
            .WithName("Content_AddCategory");

    private static async Task<Results<Ok<CategoryDto>, ProblemHttpResult>> HandleAsync(NewCategory category, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AddCategoryCommand(category.Id, category.PressId, category.NameEn ?? category.Name ?? "", category.NameAr), ct)).ToOkResult();
}

/// <summary>
/// The body of <see cref="Add"/>: the English and Arabic names (without an Arabic one it takes the English one).
/// <see cref="Name"/> is the English name of older clients. Its name is the OpenAPI schema name: keep it stable.
/// </summary>
public sealed record NewCategory(string Id, string PressId, string? NameEn = null, string? NameAr = null, string? Name = null);
