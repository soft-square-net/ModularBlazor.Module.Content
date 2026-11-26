using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// A Post of one press and category (aggregate root): a document with a URL. The aggregate enforces the state rules; who
/// may call each operation (grants, scopes, shifts) is decided in the application layer. Every change bumps
/// <see cref="Version"/> (optimistic concurrency: the second of two racing assignments gets a conflict) and is recorded in
/// <see cref="History"/>.
/// <para>Its kind is <see cref="SchemaId"/>/<see cref="SchemaVersion"/> (a DynamicFields root field). It holds its root values
/// and the ordered references to shared contents (<see cref="Contents"/>, <c>ContentItem</c> aggregates), each pinned to the
/// Desk-accepted revision it went live with. <see cref="DocumentJson"/> is the working document assembled from the root
/// values and the contents' current revisions (kept up to date by the application layer, <see cref="Recompose"/>);
/// <see cref="PublishedDocumentJson"/> is assembled from the pinned revisions at publication.</para>
/// </summary>
public sealed class Post : AggregateRoot<int>, ITenantOwned
{
    private readonly List<PostEvent> _history = [];
    private readonly List<PostContent> _contents = [];
    private readonly List<PostSlug> _slugs = [];
    private readonly List<(string Type, Guid? ContentId, string? Detail)> _feed = [];

    private Post() { } // EF Core

    public string TenantId { get; set; } = "";
    /// <summary>Stable identifier used by the public permalink; never changes when the title or slug changes.</summary>
    public string PublicId { get; private set; } = "";
    public string StablePublicId => string.IsNullOrWhiteSpace(PublicId) ? $"legacy-{Id}" : PublicId;
    public string PressId { get; private set; } = "";
    public string CategoryId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    /// <summary>Authoring document; schema ID/version are retained so edits can use the original field contract.</summary>
    public string SchemaId { get; private set; } = "legacy.basic";
    public int SchemaVersion { get; private set; } = 1;
    public string DocumentJson { get; private set; } = "{}";
    /// <summary>The post's kind: a DynamicFields root post field key (<see cref="PostKinds"/>), such as <c>post.news</c>.</summary>
    public string PostKind { get; private set; } = PostKinds.Article;
    public string OwnerId { get; private set; } = "";
    public PostState State { get; private set; }
    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? AcceptedBy { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public string? AcceptedShiftId { get; private set; }
    public string? AssigneeId { get; private set; }
    /// <summary>The shift the assignment was made in (informational; access itself comes from the assignee's grants).</summary>
    public string? AssignedShiftId { get; private set; }
    public string? ReviewerId { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public DateTimeOffset? ScheduledFor { get; private set; }
    public string? ScheduledBy { get; private set; }
    public int? ScheduledVersion { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }
    /// <summary>Snapshot of the live version, retained when a later edit waits for Desk approval.</summary>
    public string? PublishedTitle { get; private set; }
    public string? PublishedBody { get; private set; }
    /// <summary>Immutable document snapshot for the currently published version.</summary>
    public string? PublishedDocumentJson { get; private set; }
    /// <summary>
    /// The URL prefix of the kind when the live version was published (its DynamicFields root field's), so the public read
    /// needs no call to DynamicFields: the canonical path is <c>/{prefix}/{slug}</c>. Null for posts published before kinds
    /// had prefixes, until they are published again.
    /// </summary>
    public string? PublishedUrlPrefix { get; private set; }
    /// <summary>The document's translations (<see cref="PostTranslation"/> list, JSON): overlays of its text, one per language.</summary>
    public string TranslationsJson { get; private set; } = "[]";
    /// <summary>The translations of the published document, taken with it when it is published.</summary>
    public string? PublishedTranslationsJson { get; private set; }
    /// <summary>Set when unfinished work moved to the next shift's queue at check-out.</summary>
    public string? TransferredFromShift { get; private set; }

    /// <summary>False until the migration has made shared contents of the stored document's children.</summary>
    public bool ContentsSplit { get; private set; } = true;

    public IReadOnlyList<PostEvent> History => _history;

    /// <summary>The most contents one post uses.</summary>
    public const int MaxContents = 500;
    /// <summary>The contents it uses, in reading order.</summary>
    public IReadOnlyList<PostContent> Contents => _contents.OrderBy(c => c.Order).ToList();
    /// <summary>The same references as stored (the mapped navigation; unordered).</summary>
    public IReadOnlyList<PostContent> ContentLinks => _contents;
    public IReadOnlyList<PostSlug> Slugs => _slugs;

    /// <summary>The grant scopes of this Post: its press and its category.</summary>
    public IReadOnlyCollection<string> Scopes => [$"press:{PressId}", $"category:{CategoryId}"];

    /// <summary>A new public id (a new post's contents are made before it, and name it as their origin).</summary>
    public static string NewPublicId() => Guid.NewGuid().ToString("N");

    public static Post Create(string ownerId, Category category, string title, string body, DateTimeOffset now, string postKind = PostKinds.Article,
        PostDocument? document = null, IReadOnlyList<ContentRef>? contents = null, string? publicId = null)
    {
        var kind = PostKinds.Require(postKind);
        var post = new Post
        {
            OwnerId = Require(ownerId, "an owner"),
            PublicId = publicId ?? NewPublicId(),
            PressId = category.PressId,
            CategoryId = category.Id,
            PostKind = kind,
            State = PostState.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        post.SetText(title, body);
        if (contents is not null) post.SetContents(contents);
        post.SetDocument(document ?? PostDocument.FromLegacy(title, body));
        post.Record(ownerId, "created", now, $"{category.PressId}/{category.Id}");
        // A new post has no id until it is saved, so it raises no feed events: its contents' "created" events carry it.
        post._feed.Clear();
        return post;
    }

    /// <summary>The owner may edit a draft or a returned Post, and a submitted one within the edit window until it is accepted or assigned.</summary>
    public bool OwnerMayEdit(DateTimeOffset now, TimeSpan editWindow) =>
        State is PostState.Draft or PostState.Returned
        || (State == PostState.Submitted && SubmittedAt is { } submitted && now < submitted + editWindow);

    public void EditByOwner(string actor, string title, string body, DateTimeOffset now, TimeSpan editWindow, PostDocument? document = null) =>
        Edit(PostEditor.Owner, actor, now, editWindow, new PostChange(title, body, document));

    /// <summary>The assigned reviewer edits during review (the caller has checked the reviewer's shift grant).</summary>
    public void EditInReview(string actor, string title, string body, DateTimeOffset now, PostDocument? document = null) =>
        Edit(PostEditor.Reviewer, actor, now, TimeSpan.Zero, new PostChange(title, body, document));

    /// <summary>Desk edits an approved or scheduled Post: any schedule is cancelled and Desk must re-approve.</summary>
    public void EditByDesk(string actor, string title, string body, DateTimeOffset now, PostDocument? document = null) =>
        Edit(PostEditor.Desk, actor, now, TimeSpan.Zero, new PostChange(title, body, document));

    /// <summary>The editor role the post's state gives <paramref name="actor"/> now, if any (the caller checks the grants).</summary>
    public PostEditor? EditorRole(string actor, DateTimeOffset now, TimeSpan editWindow) =>
        actor == OwnerId && OwnerMayEdit(now, editWindow) ? PostEditor.Owner
        : State == PostState.InReview && AssigneeId == actor ? PostEditor.Reviewer
        : State is PostState.Approved or PostState.Scheduled or PostState.AwaitingDeskReapproval or PostState.Published ? PostEditor.Desk
        : null;

    /// <summary>
    /// Edits the post as its owner (draft, returned, or submitted within the edit window), its assigned reviewer (in review)
    /// or Desk (approved, scheduled, awaiting re-approval or published: any schedule is cancelled and Desk must re-approve;
    /// the live snapshot stays). The edit may change the text, the document (root values and assembled contents), the
    /// contents and their order, and the slugs.
    /// </summary>
    public void Edit(PostEditor editor, string actor, DateTimeOffset now, TimeSpan editWindow, PostChange edit)
    {
        string action;
        switch (editor)
        {
            case PostEditor.Owner:
                if (actor != OwnerId) throw new DomainException("Only the owner edits through this path.");
                if (!OwnerMayEdit(now, editWindow))
                    throw new DomainException(State == PostState.Submitted
                        ? "The edit window after submission has closed; the Post is locked until review sends it back."
                        : $"The owner cannot edit a Post that is {State}.");
                action = "edited";
                break;
            case PostEditor.Reviewer:
                if (State != PostState.InReview || AssigneeId != actor) throw new DomainException("Only the assigned reviewer edits a Post in review.");
                action = "edited in review";
                break;
            case PostEditor.Desk:
                if (State is not (PostState.Approved or PostState.Scheduled or PostState.AwaitingDeskReapproval or PostState.Published))
                    throw new DomainException($"Desk edits approved or scheduled Posts, not {State} ones.");
                action = State == PostState.Scheduled ? "edited; schedule cancelled" : State == PostState.Published
                    ? "edited after publication; live version retained" : "edited by desk";
                break;
            default:
                throw new DomainException($"Unknown editor '{editor}'.");
        }
        Change(actor, action, now, () =>
        {
            if (edit.Title is not null) SetText(edit.Title, edit.Body ?? Body);
            else if (edit.Body is not null) Body = edit.Body;
            if (edit.Contents is not null) SetContents(edit.Contents);
            if (edit.Slugs is not null) SetSlugs(edit.Slugs);
            if (edit.Document is not null) SetDocument(edit.Document);
            if (editor == PostEditor.Desk)
            {
                ClearSchedule();
                State = PostState.AwaitingDeskReapproval;
            }
            _feed.Add((FeedEventTypes.PostChanged, null, action));
        });
    }

    /// <summary>
    /// Refreshes the working document after one of its contents changed (no new version: the post itself did not change).
    /// The document's children must be the post's contents, in order.
    /// </summary>
    public void Recompose(PostDocument working) => SetDocument(working);

    /// <summary>The root of the working document: kind and root values, without the contents.</summary>
    public PostDocument Root => ReadDocument() with { Children = [] };

    /// <summary>
    /// The migration's split of a stored document: the post now uses these contents (made from its children, in order,
    /// pinned to their first revision when <paramref name="pinned"/>). No new version and no feed events.
    /// </summary>
    public void CompleteSplit(IReadOnlyList<Guid> contents, bool pinned)
    {
        if (ContentsSplit) return;
        _contents.Clear();
        for (var i = 0; i < contents.Count; i++)
            _contents.Add(new PostContent(contents[i], i, required: true) { PinnedRevision = pinned ? 1 : null });
        ContentsSplit = true;
    }

    /// <summary>For the migration's tests and fixtures: marks a post as stored before the split.</summary>
    public void MarkUnsplit() => ContentsSplit = false;

    public IReadOnlyList<PostTranslation> Translations => ReadTranslations(TranslationsJson);
    public IReadOnlyList<PostTranslation> PublishedTranslations => ReadTranslations(PublishedTranslationsJson);

    /// <summary>
    /// Adds or replaces the document's translation into one language (who may is decided by the caller). The workflow
    /// state does not change: a translation is not an edit of the content. A published Post whose live version is the
    /// current one gets it live at once; otherwise it goes live with the next publication.
    /// </summary>
    public void Translate(string actor, string language,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedText>> nodes, DateTimeOffset now)
    {
        var document = ReadDocument();
        PostTranslationRules.Validate(document, language, nodes);
        var tag = language.Trim();
        var others = Translations.Where(t => !string.Equals(t.Language, tag, StringComparison.OrdinalIgnoreCase)).ToList();
        if (others.Count >= PostTranslationRules.MaxLanguages)
            throw new DomainException($"A Post has at most {PostTranslationRules.MaxLanguages} translations.");
        var translation = new PostTranslation(tag, nodes, now, actor);
        Change(actor, "translated", now, () => SetTranslations([.. others, translation]), tag);
    }

    /// <summary>Removes the translation into one language (live at once when the published version is the current one).</summary>
    public void RemoveTranslation(string actor, string language, DateTimeOffset now)
    {
        var translations = Translations;
        var kept = translations.Where(t => !string.Equals(t.Language, language?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (kept.Count == translations.Count) throw new DomainException($"The Post has no '{language}' translation.");
        Change(actor, "translation removed", now, () => SetTranslations(kept), language.Trim());
    }

    private void SetTranslations(IReadOnlyList<PostTranslation> translations)
    {
        TranslationsJson = JsonSerializer.Serialize(translations, PostJson.Options);
        if (State == PostState.Published && PublishedDocumentJson == DocumentJson) PublishedTranslationsJson = TranslationsJson;
    }

    private PostDocument ReadDocument() =>
        DocumentJson is { Length: > 2 } json
            ? JsonSerializer.Deserialize<PostDocument>(json, PostJson.Options) ?? PostDocument.FromLegacy(Title, Body)
            : PostDocument.FromLegacy(Title, Body);

    private static IReadOnlyList<PostTranslation> ReadTranslations(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<PostTranslation>>(json, PostJson.Options) ?? [];

    public void Submit(string actor, DateTimeOffset now)
    {
        if (actor != OwnerId) throw new DomainException("Only the owner submits a Post.");
        Expect("submit", PostState.Draft, PostState.Returned);
        Change(actor, "submitted", now, () =>
        {
            State = PostState.Submitted;
            SubmittedAt = now;
        });
    }

    /// <summary>The shift manager accepts the submission: the owner is locked out from now on.</summary>
    public void Accept(string actor, string shiftId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(shiftId)) throw new DomainException("Accepting a submission requires its active shift id.");
        Expect("accept", PostState.Submitted);
        Change(actor, "accepted", now, () =>
        {
            State = PostState.Accepted;
            AcceptedBy = actor;
            AcceptedAt = now;
            AcceptedShiftId = shiftId;
        });
    }

    /// <summary>Assigns a reviewer (the manager, or the workflow after the manager's delay). Locks the owner out.</summary>
    public void Assign(string actor, string assigneeId, string? shiftId, DateTimeOffset now)
    {
        Expect("assign", PostState.Submitted, PostState.Accepted);
        Change(actor, "assigned", now, () =>
        {
            State = PostState.InReview;
            AssigneeId = Require(assigneeId, "an assignee");
            AssignedShiftId = shiftId;
            ReviewerId = null;
        }, $"to {assigneeId}" + (shiftId is null ? "" : $" in shift {shiftId}"));
    }

    public void Approve(string actor, DateTimeOffset now)
    {
        if (State != PostState.InReview || AssigneeId != actor) throw new DomainException("Only the assigned reviewer approves a Post in review.");
        Change(actor, "approved", now, () =>
        {
            State = PostState.Approved;
            ReviewerId = actor;
            ApprovedVersion = Version + 1; // the version this change produces
        });
    }

    /// <summary>Back to the author (Manager or Desk). The owner can edit and submit again.</summary>
    public void Return(string actor, string? reason, DateTimeOffset now)
    {
        Expect("return", PostState.Submitted, PostState.Accepted, PostState.InReview, PostState.Approved,
            PostState.Scheduled, PostState.AwaitingDeskReapproval);
        Change(actor, "returned", now, () =>
        {
            State = PostState.Returned;
            AssigneeId = null;
            AssignedShiftId = null;
            ClearSchedule();
        }, reason);
    }

    /// <summary>Handover: unfinished review work goes back to the queue for the next shift's manager.</summary>
    public void Transfer(string actor, string? fromShift, DateTimeOffset now)
    {
        Expect("transfer", PostState.InReview);
        Change(actor, "transferred", now, () =>
        {
            TransferredFromShift = fromShift ?? AssignedShiftId;
            State = PostState.Accepted;
            AssigneeId = null;
            AssignedShiftId = null;
        }, fromShift);
    }

    /// <summary>
    /// Desk schedules the current version; any later edit cancels the schedule. A post with contents pins each required
    /// one to its accepted revision (<paramref name="pins"/>); the due publication assembles the document from them.
    /// </summary>
    public void Schedule(string actor, DateTimeOffset publishAt, DateTimeOffset now, IReadOnlyDictionary<Guid, int>? pins = null)
    {
        Expect("schedule", PostState.Approved, PostState.AwaitingDeskReapproval, PostState.Scheduled);
        if (publishAt <= now) throw new DomainException("A scheduled publish time must be in the future.");
        var pinned = RequirePins(pins);
        Change(actor, "scheduled", now, () =>
        {
            Pin(pinned);
            State = PostState.Scheduled;
            ScheduledFor = publishAt;
            ScheduledBy = actor;
            ScheduledVersion = Version + 1;
        }, publishAt.ToString("O"));
    }

    /// <summary>
    /// Desk publishes now. A post with contents needs the <paramref name="plan"/>: every required content pinned to an accepted
    /// revision, and the document assembled from the pinned revisions.
    /// </summary>
    /// <param name="urlPrefix">The kind's URL prefix now (the application layer refuses to publish a kind without one).</param>
    public void Publish(string actor, DateTimeOffset now, PublishPlan? plan = null, string? urlPrefix = null)
    {
        Expect("publish", PostState.Approved, PostState.AwaitingDeskReapproval, PostState.Scheduled);
        var pinned = RequirePins(plan?.Pins);
        var document = PublishedDocument(plan?.Document);
        var prefix = UrlPrefixOrNull(urlPrefix);
        Change(actor, "published", now, () => { Pin(pinned); MarkPublished(actor, now, document, prefix)(); });
    }

    /// <summary>
    /// Publishes a scheduled Post whose time has come, if it is still the version Desk scheduled (its right was
    /// checked when it was scheduled). False when nothing is due.
    /// </summary>
    public bool PublishIfDue(string actor, DateTimeOffset now, PostDocument? pinnedDocument = null, string? urlPrefix = null)
    {
        if (!IsDue(now)) return false;
        var document = PublishedDocument(pinnedDocument);
        var prefix = UrlPrefixOrNull(urlPrefix);
        Change(actor, "published (scheduled)", now, MarkPublished(ScheduledBy ?? actor, now, document, prefix), $"scheduled by {ScheduledBy}");
        return true;
    }

    /// <summary>Scheduled, due, and unchanged since Desk scheduled it.</summary>
    public bool IsDue(DateTimeOffset now) =>
        State == PostState.Scheduled && ScheduledFor is { } due && due <= now && ScheduledVersion == Version;

    /// <summary>
    /// The canonical path of the live version, <c>/{prefix}/{slug}</c> (the prefix kept when it was published); null until it
    /// is published under a prefix. The slug is the one of the first of <paramref name="cultures"/> the post has a slug in,
    /// else of its first culture (ordinal order), else its public id.
    /// </summary>
    public string? CanonicalPath(IEnumerable<string>? cultures = null) =>
        PublishedAt is null || string.IsNullOrWhiteSpace(PublishedUrlPrefix) ? null : $"/{PublishedUrlPrefix}/{PathSlug(cultures)}";

    /// <summary>The last segment of the post's URL: its slug in the first matching culture, else its first slug, else its public id.</summary>
    public string PathSlug(IEnumerable<string>? cultures = null)
    {
        foreach (var culture in cultures ?? [])
            if (_slugs.FirstOrDefault(s => string.Equals(s.Culture, culture, StringComparison.OrdinalIgnoreCase)) is { } match) return match.Slug;
        return _slugs.OrderBy(s => s.Culture, StringComparer.Ordinal).FirstOrDefault()?.Slug ?? StablePublicId;
    }

    private static string? UrlPrefixOrNull(string? urlPrefix)
    {
        if (string.IsNullOrWhiteSpace(urlPrefix)) return null;
        var prefix = urlPrefix.Trim();
        return UrlPrefixes.IsValid(prefix) ? prefix : throw new DomainException($"'{urlPrefix}' is not a URL prefix such as news or ar/news.");
    }

    private Action MarkPublished(string publisher, DateTimeOffset now, string document, string? urlPrefix) => () =>
    {
        PublishedUrlPrefix = urlPrefix;
        State = PostState.Published;
        PublishedAt = now;
        PublishedBy = publisher;
        PublishedTitle = Title;
        PublishedBody = Body;
        PublishedDocumentJson = document;
        PublishedTranslationsJson = TranslationsJson;
    };

    /// <summary>The document a publication goes live with: assembled from the pinned revisions, or the working one without contents.</summary>
    private string PublishedDocument(PostDocument? assembled)
    {
        if (assembled is null)
            return _contents.Count == 0 ? DocumentJson : throw new DomainException("A Post with contents is published from its accepted contents.");
        PostDocumentRules.Validate(assembled);
        return JsonSerializer.Serialize(assembled, PostJson.Options);
    }

    /// <summary>Checks that every required content has an accepted revision to pin; optional ones without one are left out.</summary>
    private IReadOnlyDictionary<Guid, int> RequirePins(IReadOnlyDictionary<Guid, int>? pins)
    {
        pins ??= new Dictionary<Guid, int>();
        var missing = _contents.Count(c => c.Required && !pins.ContainsKey(c.ContentId));
        if (missing > 0)
            throw new DomainException(missing == 1
                ? "A required content of this Post is not accepted yet."
                : $"{missing} required contents of this Post are not accepted yet.");
        return pins;
    }

    private void Pin(IReadOnlyDictionary<Guid, int> pins)
    {
        foreach (var content in _contents) content.PinnedRevision = pins.TryGetValue(content.ContentId, out var revision) ? revision : null;
    }

    private void ClearSchedule()
    {
        ScheduledFor = null;
        ScheduledBy = null;
        ScheduledVersion = null;
    }

    private void SetContents(IReadOnlyList<ContentRef> contents)
    {
        if (contents.Select(c => c.ContentId).Distinct().Count() != contents.Count) throw new DomainException("A Post uses a content once.");
        if (contents.Count > MaxContents) throw new DomainException($"A Post uses at most {MaxContents} contents.");
        foreach (var gone in _contents.Where(c => contents.All(n => n.ContentId != c.ContentId)).ToList())
        {
            _contents.Remove(gone);
            _feed.Add((FeedEventTypes.ContentDetached, gone.ContentId, null));
        }
        for (var i = 0; i < contents.Count; i++)
        {
            var wanted = contents[i];
            var existing = _contents.FirstOrDefault(c => c.ContentId == wanted.ContentId);
            if (existing is null)
            {
                _contents.Add(new PostContent(wanted.ContentId, i, wanted.Required));
                _feed.Add((FeedEventTypes.ContentAttached, wanted.ContentId, null));
            }
            else
            {
                existing.Order = i;
                existing.Required = wanted.Required;
            }
        }
    }

    private void SetSlugs(IReadOnlyDictionary<string, string> slugs)
    {
        if (slugs.Count > PostSlug.MaxCultures) throw new DomainException($"A Post has slugs in at most {PostSlug.MaxCultures} cultures.");
        foreach (var (culture, slug) in slugs)
        {
            if (!PostTranslationRules.IsLanguage(culture)) throw new DomainException($"'{culture}' is not a culture such as ar, en or en-GB.");
            if (!PostSlug.IsSlug(slug))
                throw new DomainException($"'{slug}' is not a slug: lowercase letters, digits and single '-' (at most {PostSlug.MaxLength}).");
        }
        _slugs.RemoveAll(s => !slugs.Keys.Any(k => string.Equals(k.Trim(), s.Culture, StringComparison.OrdinalIgnoreCase)));
        foreach (var (culture, slug) in slugs)
        {
            if (_slugs.FirstOrDefault(s => string.Equals(s.Culture, culture.Trim(), StringComparison.OrdinalIgnoreCase)) is { } existing) existing.Slug = slug;
            else _slugs.Add(new PostSlug(culture.Trim(), slug));
        }
    }

    private void SetText(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300) throw new DomainException("A Post needs a title (at most 300 characters).");
        Title = title.Trim();
        Body = body ?? "";
    }

    private void SetDocument(PostDocument? document)
    {
        if (document is null) return;
        PostDocumentRules.Validate(document);
        SchemaId = document.SchemaId.Trim();
        SchemaVersion = document.SchemaVersion;
        DocumentJson = JsonSerializer.Serialize(document, PostJson.Options);
        // Translations follow the document: values of removed nodes or keys go (stale ones stay, flagged by their hash).
        var translations = Translations;
        if (translations.Count > 0)
            TranslationsJson = JsonSerializer.Serialize(
                translations.Select(t => PostTranslationRules.Prune(document, t)).OfType<PostTranslation>().ToList(), PostJson.Options);
    }

    private void Expect(string action, params PostState[] states)
    {
        if (!states.Contains(State)) throw new DomainException($"Cannot {action} a Post that is {State}.");
    }

    private void Change(string actor, string action, DateTimeOffset now, Action apply, string? detail = null)
    {
        var before = State;
        _feed.Clear();
        apply();
        Version++;
        UpdatedAt = now;
        Record(actor, action, now, detail);
        if (State != before) _feed.Add((FeedEventTypes.PostStateChanged, null, action));
        foreach (var (type, contentId, text) in Id == 0 ? [] : _feed)
            Raise(new FeedEvent(type, Id, contentId, Version, null, null, State.ToString(), actor, now, text));
        _feed.Clear();
    }

    private void Record(string actor, string action, DateTimeOffset now, string? detail) =>
        _history.Add(new PostEvent(now, actor, action, State, detail));

    private static string Require(string value, string what) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new DomainException($"A Post needs {what}.");
}
