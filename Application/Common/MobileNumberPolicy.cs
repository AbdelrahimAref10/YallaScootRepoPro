using CSharpFunctionalExtensions;

namespace Application.Common
{
    /// <summary>
    /// Customer mobile numbers are stored as digits only: country code followed by the local number
    /// as typed, leading 0 included, without a "+" (Egypt 010500 → 20010500). The customer signs in with that same number.
    /// </summary>
    public static class MobileNumberPolicy
    {
        /// <summary>Removes spaces, dashes and a leading "+"; anything else is left for <see cref="Validate"/>.</summary>
        public static string Normalize(string? mobile) =>
            (mobile ?? string.Empty).Replace(" ", "").Replace("-", "").Trim().TrimStart('+');

        public static Result Validate(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return Result.Failure("Mobile number is required");

            if (!mobile.All(char.IsDigit))
                return Result.Failure("Mobile number must contain digits only");

            return Result.Success();
        }
    }
}
