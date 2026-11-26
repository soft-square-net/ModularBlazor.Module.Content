using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations;

/// <summary>
/// The data part of the <c>WorkItemStages</c> migration, the same on every engine (only the names are quoted differently):
/// the stage keys of the first built-in pipeline become today's (<see cref="PipelineStageKeys.Legacy"/>: draft → assigned,
/// desk → submitted, rejected → returned; accepted is unchanged)
/// <list type="bullet">
/// <item>in <c>contents.Stage</c>;</item>
/// <item>in a published pipeline (<c>pipelines.DefinitionJson</c>, every <c>"draft"</c> JSON string), as
/// <c>PUT /pipeline</c> renames them.</item>
/// </list>
/// A key is left alone when the tenant's published pipeline already has a stage with its new key (a custom pipeline that
/// uses both). The history (<c>content_history</c>, the feed) keeps the keys it was written with.
/// Down renames them back, and, while the tenant has no published pipeline, moves the stages that did not exist before
/// (in-progress → draft, in-review → desk).
/// </summary>
internal static class WorkItemStagesSql
{
    private static readonly (string From, string To)[] Added =
        [(PipelineStageKeys.InProgress, "draft"), (PipelineStageKeys.InReview, "desk")];

    /// <param name="table">Quotes a table of the module (<c>contents</c>, <c>pipelines</c>) with its schema or prefix.</param>
    /// <param name="column">Quotes a column name.</param>
    /// <param name="national">Writes the literals as national strings (<c>N'...'</c>, Oracle's NVARCHAR2 and NCLOB columns).</param>
    public static IEnumerable<string> Up(Func<string, string> table, Func<string, string> column, bool national = false) =>
        Statements(table, column, national, [.. PipelineStageKeys.Legacy.Select(m => (m.Key, m.Value))], []);

    public static IEnumerable<string> Down(Func<string, string> table, Func<string, string> column, bool national = false) =>
        Statements(table, column, national, [.. PipelineStageKeys.Legacy.Select(m => (m.Value, m.Key))], Added);

    private static IEnumerable<string> Statements(Func<string, string> table, Func<string, string> column, bool national,
        IReadOnlyList<(string From, string To)> renames, IReadOnlyList<(string From, string To)> defaultOnly)
    {
        string L(string text) => (national ? "N'" : "'") + text + "'";
        string Quoted(string key) => $"\"{key}\"";
        var contents = table("contents");
        var pipelines = table("pipelines");
        var stage = column("Stage");
        var json = column("DefinitionJson");

        // The contents first: the condition reads the pipeline before it is renamed.
        foreach (var (from, to) in renames)
            yield return $"UPDATE {contents} SET {stage} = {L(to)} WHERE {stage} = {L(from)} " +
                         $"AND NOT EXISTS (SELECT 1 FROM {pipelines} WHERE {json} LIKE {L($"%{Quoted(to)}%")})";
        foreach (var (from, to) in defaultOnly)
            yield return $"UPDATE {contents} SET {stage} = {L(to)} WHERE {stage} = {L(from)} AND NOT EXISTS (SELECT 1 FROM {pipelines})";
        foreach (var (from, to) in renames)
            yield return $"UPDATE {pipelines} SET {json} = REPLACE({json}, {L(Quoted(from))}, {L(Quoted(to))}) " +
                         $"WHERE {json} LIKE {L($"%{Quoted(from)}%")} AND {json} NOT LIKE {L($"%{Quoted(to)}%")}";
    }
}
