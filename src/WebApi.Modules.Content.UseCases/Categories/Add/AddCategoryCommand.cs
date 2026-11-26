namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories.Add;

/// <summary>Add a category to an existing press (needs content.manage for the whole tenant). Without an Arabic name it takes the English one.</summary>
public sealed record AddCategoryCommand(string Id, string PressId, string NameEn, string? NameAr = null) : ICommand<Result<CategoryDto>>;
