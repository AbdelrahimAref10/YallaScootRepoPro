using Application.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff.Command.CreateMerchantStaffCommand
{
    public record CreateMerchantStaffCommand : IRequest<Result<int>>
    {
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int SubRoleId { get; set; }
    }

    public class CreateMerchantStaffCommandHandler : IRequestHandler<CreateMerchantStaffCommand, Result<int>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CreateMerchantStaffCommandHandler(
            UserManager<ApplicationUser> userManager,
            DatabaseContext context,
            IUserSession userSession,
            IDateTimeProvider dateTimeProvider)
        {
            _userManager = userManager;
            _context = context;
            _userSession = userSession;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<int>> Handle(CreateMerchantStaffCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return Result.Failure<int>("Full name is required");
            if (string.IsNullOrWhiteSpace(request.UserName))
                return Result.Failure<int>("User name is required");
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Result.Failure<int>("Phone number is required");
            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Failure<int>("Email is required");

            var passwordCheck = PasswordPolicy.Validate(request.Password);
            if (passwordCheck.IsFailure)
                return Result.Failure<int>(passwordCheck.Error);

            var merchantId = await MerchantStaffAccess.CurrentMerchantIdAsync(_context, _userSession.UserId, cancellationToken);
            if (merchantId.IsFailure)
                return Result.Failure<int>(merchantId.Error);

            if (!await MerchantStaffAccess.IsAssignableSubRoleAsync(_context, request.SubRoleId, cancellationToken))
                return Result.Failure<int>("Invalid role");

            if (await _userManager.FindByNameAsync(request.UserName.Trim()) != null)
                return Result.Failure<int>("User with this username already exists");
            if (await _userManager.FindByEmailAsync(request.Email.Trim()) != null)
                return Result.Failure<int>("User with this email already exists");
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken))
                return Result.Failure<int>("User with this phone number already exists");

            var user = new ApplicationUser
            {
                UserName = request.UserName.Trim(),
                Email = request.Email.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                Active = true,
                CreatedBy = _userSession.UserName,
                CreatedDate = _dateTimeProvider.Now,
                LastModifiedDate = _dateTimeProvider.Now
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
                return Result.Failure<int>($"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoleNames.Merchant);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return Result.Failure<int>($"Failed to assign role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            var staff = MerchantUser.CreateStaff(merchantId.Value, user.Id, request.SubRoleId, request.FullName, _userSession.UserName);
            _context.MerchantUsers.Add(staff);

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            if (!saveResult.IsSuccess)
            {
                await _userManager.DeleteAsync(user);
                return Result.Failure<int>($"Failed to save staff member: {saveResult.ErrorMessage}");
            }

            return Result.Success(staff.MerchantUserId);
        }
    }
}
