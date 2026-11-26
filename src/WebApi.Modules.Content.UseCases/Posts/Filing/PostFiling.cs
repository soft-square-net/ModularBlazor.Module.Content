using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Events;
using soft.square.net.WebApi.Modules.Content.UseCases.Units;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Filing;

/// <summary>
/// Set a post's categories: <see cref="PrimaryCategoryId"/> and <see cref="CategoryIds"/> (which may repeat it), all of the
/// post's press (D4). Publishes <c>content.post-categories-changed.v1</c>.
/// </summary>
public sealed record ChangePostCategoriesCommand(int Id, int Version, string PrimaryCategoryId, IReadOnlyList<string> CategoryIds)
    : ICommand<Result<PostDetail>>;

/// <summary>
/// Who may: whoever may edit the post now (its owner while it is editable, its reviewer on shift, Desk) or the workflow
/// (<c>content.unit.assign</c>); a category the post is not in yet also needs the author right or a seeing right in it.
/// A post in a unit keeps it only while the unit serves one of the new categories (409 otherwise: the workflow moves it first).
/// </summary>
public sealed class ChangePostCategoriesHandler(PostChanges changes, PostAccess access, PostEditors editors, IReadRepository<Category> categories,
    UnitRules units, ContentEventPublisher events) : ICommandHandler<ChangePostCategoriesCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(ChangePostCategoriesCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            if (!await access.HasAsync(ContentPermissions.UnitAssign, post.Scopes, ct))
            {
                var role = await editors.RoleAsync(post, ct);
                if (!role.IsSuccess) return role.Status == ResultStatus.Forbidden ? Result.Forbidden([.. role.Errors]) : Result.Conflict([.. role.Errors]);
                if (role.Value == Core.PostAggregate.PostEditor.Owner && !post.OwnerMayEdit(access.Now, editors.EditWindow))
                    return Result.Conflict($"The owner cannot change the categories of a Post that is {post.State}.");
            }

            var wanted = new List<Category>();
            foreach (var id in new[] { command.PrimaryCategoryId }.Concat(command.CategoryIds ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct())
            {
                var category = await categories.GetByIdAsync(id, ct);
                if (category is null) return Result.Invalid(new ValidationError("categoryIds", $"Unknown category '{id}'."));
                if (category.PressId != post.PressId)
                    return Result.Invalid(new ValidationError("categoryIds",
                        $"Category '{id}' is of press '{category.PressId}': a post's categories are all of its press ('{post.PressId}')."));
                if (!post.Categories.Contains(id))
                {
                    var scopes = new[] { AccessScope.Press(category.PressId), AccessScope.Category(category.Id) };
                    if (!await access.HasAsync(ContentPermissions.Author, scopes, ct) && !await access.SeesAsync(scopes, ct)
                        && !await access.HasAsync(ContentPermissions.UnitAssign, scopes, ct))
                        return Result.Forbidden($"You may not file posts in category '{id}' ({ContentPermissions.Author}).");
                }
                wanted.Add(category);
            }
            if (wanted.Count == 0) return Result.Invalid(new ValidationError("primaryCategoryId", "A post needs a primary category."));

            if (post.UnitId is { } unitId)
            {
                var serving = await units.ServingAsync(unitId, wanted.Select(c => c.Id).ToArray(), "any of the new categories", ct);
                if (!serving.IsSuccess) return serving.Status == ResultStatus.Invalid ? Result.Conflict($"The post's unit is no longer known; {string.Join(" ", serving.ValidationErrors.Select(e => e.ErrorMessage))}") : serving.AsResult();
            }

            var previous = post.Categories.ToList();
            if (post.ChangeCategories(access.Me, wanted[0], wanted, access.Now)) events.CategoriesChanged(post, previous);
            return Result.Success();
        }, ct);
}

/// <summary>
/// Put a post in an Organization unit, or take it out of any (null). Only the workflow (<c>content.unit.assign</c>; nobody
/// else holds it by default: ordinary callers get 403). The unit must serve one of the post's categories, <c>*</c> serving
/// all. Publishes <c>content.post-unit-changed.v1</c>.
/// </summary>
public sealed record SetPostUnitCommand(int Id, int Version, Guid? UnitId) : ICommand<Result<PostDetail>>;

public sealed class SetPostUnitHandler(PostChanges changes, PostAccess access, UnitRules units, ContentEventPublisher events)
    : ICommandHandler<SetPostUnitCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SetPostUnitCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var allowed = await access.RequireAsync(ContentPermissions.UnitAssign, post.Scopes,
                "put posts in an Organization unit: the workflow does, from the moderator's choice", ct);
            if (!allowed.IsSuccess) return allowed;
            string? path = null;
            if (command.UnitId is { } unitId)
            {
                var unit = await units.ForPostAsync(post, unitId, ct);
                if (!unit.IsSuccess) return unit.AsResult();
                path = unit.Value.Path;
            }
            var previous = post.UnitId;
            if (post.ChangeUnit(access.Me, command.UnitId, path, access.Now)) events.UnitChanged(post, previous);
            return Result.Success();
        }, ct);
}

/// <summary>
/// Give a content to an Organization unit, or to none (null). Only the workflow (<c>content.unit.assign</c>). The unit must
/// serve the content's category (<c>*</c> serves all). A legacy Shifts section is cleared.
/// </summary>
public sealed record SetContentUnitCommand(Guid Id, int Version, Guid? UnitId) : ICommand<Result<ContentDetail>>;

public sealed class SetContentUnitHandler(ContentChanges changes, ContentAccess access, UnitRules units)
    : ICommandHandler<SetContentUnitCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(SetContentUnitCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async content =>
        {
            var allowed = await access.Posts.RequireAsync(ContentPermissions.UnitAssign, content.Scopes,
                "give contents to an Organization unit: the workflow does", ct);
            if (!allowed.IsSuccess) return allowed;
            string? path = null;
            if (command.UnitId is { } unitId)
            {
                var unit = await units.ForContentAsync(content, unitId, ct);
                if (!unit.IsSuccess) return unit.AsResult();
                path = unit.Value.Path;
            }
            content.ChangeUnit(access.Me, command.UnitId, path, access.Now);
            return Result.Success();
        }, ct);
}
