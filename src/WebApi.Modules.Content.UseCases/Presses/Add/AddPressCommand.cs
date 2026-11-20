namespace soft.square.net.WebApi.Modules.Content.UseCases.Presses.Add;

/// <summary>Add a press (needs content.manage for the whole tenant).</summary>
public sealed record AddPressCommand(string Id, string Name) : ICommand<Result<PressDto>>;
