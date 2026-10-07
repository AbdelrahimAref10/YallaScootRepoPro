using Application.Features.MerchantStaff.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff.Query.GetMyStaffQuery
{
    /// <summary>Owner and staff accounts of the signed-in merchant.</summary>
    public record GetMyStaffQuery : IRequest<Result<List<MerchantStaffDto>>>;

    public class GetMyStaffQueryHandler : IRequestHandler<GetMyStaffQuery, Result<List<MerchantStaffDto>>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetMyStaffQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<List<MerchantStaffDto>>> Handle(GetMyStaffQuery request, CancellationToken cancellationToken)
        {
            var merchantId = await MerchantStaffAccess.CurrentMerchantIdAsync(_context, _userSession.UserId, cancellationToken);
            if (merchantId.IsFailure)
                return Result.Failure<List<MerchantStaffDto>>(merchantId.Error);

            var staff = await _context.MerchantUsers
                .Where(mu => mu.MerchantId == merchantId.Value && !mu.IsDeleted)
                .OrderByDescending(mu => mu.IsOwner)
                .ThenBy(mu => mu.FullName)
                .Select(MerchantStaffProjection.ToDto)
                .ToListAsync(cancellationToken);

            return Result.Success(staff);
        }
    }
}
