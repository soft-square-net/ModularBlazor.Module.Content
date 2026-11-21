using soft.square.net.WebApi.Modules.Content.Core.Common;

namespace soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

/// <summary>A press (newspaper or magazine) of the tenant. Its id is a slug and appears in grant scopes as <c>press:{id}</c>.</summary>
public sealed class Press : AggregateRoot<string>, ITenantOwned
{
    private Press() { } // EF Core

    public string TenantId { get; set; } = "";
    public string Name { get; private set; } = "";

    public static Press Create(string id, string name) => new() { Id = Slugs.Require(id, "press"), Name = Names.Require(name, "press") };

    public void Rename(string name) => Name = Names.Require(name, "press");
}
