using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;

/// <summary>
/// The assigned reviewer approves the post; the contents made for it that are still in a work stage go to the Desk with it
/// (the pipeline's submit action, taken by the reviewer). Shared contents from elsewhere follow their own pipeline.
/// DynamicFields first checks the whole post (<see cref="ContentValidation.CheckPostAsync"/>).
/// </summary>
public sealed class ApprovePostHandler(PostChanges changes, PostAccess access, PostContents contents, ContentPipelines pipelines,
    ContentValidation validation)
    : ICommandHandler<ApprovePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(ApprovePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var reviewer = await access.RequireAsync(ContentPermissions.Review, post.Scopes, "review in this category now (is your shift over?)", ct);
            if (!reviewer.IsSuccess) return reviewer;
            if (post.State != Core.PostAggregate.PostState.InReview || post.AssigneeId != access.Me)
                return Result.Conflict("Only the assigned reviewer approves a Post in review.");
            var valid = await validation.CheckPostAsync(post, "approve", ct);
            if (!valid.IsSuccess) return valid;
            post.Approve(access.Me, access.Now);

            var pipeline = await pipelines.CurrentAsync(ct);
            foreach (var content in (await contents.LoadAsync(post, ct)).Values)
                if (content.OriginPostId == post.StablePublicId && !content.IsArchived
                    && ContentPipelines.StageOf(pipeline, content).Kind == StageKind.Work && pipeline.SubmitFrom(content.Stage) is { } submit)
                    content.Act(access.Me, submit, null, access.Now);
            return Result.Success();
        }, ct);
}
