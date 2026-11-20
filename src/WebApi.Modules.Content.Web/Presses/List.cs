using soft.square.net.WebApi.Modules.Content.UseCases.Presses;
using soft.square.net.WebApi.Modules.Content.UseCases.Presses.List;

namespace soft.square.net.WebApi.Modules.Content.Web.Presses;

/// <summary>GET /presses: every press of the tenant.</summary>
public sealed class List : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/presses", HandleAsync)
            .WithName("Content_ListPresses");

    private static async Task<Results<Ok<IReadOnlyList<PressDto>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListPressesQuery(), ct)).ToOkResult();
}
