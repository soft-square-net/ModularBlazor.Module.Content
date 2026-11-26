using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// One Post with its whole history, as its owner, reviewer or Desk sees it. <see cref="Document"/> is the working document
/// (root values and the contents' current revisions); <see cref="Contents"/> the contents it uses, in reading order, with
/// their stage and pins; <see cref="Slugs"/> its URL slug per culture.
/// <para><see cref="PostKind"/> is a DynamicFields root post field key; <see cref="UrlPrefix"/> its static URL prefix now (null
/// while the kind has no published root field, or DynamicFields cannot answer: the post then cannot be published).
/// <see cref="CanonicalPath"/> is the live version's public path, <c>/{prefix}/{slug}</c> with the prefix it was published
/// under (null until it is published under one).</para>
/// </summary>
public sealed record PostDetail(int Id, string PostKind, string PressId, string CategoryId, string Title, string Body, string OwnerId, PostState State,
    int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? SubmittedAt, bool OwnerMayEdit, string? AcceptedBy,
    DateTimeOffset? AcceptedAt, string? AcceptedShiftId,
    string? AssigneeId, string? AssignedShiftId, string? ReviewerId, DateTimeOffset? ScheduledFor, string? ScheduledBy,
    DateTimeOffset? PublishedAt, string? PublishedBy, string? PublishedTitle, string? PublishedBody,
    string? TransferredFromShift, IReadOnlyList<PostEventDto> History, string PublicId, string Permalink,
    string SchemaId, int SchemaVersion, PostDocument Document, IReadOnlyList<PostTranslation> Translations,
    IReadOnlyList<PostContentDto> Contents, IReadOnlyDictionary<string, string> Slugs, string? UrlPrefix, string? CanonicalPath);

/// <summary>One entry of <see cref="PostDetail.History"/>.</summary>
public sealed record PostEventDto(DateTimeOffset At, string Actor, string Action, PostState State, string? Detail);
