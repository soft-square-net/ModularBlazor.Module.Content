using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Presses.Add;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases.Presses;

public class AddPressHandlerHandle
{
    private readonly IRepository<Press> _presses = Substitute.For<IRepository<Press>>();
    private readonly IAccessScopes _access = Substitute.For<IAccessScopes>();
    private readonly AddPressHandler _handler;

    public AddPressHandlerHandle() => _handler = new AddPressHandler(_presses, _access);

    private void ManageIn(params string[] scopes) =>
        _access.ScopesAsync(ContentPermissions.Manage, Arg.Any<CancellationToken>()).Returns(scopes);

    [Fact]
    public async Task ForbidsAManagerWithoutTheWholeTenant()
    {
        ManageIn(AccessScope.Press("daily"));

        var result = await _handler.Handle(new AddPressCommand("weekly", "Weekly"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Forbidden);
        await _presses.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConflictsWhenThePressExists()
    {
        ManageIn(AccessScope.Tenant);
        _presses.GetByIdAsync("daily", Arg.Any<CancellationToken>()).Returns(Press.Create("daily", "Daily"));

        var result = await _handler.Handle(new AddPressCommand("daily", "Daily"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
    }

    [Fact]
    public async Task AddsThePress()
    {
        ManageIn(AccessScope.Tenant);
        _presses.AddAsync(Arg.Any<Press>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Press>());

        var result = await _handler.Handle(new AddPressCommand("weekly", "Weekly"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe("weekly");
        await _presses.Received(1).AddAsync(Arg.Is<Press>(p => p.Name == "Weekly"), Arg.Any<CancellationToken>());
    }
}
