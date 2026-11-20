namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.HandOverShiftWork;

/// <summary>
/// At automatic shift check-out, return every unfinished review of the shift to the incoming shift's queue. Safe to
/// repeat. Returns how many Posts moved.
/// </summary>
public sealed record HandOverShiftWorkCommand(string ShiftId) : ICommand<Result<int>>;
