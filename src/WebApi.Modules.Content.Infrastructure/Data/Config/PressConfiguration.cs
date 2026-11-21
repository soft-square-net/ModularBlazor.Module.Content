using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

public sealed class PressConfiguration : IEntityTypeConfiguration<Press>
{
    public void Configure(EntityTypeBuilder<Press> press)
    {
        press.ToTable("presses");
        press.Property(p => p.Id).HasMaxLength(DataSchemaConstants.SlugLength);
        press.Property(p => p.Name).HasMaxLength(DataSchemaConstants.NameLength);
    }
}
