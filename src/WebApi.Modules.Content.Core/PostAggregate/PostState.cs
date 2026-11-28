using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// Workflow states (see the auth design, "Content workflow"):
/// <code>
/// Draft → Submitted ─(Manager accepts)→ Accepted ─(assign)→ InReview ─(approve)→ Approved → Scheduled → Published
/// Returned ← Manager or Desk;  an edit while Approved/Scheduled → AwaitingDeskReapproval (schedule cancelled)
/// </code>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PostState>))]
public enum PostState { Draft, Submitted, Accepted, InReview, Returned, Approved, Scheduled, AwaitingDeskReapproval, Published }

/// <summary>What <see cref="Post.CheckReviewer"/> did: nothing, noted the reviewer's absence or return, or handed the review back.</summary>
public enum ReviewerCheck { Unchanged, Noted, HandedOver }
