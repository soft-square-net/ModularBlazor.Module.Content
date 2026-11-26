namespace soft.square.net.Content.Contracts;

/// <summary>
/// The work items Content owns, as the platform's work-item events name them (<c>WorkItemRef(Module, Type, Id)</c>,
/// written <c>{Module}:{Type}:{Id}</c>, also the events' subject): every content of the shared library is one,
/// <c>content:content:{contentId}</c>. Its state is the content's pipeline stage key (<see cref="States"/> for the built-in
/// pipeline; a tenant's published pipeline may name others).
/// </summary>
public static class ContentWorkItems
{
    /// <summary>The owning module (<c>WorkItemRef.Module</c>).</summary>
    public const string Module = "content";

    /// <summary>A content of the shared library (<c>WorkItemRef.Type</c>); its id is the content's id (a GUID, "D" format).</summary>
    public const string Content = "content";

    /// <summary><c>content.work-item-assigned.v1</c>.</summary>
    public const string AssignedEvent = ContentEventTypes.WorkItemAssigned;

    /// <summary><c>content.work-item-state-changed.v1</c>.</summary>
    public const string StateChangedEvent = ContentEventTypes.WorkItemStateChanged;

    /// <summary><c>content:content:{id}</c>.</summary>
    public static string RefOf(Guid contentId) => $"{Module}:{Content}:{contentId:D}";

    /// <summary>The content id of a <c>content:content:{id}</c> reference, or null when it is not one.</summary>
    public static Guid? ContentIdOf(string? reference) =>
        reference is not null && reference.StartsWith($"{Module}:{Content}:", StringComparison.Ordinal)
        && Guid.TryParse(reference.AsSpan(Module.Length + Content.Length + 2), out var id)
            ? id
            : null;

    /// <summary>
    /// The states of the built-in pipeline: assigned → in-progress → submitted → in-review → accepted | returned.
    /// </summary>
    public static class States
    {
        public const string Assigned = "assigned";
        public const string InProgress = "in-progress";
        public const string Submitted = "submitted";
        public const string InReview = "in-review";
        public const string Accepted = "accepted";
        public const string Returned = "returned";

        /// <summary>All six, in the pipeline's order.</summary>
        public static IReadOnlyList<string> All { get; } = [Assigned, InProgress, Submitted, InReview, Accepted, Returned];

        /// <summary>
        /// The stage keys of the first built-in pipeline and today's (stored stages were renamed by the migration
        /// <c>WorkItemStages</c>; <c>PUT /pipeline</c> still accepts the old keys): draft → assigned, desk → submitted,
        /// rejected → returned; accepted kept its key.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Legacy { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["draft"] = Assigned,
            ["desk"] = Submitted,
            ["rejected"] = Returned
        };
    }
}
