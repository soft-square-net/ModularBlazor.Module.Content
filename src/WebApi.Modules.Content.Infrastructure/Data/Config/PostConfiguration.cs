using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Content.Core.Common;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

/// <summary>The Post aggregate. Oracle's column types for the documents are set in <see cref="ContentDbContext"/>.</summary>
public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> post)
    {
        // Retain deployed table/column names until an explicit data migration is planned.
        post.ToTable("articles");
        post.Property(a => a.PressId).HasMaxLength(DataSchemaConstants.SlugLength);
        // The primary category keeps its column; all the categories (the primary one too) are rows of post_categories.
        post.Property(a => a.PrimaryCategoryId).HasColumnName("CategoryId").HasMaxLength(DataSchemaConstants.SlugLength);
        post.Property(a => a.UnitPath).HasMaxLength(UnitPathRules.MaxLength);
        post.Property(a => a.Title).HasMaxLength(300);
        post.Property(a => a.OwnerId).HasMaxLength(DataSchemaConstants.UserIdLength);
        post.Property(a => a.State).HasConversion<string>().HasMaxLength(32);
        // The kind, a DynamicFields root field key (the enum names were migrated to keys by PostKindsAsFieldKeys).
        post.Property(a => a.PostKind).HasColumnName("Type").HasMaxLength(PostKinds.MaxLength).HasDefaultValue(PostKinds.Article);
        post.Property(a => a.PublishedUrlPrefix).HasMaxLength(UrlPrefixes.MaxLength);
        post.Property(a => a.PublicId).HasMaxLength(32);
        post.Property(a => a.SchemaId).HasMaxLength(128).HasDefaultValue("legacy.basic");
        post.Property(a => a.SchemaVersion).HasDefaultValue(1);
        post.Property(a => a.DocumentJson).HasDefaultValue("{}");
        post.Property(a => a.PublishedDocumentJson);
        post.Property(a => a.TranslationsJson).HasDefaultValue("[]");
        post.Property(a => a.PublishedTranslationsJson);
        post.Ignore(a => a.Translations);
        post.Ignore(a => a.PublishedTranslations);
        post.Property(a => a.PublishedTitle).HasMaxLength(300);
        post.Property(a => a.Version).IsConcurrencyToken();
        foreach (var user in new[] { nameof(Post.AcceptedBy), nameof(Post.AssigneeId), nameof(Post.ReviewerId), nameof(Post.ScheduledBy), nameof(Post.PublishedBy) })
            post.Property(user).HasMaxLength(DataSchemaConstants.UserIdLength);
        post.Property(a => a.AssignedShiftId).HasMaxLength(DataSchemaConstants.SlugLength);
        post.Property(a => a.TransferredFromShift).HasMaxLength(DataSchemaConstants.SlugLength);
        post.Ignore(a => a.Scopes);
        post.Ignore(a => a.Categories);
        post.HasIndex(a => new { a.State, a.PrimaryCategoryId });
        post.HasIndex(a => a.UnitId);
        post.HasIndex(a => a.OwnerId);
        post.HasIndex(a => a.AssigneeId);
        post.HasIndex(a => a.PublicId).IsUnique();
        post.HasMany(a => a.History).WithOne().HasForeignKey("ArticleId").OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        post.HasMany(a => a.ContentLinks).WithOne().HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.ContentLinks).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_contents");
        post.HasMany(a => a.Slugs).WithOne().HasForeignKey(s => s.PostId).OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.Slugs).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Always loaded: the post's scopes (every rights check) need its categories.
        post.HasMany(a => a.CategoryLinks).WithOne().HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.CategoryLinks).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_categories").AutoInclude();
        post.Ignore(a => a.Contents);
        post.Ignore(a => a.Root);
        // Rows that predate the split get false from the migration (the backfill then splits them); new rows are written true.
    }
}

/// <summary>A post's categories (the primary one included): one row per category, unique per post.</summary>
public sealed class PostCategoryConfiguration : IEntityTypeConfiguration<PostCategory>
{
    public void Configure(EntityTypeBuilder<PostCategory> category)
    {
        category.ToTable("post_categories");
        category.Property(c => c.CategoryId).HasMaxLength(DataSchemaConstants.SlugLength);
        category.HasIndex(c => new { c.PostId, c.CategoryId }).IsUnique();
        category.HasIndex(c => c.CategoryId);
    }
}
