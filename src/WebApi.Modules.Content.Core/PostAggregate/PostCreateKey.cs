using System.Security.Cryptography;
using System.Text;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// The idempotency key a Post was created with: unique in the tenant, so a repeated create request gets the Post made
/// first instead of a second one. Written in the same transaction as the Post. Only the key's SHA-256 is kept
/// (<see cref="Hash"/>): keys then compare exactly (case included) on every database engine.
/// </summary>
public sealed class PostCreateKey : EntityBase<int>, ITenantOwned
{
    /// <summary>The longest key a caller may send (after trimming).</summary>
    public const int MaxLength = 200;
    public const int HashLength = 64;

    private PostCreateKey() { } // EF Core

    public PostCreateKey(string key, string ownerId, int postId, DateTimeOffset now)
    {
        KeyHash = Hash(key);
        OwnerId = ownerId;
        PostId = postId;
        CreatedAt = now;
    }

    public string TenantId { get; set; } = "";
    public string KeyHash { get; private set; } = "";
    public string OwnerId { get; private set; } = "";
    public int PostId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
