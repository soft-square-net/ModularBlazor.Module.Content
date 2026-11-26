using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace soft.square.net.WebApi.Modules.Content.Web.Extensions;

/// <summary>The responses of a step that DynamicFields checks first (<see cref="ResultExtensions.ProblemsKey"/>).</summary>
public static class ValidationProblems
{
    /// <summary>
    /// 422: the content is not valid (problem details with <c>problems</c>: <c>{ path, message, nodeId }</c>); 503:
    /// DynamicFields cannot answer, nothing changed.
    /// </summary>
    public static RouteHandlerBuilder ProducesValidationFailure(this RouteHandlerBuilder endpoint) =>
        endpoint.ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
