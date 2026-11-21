using soft.square.net.WebApi.Modules.Content.UseCases.Presses;
using soft.square.net.WebApi.Modules.Content.UseCases.Presses.Add;

namespace soft.square.net.WebApi.Modules.Content.Web.Presses;

/// <summary>POST /presses: add a press (content.manage for the whole tenant).</summary>
public sealed class Add : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/presses", HandleAsync)
            .RequirePermission(ContentPermissions.Manage)
            .WithName("Content_AddPress");

    private static async Task<Results<Ok<PressDto>, ProblemHttpResult>> HandleAsync(PressDto press, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AddPressCommand(press.Id, press.Name), ct)).ToOkResult();
}
