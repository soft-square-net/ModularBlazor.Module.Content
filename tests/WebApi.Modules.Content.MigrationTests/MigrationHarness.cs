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
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

namespace soft.square.net.WebApi.Modules.Content.MigrationTests;

/// <summary>
/// One engine's migration set against a real database of that engine in Docker:
/// <list type="number">
/// <item>its model snapshot matches the model (no pending model changes; no database needed);</item>
/// <item>every migration applies to an empty database, in order;</item>
/// <item>a post with an empty body saves and reads back with an empty body (Oracle stores '' as NULL);</item>
/// <item>the data step of <c>PostKindsAsFieldKeys</c> works both ways: down to the enum names, up to the field keys,
/// in the kind columns and in the stored JSON;</item>
/// <item>the data step of <c>UnitsAndCategories</c>: posts and contents keep their unit, a post its categories; down and
/// up again, a post's single category (<c>articles.CategoryId</c>) becomes its <c>post_categories</c> row and each category's
/// Arabic name starts as its English one;</item>
/// <item>the data step of <c>WorkItemStages</c>: down, a content in a stage that did not exist (in-progress) goes back to the
/// old start stage (draft); up, the old keys become the work-item states, in the contents and in a published pipeline
/// (rejected → returned in both);</item>
/// <item>the integration outbox and inbox tables exist (Content publishes and subscribes);</item>
/// <item>every migration's Down runs (down to an empty schema), and every Up again.</item>
/// </list>
/// Without Docker the database tests are skipped.
/// </summary>
public abstract class MigrationHarness
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The migration that turns the kinds into field keys (its id ends with this name on every engine).</summary>
    private const string KindsMigration = "_PostKindsAsFieldKeys";

    /// <summary>The migration of the post categories, the category names and the units.</summary>
    private const string UnitsMigration = "_UnitsAndCategories";

    /// <summary>The migration of the stage keys to the work-item states.</summary>
    private const string StagesMigration = "_WorkItemStages";

    private static readonly Guid Root = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Desk = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static string DeskPath => $"/{Root:N}/{Desk:N}";

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

        // 2. Posts and contents of the new kinds, in two categories and a unit; they read back through EF.
        await SeedAsync(connection);
        await using (var context = Context(connection))
        {
            var post = await context.Posts.AsNoTracking().SingleAsync(Ct);
            post.Body.ShouldBe("");
            post.Categories.ShouldBe(["politics", "economy"]);
            post.UnitId.ShouldBe(Desk);
            post.UnitPath.ShouldBe(DeskPath);
            post.Scopes.ShouldContain($"unit:{Root:N}");
            var content = await context.Contents.AsNoTracking().SingleAsync(Ct);
            content.UnitId.ShouldBe(Desk);
            content.Scopes.ShouldContain($"section:{Desk:N}");
            (await context.Categories.AsNoTracking().SingleAsync(c => c.Id == "politics", Ct)).NameAr.ShouldBe("السياسة");
            (await Count(context, "integration_outbox")).ShouldBe(0);
            (await Count(context, "integration_inbox")).ShouldBe(0);
        }

        // 2b. The stage keys: down to before WorkItemStages (in-progress is the old start stage, draft), up again (assigned).
        await using (var context = Context(connection))
        {
            (await Scalar(context, "contents", "Stage")).ShouldBe("in-progress");
            await context.GetService<IMigrator>().MigrateAsync(Before(context, StagesMigration), Ct);
            (await Scalar(context, "contents", "Stage")).ShouldBe("draft");
        }
        await using (var context = Context(connection))
        {
            await context.Database.MigrateAsync(Ct);
            (await Scalar(context, "contents", "Stage")).ShouldBe("assigned");
        }
        // ... and with a pipeline Elsa published with the old keys: both its definition and the content's stage are renamed.
        await using (var context = Context(connection))
        {
            await context.GetService<IMigrator>().MigrateAsync(Before(context, StagesMigration), Ct);
            context.Pipelines.Add(Pipeline.Create(LegacyPipeline, "elsa", Now));
            await context.SaveChangesAsync(Ct);
            var sql = $"UPDATE {Table("contents")} SET {Column("Stage")} = 'rejected'"; // names only, from this class
            await context.Database.ExecuteSqlRawAsync(sql, Ct);
        }
        await using (var context = Context(connection))
        {
            await context.Database.MigrateAsync(Ct);
            (await Scalar(context, "contents", "Stage")).ShouldBe("returned");
            var json = await Scalar(context, "pipelines", "DefinitionJson");
            json.ShouldNotContain("\"rejected\"");
            json.ShouldNotContain("\"draft\"");
            var pipeline = (await context.Pipelines.AsNoTracking().SingleAsync(Ct)).Definition;
            pipeline.StartStage.ShouldBe("assigned");
            pipeline.Stages.Select(s => s.Key).ShouldBe(["assigned", "submitted", "accepted", "returned"]);
            pipeline.Action("reject").To.ShouldBe("returned");
            pipeline.Validate();
        }

        // 3. Down to the step before the units migration, and up again: the primary category becomes the post's row of
        // post_categories (the others were in the dropped table), and the Arabic names start as the English ones.
        await using (var context = Context(connection))
        {
            var all = context.Database.GetMigrations().ToList();
            var units = all.FindIndex(m => m.EndsWith(UnitsMigration, StringComparison.Ordinal));
            units.ShouldBeGreaterThan(0, "the units migration exists");
            await context.GetService<IMigrator>().MigrateAsync(all[units - 1], Ct);
            (await Scalars(context, "categories", "Name")).Order().ShouldBe(["Economy", "Politics"]);
        }
        await using (var context = Context(connection))
        {
            await context.Database.MigrateAsync(Ct);
            (await Scalars(context, "post_categories", "CategoryId")).ShouldBe(["politics"]);
            (await Scalars(context, "categories", "NameAr")).Order().ShouldBe(["Economy", "Politics"]);
            var post = await context.Posts.AsNoTracking().SingleAsync(Ct);
            post.Categories.ShouldBe(["politics"]);
            post.PrimaryCategoryId.ShouldBe("politics");
            post.UnitId.ShouldBeNull();
            (await context.Categories.AsNoTracking().SingleAsync(c => c.Id == "politics", Ct)).NameEn.ShouldBe("Politics");
        }

        // 4. Down to the enum names (the step before the kinds migration).
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

        // 5. Up again: the enum names become field keys.
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

        // 6. Every Down, then every Up again.
        await using (var context = Context(connection))
        {
            await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, Ct);
            (await context.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
            await context.Database.MigrateAsync(Ct);
            (await context.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        }
    }

    /// <summary>
    /// A post of kind post.video with an empty body, whose document has one photo-story node, and a content of that node
    /// (one row each); the post is in two categories of its press and in a unit, the content in the same unit.
    /// </summary>
    private async Task SeedAsync(string connection)
    {
        await using var context = Context(connection);
        var press = Press.Create("daily", "Daily");
        var category = Category.Create("politics", "daily", "Politics", "السياسة");
        var economy = Category.Create("economy", "daily", "Economy");
        var node = new PostNode("n1", PostKinds.PhotoStory, "block.paragraph", 1, Json("""{ "text": "One" }"""), Json("{}"), []);
        var content = ContentItem.Create("owner-1", "daily", "politics", null, node, "in-progress", Now);
        content.ChangeUnit("workflow", Desk, DeskPath, Now);
        var document = new PostDocument("post.video", 1, Json("""{ "headline": "A title" }"""), Json("{}"), [node]);
        // An empty body, as a document-only post has (Oracle stores it as NULL).
        var post = Post.Create("owner-1", category, "A title", "", Now, PostKinds.Video, document, otherCategories: [economy]);
        post.ChangeUnit("workflow", Desk, DeskPath, Now);
        context.AddRange(press, category, economy, content, post);
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>The migration just before the one whose id ends with <paramref name="name"/>.</summary>
    private static string Before(ContentDbContext context, string name)
    {
        var all = context.Database.GetMigrations().ToList();
        var index = all.FindIndex(m => m.EndsWith(name, StringComparison.Ordinal));
        index.ShouldBeGreaterThan(0, $"the migration {name} exists");
        return all[index - 1];
    }

    /// <summary>The first built-in pipeline, as Elsa may have published it before the stages were renamed.</summary>
    private static PipelineDefinition LegacyPipeline { get; } = new("newsroom", "draft",
    [
        new("draft", StageKind.Work, ["assignee"]),
        new("desk", StageKind.Review, ["content.publish"]),
        new("accepted", StageKind.Accepted, ["content.publish"], OnEdit: "desk"),
        new("rejected", StageKind.Work, ["assignee"])
    ],
    [
        new("submit", ["draft", "rejected"], "desk", ["assignee"]),
        new("accept", ["desk"], "accepted", ["content.publish"], Pins: true),
        new("reject", ["desk", "accepted"], "rejected", ["content.publish", "content.return"], RequiresNote: true),
        new("reopen", ["accepted"], "desk", ["content.publish"])
    ]);

    /// <summary>The values of a column of a table, every row.</summary>
    private async Task<List<string>> Scalars(ContentDbContext context, string table, string column)
    {
        var sql = $"SELECT {Column(column)} AS {Column("Value")} FROM {Table(table)}"; // names only, from this class
        return await context.Database.SqlQueryRaw<string>(sql).ToListAsync(Ct);
    }

    /// <summary>The number of rows of a table.</summary>
    private Task<int> Count(ContentDbContext context, string table)
    {
        var sql = $"SELECT COUNT(*) AS {Column("Value")} FROM {Table(table)}"; // names only, from this class
        return context.Database.SqlQueryRaw<int>(sql).SingleAsync(Ct);
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
