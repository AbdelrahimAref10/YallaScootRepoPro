using CSharpFunctionalExtensions;

namespace Application.Common
{
    /// <summary>
    /// Mirrors Identity's default AllowedUserNameCharacters so the user gets a clear message
    /// instead of a raw "Failed to create user" from UserManager.
    /// </summary>
    public static class UserNamePolicy
    {
        private const string AllowedSymbols = "-._@+";

        public static Result Validate(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return Result.Failure("User name is required");

            if (!userName.All(c => char.IsAsciiLetterOrDigit(c) || AllowedSymbols.Contains(c)))
                return Result.Failure("User name can only contain English letters, digits and - . _ @ + (no spaces or Arabic characters)");

            return Result.Success();
        }
    }
}
