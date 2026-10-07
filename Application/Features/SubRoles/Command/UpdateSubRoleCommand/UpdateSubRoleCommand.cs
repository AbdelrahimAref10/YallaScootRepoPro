using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SubRoles.Command.UpdateSubRoleCommand
{
    public record UpdateSubRoleCommand : IRequest<Result<bool>>
    {
        public int SubRoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Permission names, e.g. "Admin.Orders.View". Replaces the current set.</summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public class UpdateSubRoleCommandHandler : IRequestHandler<UpdateSubRoleCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public UpdateSubRoleCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(UpdateSubRoleCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result.Failure<bool>("Role name is required");

            var role = await _context.SubRoles
                .AsTracking()
                .Include(r => r.SubRolePermissions)
                .FirstOrDefaultAsync(r => r.SubRoleId == request.SubRoleId, cancellationToken);
            if (role == null)
                return Result.Failure<bool>("Role not found");
            if (role.IsSystem)
                return Result.Failure<bool>("System roles cannot be changed");

            if (await SubRoleRules.NameTakenAsync(_context, role.Scope, request.Name, role.SubRoleId, cancellationToken))
                return Result.Failure<bool>("A role with this name already exists");

            var permissionIds = await SubRoleRules.ResolvePermissionIdsAsync(_context, role.Scope, request.Permissions, cancellationToken);
            if (permissionIds.IsFailure)
                return Result.Failure<bool>(permissionIds.Error);

            role.Update(request.Name, request.NameAr, request.IsActive, _userSession.UserName);
            role.SetPermissions(permissionIds.Value, _userSession.UserName);

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(true)
                : Result.Failure<bool>($"Failed to update role: {saveResult.ErrorMessage}");
        }
    }
}
