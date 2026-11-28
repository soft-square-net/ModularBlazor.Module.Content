namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Accept;

/// <summary>
/// Accept a submission into the review queue; the owner is locked out from now on. <see cref="ShiftId"/>, when given, is
/// recorded as the shift whose queue it joined.
/// </summary>
public sealed record AcceptPostCommand(int Id, int Version, string? ShiftId) : ICommand<Result<PostDetail>>;
