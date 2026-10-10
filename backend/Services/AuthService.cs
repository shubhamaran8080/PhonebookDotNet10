using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Models.Entities;
using Phonebook.Api.Security;
using Phonebook.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Phonebook.Api.Services;

/// <summary>
/// Authentication logic: login with username or email, JWT creation.
/// Mirrors the Java AuthService.
/// </summary>
public class AuthService
{
    private readonly PhonebookDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthService(PhonebookDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    /// <summary>
    /// Authenticates with a username OR an email and returns a JWT.
    /// Never reveals whether the username or the password was wrong.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = request.Username;
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.HashedPassword))
        {
            throw new UnauthorizedException("Incorrect username/email or password");
        }

        var token = _jwt.CreateToken(user.Username);
        return new LoginResponse
        {
            AccessToken = token,
            TokenType = "bearer",
            User = UserResponse.FromEntity(user)
        };
    }

    /// <summary>
    /// Finds a user by username (used by /auth/me).
    /// </summary>
    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
    }
}

/// <summary>
/// Thrown when authentication fails. Maps to HTTP 401.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
