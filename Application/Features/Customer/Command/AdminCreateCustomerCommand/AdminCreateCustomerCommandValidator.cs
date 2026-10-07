using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Command.AdminCreateCustomerCommand
{
    public class AdminCreateCustomerCommandValidator
    {
        private readonly DatabaseContext _context;

        public AdminCreateCustomerCommandValidator(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result> ValidateAsync(AdminCreateCustomerCommand request, CancellationToken cancellationToken)
        {
            request.MobileNumber = Application.Common.MobileNumberPolicy.Normalize(request.MobileNumber);
            var mobileResult = Application.Common.MobileNumberPolicy.Validate(request.MobileNumber);
            if (mobileResult.IsFailure)
                return mobileResult;

            if (string.IsNullOrWhiteSpace(request.FullName))
                return Result.Failure("Full name is required");

            if (string.IsNullOrWhiteSpace(request.Gender))
                return Result.Failure("Gender is required");

            if (request.Gender != "Male" && request.Gender != "Female")
                return Result.Failure("Gender must be either 'Male' or 'Female'");

            if (!Enum.IsDefined(typeof(RegisterAs), request.RegisterAs))
                return Result.Failure("Invalid RegisterAs value");

            if (!Enum.IsDefined(typeof(VerificationBy), request.VerificationBy))
                return Result.Failure("Invalid VerificationBy value");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Failure("Email is required");

            request.Email = request.Email.Trim();
            if (!IsValidEmail(request.Email))
                return Result.Failure("Invalid email format");

            var normalizedEmail = request.Email.ToUpperInvariant();
            if (await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
                return Result.Failure("A user with this email already exists");

            var passwordResult = Application.Common.PasswordPolicy.Validate(request.Password);
            if (passwordResult.IsFailure)
                return passwordResult;

            if (request.CityId <= 0)
                return Result.Failure("Valid city is required");

            var cityExists = await _context.Cities.AnyAsync(c => c.CityId == request.CityId && c.IsActive, cancellationToken);
            if (!cityExists)
                return Result.Failure("Invalid or inactive city");

            if (request.ZoneId <= 0)
                return Result.Failure("Zone is required");

            if (!await Application.Features.Order.Common.OrderZoneFeeHelper.ZoneBelongsToCityAsync(
                    _context, request.CityId, request.ZoneId, cancellationToken))
                return Result.Failure("Zone must belong to the selected city group");

            var existingCustomer = await _context.Customers
                .AnyAsync(c => c.MobileNumber == request.MobileNumber.Trim(), cancellationToken);
            if (existingCustomer)
                return Result.Failure("Customer with this mobile number already exists");

            if (request.RegisterAs == (int)RegisterAs.Institution && string.IsNullOrWhiteSpace(request.CommercialRegisterImage))
                return Result.Failure("Commercial Register Image is required when registering as an Institution");

            if (request.RegisterAs == (int)RegisterAs.Individual)
                request.CommercialRegisterImage = null;

            return Result.Success();
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
