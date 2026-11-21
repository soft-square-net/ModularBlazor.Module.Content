namespace soft.square.net.WebApi.Modules.Content.UseCases.Presses.List;

/// <summary>Every press of the tenant, by id.</summary>
public sealed record ListPressesQuery : IQuery<Result<IReadOnlyList<PressDto>>>;
