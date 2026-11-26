using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;

/// <summary>What changed about a content as a work item.</summary>
public enum ContentWorkItemChange { Assigned, StateChanged }

/// <summary>
/// Raised by <see cref="ContentItem"/> when it moves to another stage (its work-item state) or gets an assignee; a handler
/// adds <c>content.work-item-state-changed.v1</c> or <c>content.work-item-assigned.v1</c> to the outbox of the same save.
/// </summary>
public sealed record ContentWorkItemChanged(ContentWorkItemChange Change, Guid ContentId, string Title, string Stage, string PreviousStage,
    string? AssigneeId, string? PreviousAssigneeId, Guid? UnitId, string? UnitPath) : IDomainEvent;

/// <summary>A content's title for lists, from its values.</summary>
public static class ContentTitles
{
    private static readonly string[] Preferred = ["title", "heading", "caption", "alt", "text"];

    public static string Of(PostNode node, string fieldKey, string nodeId, int max)
    {
        var text = Find(node.Content);
        text = string.IsNullOrWhiteSpace(text)
            ? $"{fieldKey} {nodeId}"
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    private static string? Find(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in Preferred)
            foreach (var property in content.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && Text(property.Value) is { } preferred)
                    return preferred;
        foreach (var property in content.EnumerateObject())
            if (Text(property.Value) is { } any)
                return any;
        return null;
    }

    // A string, or a localized value ({"en": "...", "ar": "..."}): its first non-empty string.
    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString(),
        JsonValueKind.Object => value.EnumerateObject().Select(p => p.Value).Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
        _ => null
    };
}
