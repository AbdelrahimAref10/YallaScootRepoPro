using Application.Features.MerchantStaff.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff.Query.GetMyStaffByIdQuery
{
    public record GetMyStaffByIdQuery(int MerchantUserId) : IRequest<Result<MerchantStaffDto>>;

    public class GetMyStaffByIdQueryHandler : IRequestHandler<GetMyStaffByIdQuery, Result<MerchantStaffDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetMyStaffByIdQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<MerchantStaffDto>> Handle(GetMyStaffByIdQuery request, CancellationToken cancellationToken)
        {
            var merchantId = await MerchantStaffAccess.CurrentMerchantIdAsync(_context, _userSession.UserId, cancellationToken);
            if (merchantId.IsFailure)
                return Result.Failure<MerchantStaffDto>(merchantId.Error);

            var staff = await _context.MerchantUsers
                .Where(mu => mu.MerchantUserId == request.MerchantUserId && mu.MerchantId == merchantId.Value && !mu.IsDeleted)
                .Select(MerchantStaffProjection.ToDto)
                .FirstOrDefaultAsync(cancellationToken);

            return staff == null
                ? Result.Failure<MerchantStaffDto>("Staff member not found")
                : Result.Success(staff);
        }
    }
}
