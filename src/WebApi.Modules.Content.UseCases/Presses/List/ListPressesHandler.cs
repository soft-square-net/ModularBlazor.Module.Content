using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Presses.List;

public sealed class ListPressesHandler(IReadRepository<Press> presses) : IQueryHandler<ListPressesQuery, Result<IReadOnlyList<PressDto>>>
{
    public async ValueTask<Result<IReadOnlyList<PressDto>>> Handle(ListPressesQuery query, CancellationToken ct) =>
        (await presses.ListAsync(ct)).OrderBy(p => p.Id).Select(p => new PressDto(p.Id, p.Name)).ToArray();
}
