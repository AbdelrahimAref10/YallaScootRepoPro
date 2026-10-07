using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class SubRoleConfiguration : IEntityTypeConfiguration<SubRole>
    {
        public void Configure(EntityTypeBuilder<SubRole> builder)
        {
            builder.ToTable("VO_SubRole");

            builder.HasKey(r => r.SubRoleId);

            builder.Property(r => r.SubRoleId)
                .HasColumnName("SubRoleId")
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Property(r => r.Name)
                .HasColumnName("Name")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(r => r.NameAr)
                .HasColumnName("NameAr")
                .HasMaxLength(100);

            builder.Property(r => r.Scope)
                .HasColumnName("Scope")
                .HasConversion<int>()
                .IsRequired();

            builder.Property(r => r.IsSystem)
                .HasColumnName("IsSystem")
                .IsRequired();

            builder.Property(r => r.IsFullAccess)
                .HasColumnName("IsFullAccess")
                .IsRequired();

            builder.Property(r => r.IsActive)
                .HasColumnName("IsActive")
                .HasDefaultValue(true)
                .IsRequired();

            builder.Property(r => r.CreatedBy)
                .HasColumnName("CreatedBy")
                .HasMaxLength(256);

            builder.Property(r => r.CreatedDate)
                .HasColumnName("CreatedDate")
                .IsRequired();

            builder.Property(r => r.LastModifiedBy)
                .HasColumnName("LastModifiedBy")
                .HasMaxLength(256);

            builder.Property(r => r.LastModifiedDate)
                .HasColumnName("LastModifiedDate")
                .IsRequired();

            builder.HasMany(r => r.SubRolePermissions)
                .WithOne(p => p.SubRole)
                .HasForeignKey(p => p.SubRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => new { r.Scope, r.Name })
                .HasDatabaseName("IX_SubRole_Scope_Name")
                .IsUnique();
        }
    }
}
