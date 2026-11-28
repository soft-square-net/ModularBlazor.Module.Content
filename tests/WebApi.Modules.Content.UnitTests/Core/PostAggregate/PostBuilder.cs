using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PostAggregate;

/// <summary>Posts in a given workflow state, made through the aggregate's own methods.</summary>
public static class PostBuilder
{
    public const string Owner = "owner-1";
    public const string Manager = "manager-1";
    public const string Reviewer = "reviewer-1";
    public const string Desk = "desk-1";
    public const string Shift = "shift-1";

    public static Category Category() => Content.Core.CategoryAggregate.Category.Create("politics", "daily", "Politics");

    public static Post Draft(DateTimeOffset now) => Post.Create(Owner, Category(), "A title", "A body", now);

    public static Post Submitted(DateTimeOffset now)
    {
        var post = Draft(now);
        post.Submit(Owner, now);
        return post;
    }

    public static Post InReview(DateTimeOffset now)
    {
        var post = Submitted(now);
        post.Accept(Manager, Shift, now);
        post.Assign(Manager, Reviewer, now);
        return post;
    }

    public static Post Approved(DateTimeOffset now)
    {
        var post = InReview(now);
        post.Approve(Reviewer, now);
        return post;
    }
}
