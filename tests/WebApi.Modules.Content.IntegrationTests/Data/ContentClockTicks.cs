using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Contracts.Security;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Clock;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Clock;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.SharedKernel;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>The module's clock: tenant scopes, the lock and a real database around <see cref="ReviewHandover"/>.</summary>
public sealed class ContentClockTicks : IDisposable
{
    private static readonly TenantInfo Acme = new("acme", "Acme", TenantStatus.Active, [], "", "acme");
    private static readonly TenantInfo Broken = new("broken", "Broken", TenantStatus.Active, [], "", "broken");
    private readonly ContentDatabase _database = new();
    private readonly FakeLock _lock = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private ContentClock Clock(params string[] ready)
    {
        // The host's rights in a background scope: no signed-in user, and the reviewer's grants are gone.
        var access = Substitute.For<IAccessScopes>();
        access.UserId.Returns((string?)null);

        var services = new ServiceCollection();
        services.AddScoped<ScopeTenant>();
        services.AddScoped<ITenantScopeInitializer>(sp => sp.GetRequiredService<ScopeTenant>());
        services.AddScoped(sp =>
        {
            var tenant = sp.GetRequiredService<ScopeTenant>().Id;
            if (tenant == Broken.Id) throw new InvalidOperationException("This tenant's database is down.");
            var context = _database.For(tenant);
            var options = Options.Create(new ContentOptions { Newsroom = new() { ReviewHandoverGrace = TimeSpan.Zero } });
            return new ReviewHandover(new EfRepository<Post>(context), new EfRepository<Post>(context),
                new PostAccess(access, TimeProvider.System), options);
        });

        var directory = Substitute.For<ITenantDirectory>();
        directory.All.Returns([Broken, Acme]);
        var tenants = new ContentClockTenants();
        foreach (var id in ready) tenants.InitializeAsync(id == Acme.Id ? Acme : Broken, Ct).Wait(Ct);
        return new ContentClock(services.BuildServiceProvider(), directory, tenants, _lock,
            Options.Create(new ContentOptions()), NullLogger<ContentClock>.Instance);
    }

    private async Task<int> SeedInReviewAsync()
    {
        await using var context = _database.For("acme");
        var category = Category.Create("politics", "daily", "Politics");
        context.AddRange(Press.Create("daily", "Daily"), category);
        var now = DateTimeOffset.UtcNow;
        var post = Post.Create("owner-1", category, "A title", "A body", now);
        post.Submit("owner-1", now);
        post.Assign("manager-1", "reviewer-1", now);
        context.Add(post);
        await context.SaveChangesAsync(Ct);
        return post.Id;
    }

    private async Task<Post> ReadAsync(int id)
    {
        await using var context = _database.For("acme");
        return (await new EfRepository<Post>(context).FirstOrDefaultAsync(new PostDetailSpec(id), Ct))!;
    }

    [Fact]
    public async Task ATickHandsTheReviewBackEvenWhenAnotherTenantFails()
    {
        var id = await SeedInReviewAsync();

        await Clock(Broken.Id, Acme.Id).RunOnceAsync(Ct);

        var post = await ReadAsync(id);
        post.State.ShouldBe(PostState.Accepted);
        post.AssigneeId.ShouldBeNull();
        post.History.ShouldContain(e => e.Action == "handed over" && e.Actor == ReviewHandover.ClockActor);
        _lock.Held.ShouldBe(0);
    }

    [Fact]
    public async Task ATenantAnotherReplicaHoldsIsLeftAlone()
    {
        var id = await SeedInReviewAsync();
        _lock.Busy = true;

        await Clock(Acme.Id).RunOnceAsync(Ct);

        (await ReadAsync(id)).State.ShouldBe(PostState.InReview);
    }

    [Fact]
    public async Task ATenantThatIsNotMigratedYetIsNotRead()
    {
        var id = await SeedInReviewAsync();

        await Clock().RunOnceAsync(Ct);

        (await ReadAsync(id)).State.ShouldBe(PostState.InReview);
        _lock.Asked.ShouldBe(0);
    }

    private sealed class ScopeTenant : ITenantScopeInitializer
    {
        public string Id { get; private set; } = "";
        public void Use(TenantInfo tenant, string resolvedBy) => Id = tenant.Id;
    }

    private sealed class FakeLock : IContentClockLock
    {
        public bool Busy { get; set; }
        public int Asked { get; private set; }
        public int Held { get; private set; }

        public Task<IAsyncDisposable?> TryAcquireAsync(TenantInfo tenant, CancellationToken ct)
        {
            Asked++;
            if (Busy) return Task.FromResult<IAsyncDisposable?>(null);
            Held++;
            return Task.FromResult<IAsyncDisposable?>(new Release(this));
        }

        private sealed class Release(FakeLock owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.Held--;
                return ValueTask.CompletedTask;
            }
        }
    }
}
