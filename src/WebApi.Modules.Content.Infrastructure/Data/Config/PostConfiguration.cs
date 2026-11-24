using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
        post.Property(a => a.CategoryId).HasMaxLength(DataSchemaConstants.SlugLength);
        post.Property(a => a.Title).HasMaxLength(300);
        post.Property(a => a.OwnerId).HasMaxLength(DataSchemaConstants.UserIdLength);
        post.Property(a => a.State).HasConversion<string>().HasMaxLength(32);
        post.Property(a => a.PostKind).HasColumnName("Type").HasConversion<string>().HasMaxLength(32).HasDefaultValue(PostKind.Article);
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
        post.HasIndex(a => new { a.State, a.CategoryId });
        post.HasIndex(a => a.OwnerId);
        post.HasIndex(a => a.AssigneeId);
        post.HasIndex(a => a.PublicId).IsUnique();
        post.HasMany(a => a.History).WithOne().HasForeignKey("ArticleId").OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        post.HasMany(a => a.ContentLinks).WithOne().HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.ContentLinks).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_contents");
        post.HasMany(a => a.Slugs).WithOne().HasForeignKey(s => s.PostId).OnDelete(DeleteBehavior.Cascade);
        post.Navigation(a => a.Slugs).UsePropertyAccessMode(PropertyAccessMode.Field);
        post.Ignore(a => a.Contents);
        post.Ignore(a => a.Root);
        // Rows that predate the split get false from the migration (the backfill then splits them); new rows are written true.
    }
}
