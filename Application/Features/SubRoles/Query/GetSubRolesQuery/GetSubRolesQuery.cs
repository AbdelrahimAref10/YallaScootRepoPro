using Application.Features.SubRoles.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SubRoles.Query.GetSubRolesQuery
{
    public record GetSubRolesQuery : IRequest<Result<List<SubRoleDto>>>
    {
        /// <summary>Optional AppRole int filter: SuperAdmin=2, Merchant=3.</summary>
        public int? Scope { get; set; }
        public bool? IsActive { get; set; }
    }

    public class GetSubRolesQueryHandler : IRequestHandler<GetSubRolesQuery, Result<List<SubRoleDto>>>
    {
        private readonly DatabaseContext _context;

        public GetSubRolesQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SubRoleDto>>> Handle(GetSubRolesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.SubRoles.AsQueryable();
            if (request.Scope.HasValue)
            {
                var scope = SubRoleRules.ParseScope(request.Scope.Value);
                if (scope.IsFailure)
                    return Result.Failure<List<SubRoleDto>>(scope.Error);
                query = query.Where(r => r.Scope == scope.Value);
            }
            if (request.IsActive.HasValue)
                query = query.Where(r => r.IsActive == request.IsActive.Value);

            var roles = await query
                .OrderBy(r => r.Scope)
                .ThenByDescending(r => r.IsSystem)
                .ThenBy(r => r.Name)
                .Select(r => new SubRoleDto
                {
                    SubRoleId = r.SubRoleId,
                    Name = r.Name,
                    NameAr = r.NameAr,
                    Scope = (int)r.Scope,
                    IsSystem = r.IsSystem,
                    IsFullAccess = r.IsFullAccess,
                    IsActive = r.IsActive,
                    UsersCount = r.Scope == AppRole.SuperAdmin
                        ? _context.Employees.Count(e => e.SubRoleId == r.SubRoleId)
                        : _context.MerchantUsers.Count(mu => mu.SubRoleId == r.SubRoleId && !mu.IsDeleted),
                    PermissionsCount = r.IsFullAccess
                        ? _context.Permissions.Count(p => p.Scope == r.Scope && p.IsActive)
                        : r.SubRolePermissions.Count(rp => rp.Permission.IsActive)
                })
                .ToListAsync(cancellationToken);

            return Result.Success(roles);
        }
    }
}
