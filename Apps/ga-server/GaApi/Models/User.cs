namespace GaApi.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// OAuth2-authenticated user. Identity is (provider, providerUserId) — email
/// is informational and may be empty for GitHub users with private email.
/// Admin role is granted on first login if the user's email matches an entry
/// in Authentication:OwnerEmails (comma-separated config).
/// </summary>
[BsonIgnoreExtraElements]
public class User
{
    [BsonId] [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("email")] public string Email { get; set; } = string.Empty;

    [BsonElement("name")] public string Name { get; set; } = string.Empty;

    [BsonElement("avatarUrl")] public string? AvatarUrl { get; set; }

    /// <summary>"google" or "github".</summary>
    [BsonElement("provider")] public string Provider { get; set; } = string.Empty;

    /// <summary>Stable user ID from the external provider.</summary>
    [BsonElement("providerUserId")] public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>Role claims — "admin" granted to OwnerEmails, "viewer" otherwise.</summary>
    [BsonElement("roles")] public List<string> Roles { get; set; } = new();

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }

    [BsonElement("lastLoginAt")] public DateTime LastLoginAt { get; set; }
}
