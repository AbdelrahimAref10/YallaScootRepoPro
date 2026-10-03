using CSharpFunctionalExtensions;
using Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Common
{
    /// <summary>
    /// Password rules shared by register, reset and profile update. They mirror the Identity
    /// options in Infrastructure/DatabaseConfiguration.cs (length 8, digit, upper, lower,
    /// no symbol required) so a request is rejected up front with a clear message instead of
    /// failing inside UserManager.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        public static Result Validate(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return Result.Failure("Password is required");

            if (password.Length < MinLength)
                return Result.Failure($"Password must be at least {MinLength} characters long");

            if (!password.Any(c => c >= '0' && c <= '9'))
                return Result.Failure("Password must contain at least one digit (0-9)");

            if (!password.Any(c => c >= 'A' && c <= 'Z'))
                return Result.Failure("Password must contain at least one uppercase letter (A-Z)");

            if (!password.Any(c => c >= 'a' && c <= 'z'))
                return Result.Failure("Password must contain at least one lowercase letter (a-z)");

            return Result.Success();
        }

        /// <summary>
        /// Runs every password validator registered on the UserManager without touching the user.
        /// </summary>
        public static async Task<Result> ValidateWithIdentityAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user,
            string newPassword)
        {
            var errors = new List<string>();
            foreach (var validator in userManager.PasswordValidators)
            {
                var result = await validator.ValidateAsync(userManager, user, newPassword);
                if (!result.Succeeded)
                    errors.AddRange(result.Errors.Select(e => e.Description));
            }

            return errors.Count == 0
                ? Result.Success()
                : Result.Failure(string.Join(", ", errors));
        }

        /// <summary>
        /// Replaces the user's password in a single write. The new password is validated first;
        /// if it is rejected nothing is changed, so the user can never be left without a password
        /// (which is what RemovePasswordAsync followed by a failing AddPasswordAsync used to do).
        /// </summary>
        public static async Task<Result> ReplacePasswordAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user,
            string newPassword)
        {
            var validation = await ValidateWithIdentityAsync(userManager, user, newPassword);
            if (validation.IsFailure)
                return validation;

            var previousHash = user.PasswordHash;
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, newPassword);

            // UpdateSecurityStampAsync persists the user (hash included) in one UpdateAsync call.
            var updateResult = await userManager.UpdateSecurityStampAsync(user);
            if (!updateResult.Succeeded)
            {
                user.PasswordHash = previousHash;
                return Result.Failure(string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }

            return Result.Success();
        }
    }
}
