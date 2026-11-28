namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>The ids of Posts in review after this id, in id order (the clock reads them in batches).</summary>
public sealed class InReviewPostIdsSpec : Specification<Post, int>
{
    public InReviewPostIdsSpec(int afterId, int take) =>
        Query.Where(a => a.State == PostState.InReview && a.Id > afterId).OrderBy(a => a.Id).Take(take).Select(a => a.Id);
}
