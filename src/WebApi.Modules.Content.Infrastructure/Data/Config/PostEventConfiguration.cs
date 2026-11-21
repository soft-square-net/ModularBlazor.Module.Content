using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

public sealed class PostEventConfiguration : IEntityTypeConfiguration<PostEvent>
{
    public void Configure(EntityTypeBuilder<PostEvent> e)
    {
        e.ToTable("article_history");
        e.Property(x => x.Actor).HasMaxLength(DataSchemaConstants.UserIdLength);
        e.Property(x => x.Action).HasMaxLength(64);
        e.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Detail).HasMaxLength(500);
    }
}
