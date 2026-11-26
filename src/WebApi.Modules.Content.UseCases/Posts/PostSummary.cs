using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>A row of a Post list.</summary>
public sealed record PostSummary(int Id, string PostKind, string PressId, string CategoryId, string Title, string OwnerId, PostState State, int Version,
    DateTimeOffset? AcceptedAt, string? AcceptedShiftId,
    string? AssigneeId, DateTimeOffset? SubmittedAt, DateTimeOffset? ScheduledFor, string? TransferredFromShift, DateTimeOffset UpdatedAt);
