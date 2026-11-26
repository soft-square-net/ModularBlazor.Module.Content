using System.Text.Json;
using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;

/// <summary>What a stage means to Content: work in a section, review at the Desk, or accepted (a version to pin).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StageKind>))]
public enum StageKind { Work, Review, Accepted }

/// <summary>
/// One stage of the pipeline. <see cref="Editors"/> say who may change a content's values in it (see
/// <see cref="PipelineDefinition"/> for the tokens). <see cref="OnEdit"/>: the stage an edit of the values moves the content
/// to (an accepted content goes back to the Desk), or null to stay.
/// </summary>
public sealed record PipelineStage(string Key, StageKind Kind, IReadOnlyList<string> Editors, string? OnEdit = null);

/// <summary>
/// One user (or system) action: from any of <see cref="From"/> to <see cref="To"/>, for whoever matches one of
/// <see cref="Who"/>. <see cref="Pins"/>: the action accepts the current revision (the Desk's accept).
/// </summary>
public sealed record PipelineAction(string Key, IReadOnlyList<string> From, string To, IReadOnlyList<string> Who,
    bool RequiresNote = false, bool Pins = false);

/// <summary>
/// The content pipeline as data: the stages a content goes through and the actions that move it. Elsa designs it and
/// publishes it to Content (<c>PUT /pipeline</c>), so Content checks a user's action without calling Elsa. Until then the
/// <see cref="Default"/> applies.
/// <para>Who tokens (<see cref="PipelineStage.Editors"/>, <see cref="PipelineAction.Who"/>): <c>assignee</c> (the content's
/// assignee, or its creator while nobody is assigned), <c>creator</c>, or a permission such as <c>content.publish</c>
/// held on the content's scopes.</para>
/// </summary>
public sealed record PipelineDefinition(string Key, string StartStage, IReadOnlyList<PipelineStage> Stages, IReadOnlyList<PipelineAction> Actions)
{
    public const string Assignee = "assignee";
    public const string Creator = "creator";
    public const int MaxKeyLength = 40;
    private const int MaxStages = 30;
    private const int MaxActions = 60;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The built-in newsroom pipeline, whose stage keys are the work-item states other modules see
    /// (<see cref="PipelineStageKeys"/>): a content starts <c>assigned</c> to its section or person, is worked on
    /// (<c>in-progress</c>, by its first edit or <c>start</c>), handed in (<c>submitted</c>), taken up by the Desk
    /// (<c>in-review</c>), then accepted (pinning the revision) or returned with a note (<c>returned</c>, back to the section).
    /// The Desk may edit in its stages; an edit of an accepted content sends it back to <c>in-review</c>.
    /// </summary>
    public static PipelineDefinition Default { get; } = new("newsroom", PipelineStageKeys.Assigned,
    [
        new(PipelineStageKeys.Assigned, StageKind.Work, [Assignee], OnEdit: PipelineStageKeys.InProgress),
        new(PipelineStageKeys.InProgress, StageKind.Work, [Assignee]),
        new(PipelineStageKeys.Submitted, StageKind.Review, ["content.publish"]),
        new(PipelineStageKeys.InReview, StageKind.Review, ["content.publish"]),
        new(PipelineStageKeys.Accepted, StageKind.Accepted, ["content.publish"], OnEdit: PipelineStageKeys.InReview),
        new(PipelineStageKeys.Returned, StageKind.Work, [Assignee], OnEdit: PipelineStageKeys.InProgress)
    ],
    [
        new("start", [PipelineStageKeys.Assigned, PipelineStageKeys.Returned], PipelineStageKeys.InProgress, [Assignee]),
        new("submit", [PipelineStageKeys.Assigned, PipelineStageKeys.InProgress, PipelineStageKeys.Returned], PipelineStageKeys.Submitted, [Assignee]),
        new("review", [PipelineStageKeys.Submitted], PipelineStageKeys.InReview, ["content.publish"]),
        new("accept", [PipelineStageKeys.Submitted, PipelineStageKeys.InReview], PipelineStageKeys.Accepted, ["content.publish"], Pins: true),
        new("return", [PipelineStageKeys.Submitted, PipelineStageKeys.InReview, PipelineStageKeys.Accepted], PipelineStageKeys.Returned,
            ["content.publish", "content.return"], RequiresNote: true),
        new("reopen", [PipelineStageKeys.Accepted], PipelineStageKeys.InReview, ["content.publish"])
    ]);

    /// <summary>
    /// The definition with the stage keys of the first built-in pipeline renamed to today's (<see cref="PipelineStageKeys.Legacy"/>:
    /// draft → assigned, desk → submitted, rejected → returned; accepted stays). Elsa may still publish the old keys, so
    /// <c>PUT /pipeline</c> renames them; a legacy key is kept when the definition already has a stage with its new key.
    /// </summary>
    public PipelineDefinition WithCurrentStageKeys()
    {
        var keys = Stages?.Where(s => s is not null).Select(s => s.Key).ToHashSet(StringComparer.Ordinal) ?? [];
        var rename = PipelineStageKeys.Legacy.Where(m => keys.Contains(m.Key) && !keys.Contains(m.Value))
            .ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal);
        if (rename.Count == 0) return this;
        string Map(string? key) => key is not null && rename.TryGetValue(key, out var to) ? to : key!;
        return this with
        {
            StartStage = Map(StartStage),
            Stages = [.. Stages!.Select(s => s is null ? s! : s with { Key = Map(s.Key), OnEdit = s.OnEdit is null ? null : Map(s.OnEdit) })],
            Actions = [.. (Actions ?? []).Select(a => a is null ? a! : a with { From = [.. (a.From ?? []).Select(Map)], To = Map(a.To) })]
        };
    }

    /// <summary>
    /// The action with this key; a key the first built-in pipeline used (<c>reject</c>, now <c>return</c>) finds today's
    /// action when the definition has no action of that name.
    /// </summary>
    public PipelineAction? FindAction(string key) =>
        Actions.FirstOrDefault(a => a.Key == key)
        ?? (PipelineStageKeys.LegacyActions.TryGetValue(key, out var renamed) ? Actions.FirstOrDefault(a => a.Key == renamed) : null);

    public PipelineStage Stage(string key) =>
        Stages.FirstOrDefault(s => s.Key == key) ?? throw new DomainException($"The pipeline has no stage '{key}'.");

    public PipelineStage? FindStage(string key) => Stages.FirstOrDefault(s => s.Key == key);

    public PipelineAction Action(string key) =>
        FindAction(key) ?? throw new DomainException($"The pipeline has no action '{key}'.");

    /// <summary>The stages of a kind (the first is used where Content picks one itself: the migration, the Desk's publish).</summary>
    public IEnumerable<PipelineStage> OfKind(StageKind kind) => Stages.Where(s => s.Kind == kind);

    /// <summary>The action that hands a content in this stage to the Desk (a review stage), if any.</summary>
    public PipelineAction? SubmitFrom(string stage) =>
        Actions.FirstOrDefault(a => a.From.Contains(stage) && FindStage(a.To)?.Kind == StageKind.Review);

    /// <summary>The action that accepts a content in this stage (one that pins), if any.</summary>
    public PipelineAction? AcceptFrom(string stage) => Actions.FirstOrDefault(a => a.Pins && a.From.Contains(stage));

    /// <summary>
    /// Checks the definition: unique keys; actions between known stages; a pinning action leads to an accepted stage and
    /// every accepted stage is reached by one; the start is a work stage; every stage and action says who.
    /// </summary>
    public void Validate()
    {
        RequireKey(Key, "pipeline");
        if (Stages is not { Count: > 0 and <= MaxStages }) throw new DomainException($"A pipeline has 1 to {MaxStages} stages.");
        if (Actions is not { Count: > 0 and <= MaxActions }) throw new DomainException($"A pipeline has 1 to {MaxActions} actions.");
        var stages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in Stages)
        {
            RequireKey(stage?.Key, "stage");
            if (!stages.Add(stage!.Key)) throw new DomainException($"Stage '{stage.Key}' is defined twice.");
            if (!Enum.IsDefined(stage.Kind)) throw new DomainException($"Stage '{stage.Key}' has an unknown kind.");
            RequireWho(stage.Editors, $"stage '{stage.Key}' (editors)");
        }
        foreach (var stage in Stages.Where(s => s.OnEdit is not null))
            if (!stages.Contains(stage.OnEdit!)) throw new DomainException($"Stage '{stage.Key}' moves edits to unknown stage '{stage.OnEdit}'.");
        if (FindStage(StartStage)?.Kind != StageKind.Work) throw new DomainException("The start stage must be a work stage.");
        if (!Stages.Any(s => s.Kind == StageKind.Accepted)) throw new DomainException("A pipeline needs an accepted stage.");

        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            RequireKey(action?.Key, "action");
            if (!actions.Add(action!.Key)) throw new DomainException($"Action '{action.Key}' is defined twice.");
            if (action.From is not { Count: > 0 } || action.From.Any(f => !stages.Contains(f)))
                throw new DomainException($"Action '{action.Key}' starts from an unknown stage.");
            if (!stages.Contains(action.To)) throw new DomainException($"Action '{action.Key}' leads to unknown stage '{action.To}'.");
            if (action.Pins != (Stage(action.To).Kind == StageKind.Accepted))
                throw new DomainException($"Action '{action.Key}': exactly the actions that lead to an accepted stage pin the revision.");
            RequireWho(action.Who, $"action '{action.Key}'");
        }
    }

    private static void RequireKey(string? key, string what)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new DomainException($"A {what} key is letters, digits, '-', '_' or '.' (at most {MaxKeyLength}).");
    }

    private static void RequireWho(IReadOnlyList<string>? who, string what)
    {
        if (who is not { Count: > 0 } || who.Any(string.IsNullOrWhiteSpace))
            throw new DomainException($"The pipeline must say who may act in {what}.");
    }
}
