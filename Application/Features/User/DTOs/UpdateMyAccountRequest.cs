namespace Application.Features.User.DTOs
{
    /// <summary>Profile fields a user may change on their own account (never the role).</summary>
    public class UpdateMyAccountRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        /// <summary>Optional; leave empty to keep the current password.</summary>
        public string? Password { get; set; }
    }
}
