using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>One Post with its whole history, as its owner, reviewer or Desk sees it.</summary>
public sealed record PostDetail(int Id, PostKind PostKind, string PressId, string CategoryId, string Title, string Body, string OwnerId, PostState State,
    int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? SubmittedAt, bool OwnerMayEdit, string? AcceptedBy,
    DateTimeOffset? AcceptedAt, string? AcceptedShiftId,
    string? AssigneeId, string? AssignedShiftId, string? ReviewerId, DateTimeOffset? ScheduledFor, string? ScheduledBy,
    DateTimeOffset? PublishedAt, string? PublishedBy, string? PublishedTitle, string? PublishedBody,
    string? TransferredFromShift, IReadOnlyList<PostEventDto> History, string PublicId, string Permalink,
    string SchemaId, int SchemaVersion, PostDocument Document, IReadOnlyList<PostTranslation> Translations);

/// <summary>One entry of <see cref="PostDetail.History"/>.</summary>
public sealed record PostEventDto(DateTimeOffset At, string Actor, string Action, PostState State, string? Detail);
