namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories.List;

/// <summary>The categories of one press, or of all presses when <paramref name="PressId"/> is null.</summary>
public sealed record ListCategoriesQuery(string? PressId) : IQuery<Result<IReadOnlyList<CategoryDto>>>;
