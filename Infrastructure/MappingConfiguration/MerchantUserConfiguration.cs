using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class MerchantUserConfiguration : IEntityTypeConfiguration<MerchantUser>
    {
        public void Configure(EntityTypeBuilder<MerchantUser> builder)
        {
            builder.ToTable("VO_MerchantUser");

            builder.HasKey(mu => mu.MerchantUserId);

            builder.Property(mu => mu.MerchantUserId)
                .HasColumnName("MerchantUserId")
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Property(mu => mu.MerchantId)
                .HasColumnName("MerchantId")
                .IsRequired();

            builder.Property(mu => mu.UserId)
                .HasColumnName("UserId")
                .IsRequired();

            builder.Property(mu => mu.SubRoleId)
                .HasColumnName("SubRoleId")
                .IsRequired();

            builder.Property(mu => mu.IsOwner)
                .HasColumnName("IsOwner")
                .IsRequired();

            builder.Property(mu => mu.FullName)
                .HasColumnName("FullName")
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(mu => mu.IsActive)
                .HasColumnName("IsActive")
                .HasDefaultValue(true)
                .IsRequired();

            builder.Property(mu => mu.IsDeleted)
                .HasColumnName("IsDeleted")
                .HasDefaultValue(false)
                .IsRequired();

            builder.Property(mu => mu.CreatedBy)
                .HasColumnName("CreatedBy")
                .HasMaxLength(256);

            builder.Property(mu => mu.CreatedDate)
                .HasColumnName("CreatedDate")
                .IsRequired();

            builder.Property(mu => mu.LastModifiedBy)
                .HasColumnName("LastModifiedBy")
                .HasMaxLength(256);

            builder.Property(mu => mu.LastModifiedDate)
                .HasColumnName("LastModifiedDate")
                .IsRequired();

            builder.HasOne(mu => mu.Merchant)
                .WithMany()
                .HasForeignKey(mu => mu.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(mu => mu.User)
                .WithOne(u => u.MerchantUser)
                .HasForeignKey<MerchantUser>(mu => mu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(mu => mu.SubRole)
                .WithMany()
                .HasForeignKey(mu => mu.SubRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(mu => mu.UserId)
                .IsUnique()
                .HasDatabaseName("IX_MerchantUser_UserId");

            builder.HasIndex(mu => mu.MerchantId)
                .HasDatabaseName("IX_MerchantUser_MerchantId");
        }
    }
}
