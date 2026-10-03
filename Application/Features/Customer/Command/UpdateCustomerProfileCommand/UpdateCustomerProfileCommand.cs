using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Command.UpdateCustomerProfileCommand
{
    public record UpdateCustomerProfileCommand : IRequest<Result<bool>>
    {
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int ZoneId { get; set; }
        public string? Email { get; set; }
        public string? PersonalImage { get; set; }
        public string? CommercialRegisterImage { get; set; }
        public string? Password { get; set; }
    }

    public class UpdateCustomerProfileCommandHandler : IRequestHandler<UpdateCustomerProfileCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IImageService _imageService;
        private readonly UserManager<ApplicationUser> _userManager;

        public UpdateCustomerProfileCommandHandler(
            DatabaseContext context,
            IUserSession userSession,
            IImageService imageService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userSession = userSession;
            _imageService = imageService;
            _userManager = userManager;
        }

        public async Task<Result<bool>> Handle(UpdateCustomerProfileCommand request, CancellationToken cancellationToken)
        {
            if (_userSession.UserId <= 0)
                return Result.Failure<bool>("Customer not authenticated");

            var customer = await _context.Customers
                .AsTracking()
                .FirstOrDefaultAsync(c => c.UserId == _userSession.UserId, cancellationToken);

            if (customer == null)
                return Result.Failure<bool>("Customer not found");

            var validator = new UpdateCustomerProfileCommandValidator(_context);
            var validationResult = await validator.ValidateAsync(request, customer.CustomerId, cancellationToken);
            if (validationResult.IsFailure)
                return Result.Failure<bool>(validationResult.Error);

            // Check the new password against Identity's validators before anything is changed
            // (images included), so a rejected password fails the request cleanly.
            ApplicationUser? passwordUser = null;
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                passwordUser = await _userManager.FindByIdAsync(_userSession.UserId.ToString());
                if (passwordUser == null)
                    return Result.Failure<bool>("User account not found");

                var passwordCheck = await Application.Common.PasswordPolicy.ValidateWithIdentityAsync(_userManager, passwordUser, request.Password);
                if (passwordCheck.IsFailure)
                    return Result.Failure<bool>($"Failed to update password: {passwordCheck.Error}");
            }

            string? oldPersonalImage = customer.PersonalImage;
            string? oldCommercialRegisterImage = customer.CommercialRegisterImage;

            string? personalImageUrl = customer.PersonalImage;
            if (!string.IsNullOrWhiteSpace(request.PersonalImage) && _imageService.IsBase64String(request.PersonalImage))
            {
                personalImageUrl = _imageService.SaveBase64Image(request.PersonalImage, "customers");
                if (!string.IsNullOrWhiteSpace(oldPersonalImage) && oldPersonalImage != personalImageUrl)
                    _imageService.DeleteImage(oldPersonalImage);
            }

            string? commercialRegisterImageUrl = customer.CommercialRegisterImage;
            if (!string.IsNullOrWhiteSpace(request.CommercialRegisterImage) && _imageService.IsBase64String(request.CommercialRegisterImage))
            {
                commercialRegisterImageUrl = _imageService.SaveBase64Image(request.CommercialRegisterImage, "customers");
                if (!string.IsNullOrWhiteSpace(oldCommercialRegisterImage) && oldCommercialRegisterImage != commercialRegisterImageUrl)
                    _imageService.DeleteImage(oldCommercialRegisterImage);
            }

            customer.UpdateProfile(
                request.FullName,
                request.Gender,
                request.CityId,
                request.ZoneId,
                request.Email,
                personalImageUrl,
                commercialRegisterImageUrl,
                _userSession.UserId.ToString());

            if (passwordUser != null)
            {
                var user = passwordUser;

                // Single validated write: the old password stays valid if this fails.
                var replaceResult = await Application.Common.PasswordPolicy.ReplacePasswordAsync(_userManager, user, request.Password!);
                if (replaceResult.IsFailure)
                    return Result.Failure<bool>($"Failed to update password: {replaceResult.Error}");

                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    user.Email = request.Email;
                    await _userManager.UpdateAsync(user);
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var user = await _userManager.FindByIdAsync(_userSession.UserId.ToString());
                if (user != null)
                {
                    user.Email = request.Email;
                    await _userManager.UpdateAsync(user);
                }
            }

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            if (!saveResult.IsSuccess)
                return Result.Failure<bool>($"Failed to update customer profile: {saveResult.ErrorMessage}");

            return Result.Success(true);
        }
    }
}
