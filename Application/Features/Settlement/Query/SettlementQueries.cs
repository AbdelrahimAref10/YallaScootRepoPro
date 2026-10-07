using Application.Common;
using Application.Features.Settlement.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Settlement.Query
{
    /// <summary>Open balance of a merchant / delivery and the orders a voucher would settle.</summary>
    public record GetSettlementSummaryQuery(LedgerPartyType PartyType, int PartyId) : IRequest<Result<SettlementSummaryDto>>;

    public record GetSettlementVouchersQuery : IRequest<Result<PagedResult<SettlementVoucherDto>>>
    {
        public LedgerPartyType? PartyType { get; set; }
        public int? PartyId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public record GetSettlementVoucherByIdQuery(int SettlementVoucherId) : IRequest<Result<SettlementVoucherDetailDto>>;

    public class SettlementQueriesHandler :
        IRequestHandler<GetSettlementSummaryQuery, Result<SettlementSummaryDto>>,
        IRequestHandler<GetSettlementVouchersQuery, Result<PagedResult<SettlementVoucherDto>>>,
        IRequestHandler<GetSettlementVoucherByIdQuery, Result<SettlementVoucherDetailDto>>
    {
        private readonly DatabaseContext _context;

        public SettlementQueriesHandler(DatabaseContext context)
        {
            _context = context;
        }

        public Task<Result<SettlementSummaryDto>> Handle(GetSettlementSummaryQuery request, CancellationToken cancellationToken) =>
            SettlementBalances.GetAsync(_context, request.PartyType, request.PartyId, cancellationToken);

        public async Task<Result<PagedResult<SettlementVoucherDto>>> Handle(GetSettlementVouchersQuery request, CancellationToken cancellationToken)
        {
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var query = _context.SettlementVouchers.AsNoTracking();
            if (request.PartyType.HasValue)
                query = query.Where(v => v.PartyType == request.PartyType.Value);
            if (request.PartyId.HasValue)
                query = query.Where(v => v.PartyId == request.PartyId.Value);

            var total = await query.CountAsync(cancellationToken);
            var items = await SettlementVoucherReader
                .Project(_context, query.OrderByDescending(v => v.SettlementVoucherId).Skip((pageNumber - 1) * pageSize).Take(pageSize))
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<SettlementVoucherDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<SettlementVoucherDetailDto>> Handle(GetSettlementVoucherByIdQuery request, CancellationToken cancellationToken)
        {
            var voucher = await SettlementVoucherReader.GetDetailAsync(_context, v => v.SettlementVoucherId == request.SettlementVoucherId, cancellationToken);
            return voucher == null
                ? Result.Failure<SettlementVoucherDetailDto>("Voucher not found")
                : Result.Success(voucher);
        }
    }
}
