namespace soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;

/// <summary>
/// The stage keys of the built-in pipeline (<see cref="PipelineDefinition.Default"/>). A content's stage is its work-item
/// state (<c>content.work-item-state-changed.v1</c>): assigned → in-progress → submitted → in-review → accepted | returned.
/// </summary>
public static class PipelineStageKeys
{
    public const string Assigned = "assigned";
    public const string InProgress = "in-progress";
    public const string Submitted = "submitted";
    public const string InReview = "in-review";
    public const string Accepted = "accepted";
    public const string Returned = "returned";

    /// <summary>
    /// The first built-in pipeline's stage keys and today's: draft → assigned (both the start stage), desk → submitted (handed
    /// to the Desk, not yet taken up), rejected → returned. <c>accepted</c> kept its key. The migration
    /// <c>WorkItemStages</c> renames stored stages; <c>PUT /pipeline</c> renames them in a published definition.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Legacy { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = Assigned,
        ["desk"] = Submitted,
        ["rejected"] = Returned
    };

    /// <summary>The first built-in pipeline's action keys that were renamed: reject → return.</summary>
    public static IReadOnlyDictionary<string, string> LegacyActions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["reject"] = "return"
    };
}
