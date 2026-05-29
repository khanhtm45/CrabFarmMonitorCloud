using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class AuthService
{
    private readonly RasCloudDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthService(RasCloudDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<object?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var norm = email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == norm && u.IsActive, ct);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return new
        {
            ok = true,
            token = _jwt.CreateToken(user),
            user = new { user.Id, user.Email, user.DisplayName, user.Role, user.OrgId }
        };
    }
}
