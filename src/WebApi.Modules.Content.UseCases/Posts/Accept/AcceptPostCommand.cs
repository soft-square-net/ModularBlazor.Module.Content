namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Accept;

/// <summary>The shift manager accepts a submission into the shift's queue; the owner is locked out from now on.</summary>
public sealed record AcceptPostCommand(int Id, int Version, string ShiftId) : ICommand<Result<PostDetail>>;
