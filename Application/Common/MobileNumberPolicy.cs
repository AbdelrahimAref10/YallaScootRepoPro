using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace Application.Common
{
    /// <summary>
    /// Customer mobile numbers are stored with the country code in international format (+201001234567)
    /// and the customer signs in with that same number.
    /// </summary>
    public static class MobileNumberPolicy
    {
        private static readonly Regex International = new(@"^\+[1-9]\d{6,14}$", RegexOptions.Compiled);

        /// <summary>Removes spaces and dashes; anything else is left for <see cref="Validate"/>.</summary>
        public static string Normalize(string? mobile) =>
            (mobile ?? string.Empty).Replace(" ", "").Replace("-", "").Trim();

        public static Result Validate(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return Result.Failure("Mobile number is required");

            if (!International.IsMatch(mobile))
                return Result.Failure("Mobile number must include the country code, e.g. +201001234567");

            return Result.Success();
        }
    }
}
