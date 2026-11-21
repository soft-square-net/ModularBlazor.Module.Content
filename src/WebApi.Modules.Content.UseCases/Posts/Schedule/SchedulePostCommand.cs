namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;

/// <summary>Desk schedules the current version for publication; any later edit cancels the schedule.</summary>
public sealed record SchedulePostCommand(int Id, int Version, DateTimeOffset PublishAt) : ICommand<Result<PostDetail>>;
