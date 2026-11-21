using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("categories");
        category.Property(c => c.Id).HasMaxLength(DataSchemaConstants.SlugLength);
        category.Property(c => c.PressId).HasMaxLength(DataSchemaConstants.SlugLength);
        category.Property(c => c.Name).HasMaxLength(DataSchemaConstants.NameLength);
        category.HasIndex(c => c.PressId);
    }
}
