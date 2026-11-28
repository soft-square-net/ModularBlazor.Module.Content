namespace soft.square.net.WebApi.Modules.Content.UseCases;

/// <summary>Module settings (section <c>Content</c>).</summary>
public sealed class ContentOptions
{
    /// <summary>How long the owner may keep editing after submitting (until accepted or assigned). Default 24 hours.</summary>
    public TimeSpan OwnerEditWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// The newest feed events held back from readers (default 2 seconds), so a slower save cannot land behind an id a
    /// reader has already passed.
    /// </summary>
    public TimeSpan EventDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The gateway's internal address, where this module calls DynamicFields (<c>{ApiBaseUrl}/api/dynamicfields/...</c>) for
    /// the post kinds. Always over HTTP: modules load in isolated contexts and never call each other in process.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>How long the tenant's post kinds (DynamicFields' root post fields) stay cached, in seconds (0: not cached). Default 60.</summary>
    public int PostKindCacheSeconds { get; set; } = 60;

    /// <summary>
    /// How long an Organization unit (its path and categories, read from Organization's directory at
    /// <c>{ApiBaseUrl}/api/organization/local/units/...</c>) stays cached, in seconds (0: not cached). Default 60; an
    /// <c>org.unit-*</c> event drops it at once.
    /// </summary>
    public int UnitCacheSeconds { get; set; } = 60;

    /// <summary>The newsroom workflow's settings (section <c>Content:Newsroom</c>).</summary>
    public NewsroomOptions Newsroom { get; set; } = new();
}

/// <summary>Section <c>Content:Newsroom</c>.</summary>
public sealed class NewsroomOptions
{
    /// <summary>
    /// Whether the newsroom workflow is the only one that assigns work. When true, a post's accept, assign, transfer and
    /// return, and a content's assign, need <c>content.unit.assign</c> instead of the shift manager's (or the Desk's)
    /// rights. Default false (the manual desk flow) until NewsRoom ships.
    /// </summary>
    public bool WorkflowOwnsAssignment { get; set; }
}
