namespace GaApi.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Settings for role assignment. OwnerEmails (comma-separated) gets 'admin'
/// on first login; everyone else is 'viewer'.
/// </summary>
public class AuthOwnerSettings
{
    public string OwnerEmails { get; set; } = string.Empty;

    public HashSet<string> OwnerEmailSet =>
        new(OwnerEmails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
}

public class UserService
{
    private readonly MongoDbService _mongo;
    private readonly AuthOwnerSettings _ownerSettings;

    public UserService(MongoDbService mongo, IOptions<AuthOwnerSettings> ownerSettings)
    {
        _mongo = mongo;
        _ownerSettings = ownerSettings.Value;
    }

    /// <summary>
    /// Find existing user by (provider, providerUserId) or create a new one.
    /// Updates the name/avatar/lastLoginAt on each login. Admin role is
    /// assigned on creation (or re-asserted on login) if the email matches
    /// the OwnerEmails list.
    /// </summary>
    public async Task<User> FindOrCreateAsync(
        string provider,
        string providerUserId,
        string email,
        string name,
        string? avatarUrl,
        CancellationToken ct = default)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Provider, provider),
            Builders<User>.Filter.Eq(u => u.ProviderUserId, providerUserId));

        var existing = await _mongo.Users.Find(filter).FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        var isOwner = !string.IsNullOrEmpty(email) && _ownerSettings.OwnerEmailSet.Contains(email);
        var desiredRoles = isOwner
            ? new List<string> { "admin", "viewer" }
            : new List<string> { "viewer" };

        if (existing is not null)
        {
            var update = Builders<User>.Update
                .Set(u => u.Email, email)
                .Set(u => u.Name, name)
                .Set(u => u.AvatarUrl, avatarUrl)
                .Set(u => u.LastLoginAt, now)
                .Set(u => u.Roles, desiredRoles);
            await _mongo.Users.UpdateOneAsync(filter, update, cancellationToken: ct);
            existing.Email = email;
            existing.Name = name;
            existing.AvatarUrl = avatarUrl;
            existing.LastLoginAt = now;
            existing.Roles = desiredRoles;
            return existing;
        }

        var user = new User
        {
            Id = ObjectId.GenerateNewId(),
            Email = email,
            Name = name,
            AvatarUrl = avatarUrl,
            Provider = provider,
            ProviderUserId = providerUserId,
            Roles = desiredRoles,
            CreatedAt = now,
            LastLoginAt = now,
        };
        await _mongo.Users.InsertOneAsync(user, cancellationToken: ct);
        return user;
    }

    public async Task<User?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        return await _mongo.Users.Find(Builders<User>.Filter.Eq(u => u.Id, id)).FirstOrDefaultAsync(ct);
    }

    /// <summary>Hash a refresh token with SHA256 (hex-encoded).</summary>
    public static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
