using CSharpFunctionalExtensions;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SubRoles.Command.DeleteSubRoleCommand
{
    public record DeleteSubRoleCommand(int SubRoleId) : IRequest<Result<bool>>;

    public class DeleteSubRoleCommandHandler : IRequestHandler<DeleteSubRoleCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;

        public DeleteSubRoleCommandHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<bool>> Handle(DeleteSubRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _context.SubRoles
                .AsTracking()
                .FirstOrDefaultAsync(r => r.SubRoleId == request.SubRoleId, cancellationToken);
            if (role == null)
                return Result.Failure<bool>("Role not found");
            if (role.IsSystem)
                return Result.Failure<bool>("System roles cannot be deleted");

            var inUse = await _context.Employees.AnyAsync(e => e.SubRoleId == role.SubRoleId, cancellationToken)
                        || await _context.MerchantUsers.AnyAsync(mu => mu.SubRoleId == role.SubRoleId && !mu.IsDeleted, cancellationToken);
            if (inUse)
                return Result.Failure<bool>("This role is assigned to users. Move them to another role first.");

            // Deleted merchant staff still point at the role, so it is kept but switched off.
            if (await _context.MerchantUsers.AnyAsync(mu => mu.SubRoleId == role.SubRoleId, cancellationToken))
                role.Update(role.Name, role.NameAr, isActive: false);
            else
                _context.SubRoles.Remove(role);

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(true)
                : Result.Failure<bool>($"Failed to delete role: {saveResult.ErrorMessage}");
        }
    }
}
