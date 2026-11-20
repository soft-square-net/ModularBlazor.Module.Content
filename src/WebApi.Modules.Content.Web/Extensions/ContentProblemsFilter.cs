using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.SharedKernel;

namespace soft.square.net.WebApi.Modules.Content.Web.Extensions;

/// <summary>
/// The exceptions that still reach the endpoints, as problem details: a broken domain rule (the aggregate throws
/// <see cref="DomainException"/>) and a lost race on the Post version are 409; a caller who is not signed in is 403.
/// Expected outcomes come back as an Ardalis Result instead (see <see cref="ResultExtensions"/>). On the module's route
/// group, so it is module code and unloads with the module.
/// </summary>
public sealed class ContentProblemsFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (ContentAccessDeniedException e) { return Results.Problem(e.Message, statusCode: StatusCodes.Status403Forbidden); }
        catch (DbUpdateConcurrencyException) { return Results.Problem("Someone else changed this Post first; reload it and try again.", statusCode: StatusCodes.Status409Conflict); }
        catch (DomainException e) { return Results.Problem(e.Message, statusCode: StatusCodes.Status409Conflict); }
    }
}
