namespace GaApi.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Refresh-token record. The raw token is never stored — only a SHA256 hash.
/// The client presents the raw token via httpOnly cookie; the server hashes
/// and looks it up here. Rotation: each successful refresh revokes the used
/// token and issues a new one (single-use). Revocation cascades to logout.
/// </summary>
[BsonIgnoreExtraElements]
public class AuthRefreshToken
{
    [BsonId] [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("userId")] public ObjectId UserId { get; set; }

    /// <summary>SHA256 hex of the raw token — the raw value never touches the DB.</summary>
    [BsonElement("tokenHash")] public string TokenHash { get; set; } = string.Empty;

    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }

    /// <summary>Null while valid; set when rotated or explicitly revoked (logout).</summary>
    [BsonElement("revokedAt")] public DateTime? RevokedAt { get; set; }

    /// <summary>IP or origin that issued/used the token — audit trail.</summary>
    [BsonElement("clientInfo")] public string? ClientInfo { get; set; }
}
