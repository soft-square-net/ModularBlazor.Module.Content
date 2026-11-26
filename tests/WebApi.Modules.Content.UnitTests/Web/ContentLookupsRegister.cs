using System.Linq.Expressions;
using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Web.Lookups;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Web;

/// <summary>
/// The declaration of <c>content.categories</c>. The host's engine (search, paging, take + 1, keys) is the platform's and
/// is tested there; here: what Content declares, and that its expressions read the right fields.
/// </summary>
public sealed class ContentLookupsRegister
{
    private readonly Recorder _lookups = new();

    public ContentLookupsRegister() => new ContentLookups().Register(_lookups);

    [Fact]
    public void DeclaresTheCategoriesLookup()
    {
        var categories = _lookups.Definitions.ShouldHaveSingleItem();
        categories.Name.ShouldBe("categories");
        categories.Entity.ShouldBe(typeof(Category));
        categories.SearchOn.ShouldBe(["NameEn", "NameAr", "Id"]);
        categories.Parent.ShouldBe("PressId");
        categories.Filters.ShouldBe(["PressId"]);
        categories.Columns.ShouldBe(["PressId", "NameEn", "NameAr"]);
        // Like GET /categories: a signed-in user, no permission.
        categories.Permission.ShouldBeNull();
        categories.Label.ShouldBe("Categories");
    }

    [Fact]
    public void TheKeyIsTheIdAndTheTextIsTheName()
    {
        var categories = _lookups.Definitions.Single();
        var sports = Category.Create("ahram-sports", "ahram", "Sports", "رياضة");

        categories.Key!.Compile().DynamicInvoke(sports).ShouldBe("ahram-sports");
        categories.Text!.Compile().DynamicInvoke(sports).ShouldBe("Sports");
        categories.Order!.Compile().DynamicInvoke(sports).ShouldBe("Sports");
    }

    public sealed class Definition(string name, Type entity)
    {
        public string Name { get; } = name;
        public Type Entity { get; } = entity;
        public LambdaExpression? Key { get; set; }
        public LambdaExpression? Text { get; set; }
        public LambdaExpression? Order { get; set; }
        public string[] SearchOn { get; set; } = [];
        public string? Parent { get; set; }
        public List<string> Filters { get; } = [];
        public string[] Columns { get; set; } = [];
        public string? Permission { get; set; }
        public string? Label { get; set; }
    }

    /// <summary>Records the builder calls (the host's builder is internal to the platform).</summary>
    private sealed class Recorder : ILookupBuilder
    {
        public List<Definition> Definitions { get; } = [];

        public ILookupDefinitionBuilder<T> For<T>(string name) where T : class
        {
            var definition = new Definition(name, typeof(T));
            Definitions.Add(definition);
            return new Builder<T>(definition);
        }
    }

    private sealed class Builder<T>(Definition d) : ILookupDefinitionBuilder<T> where T : class
    {
        public ILookupDefinitionBuilder<T> Key<TKey>(Expression<Func<T, TKey>> key) { d.Key = key; return this; }
        public ILookupDefinitionBuilder<T> Text(Expression<Func<T, string>> text) { d.Text = text; return this; }
        public ILookupDefinitionBuilder<T> Text(string culture, Expression<Func<T, string>> text) => this;
        public ILookupDefinitionBuilder<T> TextField(string property) => this;
        public ILookupDefinitionBuilder<T> Render(Func<LookupItem, string> render) => this;
        public ILookupDefinitionBuilder<T> SearchOn(params string[] properties) { d.SearchOn = properties; return this; }
        public ILookupDefinitionBuilder<T> Where(Expression<Func<T, bool>> predicate) => this;
        public ILookupDefinitionBuilder<T> OrderBy<TValue>(Expression<Func<T, TValue>> order, bool descending = false) { d.Order = order; return this; }
        public ILookupDefinitionBuilder<T> ParentField(string property) { d.Parent = property; return this; }
        public ILookupDefinitionBuilder<T> Hierarchy(string parentProperty, string? leafProperty = null) => this;
        public ILookupDefinitionBuilder<T> FilterOn(string property, string? label = null, string? valueLookup = null) { d.Filters.Add(property); return this; }
        public ILookupDefinitionBuilder<T> Columns(params string[] properties) { d.Columns = properties; return this; }
        public ILookupDefinitionBuilder<T> Permission(string permission) { d.Permission = permission; return this; }
        public ILookupDefinitionBuilder<T> Label(string label, string? description = null) { d.Label = label; return this; }
        public ILookupDefinitionBuilder<T> Source(Func<IServiceProvider, IQueryable<T>> source) => this;
    }
}
