using Application.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff.Command.UpdateMerchantStaffCommand
{
    public record UpdateMerchantStaffCommand : IRequest<Result<bool>>
    {
        public int MerchantUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int SubRoleId { get; set; }
        /// <summary>Optional; leave empty to keep the current password.</summary>
        public string? Password { get; set; }
    }

    public class UpdateMerchantStaffCommandHandler : IRequestHandler<UpdateMerchantStaffCommand, Result<bool>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public UpdateMerchantStaffCommandHandler(UserManager<ApplicationUser> userManager, DatabaseContext context, IUserSession userSession)
        {
            _userManager = userManager;
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(UpdateMerchantStaffCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return Result.Failure<bool>("Full name is required");
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Result.Failure<bool>("Phone number is required");
            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Failure<bool>("Email is required");
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var passwordCheck = PasswordPolicy.Validate(request.Password);
                if (passwordCheck.IsFailure)
                    return Result.Failure<bool>(passwordCheck.Error);
            }

            var staffResult = await MerchantStaffAccess.EditableStaffAsync(_context, _userSession.UserId, request.MerchantUserId, cancellationToken);
            if (staffResult.IsFailure)
                return Result.Failure<bool>(staffResult.Error);
            var staff = staffResult.Value;

            if (!await MerchantStaffAccess.IsAssignableSubRoleAsync(_context, request.SubRoleId, cancellationToken))
                return Result.Failure<bool>("Invalid role");

            var email = request.Email.Trim();
            var phone = request.PhoneNumber.Trim();
            var byEmail = await _userManager.FindByEmailAsync(email);
            if (byEmail != null && byEmail.Id != staff.UserId)
                return Result.Failure<bool>("Email is already taken");
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != staff.UserId, cancellationToken))
                return Result.Failure<bool>("Phone number is already taken");

            var user = staff.User;
            user.Email = email;
            user.PhoneNumber = phone;
            user.LastModifiedBy = _userSession.UserName;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return Result.Failure<bool>($"Failed to update user: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.Password);
                if (!passwordResult.Succeeded)
                    return Result.Failure<bool>($"Failed to update password: {string.Join(", ", passwordResult.Errors.Select(e => e.Description))}");
            }

            staff.Update(request.FullName, request.SubRoleId, _userSession.UserName);
            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            return saveResult.IsSuccess
                ? Result.Success(true)
                : Result.Failure<bool>($"Failed to save staff member: {saveResult.ErrorMessage}");
        }
    }
}
