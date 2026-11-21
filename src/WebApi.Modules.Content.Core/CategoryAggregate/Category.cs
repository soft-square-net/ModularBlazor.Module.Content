using soft.square.net.WebApi.Modules.Content.Core.Common;

namespace soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

/// <summary>
/// A category of one press. Category ids are unique within the tenant (e.g. <c>ahram-sports</c>), so a grant scope
/// <c>category:{id}</c> names exactly one category even when a shift section spans presses.
/// </summary>
public sealed class Category : AggregateRoot<string>, ITenantOwned
{
    private Category() { } // EF Core

    public string TenantId { get; set; } = "";
    public string PressId { get; private set; } = "";
    public string Name { get; private set; } = "";

    public static Category Create(string id, string pressId, string name) =>
        new() { Id = Slugs.Require(id, "category"), PressId = Slugs.Require(pressId, "press"), Name = Names.Require(name, "category") };

    public void Rename(string name) => Name = Names.Require(name, "category");
}
