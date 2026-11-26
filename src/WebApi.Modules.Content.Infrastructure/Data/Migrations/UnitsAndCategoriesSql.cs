namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations;

/// <summary>
/// The data part of the <c>UnitsAndCategories</c> migration, the same on every engine (only the names are quoted
/// differently), run after its schema changes:
/// <list type="bullet">
/// <item>every category's Arabic name starts as its English one (<c>categories.NameAr</c> is added nullable, filled, then
/// made required: Oracle cannot add a required column with an empty default);</item>
/// <item>every post's single category (<c>articles.CategoryId</c>, which stays as the primary one) becomes its first row of
/// <c>post_categories</c>.</item>
/// </list>
/// Down needs no data step: the primary category never left <c>articles.CategoryId</c>, and the English name is the old
/// <c>Name</c> column renamed.
/// </summary>
internal static class UnitsAndCategoriesSql
{
    /// <param name="table">Quotes a table of the module with its schema or prefix.</param>
    /// <param name="column">Quotes a column name.</param>
    public static IEnumerable<string> Up(Func<string, string> table, Func<string, string> column)
    {
        yield return $"UPDATE {table("categories")} SET {column("NameAr")} = {column("NameEn")}";
        yield return $"INSERT INTO {table("post_categories")} ({column("PostId")}, {column("CategoryId")}) " +
                     $"SELECT {column("Id")}, {column("CategoryId")} FROM {table("articles")} WHERE {column("CategoryId")} IS NOT NULL";
    }
}
