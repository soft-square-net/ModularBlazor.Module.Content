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

    private static async Task<Results<Ok<CategoryDto>, ProblemHttpResult>> HandleAsync(CategoryDto category, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AddCategoryCommand(category.Id, category.PressId, category.Name), ct)).ToOkResult();
}
