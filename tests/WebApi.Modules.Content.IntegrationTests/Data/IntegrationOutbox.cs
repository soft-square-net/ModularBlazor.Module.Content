using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Data.Messaging;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>Wave 4 C3: the feed rows and the work-item events reach the integration outbox in the save that makes the change.</summary>
public sealed class IntegrationOutbox : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private readonly ContentDatabase _database = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static PostNode Paragraph(string id, string text) =>
        new(id, PostKinds.Article, "block.paragraph", 1, Json($$"""{ "text": "{{text}}" }"""), Json("{}"), []);

    private async Task<List<OutboxMessage>> OutboxAsync()
    {
        await using var reader = _database.For("acme");
        return await reader.Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct);
    }

    [Fact]
    public async Task AContentsChangesAreFeedRowsAndIntegrationEventsInOneSave()
    {
        Guid id;
        await using (var context = _database.For("acme"))
        {
            await new EfRepository<Press>(context).AddAsync(Press.Create("daily", "Daily"), Ct);
            await new EfRepository<Category>(context).AddAsync(Category.Create("politics", "daily", "Politics"), Ct);
            var content = ContentItem.Create("owner-1", "daily", "politics", null, Paragraph("p1", "A paragraph"), "assigned", Now,
                assigneeId: "writer-1");
            id = content.Id;
            context.Contents.Add(content);
            await context.SaveChangesAsync(Ct);
        }
        await using (var context = _database.For("acme"))
        {
            var content = await context.Contents.SingleAsync(c => c.Id == id, Ct);
            content.Act("writer-1", PipelineDefinition.Default.Action("submit"), null, Now);
            await context.SaveChangesAsync(Ct);
        }

        var outbox = await OutboxAsync();
        outbox.Select(o => o.Type).ShouldBe([ContentEventTypes.ContentCreated, ContentEventTypes.WorkItemAssigned,
            ContentEventTypes.ContentStateChanged, ContentEventTypes.WorkItemStateChanged], ignoreOrder: true);
        await using var check = _database.For("acme");
        (await check.Events.CountAsync(Ct)).ShouldBe(2); // the feed: created, state-changed

        var state = JsonDocument.Parse(outbox.Single(o => o.Type == ContentEventTypes.WorkItemStateChanged).Envelope).RootElement;
        state.GetProperty("subject").GetString().ShouldBe(ContentWorkItems.RefOf(id));
        var item = state.GetProperty("data").GetProperty("item");
        item.GetProperty("ref").GetProperty("module").GetString().ShouldBe("content");
        item.GetProperty("state").GetString().ShouldBe("submitted");
        item.GetProperty("title").GetString().ShouldBe("A paragraph");
        item.GetProperty("assignee").GetProperty("kind").GetString().ShouldBe("user");
        state.GetProperty("data").GetProperty("previousState").GetString().ShouldBe("assigned");

        var feed = JsonDocument.Parse(outbox.Single(o => o.Type == ContentEventTypes.ContentStateChanged).Envelope).RootElement;
        feed.GetProperty("subject").GetString().ShouldBe(ContentEventTypes.ContentSubject(id));
        feed.GetProperty("data").GetProperty("feedType").GetString().ShouldBe(FeedEventTypes.ContentStateChanged);
        feed.GetProperty("data").GetProperty("stage").GetString().ShouldBe("submitted");
    }

    [Fact]
    public async Task ARolledBackTransactionLeavesNoIntegrationEvent()
    {
        await using (var context = _database.For("acme"))
        {
            var store = new ContentStore(context);
            await Should.ThrowAsync<InvalidOperationException>(() => store.InTransactionAsync(async () =>
            {
                context.Contents.Add(ContentItem.Create("owner-1", "daily", "politics", null, Paragraph("p1", "x"), "assigned", Now));
                await store.SaveChangesAsync(Ct);
                throw new InvalidOperationException("the second step failed");
            }, Ct));
        }

        (await OutboxAsync()).ShouldBeEmpty();
        await using var check = _database.For("acme");
        (await check.Contents.CountAsync(Ct)).ShouldBe(0);
    }
}
