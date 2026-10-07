using Application.Features.SubRoles.DTOs;
using CSharpFunctionalExtensions;
using Domain.Authorization;
using MediatR;

namespace Application.Features.SubRoles.Query.GetPermissionsQuery
{
    /// <summary>Permission matrix (modules × actions) of a scope, in catalog order.</summary>
    public record GetPermissionsQuery(int Scope) : IRequest<Result<List<PermissionModuleDto>>>;

    public class GetPermissionsQueryHandler : IRequestHandler<GetPermissionsQuery, Result<List<PermissionModuleDto>>>
    {
        public Task<Result<List<PermissionModuleDto>>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
        {
            var scope = SubRoleRules.ParseScope(request.Scope);
            if (scope.IsFailure)
                return Task.FromResult(Result.Failure<List<PermissionModuleDto>>(scope.Error));

            var modules = PermissionCatalog.ForScope(scope.Value)
                .GroupBy(p => p.Module)
                .Select(g => new PermissionModuleDto
                {
                    Module = g.Key,
                    Permissions = g
                        .OrderBy(p => Array.IndexOf(PermissionCatalog.Actions, p.Action))
                        .Select(p => new PermissionItemDto { Name = p.Name, Action = p.Action })
                        .ToList()
                })
                .ToList();

            return Task.FromResult(Result.Success(modules));
        }
    }
}
