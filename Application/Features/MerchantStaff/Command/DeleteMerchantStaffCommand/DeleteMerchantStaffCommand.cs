using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;

namespace Application.Features.MerchantStaff.Command.DeleteMerchantStaffCommand
{
    /// <summary>Soft-deletes the staff member and disables their login.</summary>
    public record DeleteMerchantStaffCommand(int MerchantUserId) : IRequest<Result<bool>>;

    public class DeleteMerchantStaffCommandHandler : IRequestHandler<DeleteMerchantStaffCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IDateTimeProvider _dateTimeProvider;

        public DeleteMerchantStaffCommandHandler(DatabaseContext context, IUserSession userSession, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _userSession = userSession;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<bool>> Handle(DeleteMerchantStaffCommand request, CancellationToken cancellationToken)
        {
            var staff = await MerchantStaffAccess.EditableStaffAsync(_context, _userSession.UserId, request.MerchantUserId, cancellationToken);
            if (staff.IsFailure)
                return Result.Failure<bool>(staff.Error);

            staff.Value.SoftDelete(_userSession.UserName);
            staff.Value.User.Active = false;
            staff.Value.User.LastModifiedBy = _userSession.UserName;
            staff.Value.User.LastModifiedDate = _dateTimeProvider.Now;

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(true)
                : Result.Failure<bool>($"Failed to delete staff member: {saveResult.ErrorMessage}");
        }
    }
}
