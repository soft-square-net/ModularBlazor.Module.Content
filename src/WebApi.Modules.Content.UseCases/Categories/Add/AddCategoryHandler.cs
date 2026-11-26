using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Presses;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories.Add;

public sealed class AddCategoryHandler(IReadRepository<Press> presses, IRepository<Category> categories, IAccessScopes access)
    : ICommandHandler<AddCategoryCommand, Result<CategoryDto>>
{
    public async ValueTask<Result<CategoryDto>> Handle(AddCategoryCommand command, CancellationToken ct)
    {
        if (!(await access.ScopesAsync(ContentPermissions.Manage, ct)).Contains(AccessScope.Tenant))
            return Result<CategoryDto>.Forbidden(TaxonomyMessages.ManageNeedsTenant);
        if (await presses.GetByIdAsync(command.PressId, ct) is null)
            return Result<CategoryDto>.Conflict($"Unknown press '{command.PressId}'.");
        if (await categories.GetByIdAsync(command.Id, ct) is not null)
            return Result<CategoryDto>.Conflict($"Category '{command.Id}' exists already.");

        var category = await categories.AddAsync(Category.Create(command.Id, command.PressId, command.NameEn, command.NameAr), ct);
        return CategoryDto.From(category);
    }
}
