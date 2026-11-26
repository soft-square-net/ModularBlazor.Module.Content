using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations;

/// <summary>
/// The data part of the <c>PostKindsAsFieldKeys</c> migration, the same on every engine (only the names are quoted
/// differently): the old <c>PostKind</c> enum names become DynamicFields root field keys (<see cref="PostKinds.Legacy"/>,
/// Article → <c>post.article</c>, ... PhotoStory → <c>post.photo-story</c>) in
/// <list type="bullet">
/// <item>the kind columns: <c>articles.Type</c> and <c>contents.PostKind</c>;</item>
/// <item>the stored JSON that holds nodes (<c>"postKind":"Article"</c>, as System.Text.Json writes it): the working and
/// published documents of posts, and the values of contents and of their revisions.</item>
/// </list>
/// The code also reads the old names (<see cref="PostNode.PostKind"/> normalizes them), so a row this misses still works.
/// Down maps the keys back; a kind that was never an enum name becomes <c>Other</c> in the columns (the JSON keeps it).
/// </summary>
internal static class PostKindKeysSql
{
    /// <param name="table">Quotes a table of the module (<c>articles</c>, <c>contents</c>, <c>content_revisions</c>) with its
    /// schema or prefix (on MySQL and Oracle a name that already starts with <c>content_</c> is not prefixed again).</param>
    /// <param name="column">Quotes a column name.</param>
    /// <param name="national">Writes the literals as national strings (<c>N'...'</c>): Oracle refuses to compare its NVARCHAR2
    /// and NCLOB columns with plain literals in a CASE (ORA-12704).</param>
    public static IEnumerable<string> Up(Func<string, string> table, Func<string, string> column, bool national = false) =>
        Statements(table, column, national, PostKinds.Legacy.Select(k => (From: k.Name, To: k.Key)).ToList(), elseValue: null);

    public static IEnumerable<string> Down(Func<string, string> table, Func<string, string> column, bool national = false) =>
        Statements(table, column, national, PostKinds.Legacy.Select(k => (From: k.Key, To: k.Name)).ToList(), elseValue: "Other");

    private static IEnumerable<string> Statements(Func<string, string> table, Func<string, string> column, bool national,
        IReadOnlyList<(string From, string To)> map, string? elseValue)
    {
        string L(string text) => (national ? "N'" : "'") + text + "'";

        foreach (var (t, c) in new[] { ("articles", "Type"), ("contents", "PostKind") })
        {
            var name = column(c);
            var cases = string.Join(" ", map.Select(m => $"WHEN {L(m.From)} THEN {L(m.To)}"));
            yield return elseValue is null
                ? $"UPDATE {table(t)} SET {name} = CASE {name} {cases} END WHERE {name} IN ({string.Join(", ", map.Select(m => L(m.From)))})"
                : $"UPDATE {table(t)} SET {name} = CASE {name} {cases} ELSE {L(elseValue)} END";
        }

        foreach (var (t, c) in new[] { ("articles", "DocumentJson"), ("articles", "PublishedDocumentJson"), ("contents", "ValuesJson"), ("content_revisions", "ValuesJson") })
        {
            var name = column(c);
            var value = name;
            foreach (var (from, to) in map) value = $"REPLACE({value}, {L($"\"postKind\":\"{from}\"")}, {L($"\"postKind\":\"{to}\"")})";
            yield return $"UPDATE {table(t)} SET {name} = {value} WHERE {name} LIKE {L("%\"postKind\":\"%")}";
        }
    }
}
