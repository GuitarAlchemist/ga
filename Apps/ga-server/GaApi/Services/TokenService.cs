namespace GaApi.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using GaApi.Models;

public class JwtSettings
{
    /// <summary>Base64-encoded signing key (>=256 bits).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ga-server";
    public string Audience { get; set; } = "ga-clients";

    /// <summary>Access-token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 30;
}

public class TokenService
{
    private readonly JwtSettings _jwt;
    private readonly MongoDbService _mongo;
    private readonly SymmetricSecurityKey _signingKey;

    public TokenService(IOptions<JwtSettings> jwt, MongoDbService mongo)
    {
        _jwt = jwt.Value;
        _mongo = mongo;
        if (string.IsNullOrWhiteSpace(_jwt.SigningKey))
            throw new InvalidOperationException("Authentication:Jwt:SigningKey is not configured");
        _signingKey = new SymmetricSecurityKey(Convert.FromBase64String(_jwt.SigningKey));
    }

    public string IssueJwt(User user)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new("provider", user.Provider),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (!string.IsNullOrEmpty(user.AvatarUrl))
            claims.Add(new Claim("avatar", user.AvatarUrl));
        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters BuildValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = _jwt.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ClockSkew = TimeSpan.FromSeconds(30),
    };

    /// <summary>Generate a fresh refresh token (raw) + persist its hash.</summary>
    public async Task<string> IssueRefreshTokenAsync(ObjectId userId, string? clientInfo, CancellationToken ct = default)
    {
        var raw = RandomUrlSafe(48);
        var hash = UserService.HashToken(raw);
        var record = new AuthRefreshToken
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            ClientInfo = clientInfo,
        };
        await _mongo.AuthRefreshTokens.InsertOneAsync(record, cancellationToken: ct);
        return raw;
    }

    /// <summary>
    /// Validate a raw refresh token. Returns the associated UserId if valid
    /// and unrevoked, null otherwise. Does NOT revoke — caller decides.
    /// </summary>
    public async Task<ObjectId?> ValidateRefreshAsync(string raw, CancellationToken ct = default)
    {
        var hash = UserService.HashToken(raw);
        var filter = Builders<AuthRefreshToken>.Filter.And(
            Builders<AuthRefreshToken>.Filter.Eq(t => t.TokenHash, hash),
            Builders<AuthRefreshToken>.Filter.Eq(t => t.RevokedAt, null),
            Builders<AuthRefreshToken>.Filter.Gt(t => t.ExpiresAt, DateTime.UtcNow));
        var record = await _mongo.AuthRefreshTokens.Find(filter).FirstOrDefaultAsync(ct);
        return record?.UserId;
    }

    /// <summary>Revoke a refresh token by its raw value.</summary>
    public async Task RevokeRefreshAsync(string raw, CancellationToken ct = default)
    {
        var hash = UserService.HashToken(raw);
        var filter = Builders<AuthRefreshToken>.Filter.Eq(t => t.TokenHash, hash);
        var update = Builders<AuthRefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);
        await _mongo.AuthRefreshTokens.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    /// <summary>Revoke ALL refresh tokens for a user (logout-all / password-change).</summary>
    public async Task RevokeAllForUserAsync(ObjectId userId, CancellationToken ct = default)
    {
        var filter = Builders<AuthRefreshToken>.Filter.And(
            Builders<AuthRefreshToken>.Filter.Eq(t => t.UserId, userId),
            Builders<AuthRefreshToken>.Filter.Eq(t => t.RevokedAt, null));
        var update = Builders<AuthRefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);
        await _mongo.AuthRefreshTokens.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    private static string RandomUrlSafe(int byteLen)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLen);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
