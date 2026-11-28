using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Clock;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Clock;

/// <summary>
/// One clock tick of a tenant at a time, across every replica of every pool that runs the module: a try-lock, so a
/// replica that does not get it skips the tenant for this tick (another one is running it).
/// </summary>
public interface IContentClockLock
{
    /// <summary>The lock, held until disposed; null when another replica holds it.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(TenantInfo tenant, CancellationToken ct);
}

/// <summary>The lock in the tenant's own database: the platform's advisory lock (as for migrations), without waiting.</summary>
public sealed class TenantDatabaseClockLock(TenantDatabases databases) : IContentClockLock
{
    public const string LockName = "content:clock";

    public Task<IAsyncDisposable?> TryAcquireAsync(TenantInfo tenant, CancellationToken ct)
    {
        var server = databases.ServerOf(tenant);
        var connection = server.Adapter.LockConnectionString(server.ConnectionString, databases.ConnectionString(tenant));
        return server.Adapter.TryLockAsync(connection, $"{tenant.DatabaseName}:{LockName}", ct);
    }
}

/// <summary>
/// The tenants the module serves on this replica: added when the module's tenant initializers have run for one
/// (registered after the migrations and the backfill), so the clock never queries a database that is not migrated yet.
/// </summary>
public sealed class ContentClockTenants : ITenantInitializer
{
    private readonly ConcurrentDictionary<string, bool> _ready = new(StringComparer.OrdinalIgnoreCase);

    public Task InitializeAsync(TenantInfo tenant, CancellationToken ct)
    {
        _ready[tenant.Id] = true;
        return Task.CompletedTask;
    }

    public bool IsReady(string tenantId) => _ready.ContainsKey(tenantId);
}

/// <summary>
/// The module's clock (a hosted service of the module's container, stopped on unload). Every
/// <see cref="ContentOptions.ClockInterval"/> it does the module's timed work for every active tenant it serves, under
/// <see cref="IContentClockLock"/>: it hands back reviews whose reviewer lost the review right
/// (<see cref="ReviewHandover"/>). Each post is handled in a tenant scope of its own, so one that fails (a racing change,
/// say) is logged and tried again at the next tick without holding up the others; a tenant that fails likewise.
/// </summary>
public sealed class ContentClock(
    IServiceProvider services, ITenantDirectory tenants, ContentClockTenants ready, IContentClockLock locks,
    IOptions<ContentOptions> options, ILogger<ContentClock> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.ClockInterval > TimeSpan.Zero ? options.Value.ClockInterval : TimeSpan.FromSeconds(15);
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    log.LogWarning(e, "The content clock's tick failed; retrying in {Interval}.", interval);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The module is unloading.
        }
    }

    /// <summary>One tick: every active tenant the module serves.</summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (var tenant in tenants.All.Where(t => t.IsActive && ready.IsReady(t.Id)))
            await TickAsync(tenant, ct);
    }

    /// <summary>One tenant's tick, when this replica gets the tenant's lock. Never throws but on cancellation.</summary>
    public async Task TickAsync(TenantInfo tenant, CancellationToken ct)
    {
        try
        {
            await using var held = await locks.TryAcquireAsync(tenant, ct);
            if (held is null) return; // another replica runs this tenant's tick
            await HandOverReviewsAsync(tenant, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(e, "The content clock could not run for tenant {Tenant}; it tries again at the next tick.", tenant.Id);
        }
    }

    private async Task HandOverReviewsAsync(TenantInfo tenant, CancellationToken ct)
    {
        var after = 0;
        while (true)
        {
            IReadOnlyList<int> ids;
            await using (var scope = services.CreateTenantScope(tenant))
                ids = await scope.ServiceProvider.GetRequiredService<ReviewHandover>().InReviewAsync(after, ct);

            foreach (var id in ids)
                await ForPostAsync(tenant, id, "check the reviewer of", async provider =>
                {
                    if (await provider.GetRequiredService<ReviewHandover>().CheckAsync(id, ct) == ReviewerCheck.HandedOver)
                        log.LogInformation("Content clock, tenant {Tenant}: post {Post} went back to the review queue.", tenant.Id, id);
                }, ct);

            if (ids.Count < ReviewHandover.BatchSize) return;
            after = ids[^1];
        }
    }

    private async Task ForPostAsync(TenantInfo tenant, int postId, string what, Func<IServiceProvider, Task> work, CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateTenantScope(tenant);
            await work(scope.ServiceProvider);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(e, "The content clock could not {What} post {Post} of tenant {Tenant}; it tries again at the next tick.",
                what, postId, tenant.Id);
        }
    }
}
