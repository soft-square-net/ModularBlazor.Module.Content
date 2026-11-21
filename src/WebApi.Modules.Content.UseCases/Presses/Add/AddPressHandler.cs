using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Presses.Add;

public sealed class AddPressHandler(IRepository<Press> presses, IAccessScopes access) : ICommandHandler<AddPressCommand, Result<PressDto>>
{
    public async ValueTask<Result<PressDto>> Handle(AddPressCommand command, CancellationToken ct)
    {
        if (!(await access.ScopesAsync(ContentPermissions.Manage, ct)).Contains(AccessScope.Tenant))
            return Result<PressDto>.Forbidden(TaxonomyMessages.ManageNeedsTenant);
        if (await presses.GetByIdAsync(command.Id, ct) is not null)
            return Result<PressDto>.Conflict($"Press '{command.Id}' exists already.");

        var press = await presses.AddAsync(Press.Create(command.Id, command.Name), ct);
        return new PressDto(press.Id, press.Name);
    }
}
