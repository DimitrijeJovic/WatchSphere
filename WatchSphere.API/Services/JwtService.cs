using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WatchSphere.API.Models;

namespace WatchSphere.API.Services
{
    public class JwtService
    {
        private readonly JwtSettings _settings;

        public JwtService(JwtSettings settings)
        {
            _settings = settings;
        }

        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var credentials = new SigningCredentials(
                _settings.SigningKey,
                SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTime.UtcNow.AddHours(_settings.ExpiryHours);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }

    /// <summary>
    /// Validated once at startup so a missing or too-short key fails the boot
    /// instead of every login.
    /// </summary>
    public class JwtSettings
    {
        public const int MinimumKeyLength = 32;

        public string Issuer { get; }

        public string Audience { get; }

        public int ExpiryHours { get; }

        public SymmetricSecurityKey SigningKey { get; }

        public JwtSettings(IConfiguration configuration)
        {
            // An environment variable takes precedence, so a deployed instance
            // never has to keep its signing key in a file under version control.
            var key = Environment.GetEnvironmentVariable("WATCHSPHERE_JWT_KEY")
                      ?? configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyLength)
            {
                throw new InvalidOperationException(
                    $"JWT signing key is missing or shorter than {MinimumKeyLength} bytes. " +
                    "Set the WATCHSPHERE_JWT_KEY environment variable or Jwt:Key in user secrets.");
            }

            Issuer = configuration["Jwt:Issuer"] ?? "WatchSphereAPI";
            Audience = configuration["Jwt:Audience"] ?? "WatchSphereUsers";
            ExpiryHours = configuration.GetValue("Jwt:ExpiryHours", 168);

            SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }
    }
}
