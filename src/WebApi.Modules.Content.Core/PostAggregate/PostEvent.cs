namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>One entry of a Post's audit trail: who did what, when, and the state it led to.</summary>
public sealed class PostEvent : EntityBase<int>
{
    private PostEvent() { } // EF Core

    public PostEvent(DateTimeOffset at, string actor, string action, PostState state, string? detail)
    {
        At = at;
        Actor = actor;
        Action = action;
        State = state;
        Detail = detail;
    }

    public DateTimeOffset At { get; private set; }
    public string Actor { get; private set; } = "";
    public string Action { get; private set; } = "";
    public PostState State { get; private set; }
    public string? Detail { get; private set; }
}
