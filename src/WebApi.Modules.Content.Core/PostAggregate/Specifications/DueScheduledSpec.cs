namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>Scheduled Posts whose publish time has come (at most 100 per run).</summary>
public sealed class DueScheduledSpec : Specification<Post>
{
    public DueScheduledSpec(DateTimeOffset now) => Query.Where(a => a.State == PostState.Scheduled && a.ScheduledFor <= now).Take(100);
}
