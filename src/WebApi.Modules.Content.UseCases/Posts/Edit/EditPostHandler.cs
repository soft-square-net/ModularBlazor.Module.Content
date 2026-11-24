using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;

/// <summary>
/// A whole-document edit (today's editor): the owner, the assigned reviewer on shift, or Desk (<see cref="PostEditors"/>).
/// The document is split into the post's root values and its shared contents (<see cref="PostDocumentSplit"/>).
/// </summary>
public sealed class EditPostHandler(PostChanges changes, PostAccess access, PostEditors editors, PostDocumentSplit split)
    : ICommandHandler<EditPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(EditPostCommand edit, CancellationToken ct) =>
        await changes.ChangeAsync(edit.Id, edit.Version, async post =>
        {
            var role = await editors.RoleAsync(post, ct);
            if (!role.IsSuccess) return role.Status == ResultStatus.Forbidden ? Result.Forbidden([.. role.Errors]) : Result.Conflict([.. role.Errors]);
            var change = await split.EditAsync(post, access.Me, edit.Title, edit.Body, edit.Document, access.Now, ct);
            post.Edit(role.Value, access.Me, access.Now, editors.EditWindow, change);
            return Result.Success();
        }, ct);
}
