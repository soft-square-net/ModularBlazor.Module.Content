using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

namespace soft.square.net.WebApi.Modules.Content.Web.Lookups;

/// <summary>
/// The module's lookups, served by the host under <c>/api/content/lookups/{name}</c> from the tenant's database.
/// <list type="bullet">
/// <item><c>content.categories</c>: the tenant's categories, key = the category id (unique in the tenant), text = its English
/// name (its Arabic one in <c>ar</c>), searched on both names and the id, ordered by the English name. <c>?parent={pressId}</c> lists one press's categories; the item's
/// <c>parentKey</c> and the <c>PressId</c> column give the press. Like <c>GET /categories</c>, it needs a signed-in user
/// and no permission (categories are the tenant's taxonomy, not content).</item>
/// </list>
/// </summary>
public sealed class ContentLookups : ILookupModule
{
    public const string Categories = "categories";

    public void Register(ILookupBuilder lookups) =>
        lookups.For<Category>(Categories)
            .Label("Categories", "The tenant's content categories, with their press.")
            .Key(c => c.Id)
            .Text(c => c.NameEn)
            .Text("ar", c => c.NameAr)
            .SearchOn(nameof(Category.NameEn), nameof(Category.NameAr), nameof(Category.Id))
            .OrderBy(c => c.NameEn)
            .ParentField(nameof(Category.PressId))
            .FilterOn(nameof(Category.PressId), "Press")
            .Columns(nameof(Category.PressId), nameof(Category.NameEn), nameof(Category.NameAr));
}
