using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class SettlementVoucherConfiguration : IEntityTypeConfiguration<SettlementVoucher>
    {
        public void Configure(EntityTypeBuilder<SettlementVoucher> builder)
        {
            builder.ToTable("VO_SettlementVoucher");
            builder.HasKey(v => v.SettlementVoucherId);

            builder.Property(v => v.SettlementVoucherId).HasColumnName("SettlementVoucherId").ValueGeneratedOnAdd();
            builder.Property(v => v.VoucherNo).HasColumnName("VoucherNo").HasMaxLength(20).IsRequired();
            builder.Property(v => v.PartyType).HasColumnName("PartyType").HasConversion<int>().IsRequired();
            builder.Property(v => v.PartyId).HasColumnName("PartyId").IsRequired();
            builder.Property(v => v.Direction).HasColumnName("Direction").HasConversion<int>().IsRequired();
            builder.Property(v => v.Amount).HasColumnName("Amount").HasPrecision(18, 2).IsRequired();
            builder.Property(v => v.Note).HasColumnName("Note").HasMaxLength(500);
            builder.Property(v => v.RequestId).HasColumnName("RequestId").IsRequired();
            builder.Property(v => v.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(256);
            builder.Property(v => v.CreatedDate).HasColumnName("CreatedDate").IsRequired();
            builder.Property(v => v.LastModifiedBy).HasColumnName("LastModifiedBy").HasMaxLength(256);
            builder.Property(v => v.LastModifiedDate).HasColumnName("LastModifiedDate").IsRequired();

            builder.HasMany(v => v.Allocations)
                .WithOne(a => a.SettlementVoucher)
                .HasForeignKey(a => a.SettlementVoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(v => v.RequestId).IsUnique().HasDatabaseName("IX_SettlementVoucher_RequestId");
            builder.HasIndex(v => new { v.PartyType, v.PartyId, v.CreatedDate }).HasDatabaseName("IX_SettlementVoucher_Party");
        }
    }

    public class SettlementAllocationConfiguration : IEntityTypeConfiguration<SettlementAllocation>
    {
        public void Configure(EntityTypeBuilder<SettlementAllocation> builder)
        {
            builder.ToTable("VO_SettlementAllocation");
            builder.HasKey(a => a.SettlementAllocationId);

            builder.Property(a => a.SettlementAllocationId).HasColumnName("SettlementAllocationId").ValueGeneratedOnAdd();
            builder.Property(a => a.SettlementVoucherId).HasColumnName("SettlementVoucherId").IsRequired();
            builder.Property(a => a.OrderId).HasColumnName("OrderId");
            builder.Property(a => a.Kind).HasColumnName("Kind").HasConversion<int>().IsRequired();
            builder.Property(a => a.Amount).HasColumnName("Amount").HasPrecision(18, 2).IsRequired();

            builder.HasOne(a => a.Order)
                .WithMany()
                .HasForeignKey(a => a.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(a => a.OrderId).HasDatabaseName("IX_SettlementAllocation_OrderId");
        }
    }
}
