namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>Posts still in review that were assigned in this shift (the handover at check-out).</summary>
public sealed class AssignedShiftPostsSpec : Specification<Post>
{
    public AssignedShiftPostsSpec(string shiftId) => Query
        .Where(a => a.State == PostState.InReview && a.AssignedShiftId == shiftId)
        .OrderBy(a => a.Id).Take(500);
}
