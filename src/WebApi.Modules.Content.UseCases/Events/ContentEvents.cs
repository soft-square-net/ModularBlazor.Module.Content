using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Contracts.WorkItems;
using soft.square.net.WebApi.Modules.Content.Core.Common;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Events;

/// <summary>
/// Adds Content's integration events (<see cref="ContentEventTypes"/>, records in <c>Content.Contracts</c>) to the outbox of
/// the save that makes the change; the platform sends them once it commits.
/// </summary>
public sealed class ContentEventPublisher(IIntegrationEvents events)
{
    public void PostCreated(Post post) =>
        events.Publish(ContentEventTypes.PostCreated,
            new PostCreated(post.Id, post.StablePublicId, post.Version, post.PostKind, post.Title, post.OwnerId, post.State.ToString(),
                post.PressId, post.PrimaryCategoryId, post.Categories, [.. post.Contents.Select(c => c.ContentId)], post.UnitId, post.UnitPath,
                post.CreatedAt),
            ContentEventTypes.PostSubject(post.Id), post.UnitPath);

    public void CategoriesChanged(Post post, IReadOnlyList<string> previous) =>
        events.Publish(ContentEventTypes.PostCategoriesChanged,
            new PostCategoriesChanged(post.Id, post.StablePublicId, post.Version, post.PressId, post.PrimaryCategoryId, post.Categories, previous,
                post.UnitId, post.UnitPath),
            ContentEventTypes.PostSubject(post.Id), post.UnitPath);

    public void UnitChanged(Post post, Guid? previous) =>
        events.Publish(ContentEventTypes.PostUnitChanged,
            new PostUnitChanged(post.Id, post.StablePublicId, post.Version, post.PressId, post.PrimaryCategoryId, post.Categories,
                post.UnitId, post.UnitPath, previous),
            ContentEventTypes.PostSubject(post.Id), post.UnitPath);

    /// <summary>A feed row, mapped one to one (<see cref="ContentEventTypes.OfFeedType"/>); a type without a mapping is skipped.</summary>
    public void Feed(FeedEvent e)
    {
        if (!ContentEventTypes.OfFeedType.TryGetValue(e.Type, out var type)) return;
        var detail = e.Detail is { Length: > FeedEntry.DetailLength } text ? text[..FeedEntry.DetailLength] : e.Detail;
        events.Publish(type, new ContentFeedEvent(e.Type, e.PostId, e.ContentId, e.Version, e.Revision, e.Stage, e.PostState, e.By, e.At, detail),
            SubjectOf(e.PostId, e.ContentId), e.UnitPath);
    }

    public void ValidationFailed(ContentValidationFailed failure, string? unitPath) =>
        events.Publish(ContentEventTypes.ValidationFailed, failure, SubjectOf(failure.PostId, failure.ContentId), unitPath);

    /// <summary>The platform's work-item events for a content (<c>content:content:{id}</c>, state = its stage key).</summary>
    public void WorkItem(ContentWorkItemChanged change)
    {
        var unitPath = change.UnitPath;
        var item = new WorkItemSnapshot(new WorkItemRef(ContentWorkItems.Module, ContentWorkItems.Content, change.ContentId.ToString("D")),
            change.Title, change.Stage, AssigneeOf(change.AssigneeId), DueAt: null,
            change.UnitId is { } unit ? UnitPathRules.Id(unit) : null);
        if (change.Change == ContentWorkItemChange.Assigned) events.PublishWorkItemAssigned(item, AssigneeOf(change.PreviousAssigneeId), unitPath);
        else events.PublishWorkItemStateChanged(item, change.PreviousStage, unitPath);
    }

    private static WorkItemAssignee? AssigneeOf(string? userId) => userId is null ? null : WorkItemAssignee.User(userId);

    private static string SubjectOf(int? postId, Guid? contentId) =>
        postId is { } post ? ContentEventTypes.PostSubject(post) : contentId is { } content ? ContentEventTypes.ContentSubject(content) : "content";
}

/// <summary>
/// Maps every feed row onto the integration outbox in the save that raised it (registered after <c>FeedWriter</c>): the
/// feed (<c>GET /events</c>) and the integration events never disagree.
/// </summary>
public sealed class FeedEventsToOutbox(ContentEventPublisher publisher) : IDomainEventHandler<FeedEvent>
{
    public Task HandleAsync(FeedEvent domainEvent, CancellationToken cancellationToken)
    {
        publisher.Feed(domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>A content's stage move or assignment as the platform's work-item events, in the same save.</summary>
public sealed class WorkItemsToOutbox(ContentEventPublisher publisher) : IDomainEventHandler<ContentWorkItemChanged>
{
    public Task HandleAsync(ContentWorkItemChanged domainEvent, CancellationToken cancellationToken)
    {
        publisher.WorkItem(domainEvent);
        return Task.CompletedTask;
    }
}
