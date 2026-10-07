using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;

namespace Application.Features.MerchantStaff.Command.SetMerchantStaffActiveCommand
{
    public record SetMerchantStaffActiveCommand(int MerchantUserId, bool IsActive) : IRequest<Result<bool>>;

    public class SetMerchantStaffActiveCommandHandler : IRequestHandler<SetMerchantStaffActiveCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public SetMerchantStaffActiveCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(SetMerchantStaffActiveCommand request, CancellationToken cancellationToken)
        {
            var staff = await MerchantStaffAccess.EditableStaffAsync(_context, _userSession.UserId, request.MerchantUserId, cancellationToken);
            if (staff.IsFailure)
                return Result.Failure<bool>(staff.Error);

            staff.Value.SetActive(request.IsActive, _userSession.UserName);
            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(true)
                : Result.Failure<bool>($"Failed to save staff member: {saveResult.ErrorMessage}");
        }
    }
}
