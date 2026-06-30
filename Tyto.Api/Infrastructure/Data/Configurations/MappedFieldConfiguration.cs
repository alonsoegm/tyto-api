using Tyto.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class MappedFieldConfiguration : IEntityTypeConfiguration<MappedField>
{
    public void Configure(EntityTypeBuilder<MappedField> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FieldName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayLabel).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ExtractionHint).HasMaxLength(1000);
        builder.Property(x => x.DefaultValue).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        builder.Property(x => x.FieldType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.RequirementLevel).HasConversion<string>().HasMaxLength(50);

        builder.HasOne(x => x.Configuration)
            .WithMany(x => x.MappedFields)
            .HasForeignKey(x => x.ConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ParentField)
            .WithMany(x => x.ChildFields)
            .HasForeignKey(x => x.ParentFieldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
