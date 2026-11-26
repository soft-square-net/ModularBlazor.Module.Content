using System.Globalization;
using Microsoft.Extensions.Logging;
using soft.square.net.Organization.Contracts;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Units;

/// <summary>
/// <c>shifts.section-moved.v1</c>, as Shifts publishes it (S1, <c>ShiftsEvents</c>): its section <see cref="SectionId"/> is
/// now the Organization unit <see cref="UnitId"/>. Copied here, never referenced: no type is shared between modules. The
/// envelope's subject is <c>section:{sectionId}</c> and its unit path is the unit's.
/// </summary>
public sealed record SectionMoved(int SectionId, Guid UnitId);

/// <summary>
/// Remaps the contents of a Shifts section that moved to Organization: their <c>SectionId</c> becomes the unit (with its
/// path, from the envelope). Runs in the platform's inbox transaction, so it never saves: the changes commit with the inbox
/// row, and a second delivery is dropped. Without a valid unit path the event cannot be applied; it is logged and skipped.
/// </summary>
public sealed class SectionMovedHandler(IContentStore store, TimeProvider clock, ILogger<SectionMovedHandler> log) : IIntegrationEventHandler
{
    public const string Type = "shifts.section-moved.v1";

    public async Task HandleAsync(IntegrationEnvelope envelope, CancellationToken ct)
    {
        var moved = envelope.DataAs<SectionMoved>();
        var path = envelope.UnitPath;
        if (moved.UnitId == Guid.Empty || string.IsNullOrWhiteSpace(path)
            || !path.EndsWith(UnitPaths.Separator + UnitPaths.Id(moved.UnitId), StringComparison.Ordinal))
        {
            log.LogWarning("Event {Id} ({Type}) has no unit path for unit {Unit}; section {Section} was not remapped",
                envelope.Id, envelope.Type, moved.UnitId, moved.SectionId);
            return;
        }
        var section = moved.SectionId.ToString(CultureInfo.InvariantCulture);
        var now = clock.GetUtcNow();
        var remapped = 0;
        foreach (var content in await store.ContentsInSectionAsync(section, ct))
            if (content.RemapSection(section, moved.UnitId, path, now)) remapped++;
        log.LogInformation("Section {Section} moved to unit {Unit}: {Count} contents remapped", moved.SectionId, moved.UnitId, remapped);
    }
}

/// <summary>
/// Follows Organization's units (<c>org.unit-*.v1</c>): a move rebases the stored unit paths of the posts and contents at or
/// below the unit (their scopes follow the tree), and every unit event drops what <see cref="IUnitLookup"/> cached about the
/// unit (its categories may have changed). Never saves (the platform's inbox transaction does).
/// </summary>
public sealed class OrgUnitEventsHandler(IContentStore store, IUnitLookup units, ILogger<OrgUnitEventsHandler> log) : IIntegrationEventHandler
{
    public const string Pattern = OrgEvents.AnyUnitEvent;

    public async Task HandleAsync(IntegrationEnvelope envelope, CancellationToken ct)
    {
        switch (envelope.Type)
        {
            case OrgEvents.UnitMoved:
                var moved = envelope.DataAs<UnitMoved>();
                units.Forget(moved.UnitId);
                await RebaseAsync(moved, ct);
                break;
            case OrgEvents.UnitRemoved:
                units.Forget(envelope.DataAs<UnitRemoved>().UnitId);
                break;
            case OrgEvents.UnitCreated:
                units.Forget(envelope.DataAs<UnitCreated>().Unit.UnitId);
                break;
            case OrgEvents.UnitUpdated:
                units.Forget(envelope.DataAs<UnitUpdated>().Unit.UnitId);
                break;
        }
    }

    private async Task RebaseAsync(UnitMoved moved, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(moved.OldPath) || string.IsNullOrEmpty(moved.NewPath) || moved.OldPath == moved.NewPath) return;
        var count = 0;
        foreach (var post in await store.PostsInUnitTreeAsync(moved.OldPath, ct))
            if (post.RebaseUnit(moved.OldPath, moved.NewPath)) count++;
        foreach (var content in await store.ContentsInUnitTreeAsync(moved.OldPath, ct))
            if (content.RebaseUnit(moved.OldPath, moved.NewPath)) count++;
        if (count > 0) log.LogInformation("Unit {Unit} moved: {Count} posts and contents rebased", moved.UnitId, count);
    }
}
