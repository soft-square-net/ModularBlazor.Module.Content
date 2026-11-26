using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

/// <summary>
/// Follows the feed rows (<see cref="FeedEntry"/>) of one <see cref="ContentDbContext"/> and hands them to the scope's
/// <see cref="IFeedCommitObserver"/> once they are committed: right after the save, or, inside an explicit transaction,
/// when it commits. A failed save keeps its rows pending (still Added, the next save writes them); a rollback drops them.
/// The observer runs after the commit, so nothing it does can fail the save.
/// </summary>
internal sealed class FeedCommits : DbTransactionInterceptor
{
    private readonly ContentDbContext _db;
    private readonly List<FeedEntry> _tracked = [];   // added to the context, not written yet
    private readonly List<FeedEntry> _written = [];   // written in the open transaction

    public FeedCommits(ContentDbContext db) => _db = db;

    /// <summary>Starts following the context's tracker (from its constructor, once the interceptor exists for OnConfiguring).</summary>
    public void Follow() => _db.ChangeTracker.Tracked += OnTracked;

    private void OnTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (!e.FromQuery && e.Entry.Entity is FeedEntry entry && e.Entry.State == EntityState.Added) _tracked.Add(entry);
    }

    /// <summary>After a successful save: the rows it wrote are committed now, or with the open transaction.</summary>
    public Task SavedAsync(bool acceptedChanges, CancellationToken ct)
    {
        var written = _tracked.Where(f => _db.Entry(f).State is EntityState.Unchanged || (!acceptedChanges && _db.Entry(f).State is EntityState.Added)).ToList();
        _tracked.RemoveAll(f => written.Contains(f) || _db.Entry(f).State is EntityState.Detached);
        _written.AddRange(written);
        return _db.Database.CurrentTransaction is null ? FlushAsync(ct) : Task.CompletedTask;
    }

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default) =>
        await FlushAsync(ct);

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
    {
        Drop();
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Drop();

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken ct = default)
    {
        Drop();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Drop();

    private void Drop() => _written.Clear();

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_written.Count == 0) return;
        var rows = _written.ToList();
        _written.Clear();
        try
        {
            var scope = _db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
            if (scope?.GetService(typeof(IFeedCommitObserver)) is IFeedCommitObserver observer)
                await observer.CommittedAsync(rows, ct);
        }
        catch
        {
            // Committed already: a live nudge that fails must not fail the save (clients catch up from the feed).
        }
    }
}
