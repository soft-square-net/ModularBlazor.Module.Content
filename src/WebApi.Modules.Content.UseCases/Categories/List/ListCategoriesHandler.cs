using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories.List;

public sealed class ListCategoriesHandler(IReadRepository<Category> categories)
    : IQueryHandler<ListCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async ValueTask<Result<IReadOnlyList<CategoryDto>>> Handle(ListCategoriesQuery query, CancellationToken ct) =>
        (await categories.ListAsync(ct)).Where(c => query.PressId is null || c.PressId == query.PressId)
            .OrderBy(c => c.PressId).ThenBy(c => c.Id).Select(c => new CategoryDto(c.Id, c.PressId, c.Name)).ToArray();
}
