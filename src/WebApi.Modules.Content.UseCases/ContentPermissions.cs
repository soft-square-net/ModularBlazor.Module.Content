namespace soft.square.net.WebApi.Modules.Content.UseCases;

/// <summary>
/// The content module's permissions. Standing grants (admins): <see cref="Author"/>, <see cref="Editor"/>,
/// <see cref="Manage"/>. Shift grants (written by the shifts module from the rota, valid only during the shift):
/// Manager → accept, assign, transfer, return; Desk → publish, return; Editor → review.
/// </summary>
public static class ContentPermissions
{
    public const string Author = "content.author";
    /// <summary>Standing editor of a category: may also publish a Post they wrote or reviewed (no second pair of eyes needed).</summary>
    public const string Editor = "content.editor";
    public const string Accept = "content.accept";
    public const string Assign = "content.assign";
    public const string Transfer = "content.transfer";
    public const string Return = "content.return";
    public const string Review = "content.review";
    public const string Publish = "content.publish";
    /// <summary>Presses and categories.</summary>
    public const string Manage = "content.manage";
    /// <summary>Read the change feed of the whole tenant (workflow services: Elsa).</summary>
    public const string Events = "content.events";
    /// <summary>Publish the content pipeline (Elsa, or an admin), tenant-wide.</summary>
    public const string Pipeline = "content.pipeline";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Author, "Create Posts in a press or category (standing)."),
        new(Editor, "Standing editor of a category: may publish their own or self-reviewed Posts."),
        new(Accept, "Accept submissions into the shift's queue (shift Manager)."),
        new(Assign, "Assign queued Posts to an editor on shift (shift Manager)."),
        new(Transfer, "Hand unfinished review work to the next shift (shift Manager)."),
        new(Return, "Send a Post back to its author (shift Manager, Desk)."),
        new(Review, "Edit and approve Posts assigned to you (shift Editor)."),
        new(Publish, "Schedule and publish approved Posts (shift Desk)."),
        new(Manage, "Manage presses and categories."),
        new(Events, "Read the change feed of posts and contents (workflow services), tenant-wide."),
        new(Pipeline, "Publish the content pipeline: stages and actions (workflow services, admins), tenant-wide.")
    ];
}
