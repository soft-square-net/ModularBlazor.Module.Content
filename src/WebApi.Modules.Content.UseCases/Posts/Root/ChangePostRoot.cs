using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Root;

/// <summary>
/// Set the root values of a post's document (the values of its kind: headline, summary, image...), at a published version
/// of the kind's field; its contents, title and body stay. It also gives a post made from a title and a body only
/// (<c>legacy.basic</c>) its kind's root. DynamicFields checks the values when the post is approved, scheduled or
/// published, as for every document.
/// </summary>
public sealed record ChangePostRootCommand(int Id, int Version, int FieldVersion, JsonElement Content, JsonElement? Style)
    : ICommand<Result<PostDetail>>;

/// <summary>Who may: whoever may edit the post now (its owner while it is editable, its reviewer on shift, Desk).</summary>
public sealed class ChangePostRootHandler(PostChanges changes, PostAccess access, PostEditors editors, PostKindRules kinds)
    : ICommandHandler<ChangePostRootCommand, Result<PostDetail>>
{
    private static readonly JsonElement NoStyle = JsonSerializer.SerializeToElement(new { });

    public async ValueTask<Result<PostDetail>> Handle(ChangePostRootCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var role = await editors.RoleAsync(post, ct);
            if (!role.IsSuccess) return role.Status == ResultStatus.Forbidden ? Result.Forbidden([.. role.Errors]) : Result.Conflict([.. role.Errors]);
            var kind = await kinds.ForPublishingAsync(post, ct);
            if (!kind.IsSuccess)
                return kind.Status == ResultStatus.Unavailable ? Result.Unavailable([.. kind.Errors]) : Result.Conflict([.. kind.Errors]);
            if (command.FieldVersion < 1 || command.FieldVersion > kind.Value.Version)
                return Result.Invalid(new ValidationError("fieldVersion",
                    $"'{post.PostKind}' is published up to version {kind.Value.Version}, not {command.FieldVersion}."));
            var style = command.Style is { ValueKind: JsonValueKind.Object } given ? given : NoStyle;
            post.ChangeRoot(role.Value, access.Me, command.FieldVersion, command.Content, style, access.Now, editors.EditWindow);
            return Result.Success();
        }, ct);
}
