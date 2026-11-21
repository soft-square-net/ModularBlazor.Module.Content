using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Translate;

/// <summary>
/// Add or replace a Post's translation into one language. The caller checks it against the fields first (the
/// DynamicFields translate dialog does); Contents checks its structure against the document. Who may: whoever may edit
/// the Post now (its owner, its assigned reviewer on shift, or Desk), without changing its workflow state.
/// </summary>
public sealed record TranslatePostCommand(int Id, int Version, string Language,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedText>> Nodes) : ICommand<Result<PostDetail>>;

/// <summary>Remove a Post's translation into one language; the same people as <see cref="TranslatePostCommand"/>.</summary>
public sealed record RemovePostTranslationCommand(int Id, int Version, string Language) : ICommand<Result<PostDetail>>;

public sealed class TranslatePostHandler(PostChanges changes, PostTranslators translators) : ICommandHandler<TranslatePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(TranslatePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var allowed = await translators.MayTranslateAsync(post, ct);
            if (!allowed.IsSuccess) return allowed;
            post.Translate(translators.Me, command.Language ?? "", command.Nodes, translators.Now);
            return Result.Success();
        }, ct);
}

public sealed class RemovePostTranslationHandler(PostChanges changes, PostTranslators translators)
    : ICommandHandler<RemovePostTranslationCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(RemovePostTranslationCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var allowed = await translators.MayTranslateAsync(post, ct);
            if (!allowed.IsSuccess) return allowed;
            post.RemoveTranslation(translators.Me, command.Language ?? "", translators.Now);
            return Result.Success();
        }, ct);
}
