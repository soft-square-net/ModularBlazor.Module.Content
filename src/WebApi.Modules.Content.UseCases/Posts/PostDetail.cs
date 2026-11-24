using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// One Post with its whole history, as its owner, reviewer or Desk sees it. <see cref="Document"/> is the working document
/// (root values and the contents' current revisions); <see cref="Contents"/> the contents it uses, in reading order, with
/// their stage and pins; <see cref="Slugs"/> its URL slug per culture.
/// </summary>
public sealed record PostDetail(int Id, PostKind PostKind, string PressId, string CategoryId, string Title, string Body, string OwnerId, PostState State,
    int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? SubmittedAt, bool OwnerMayEdit, string? AcceptedBy,
    DateTimeOffset? AcceptedAt, string? AcceptedShiftId,
    string? AssigneeId, string? AssignedShiftId, string? ReviewerId, DateTimeOffset? ScheduledFor, string? ScheduledBy,
    DateTimeOffset? PublishedAt, string? PublishedBy, string? PublishedTitle, string? PublishedBody,
    string? TransferredFromShift, IReadOnlyList<PostEventDto> History, string PublicId, string Permalink,
    string SchemaId, int SchemaVersion, PostDocument Document, IReadOnlyList<PostTranslation> Translations,
    IReadOnlyList<PostContentDto> Contents, IReadOnlyDictionary<string, string> Slugs);

/// <summary>One entry of <see cref="PostDetail.History"/>.</summary>
public sealed record PostEventDto(DateTimeOffset At, string Actor, string Action, PostState State, string? Detail);
