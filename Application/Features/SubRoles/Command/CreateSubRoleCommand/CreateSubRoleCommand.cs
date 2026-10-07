using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Models;
using Infrastructure;
using MediatR;

namespace Application.Features.SubRoles.Command.CreateSubRoleCommand
{
    public record CreateSubRoleCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        /// <summary>AppRole enum int: SuperAdmin=2, Merchant=3.</summary>
        public int Scope { get; set; }
        /// <summary>Permission names, e.g. "Admin.Orders.View".</summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public class CreateSubRoleCommandHandler : IRequestHandler<CreateSubRoleCommand, Result<int>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public CreateSubRoleCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<int>> Handle(CreateSubRoleCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result.Failure<int>("Role name is required");

            var scope = SubRoleRules.ParseScope(request.Scope);
            if (scope.IsFailure)
                return Result.Failure<int>(scope.Error);

            if (await SubRoleRules.NameTakenAsync(_context, scope.Value, request.Name, null, cancellationToken))
                return Result.Failure<int>("A role with this name already exists");

            var permissionIds = await SubRoleRules.ResolvePermissionIdsAsync(_context, scope.Value, request.Permissions, cancellationToken);
            if (permissionIds.IsFailure)
                return Result.Failure<int>(permissionIds.Error);

            var role = SubRole.Create(request.Name, request.NameAr, scope.Value, createdBy: _userSession.UserName);
            role.SetPermissions(permissionIds.Value, _userSession.UserName);
            _context.SubRoles.Add(role);

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(role.SubRoleId)
                : Result.Failure<int>($"Failed to create role: {saveResult.ErrorMessage}");
        }
    }
}
