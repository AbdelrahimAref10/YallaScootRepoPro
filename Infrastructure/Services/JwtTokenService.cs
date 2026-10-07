using Domain.Common;
using Domain.Models;
using Infrastructure.Settings;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(ApplicationUser user, IList<string> roles, UserAccess? access = null);
        string GenerateRefreshToken();
    }

    public static class AppClaimTypes
    {
        public const string SubRoleId = "sub_role_id";
        public const string SubRole = "sub_role";
        public const string Permission = "permission";
        public const string MerchantId = "merchant_id";
        public const string IsOwner = "is_owner";
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IJwtSettings _jwtSettings;
        private readonly IDateTimeProvider _dateTimeProvider;

        public JwtTokenService(IJwtSettings jwtSettings, IDateTimeProvider dateTimeProvider)
        {
            _jwtSettings = jwtSettings;
            _dateTimeProvider = dateTimeProvider;
        }

        public string GenerateToken(ApplicationUser user, IList<string> roles, UserAccess? access = null)
        {
            if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
            {
                throw new InvalidOperationException("JWT Key is not configured");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                claims.Add(new Claim("MobileNumber", user.PhoneNumber));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Sub-role and permissions are informational for clients; the API re-checks them via IPermissionService.
            if (access is { IsAllowed: true })
            {
                claims.Add(new Claim(AppClaimTypes.SubRoleId, access.SubRoleId!.Value.ToString()));
                claims.Add(new Claim(AppClaimTypes.SubRole, access.SubRoleName ?? string.Empty));
                foreach (var permission in access.Permissions)
                    claims.Add(new Claim(AppClaimTypes.Permission, permission));

                if (access.MerchantId.HasValue)
                {
                    claims.Add(new Claim(AppClaimTypes.MerchantId, access.MerchantId.Value.ToString()));
                    claims.Add(new Claim(AppClaimTypes.IsOwner, access.IsOwner ? "true" : "false"));
                }
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: _dateTimeProvider.Now.AddHours(_jwtSettings.ExpirationHours),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}
