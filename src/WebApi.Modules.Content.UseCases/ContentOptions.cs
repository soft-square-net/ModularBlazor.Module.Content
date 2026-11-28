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

    /// <summary>How often the module's clock does its timed work (reviews to hand back). Default 15 seconds.</summary>
    public TimeSpan ClockInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>The newsroom workflow's settings (section <c>Content:Newsroom</c>).</summary>
    public NewsroomOptions Newsroom { get; set; } = new();
}

/// <summary>Section <c>Content:Newsroom</c>.</summary>
public sealed class NewsroomOptions
{
    /// <summary>
    /// Whether the newsroom workflow is the only one that assigns the work of its posts (those with a NewsRoom project that is
    /// not final). When true, accept, assign, transfer and return of such a post, and assigning its contents, need
    /// <c>content.unit.assign</c> instead of the manual rights; other posts are still assigned by hand. Default false.
    /// </summary>
    public bool WorkflowOwnsAssignment { get; set; }

    /// <summary>
    /// How long the assigned reviewer of a post in review may be without the review right (their shift ended, say) before
    /// the module hands the review back to the queue. The delay leaves a workflow, or a late check-in, time to act. Default
    /// 10 minutes.
    /// </summary>
    public TimeSpan ReviewHandoverGrace { get; set; } = TimeSpan.FromMinutes(10);
}
