using soft.square.net.WebApi.Modules.Content.UseCases.Posts.HandOverShiftWork;

namespace soft.square.net.WebApi.Modules.Content.Web.Automation;

/// <summary>POST /automation/shifts/{shiftId}/handover: at check-out, unfinished reviews go back to the queue.</summary>
public sealed class HandOver : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/automation/shifts/{shiftId}/handover", HandleAsync)
            .RequirePermission(ContentPermissions.Transfer)
            .WithName("Content_HandOverShiftWork")
            .WithSummary("Return all unfinished review work from the outgoing shift to the queue; safe to repeat after recovery.");

    private static async Task<Results<Ok<int>, ProblemHttpResult>> HandleAsync(string shiftId, ISender sender, CancellationToken ct) =>
        (await sender.Send(new HandOverShiftWorkCommand(shiftId), ct)).ToOkResult();
}
