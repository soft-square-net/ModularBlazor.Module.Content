namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories.Add;

/// <summary>Add a category to an existing press (needs content.manage for the whole tenant).</summary>
public sealed record AddCategoryCommand(string Id, string PressId, string Name) : ICommand<Result<CategoryDto>>;
