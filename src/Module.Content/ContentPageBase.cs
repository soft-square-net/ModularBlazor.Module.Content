using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using soft.square.net.ModularBlazor.Abstractions;
using soft.square.net.ModularBlazor.Abstractions.MudTail;
using soft.square.net.WebApi.Modules.Content.Client;

namespace soft.square.net.Module.Content;

/// <summary>
/// What every Contents Management page shares: the Content API client on the host's "WebApi" HttpClient (the gateway,
/// with the access token), one-call-at-a-time state and readable error messages.
/// </summary>
public abstract class ContentPageBase : ComponentBase
{
    [Inject] protected IHttpClientFactory HttpClients { get; set; } = default!;
    [Inject] protected MudTailRuntime MudTail { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IStringLocalizerFactory Texts { get; set; } = default!;

    private IStringLocalizer? _l;

    /// <summary>This module's texts (its embedded i18n/{culture}.json); the keys are the English texts.</summary>
    protected IStringLocalizer L => _l ??= Texts.Create(GetType());

    /// <summary>Content routes (/api/content/...) reach the pool that runs the module through the gateway.</summary>
    protected ContentClient Content => new(HttpClients.CreateClient(HttpClientNames.WebApi));

    /// <summary>A category's name in the UI language: its Arabic name in Arabic, else its English one.</summary>
    protected static string CategoryName(CategoryDto category) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" && !string.IsNullOrWhiteSpace(category.NameAr)
            ? category.NameAr
            : string.IsNullOrWhiteSpace(category.NameEn) ? category.Name : category.NameEn;

    protected bool Busy { get; private set; }
    protected string? Error { get; set; }

    /// <summary>Runs one API call: busy flag, error message and toast, and <paramref name="then"/> when it succeeded.</summary>
    protected async Task<bool> RunAsync(Func<Task> call, Func<Task>? then = null, bool toast = true)
    {
        Busy = true;
        Error = null;
        StateHasChanged();
        try
        {
            await call();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect(); // the session ended: sign in again and come back
            return false;
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
            if (toast) await MudTail.ToastAsync(Error, "error");
            return false;
        }
        finally
        {
            Busy = false;
            StateHasChanged();
        }

        if (then is not null) await then();
        StateHasChanged();
        return true;
    }

    protected string Describe(Exception ex) => ex switch
    {
        ContentApiException { StatusCode: 400 } api when Mentions(api, "tenant") =>
            L["400: no tenant. Contents belong to a tenant: sign in to one (your organisation) to manage its contents."],
        ContentApiException { StatusCode: 401 } => L["401: the gateway did not accept the access token. Sign in again."],
        ContentApiException { StatusCode: 403 } api =>
            $"403: {ProblemDetail(api) ?? L["your account lacks the content permission for this (content.* in this tenant)."]}",
        ContentApiException { StatusCode: 404 } api => $"404: {ProblemDetail(api) ?? L["not found, or not visible to you."]}",
        ContentApiException { StatusCode: 409 } api => $"409: {ProblemDetail(api) ?? L["someone changed it meanwhile. Reload and try again."]}",
        ContentApiException api => $"{api.StatusCode}: {ProblemDetail(api) ?? FirstLine(api.Response) ?? FirstLine(api.Message)}",
        HttpRequestException http => L["The gateway is not reachable ({0}).", http.Message],
        _ => ex.Message,
    };

    /// <summary>The "detail" (else "title") of a problem-details response body, if it is one.</summary>
    private static string? ProblemDetail(ContentApiException api)
    {
        if (string.IsNullOrWhiteSpace(api.Response) || !api.Response.TrimStart().StartsWith('{')) return null;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(api.Response);
            foreach (var name in new[] { "detail", "title" })
                if (json.RootElement.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text)
                    return text;
        }
        catch (System.Text.Json.JsonException) { }
        return null;
    }

    private static bool Mentions(ContentApiException api, string word) =>
        (api.Response ?? "").Contains(word, StringComparison.OrdinalIgnoreCase);

    private static string? FirstLine(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Split('\n', 2)[0].Trim();

    protected static string When(DateTimeOffset? at) => at?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "";

    /// <summary>The tenant's published post kinds (GET /api/content/kinds); null while they are not read, or could not be.</summary>
    protected IReadOnlyList<PostKindInfo>? Kinds { get; private set; }

    /// <summary>Why the kinds could not be read, or null.</summary>
    protected string? KindsError { get; private set; }

    /// <summary>
    /// Reads the tenant's kinds. A failure (DynamicFields unreachable, or refusing the caller) leaves the page working:
    /// kinds are then named from the starter library, and the new post form offers those.
    /// </summary>
    protected async Task LoadKindsAsync()
    {
        try
        {
            Kinds = [.. await Content.ListPostKindsAsync()];
            KindsError = null;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (Exception ex)
        {
            Kinds = null;
            KindsError = Describe(ex);
        }
    }

    /// <summary>A post kind (a root field key) by name, in the UI language where the module knows it (see <see cref="ContentNames.Kind"/>).</summary>
    protected string KindName(string? kind) =>
        ContentNames.Kind(kind, Kinds?.FirstOrDefault(k => k.Key == kind)?.Title, text => L[text]);

    /// <summary>"InReview" → "In review", in the user's language (the key is the English words).</summary>
    protected string Words(Enum value) => L[EnglishWords(value)];

    private static string EnglishWords(Enum value)
    {
        var text = value.ToString();
        var result = new System.Text.StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i])) result.Append(' ').Append(char.ToLowerInvariant(text[i]));
            else result.Append(text[i]);
        }
        return result.ToString();
    }
}
