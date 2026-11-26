using soft.square.net.WebApi.Modules.Content.Core.Common;

namespace soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

/// <summary>
/// A category of one press, named in English and Arabic. Category ids are unique within the tenant (e.g.
/// <c>ahram-sports</c>), so a grant scope <c>category:{id}</c> names exactly one category even when a shift section spans
/// presses.
/// </summary>
public sealed class Category : AggregateRoot<string>, ITenantOwned
{
    private Category() { } // EF Core

    public string TenantId { get; set; } = "";
    public string PressId { get; private set; } = "";
    /// <summary>The English name (the column that was <c>Name</c>).</summary>
    public string NameEn { get; private set; } = "";
    /// <summary>The Arabic name (the English one until someone gives it).</summary>
    public string NameAr { get; private set; } = "";

    /// <summary>A new category; without an Arabic name it takes the English one.</summary>
    public static Category Create(string id, string pressId, string nameEn, string? nameAr = null)
    {
        var category = new Category { Id = Slugs.Require(id, "category"), PressId = Slugs.Require(pressId, "press") };
        category.Rename(nameEn, nameAr);
        return category;
    }

    public void Rename(string nameEn, string? nameAr = null)
    {
        NameEn = Names.Require(nameEn, "category");
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? NameEn : Names.Require(nameAr, "category");
    }

    /// <summary>The name in a culture: Arabic for <c>ar</c> and its variants, else English.</summary>
    public string NameIn(string? culture) =>
        culture is not null && culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? NameAr : NameEn;
}
