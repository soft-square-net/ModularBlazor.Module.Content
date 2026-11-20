namespace soft.square.net.WebApi.Modules.Content.Core.Common;

/// <summary>Validates the display names of presses and categories.</summary>
internal static class Names
{
    public static string Require(string value, string what) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200 ? value.Trim() : throw new DomainException($"A {what} needs a name (at most 200 characters).");
}
