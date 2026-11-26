using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

namespace soft.square.net.WebApi.Modules.Content.MigrationTests;

/// <summary>
/// One engine's migration set against a real database of that engine in Docker:
/// <list type="number">
/// <item>its model snapshot matches the model (no pending model changes; no database needed);</item>
/// <item>every migration applies to an empty database, in order;</item>
/// <item>the data step of <c>PostKindsAsFieldKeys</c> works both ways: down to the enum names, up to the field keys,
/// in the kind columns and in the stored JSON;</item>
/// <item>every migration's Down runs (down to an empty schema), and every Up again.</item>
/// </list>
/// Without Docker the database tests are skipped.
/// </summary>
public abstract class MigrationHarness
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The migration that turns the kinds into field keys (its id ends with this name on every engine).</summary>
    private const string KindsMigration = "_PostKindsAsFieldKeys";

    protected abstract DatabaseProvider Provider { get; }

    /// <summary>The engine's container (not started).</summary>
    protected abstract IDatabaseContainer CreateContainer();

    /// <summary>The connection string of the started container, for the module's database.</summary>
    protected virtual string ConnectionString(IDatabaseContainer container) => container.GetConnectionString();

    /// <summary>A connection string that is never opened (the model check needs none).</summary>
    protected abstract string OfflineConnection { get; }

    /// <summary>A table of the module (<c>articles</c>), quoted, with its schema or prefix.</summary>
    protected abstract string Table(string name);

    /// <summary>A column or alias, quoted.</summary>
    protected abstract string Column(string name);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void TheModelSnapshotMatchesTheModel()
    {
        using var context = Context(OfflineConnection);

        context.Database.HasPendingModelChanges().ShouldBeFalse($"{Provider}: the model has changes no migration has: add one.");
    }

    [Fact]
    public async Task AppliesEveryMigrationAndMapsTheKindsBothWays()
    {
        await using var container = await StartAsync();
        var connection = ConnectionString(container);

        // 1. Every migration, from an empty database.
        await using (var context = Context(connection))
        {
            var all = context.Database.GetMigrations().ToList();
            all.Count.ShouldBeGreaterThan(1);
            await context.Database.MigrateAsync(Ct);
            (await context.Database.GetAppliedMigrationsAsync(Ct)).ShouldBe(all);
            (await context.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        }

        // 2. Posts and contents of the new kinds, then down to the enum names (the step before the kinds migration).
        await SeedAsync(connection);
        string previous;
        await using (var context = Context(connection))
        {
            var all = context.Database.GetMigrations().ToList();
            var kinds = all.FindIndex(m => m.EndsWith(KindsMigration, StringComparison.Ordinal));
            kinds.ShouldBeGreaterThan(0, "the kinds migration exists");
            previous = all[kinds - 1];
            await context.GetService<IMigrator>().MigrateAsync(previous, Ct);
        }
        await using (var context = Context(connection))
        {
            (await Scalar(context, "articles", "Type")).ShouldBe("Video");
            (await Scalar(context, "contents", "PostKind")).ShouldBe("PhotoStory");
            var document = await Scalar(context, "articles", "DocumentJson");
            document.ShouldContain("\"postKind\":\"PhotoStory\"");
            document.ShouldNotContain("post.photo-story");
            (await Scalar(context, "contents", "ValuesJson")).ShouldContain("\"postKind\":\"PhotoStory\"");
            (await Scalar(context, "content_revisions", "ValuesJson")).ShouldContain("\"postKind\":\"PhotoStory\"");
        }

        // 3. Up again: the enum names become field keys.
        await using (var context = Context(connection))
        {
            await context.Database.MigrateAsync(Ct);
            (await Scalar(context, "articles", "Type")).ShouldBe(PostKinds.Video);
            (await Scalar(context, "contents", "PostKind")).ShouldBe(PostKinds.PhotoStory);
            (await Scalar(context, "articles", "DocumentJson")).ShouldContain($"\"postKind\":\"{PostKinds.PhotoStory}\"");
            (await Scalar(context, "contents", "ValuesJson")).ShouldContain($"\"postKind\":\"{PostKinds.PhotoStory}\"");
            (await Scalar(context, "content_revisions", "ValuesJson")).ShouldContain($"\"postKind\":\"{PostKinds.PhotoStory}\"");

            var post = await context.Posts.AsNoTracking().SingleAsync(Ct);
            post.PostKind.ShouldBe(PostKinds.Video);
            post.Root.SchemaId.ShouldBe("post.video");
            var content = await context.Contents.AsNoTracking().SingleAsync(Ct);
            content.Node.PostKind.ShouldBe(PostKinds.PhotoStory);
        }

        // 4. Every Down, then every Up again.
        await using (var context = Context(connection))
        {
            await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, Ct);
            (await context.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
            await context.Database.MigrateAsync(Ct);
            (await context.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        }
    }

    /// <summary>A post of kind post.video whose document has one photo-story node, and a content of that node (one row each).</summary>
    private async Task SeedAsync(string connection)
    {
        await using var context = Context(connection);
        var press = Press.Create("daily", "Daily");
        var category = Category.Create("politics", "daily", "Politics");
        var node = new PostNode("n1", PostKinds.PhotoStory, "block.paragraph", 1, Json("""{ "text": "One" }"""), Json("{}"), []);
        var content = ContentItem.Create("owner-1", "daily", "politics", null, node, "draft", Now);
        var document = new PostDocument("post.video", 1, Json("""{ "headline": "A title" }"""), Json("{}"), [node]);
        // A body: Oracle stores '' as NULL, and Body is required (a known gap, see the C1 handover).
        var post = Post.Create("owner-1", category, "A title", "A body", Now, PostKinds.Video, document);
        context.AddRange(press, category, content, post);
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>The value of a column of a table that holds one row (the seed's).</summary>
    private Task<string> Scalar(ContentDbContext context, string table, string column)
    {
        var sql = $"SELECT {Column(column)} AS {Column("Value")} FROM {Table(table)}"; // names only, from this class
        return context.Database.SqlQueryRaw<string>(sql).SingleAsync(Ct);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private async Task<IDatabaseContainer> StartAsync()
    {
        IDatabaseContainer container;
        try
        {
            container = CreateContainer();
            await container.StartAsync(Ct);
        }
        catch (Exception e) when (e is DockerUnavailableException || e.InnerException is DockerUnavailableException)
        {
            Assert.Skip($"{Provider}: Docker is not available ({e.Message}).");
            throw; // not reached
        }
        return container;
    }

    /// <summary>A context on <paramref name="connection"/> with this engine's migration set, as the platform builds it.</summary>
    private ContentDbContext Context(string connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<DomainEventDispatcher>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var options = new DbContextOptionsBuilder<ContentDbContext>();
        DatabaseProviderAdapter.For(Provider).Use(options, connection, "content_tests");
        options.ReplaceService<IMigrationsAssembly, ProviderMigrationsAssembly>();
        options.UseApplicationServiceProvider(provider);
        return new ContentDbContext(options.Options, new TestTenant("acme"));
    }

    private sealed class TestTenant(string id) : ITenantContext
    {
        public TenantInfo? Tenant { get; } = new(id, id, TenantStatus.Active, [], "", "content_tests");
        public string? ResolvedBy => "test";
    }
}
