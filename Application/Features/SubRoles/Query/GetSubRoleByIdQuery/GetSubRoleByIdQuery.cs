using Application.Features.SubRoles.DTOs;
using CSharpFunctionalExtensions;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SubRoles.Query.GetSubRoleByIdQuery
{
    public record GetSubRoleByIdQuery(int SubRoleId) : IRequest<Result<SubRoleDetailDto>>;

    public class GetSubRoleByIdQueryHandler : IRequestHandler<GetSubRoleByIdQuery, Result<SubRoleDetailDto>>
    {
        private readonly DatabaseContext _context;

        public GetSubRoleByIdQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<SubRoleDetailDto>> Handle(GetSubRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var role = await _context.SubRoles.FirstOrDefaultAsync(r => r.SubRoleId == request.SubRoleId, cancellationToken);
            if (role == null)
                return Result.Failure<SubRoleDetailDto>("Role not found");

            var permissions = role.IsFullAccess
                ? await _context.Permissions
                    .Where(p => p.Scope == role.Scope && p.IsActive)
                    .Select(p => p.PermissionName)
                    .ToListAsync(cancellationToken)
                : await _context.SubRolePermissions
                    .Where(rp => rp.SubRoleId == role.SubRoleId && rp.Permission.IsActive)
                    .Select(rp => rp.Permission.PermissionName)
                    .ToListAsync(cancellationToken);

            return Result.Success(new SubRoleDetailDto
            {
                SubRoleId = role.SubRoleId,
                Name = role.Name,
                NameAr = role.NameAr,
                Scope = (int)role.Scope,
                IsSystem = role.IsSystem,
                IsFullAccess = role.IsFullAccess,
                IsActive = role.IsActive,
                PermissionsCount = permissions.Count,
                Permissions = permissions.OrderBy(p => p).ToList()
            });
        }
    }
}
