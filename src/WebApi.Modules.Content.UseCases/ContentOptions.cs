namespace soft.square.net.WebApi.Modules.Content.UseCases;

/// <summary>Module settings (section <c>Content</c>).</summary>
public sealed class ContentOptions
{
    /// <summary>How long the owner may keep editing after submitting (until accepted or assigned). Default 24 hours.</summary>
    public TimeSpan OwnerEditWindow { get; set; } = TimeSpan.FromHours(24);
}
