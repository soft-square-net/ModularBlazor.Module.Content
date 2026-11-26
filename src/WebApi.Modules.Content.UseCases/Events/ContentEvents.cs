using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Events;

/// <summary>
/// The integration events Content publishes (source <c>content</c>, subject <c>post:{id}</c>, the envelope's unit path is
/// the post's). Subscribers copy the records; no type is shared. Both carry what the post is filed under now: its press,
/// its categories (primary first), its unit and the unit's path. Elsa's <c>EnsurePostInUnitCategories</c> reacts to them.
/// </summary>
public static class ContentEvents
{
    public const string Source = "content";
    public const string PostCategoriesChanged = "content.post-categories-changed.v1";
    public const string PostUnitChanged = "content.post-unit-changed.v1";

    public static string PostSubject(int postId) => $"post:{postId}";
}

/// <summary>Data of <c>content.post-categories-changed.v1</c>: the post's categories now (and before).</summary>
public sealed record PostCategoriesChanged(int PostId, string PublicId, int Version, string PressId, string PrimaryCategoryId,
    IReadOnlyList<string> Categories, IReadOnlyList<string> PreviousCategories, Guid? UnitId, string? UnitPath);

/// <summary>Data of <c>content.post-unit-changed.v1</c>: the post's unit now (null: none) and before.</summary>
public sealed record PostUnitChanged(int PostId, string PublicId, int Version, string PressId, string PrimaryCategoryId,
    IReadOnlyList<string> Categories, Guid? UnitId, string? UnitPath, Guid? PreviousUnitId);

/// <summary>Adds Content's events to the outbox of the save that makes the change (sent once it commits).</summary>
public sealed class ContentEventPublisher(IIntegrationEvents events)
{
    public void CategoriesChanged(Post post, IReadOnlyList<string> previous) =>
        events.Publish(ContentEvents.PostCategoriesChanged,
            new PostCategoriesChanged(post.Id, post.StablePublicId, post.Version, post.PressId, post.PrimaryCategoryId, post.Categories, previous,
                post.UnitId, post.UnitPath),
            ContentEvents.PostSubject(post.Id), post.UnitPath);

    public void UnitChanged(Post post, Guid? previous) =>
        events.Publish(ContentEvents.PostUnitChanged,
            new PostUnitChanged(post.Id, post.StablePublicId, post.Version, post.PressId, post.PrimaryCategoryId, post.Categories,
                post.UnitId, post.UnitPath, previous),
            ContentEvents.PostSubject(post.Id), post.UnitPath);
}
