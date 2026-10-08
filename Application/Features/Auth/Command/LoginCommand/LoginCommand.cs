using Application.Features.Auth.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Command.LoginCommand
{
    public record LoginCommand : IRequest<Result<AuthResponse>>
    {
        /// <summary>Username (admin) or mobile number (customer/merchant/delivery).</summary>
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// Optional AppRole enum int from client: 1=Customer, 2=SuperAdmin, 3=Merchant, 4=Delivery.
        /// When null/0/omitted, role is resolved from the user's Identity roles.
        /// </summary>
        public int? Role { get; set; }
    }

    public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IJwtSettings _jwtSettings;
        private readonly DatabaseContext _context;
        private readonly LoginCommandValidator _validator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IImageService _imageService;
        private readonly IPermissionService _permissionService;

        public LoginCommandHandler(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService jwtTokenService,
            IJwtSettings jwtSettings,
            DatabaseContext context,
            LoginCommandValidator validator,
            IDateTimeProvider dateTimeProvider,
            IImageService imageService,
            IPermissionService permissionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenService = jwtTokenService;
            _jwtSettings = jwtSettings;
            _context = context;
            _validator = validator;
            _dateTimeProvider = dateTimeProvider;
            _imageService = imageService;
            _permissionService = permissionService;
        }

        public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (validationResult.IsFailure)
                return Result.Failure<AuthResponse>(validationResult.Error);

            var user = await FindUserAsync(request.UserName, cancellationToken);
            if (user == null)
                return Result.Failure<AuthResponse>("Invalid user name or password");

            if (!user.Active)
                return Result.Failure<AuthResponse>("User account is not active");

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!signInResult.Succeeded)
                return Result.Failure<AuthResponse>("Invalid user name or password");

            AppRole appRole;
            if (request.Role is > 0)
            {
                if (!AppRoleNames.TryFromInt(request.Role.Value, out appRole))
                    return Result.Failure<AuthResponse>("Invalid role");

                if (!await _userManager.IsInRoleAsync(user, AppRoleNames.ToRoleName(appRole)))
                    return Result.Failure<AuthResponse>($"User is not authorized as {AppRoleNames.ToRoleName(appRole)}");
            }
            else
            {
                var identityRoles = await _userManager.GetRolesAsync(user);
                if (!TryResolveAppRole(identityRoles, out appRole))
                    return Result.Failure<AuthResponse>("User has no valid application role");
            }

            var profileCheck = await ValidateProfileStateAsync(user.Id, appRole, cancellationToken);
            if (profileCheck.IsFailure)
                return Result.Failure<AuthResponse>(profileCheck.Error);

            UserAccess? access = null;
            if (appRole is AppRole.SuperAdmin or AppRole.Merchant)
            {
                access = await _permissionService.GetUserAccessAsync(user.Id, appRole, cancellationToken);
                if (!access.IsAllowed)
                    return Result.Failure<AuthResponse>("Your account has no active role. Please contact the administrator.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenService.GenerateToken(user, roles, access);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenExpires = _dateTimeProvider.Now.AddDays(_jwtSettings.RefreshTokenExpirationDays);

            _context.RefreshTokens.Add(RefreshToken.Create(
                user.Id,
                refreshToken,
                refreshTokenExpires,
                user.UserName ?? "System",
                _dateTimeProvider));

            await _context.SaveChangesAsync(cancellationToken);

            var response = new AuthResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Roles = roles.ToList(),
                Role = (int)appRole
            };

            if (access != null)
            {
                response.SubRoleId = access.SubRoleId;
                response.SubRoleName = access.SubRoleName;
                response.SubRoleNameAr = access.SubRoleNameAr;
                response.IsOwner = access.IsOwner;
                response.Permissions = access.Permissions.OrderBy(p => p).ToList();
            }

            await EnrichProfileAsync(response, user.Id, appRole, cancellationToken);
            return Result.Success(response);
        }

        private static bool TryResolveAppRole(IList<string> identityRoles, out AppRole appRole)
        {
            // Prefer web panel roles first when Role is not specified by the client.
            if (identityRoles.Contains(AppRoleNames.SuperAdmin))
            {
                appRole = AppRole.SuperAdmin;
                return true;
            }

            if (identityRoles.Contains(AppRoleNames.Merchant))
            {
                appRole = AppRole.Merchant;
                return true;
            }

            if (identityRoles.Contains(AppRoleNames.Delivery))
            {
                appRole = AppRole.Delivery;
                return true;
            }

            if (identityRoles.Contains(AppRoleNames.Customer))
            {
                appRole = AppRole.Customer;
                return true;
            }

            appRole = default;
            return false;
        }

        private async Task<ApplicationUser?> FindUserAsync(string userNameOrMobile, CancellationToken cancellationToken)
        {
            // "+20 100 123 4567" signs in as "201001234567" (numbers are stored without the "+").
            if (userNameOrMobile.TrimStart().StartsWith('+'))
                userNameOrMobile = Application.Common.MobileNumberPolicy.Normalize(userNameOrMobile);

            var user = await _userManager.FindByNameAsync(userNameOrMobile);
            if (user != null)
                return user;

            return await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == userNameOrMobile || u.Email == userNameOrMobile, cancellationToken);
        }

        private async Task<Result> ValidateProfileStateAsync(int userId, AppRole role, CancellationToken cancellationToken)
        {
            switch (role)
            {
                case AppRole.Customer:
                {
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                    if (customer == null)
                        return Result.Failure("Customer profile not found");
                    if (customer.IsDeleted)
                        return Result.Failure("Customer profile not found");
                    if (customer.State == CustomerState.Blocked)
                        return Result.Failure("Customer account is blocked. Please contact support.");
                    if (customer.State != CustomerState.Active)
                        return Result.Failure("Not Verified");
                    break;
                }
                case AppRole.Merchant:
                {
                    var merchantUser = await _context.MerchantUsers
                        .Include(mu => mu.Merchant)
                        .FirstOrDefaultAsync(mu => mu.UserId == userId, cancellationToken);
                    if (merchantUser == null || merchantUser.IsDeleted)
                        return Result.Failure("Merchant profile not found");
                    if (merchantUser.Merchant.IsDeleted || !merchantUser.Merchant.IsActive)
                        return Result.Failure("Not Verified");
                    if (!merchantUser.IsActive)
                        return Result.Failure("User account is not active");
                    break;
                }
                case AppRole.Delivery:
                {
                    var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
                    if (delivery == null)
                        return Result.Failure("Delivery profile not found");
                    if (delivery.IsDeleted || !delivery.IsActive)
                        return Result.Failure("Not Verified");
                    break;
                }
                case AppRole.SuperAdmin:
                {
                    var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
                    if (employee == null)
                        return Result.Failure("Employee profile not found");
                    break;
                }
            }

            return Result.Success();
        }

        private async Task EnrichProfileAsync(AuthResponse response, int userId, AppRole role, CancellationToken cancellationToken)
        {
            switch (role)
            {
                case AppRole.Customer:
                {
                    var customer = await _context.Customers
                        .Include(c => c.City)
                        .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                    if (customer == null)
                        return;

                    response.CustomerId = customer.CustomerId;
                    response.PersonalImage = _imageService.GetImageUrl(customer.PersonalImage);
                    response.CityId = customer.CityId;
                    response.CityName = customer.City?.Name ?? string.Empty;
                    break;
                }
                case AppRole.Merchant:
                {
                    var merchant = await _context.MerchantUsers
                        .Where(mu => mu.UserId == userId)
                        .Select(mu => mu.Merchant)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (merchant == null)
                        return;
                    response.MerchantId = merchant.MerchantId;
                    response.PersonalImage = _imageService.GetImageUrl(merchant.PersonalImage);
                    break;
                }
                case AppRole.Delivery:
                {
                    var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
                    if (delivery == null)
                        return;
                    response.DeliveryId = delivery.DeliveryId;
                    response.PersonalImage = _imageService.GetImageUrl(delivery.PersonalImage);
                    break;
                }
                case AppRole.SuperAdmin:
                {
                    var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
                    if (employee != null)
                        response.EmployeeId = employee.EmployeeId;
                    break;
                }
            }
        }
    }
}
