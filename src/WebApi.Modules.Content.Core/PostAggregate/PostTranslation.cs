using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// One translated value: the text (a string, or a list of strings for a list value) and the hash of the source value it
/// was translated from. The hash is DynamicFields' (its translate dialog computes it); Contents keeps it as given.
/// </summary>
public sealed record TranslatedText(JsonElement Value, string SourceHash);

/// <summary>
/// A Post's text in one more language, as an overlay on its document: per node id (<see cref="PostTranslationRules.RootId"/>
/// for the document's root), per content key, the translated value. Everything else (shape, ids, numbers, choices,
/// style) is always the document's own. DynamicFields owns the format and its schema rules; Contents keeps one per
/// language next to the document and serves readers the merge.
/// </summary>
public sealed record PostTranslation(
    string Language,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedText>> Nodes,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

/// <summary>
/// The structural rules of translation overlays: what Contents can check without the field definitions (which only
/// DynamicFields knows; its translate dialog checks those before saving). Pure functions over a document.
/// </summary>
public static partial class PostTranslationRules
{
    public const string RootId = "$root";
    public const int MaxLanguages = 20;
    public const int MaxLanguageLength = 35;
    public const int MaxHashLength = 64;
    public const int MaxJsonBytes = 2_000_000;

    /// <summary>A BCP 47 language tag such as ar, en, en-GB or zh-Hant-TW.</summary>
    public static bool IsLanguage(string? tag) => tag is { Length: > 0 and <= MaxLanguageLength } && LanguageTag().IsMatch(tag);

    /// <summary>
    /// Checks an overlay against the document: a language tag, nodes the document has, keys whose source value is text
    /// (a string or a list of strings), translated values of the same shape, a source hash on each, and the size limit.
    /// </summary>
    public static void Validate(PostDocument document, string language,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedText>> nodes)
    {
        if (!IsLanguage(language)) throw new DomainException("A translation's language is a BCP 47 tag such as ar, en or en-GB.");
        var contents = Contents(document);
        foreach (var (id, values) in nodes ?? throw new DomainException("A translation needs its nodes."))
        {
            if (!contents.TryGetValue(id, out var content)) throw new DomainException($"The document has no node '{id}'.");
            foreach (var (key, text) in values ?? throw new DomainException($"Node '{id}' of the translation has no values."))
            {
                if (!TextSource(content, key, out var source)) throw new DomainException($"Node '{id}' has no text value '{key}' to translate.");
                if (text is null) throw new DomainException($"The translation of '{key}' in node '{id}' is missing: leave the key out instead.");
                if (string.IsNullOrWhiteSpace(text.SourceHash) || text.SourceHash.Length > MaxHashLength)
                    throw new DomainException($"The translation of '{key}' in node '{id}' needs the hash of its source value.");
                if (!SameShape(source, text.Value) || !HasText(text.Value))
                    throw new DomainException(source.ValueKind == JsonValueKind.Array
                        ? $"The translation of '{key}' in node '{id}' is a list of texts."
                        : $"The translation of '{key}' in node '{id}' is a text that is not empty.");
            }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(nodes, PostJson.Options).Length > MaxJsonBytes)
            throw new DomainException("A translation exceeds the 2 MB limit.");
    }

    /// <summary>The overlay without the nodes and values the document no longer has as text; null when nothing is left.</summary>
    public static PostTranslation? Prune(PostDocument document, PostTranslation translation)
    {
        var contents = Contents(document);
        var nodes = new Dictionary<string, IReadOnlyDictionary<string, TranslatedText>>(StringComparer.Ordinal);
        foreach (var (id, values) in translation.Nodes)
        {
            if (!contents.TryGetValue(id, out var content)) continue;
            var kept = values.Where(v => v.Value is not null && TextSource(content, v.Key, out var source) && SameShape(source, v.Value.Value))
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
            if (kept.Count > 0) nodes[id] = kept;
        }
        return nodes.Count == 0 ? null : translation with { Nodes = nodes };
    }

    /// <summary>
    /// The languages to read in, best first: the culture, then its parents (ar-EG, then ar), each when a translation
    /// has it (ignoring case). The document's own language needs no entry: it is what is left.
    /// </summary>
    public static IReadOnlyList<string> Chain(string? culture, IEnumerable<string> available)
    {
        var have = available.ToList();
        var chain = new List<string>();
        for (var tag = culture?.Trim() ?? ""; tag.Length > 0; tag = tag.LastIndexOf('-') is var dash and > 0 ? tag[..dash] : "")
            if (have.FirstOrDefault(a => string.Equals(a, tag, StringComparison.OrdinalIgnoreCase)) is { } match && !chain.Contains(match))
                chain.Add(match);
        return chain;
    }

    /// <summary>
    /// The document with each text value from the first translation of <paramref name="chain"/> that has it, else its
    /// own. A translation whose source has changed since is still served (readers prefer it to another language).
    /// </summary>
    public static PostDocument Apply(PostDocument document, IReadOnlyList<PostTranslation> chain)
    {
        if (chain.Count == 0) return document;
        var content = Merge(document.Content, RootId, chain);
        return document with { Content = content, Children = (document.Children ?? []).Select(n => Apply(n, chain)).ToList() };
    }

    private static PostNode Apply(PostNode node, IReadOnlyList<PostTranslation> chain) => node with
    {
        Content = Merge(node.Content, node.NodeId, chain),
        Children = (node.Children ?? []).Select(n => Apply(n, chain)).ToList()
    };

    private static JsonElement Merge(JsonElement content, string id, IReadOnlyList<PostTranslation> chain)
    {
        if (content.ValueKind != JsonValueKind.Object) return content;
        JsonObject? merged = null;
        foreach (var property in content.EnumerateObject())
        {
            if (!TextSource(content, property.Name, out var source)) continue;
            var hit = chain.Select(t => t.Nodes.GetValueOrDefault(id)?.GetValueOrDefault(property.Name))
                .FirstOrDefault(t => t is not null && SameShape(source, t.Value));
            if (hit is null) continue;
            merged ??= JsonNode.Parse(content.GetRawText())!.AsObject();
            merged[property.Name] = JsonNode.Parse(hit.Value.GetRawText());
        }
        return merged is null ? content : JsonSerializer.SerializeToElement(merged);
    }

    /// <summary>The content object of every node, by overlay id: the root, then each node by its node id.</summary>
    private static Dictionary<string, JsonElement> Contents(PostDocument document)
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [RootId] = document.Content };
        void Add(IReadOnlyList<PostNode>? nodes)
        {
            foreach (var node in nodes ?? [])
            {
                if (!string.IsNullOrWhiteSpace(node.NodeId)) contents.TryAdd(node.NodeId, node.Content);
                Add(node.Children);
            }
        }
        Add(document.Children);
        return contents;
    }

    private static bool TextSource(JsonElement content, string key, out JsonElement source)
    {
        source = default;
        return content.ValueKind == JsonValueKind.Object && content.TryGetProperty(key, out source) && HasText(source);
    }

    private static bool HasText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!.Trim().Length > 0,
        JsonValueKind.Array => value.GetArrayLength() > 0 && value.EnumerateArray().All(i => i.ValueKind == JsonValueKind.String),
        _ => false
    };

    private static bool SameShape(JsonElement source, JsonElement value) =>
        source.ValueKind == value.ValueKind
        && (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().All(i => i.ValueKind == JsonValueKind.String));

    [GeneratedRegex("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageTag();
}
