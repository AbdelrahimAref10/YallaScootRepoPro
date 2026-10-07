using Application.Features.MerchantStaff.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff.Query.GetMerchantSubRolesLookupQuery
{
    /// <summary>Sub-roles a merchant owner can give to staff (defined by the admin).</summary>
    public record GetMerchantSubRolesLookupQuery : IRequest<Result<List<SubRoleLookupDto>>>;

    public class GetMerchantSubRolesLookupQueryHandler : IRequestHandler<GetMerchantSubRolesLookupQuery, Result<List<SubRoleLookupDto>>>
    {
        private readonly DatabaseContext _context;

        public GetMerchantSubRolesLookupQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SubRoleLookupDto>>> Handle(GetMerchantSubRolesLookupQuery request, CancellationToken cancellationToken)
        {
            var roles = await _context.SubRoles
                .Where(r => r.Scope == AppRole.Merchant && r.IsActive && !r.IsSystem)
                .OrderBy(r => r.Name)
                .Select(r => new SubRoleLookupDto { SubRoleId = r.SubRoleId, Name = r.Name, NameAr = r.NameAr })
                .ToListAsync(cancellationToken);

            return Result.Success(roles);
        }
    }
}
