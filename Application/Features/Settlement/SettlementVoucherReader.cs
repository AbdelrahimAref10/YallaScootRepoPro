using Application.Features.Settlement.DTOs;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Application.Features.Settlement
{
    internal static class SettlementVoucherReader
    {
        public static IQueryable<SettlementVoucherDto> Project(DatabaseContext context, IQueryable<SettlementVoucher> vouchers) =>
            vouchers.Select(v => new SettlementVoucherDto
            {
                SettlementVoucherId = v.SettlementVoucherId,
                VoucherNo = v.VoucherNo,
                PartyType = v.PartyType,
                PartyId = v.PartyId,
                PartyName = v.PartyType == LedgerPartyType.Delivery
                    ? context.Deliveries.Where(d => d.DeliveryId == v.PartyId).Select(d => d.FullName).FirstOrDefault() ?? string.Empty
                    : context.Merchants.Where(m => m.MerchantId == v.PartyId).Select(m => m.FullName).FirstOrDefault() ?? string.Empty,
                Direction = v.Direction,
                Amount = v.Amount,
                Note = v.Note,
                CreatedBy = v.CreatedBy,
                CreatedDate = v.CreatedDate
            });

        public static async Task<SettlementVoucherDetailDto?> GetDetailAsync(
            DatabaseContext context, Expression<Func<SettlementVoucher, bool>> filter, CancellationToken cancellationToken)
        {
            var voucher = await Project(context, context.SettlementVouchers.AsNoTracking().Where(filter)).FirstOrDefaultAsync(cancellationToken);
            if (voucher == null)
                return null;

            var allocations = await context.SettlementAllocations
                .AsNoTracking()
                .Where(a => a.SettlementVoucherId == voucher.SettlementVoucherId)
                .OrderBy(a => a.SettlementAllocationId)
                .Select(a => new SettlementAllocationDto
                {
                    OrderId = a.OrderId,
                    OrderCode = a.Order != null ? a.Order.OrderCode : null,
                    Kind = a.Kind,
                    Amount = a.Amount
                })
                .ToListAsync(cancellationToken);

            return new SettlementVoucherDetailDto
            {
                SettlementVoucherId = voucher.SettlementVoucherId,
                VoucherNo = voucher.VoucherNo,
                PartyType = voucher.PartyType,
                PartyId = voucher.PartyId,
                PartyName = voucher.PartyName,
                Direction = voucher.Direction,
                Amount = voucher.Amount,
                Note = voucher.Note,
                CreatedBy = voucher.CreatedBy,
                CreatedDate = voucher.CreatedDate,
                Allocations = allocations
            };
        }
    }
}
