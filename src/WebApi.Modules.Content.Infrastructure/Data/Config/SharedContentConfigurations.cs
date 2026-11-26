using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

/// <summary>The shared contents (library), with their revisions and audit trail. Oracle's JSON column types are set in <see cref="ContentDbContext"/>.</summary>
public sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> c)
    {
        c.ToTable("contents");
        c.Property(x => x.Id).ValueGeneratedNever();
        c.Property(x => x.NodeId).HasMaxLength(ContentItem.NodeIdLength);
        c.Property(x => x.FieldKey).HasMaxLength(128);
        c.Property(x => x.PostKind).HasMaxLength(PostKinds.MaxLength);
        c.Property(x => x.PressId).HasMaxLength(DataSchemaConstants.SlugLength);
        c.Property(x => x.CategoryId).HasMaxLength(DataSchemaConstants.SlugLength);
        c.Property(x => x.OriginPostId).HasMaxLength(40);
        c.Property(x => x.SectionId).HasMaxLength(DataSchemaConstants.SlugLength);
        c.Property(x => x.UnitPath).HasMaxLength(soft.square.net.WebApi.Modules.Content.Core.Common.UnitPathRules.MaxLength);
        c.Property(x => x.AssigneeId).HasMaxLength(DataSchemaConstants.UserIdLength);
        c.Property(x => x.CreatedBy).HasMaxLength(DataSchemaConstants.UserIdLength);
        c.Property(x => x.Stage).HasMaxLength(ContentItem.StageLength);
        c.Property(x => x.ReviewNote).HasMaxLength(ContentItem.NoteLength);
        c.Property(x => x.Version).IsConcurrencyToken();
        c.Ignore(x => x.Scopes);
        c.Ignore(x => x.Node);
        c.Ignore(x => x.IsArchived);
        c.HasIndex(x => new { x.Stage, x.SectionId });
        c.HasIndex(x => x.AssigneeId);
        c.HasIndex(x => x.UnitId);
        c.HasIndex(x => x.CategoryId);
        c.HasIndex(x => x.UpdatedAt);
        c.HasMany(x => x.Revisions).WithOne().HasForeignKey(r => r.ContentId).OnDelete(DeleteBehavior.Cascade);
        c.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        c.HasMany(x => x.History).WithOne().HasForeignKey("ContentId").OnDelete(DeleteBehavior.Cascade);
        c.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ContentRevisionConfiguration : IEntityTypeConfiguration<ContentRevision>
{
    public void Configure(EntityTypeBuilder<ContentRevision> r)
    {
        r.ToTable("content_revisions");
        r.Property(x => x.FieldKey).HasMaxLength(128);
        r.Property(x => x.By).HasMaxLength(DataSchemaConstants.UserIdLength);
        r.Ignore(x => x.Node);
        r.HasIndex(x => new { x.ContentId, x.Revision }).IsUnique();
    }
}

public sealed class ContentHistoryConfiguration : IEntityTypeConfiguration<ContentHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ContentHistoryEntry> h)
    {
        h.ToTable("content_history");
        h.Property(x => x.Actor).HasMaxLength(DataSchemaConstants.UserIdLength);
        h.Property(x => x.Action).HasMaxLength(64);
        h.Property(x => x.Stage).HasMaxLength(ContentItem.StageLength);
        h.Property(x => x.Detail).HasMaxLength(ContentItem.NoteLength + 100);
    }
}

/// <summary>A post's references to contents (no foreign key to <c>contents</c>: they are separate aggregates; Content checks references before a delete).</summary>
public sealed class PostContentConfiguration : IEntityTypeConfiguration<PostContent>
{
    public void Configure(EntityTypeBuilder<PostContent> p)
    {
        p.ToTable("post_contents");
        p.HasIndex(x => new { x.PostId, x.ContentId }).IsUnique();
        p.HasIndex(x => x.ContentId);
    }
}

public sealed class PostSlugConfiguration : IEntityTypeConfiguration<PostSlug>
{
    public void Configure(EntityTypeBuilder<PostSlug> s)
    {
        s.ToTable("post_slugs");
        s.Property(x => x.Culture).HasMaxLength(35);
        s.Property(x => x.Slug).HasMaxLength(PostSlug.MaxLength);
        s.HasIndex(x => new { x.Culture, x.Slug }).IsUnique();
        s.HasIndex(x => new { x.PostId, x.Culture }).IsUnique();
    }
}

public sealed class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> p)
    {
        p.ToTable("pipelines");
        p.Property(x => x.Key).HasMaxLength(PipelineDefinition.MaxKeyLength);
        p.Property(x => x.UpdatedBy).HasMaxLength(DataSchemaConstants.UserIdLength);
        p.Property(x => x.Version).IsConcurrencyToken();
        p.Ignore(x => x.Definition);
    }
}

/// <summary>The outbox of the change feed.</summary>
public sealed class FeedEntryConfiguration : IEntityTypeConfiguration<FeedEntry>
{
    public void Configure(EntityTypeBuilder<FeedEntry> e)
    {
        e.ToTable("events");
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.Type).HasMaxLength(FeedEntry.TypeLength);
        e.Property(x => x.Stage).HasMaxLength(ContentItem.StageLength);
        e.Property(x => x.PostState).HasMaxLength(32);
        e.Property(x => x.By).HasMaxLength(DataSchemaConstants.UserIdLength);
        e.Property(x => x.Detail).HasMaxLength(FeedEntry.DetailLength);
        e.HasIndex(x => x.PostId);
        e.HasIndex(x => x.ContentId);
        e.HasIndex(x => x.At); // retention
    }
}
