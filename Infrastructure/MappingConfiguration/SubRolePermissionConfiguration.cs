using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class SubRolePermissionConfiguration : IEntityTypeConfiguration<SubRolePermission>
    {
        public void Configure(EntityTypeBuilder<SubRolePermission> builder)
        {
            builder.ToTable("VO_SubRolePermission");

            builder.HasKey(rp => new { rp.SubRoleId, rp.PermissionId });

            builder.Property(rp => rp.SubRoleId)
                .HasColumnName("SubRoleId")
                .IsRequired();

            builder.Property(rp => rp.PermissionId)
                .HasColumnName("PermissionId")
                .IsRequired();

            builder.Property(rp => rp.CreatedBy)
                .HasColumnName("CreatedBy")
                .HasMaxLength(256);

            builder.Property(rp => rp.CreatedDate)
                .HasColumnName("CreatedDate")
                .IsRequired();

            builder.Property(rp => rp.LastModifiedBy)
                .HasColumnName("LastModifiedBy")
                .HasMaxLength(256);

            builder.Property(rp => rp.LastModifiedDate)
                .HasColumnName("LastModifiedDate")
                .IsRequired();

            builder.HasIndex(rp => rp.PermissionId)
                .HasDatabaseName("IX_SubRolePermission_PermissionId");
        }
    }
}
