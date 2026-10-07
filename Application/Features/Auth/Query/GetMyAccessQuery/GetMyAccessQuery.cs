using Application.Features.Auth.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure.Services;
using MediatR;

namespace Application.Features.Auth.Query.GetMyAccessQuery
{
    public record GetMyAccessQuery : IRequest<Result<AccessResponse>>;

    public class GetMyAccessQueryHandler : IRequestHandler<GetMyAccessQuery, Result<AccessResponse>>
    {
        private readonly IUserSession _userSession;
        private readonly IPermissionService _permissionService;

        public GetMyAccessQueryHandler(IUserSession userSession, IPermissionService permissionService)
        {
            _userSession = userSession;
            _permissionService = permissionService;
        }

        public async Task<Result<AccessResponse>> Handle(GetMyAccessQuery request, CancellationToken cancellationToken)
        {
            var scope = PermissionService.ResolvePanelScope(_userSession.Roles);
            if (scope == null)
                return Result.Failure<AccessResponse>("Sub-roles are only available for admin and merchant users");

            var access = await _permissionService.GetUserAccessAsync(_userSession.UserId, scope.Value, cancellationToken);
            if (!access.IsAllowed)
                return Result.Failure<AccessResponse>("Your account has no active role. Please contact the administrator.");

            return Result.Success(new AccessResponse
            {
                Role = (int)scope.Value,
                SubRoleId = access.SubRoleId,
                SubRoleName = access.SubRoleName,
                SubRoleNameAr = access.SubRoleNameAr,
                IsFullAccess = access.IsFullAccess,
                IsOwner = access.IsOwner,
                MerchantId = access.MerchantId,
                Permissions = access.Permissions.OrderBy(p => p).ToList()
            });
        }
    }
}
