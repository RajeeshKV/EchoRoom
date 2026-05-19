using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Api.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    public string GenerateToken(string username)
    {
        var secret = configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret is missing.");
        var issuer = configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? "Chat.Api";
        var audience = configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "Chat.Client";
        var expiryHours = configuration.GetValue("Jwt:ExpiryHours", 12);

        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
