namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories;

/// <summary>A category of one press. Category ids are unique within the tenant.</summary>
public sealed record CategoryDto(string Id, string PressId, string Name);
