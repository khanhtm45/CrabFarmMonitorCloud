using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class JwtTokenService
{
    private readonly string _secret;
    private readonly int _expireHours;

    public JwtTokenService(IConfiguration config)
    {
        _secret = config["JWT_SECRET"] ?? "ras-dev-jwt-secret-change-in-production-32chars";
        _expireHours = int.TryParse(config["JWT_EXPIRE_HOURS"], out var h) ? h : 24;
    }

    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("org_id", user.OrgId.ToString())
        };
        var token = new JwtSecurityToken(
            issuer: "ras-iot-cloud",
            audience: "ras-dashboard",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expireHours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
