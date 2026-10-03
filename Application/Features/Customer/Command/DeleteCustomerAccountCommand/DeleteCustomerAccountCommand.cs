using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Command.DeleteCustomerAccountCommand
{
    public record DeleteCustomerAccountCommand : IRequest<Result<bool>>;

    /// <summary>
    /// Self-service account deletion (app-store requirement). Orders reference the customer with a
    /// Restrict FK and are financial records, so the customer row is kept and anonymized instead of
    /// being removed: personal data is scrubbed, the login is disabled and freed (the same phone or
    /// email can register again), refresh tokens are revoked and push stops. Everything is written
    /// in one SaveChanges so a failure leaves the account untouched.
    /// </summary>
    public class DeleteCustomerAccountCommandHandler : IRequestHandler<DeleteCustomerAccountCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserSession _userSession;
        private readonly IImageService _imageService;
        private readonly IDateTimeProvider _dateTimeProvider;

        public DeleteCustomerAccountCommandHandler(
            DatabaseContext context,
            UserManager<ApplicationUser> userManager,
            IUserSession userSession,
            IImageService imageService,
            IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _userManager = userManager;
            _userSession = userSession;
            _imageService = imageService;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<bool>> Handle(DeleteCustomerAccountCommand request, CancellationToken cancellationToken)
        {
            if (_userSession.UserId <= 0)
                return Result.Failure<bool>("Customer not authenticated");

            var userId = _userSession.UserId;

            var customer = await _context.Customers
                .AsTracking()
                .Include(c => c.CustomerLocation)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (customer == null || customer.IsDeleted)
                return Result.Failure<bool>("Customer not found");

            // Only Completed, Cancelled and CustomerRejectedReceipt are final; any other state
            // (Pending, the merchant states, Confirmed, DeliveryAssigned, OnWay, CustomerReceived)
            // still needs the customer.
            var hasOpenOrders = await _context.Orders
                .AnyAsync(o => o.CustomerId == customer.CustomerId
                               && o.OrderState != OrderState.Completed
                               && o.OrderState != OrderState.Cancelled
                               && o.OrderState != OrderState.CustomerRejectedReceipt,
                          cancellationToken);

            if (hasOpenOrders)
                return Result.Failure<bool>("Cannot delete account. You have active orders. Please complete or cancel them first.");

            var user = await _context.Users
                .AsTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            var actor = $"customer-{customer.CustomerId}";
            var imagesToDelete = new[] { customer.PersonalImage, customer.CommercialRegisterImage }
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i!)
                .ToList();

            customer.DeleteAccount(actor);

            if (customer.CustomerLocation != null)
                _context.CustomerLocations.Remove(customer.CustomerLocation);

            if (user != null)
            {
                // Free the phone / email / user name for a new registration and make the old
                // login unusable: no password, inactive, locked out, new security stamp.
                var freedUserName = $"deleted-{user.Id}-{Guid.NewGuid():N}";
                user.UserName = freedUserName;
                user.NormalizedUserName = _userManager.NormalizeName(freedUserName);
                user.Email = null;
                user.NormalizedEmail = null;
                user.EmailConfirmed = false;
                user.PhoneNumber = null;
                user.PhoneNumberConfirmed = false;
                user.PasswordHash = null;
                user.PasswordResetCode = null;
                user.PasswordResetCodeExpiry = null;
                user.Active = false;
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.LastModifiedBy = actor;
                user.LastModifiedDate = DateTime.UtcNow;
            }

            var refreshTokens = await _context.RefreshTokens
                .AsTracking()
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var refreshToken in refreshTokens)
                refreshToken.Revoke(_dateTimeProvider);

            var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
            if (!saveResult.IsSuccess)
                return Result.Failure<bool>($"Failed to delete customer account: {saveResult.ErrorMessage}");

            // Files go only after the scrub is saved; DeleteImage never throws.
            foreach (var image in imagesToDelete)
                _imageService.DeleteImage(image);

            return Result.Success(true);
        }
    }
}
