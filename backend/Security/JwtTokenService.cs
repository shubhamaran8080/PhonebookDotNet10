using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Phonebook.Api.Security;

/// <summary>
/// Creates and verifies HS256 JWT access tokens.
/// Mirrors the Java JwtTokenProvider: SHA-256 derived key, 12-hour expiry,
/// no issuer/audience claims (so token validation must not check them).
/// </summary>
public class JwtTokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TimeSpan _expiry;

    /// <summary>
    /// Exposes the signing key so token validation uses the exact same key.
    /// </summary>
    public SymmetricSecurityKey SigningKey => _signingKey;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"] ?? configuration["JWT_SECRET_KEY"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Ephemeral random key — tokens die on restart (same as Java fallback)
            var randomBytes = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            _signingKey = new SymmetricSecurityKey(randomBytes);
        }
        else
        {
            // Derive a 256-bit key from the secret using SHA-256 (same as Java)
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
            _signingKey = new SymmetricSecurityKey(keyBytes);
        }

        var expiryMinutes = configuration.GetValue("Jwt:ExpirationMinutes", 720);
        _expiry = TimeSpan.FromMinutes(expiryMinutes <= 0 ? 720 : expiryMinutes);
    }

    /// <summary>
    /// Creates a signed JWT with the username as the "sub" claim.
    /// No issuer/audience is emitted, so validation must not require them.
    /// </summary>
    public string CreateToken(string username)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(_expiry).UtcDateTime,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Token validation parameters that match the tokens created by
    /// <see cref="CreateToken"/>: signature + lifetime only.
    /// Issuer and audience validation must stay OFF because our tokens
    /// carry no "iss" / "aud" claims (leaving them on makes every request
    /// fail with IDX10208 "Unable to validate audience").
    /// </summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero
    };

    /// <summary>
    /// Validates a token and returns the username ("sub" claim) if valid.
    /// Returns null if the token is invalid, expired, or tampered.
    /// </summary>
    public string? ValidateToken(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(), out _);
            return GetUsername(principal);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the username from a validated principal. The "sub" claim is
    /// looked up directly (JwtBearer is configured with MapInboundClaims
    /// disabled, so claim types are never rewritten to the legacy
    /// ClaimTypes.* URNs).
    /// </summary>
    public static string? GetUsername(ClaimsPrincipal principal)
    {
        return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.Identity?.Name;
    }
}
