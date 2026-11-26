using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Categories;

/// <summary>
/// A category of one press, named in English and Arabic. Category ids are unique within the tenant. <see cref="Name"/> is the
/// English name (kept for older clients).
/// </summary>
public sealed record CategoryDto(string Id, string PressId, string Name, string NameEn, string NameAr)
{
    public static CategoryDto From(Category c) => new(c.Id, c.PressId, c.NameEn, c.NameEn, c.NameAr);
}
