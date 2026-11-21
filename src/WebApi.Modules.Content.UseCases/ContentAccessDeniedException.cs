namespace soft.square.net.WebApi.Modules.Content.UseCases;

/// <summary>
/// The caller is not signed in, on an endpoint that requires it (the Web layer maps it to 403). Expected outcomes
/// (not found, forbidden, conflict) are returned as an Ardalis <see cref="Result"/> instead; this exception is only
/// for the case the host's authentication should already have ruled out.
/// </summary>
public sealed class ContentAccessDeniedException(string message) : Exception(message);
